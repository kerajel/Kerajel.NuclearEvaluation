using Microsoft.EntityFrameworkCore;
using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Server.Services.Assistant;
using NuclearEvaluation.Shared.Contracts;
using NuclearEvaluation.Shared.Models.Domain;
using NuclearEvaluation.Shared.Models.Views;
using Z.EntityFramework.Plus;

namespace NuclearEvaluation.Server.Services.Evaluation;

public class ProjectService : DbServiceBase, IProjectService
{
    private readonly ILogger<ProjectService> _logger;

    public ProjectService(
        NuclearEvaluationServerDbContext dbContext,
        ILogger<ProjectService> logger
    )
        : base(dbContext)
    {
        _logger = logger;
    }

    public async Task<FetchDataResult<ProjectView>> GetProjectViews(
        FetchDataCommand<ProjectView> command
    )
    {
        try
        {
            IQueryable<ProjectView> baseQuery = _dbContext.ProjectView;
            if (command.Query?.AssistantQuery is { ProjectSeriesRanking: { } ranking } query)
            {
                return await FetchRankedProjectsAsync(baseQuery, command, query, ranking);
            }
            return await ExecuteQuery(baseQuery, command);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error fetching project views");
            return FetchDataResult<ProjectView>.Faulted(ex);
        }
    }

    async Task<FetchDataResult<ProjectView>> FetchRankedProjectsAsync(
        IQueryable<ProjectView> projects,
        FetchDataCommand<ProjectView> command,
        AssistantGridQuery query,
        ProjectSeriesRanking ranking
    )
    {
        IQueryable<ProjectView> filtered = GetFilteredQuery(
            command.AsNoTracking ? projects.AsNoTracking() : projects,
            command
        );
        IQueryable<ProjectViewSeriesView> links = _dbContext.ProjectViewSeriesView;
        if (ranking.Filters.Count > 0)
        {
            IQueryable<SeriesView> matchingSeries = new SeriesSchemaQueries(
                _dbContext
            ).ApplyFilters(
                _dbContext.SeriesView,
                new SeriesGridQuery { Filters = ranking.Filters }
            );
            links = links.Where(link => matchingSeries.Any(series => series.Id == link.SeriesId));
        }
        var counts = links
            .GroupBy(link => link.ProjectId)
            .Select(matches => new { ProjectId = matches.Key, SeriesCount = matches.Count() });
        var scored =
            from project in filtered
            join count in counts on project.Id equals count.ProjectId
            select new { Project = project, count.SeriesCount };
        IQueryable<ProjectView> selected = (
            ranking.Descending
                ? scored.OrderByDescending(project => project.SeriesCount)
                : scored.OrderBy(project => project.SeriesCount)
        )
            .ThenByDescending(project => project.Project.UpdatedAt)
            .ThenBy(project => project.Project.Id)
            .Take(query.Limit!.Value)
            .Select(project => project.Project);

        IQueryable<ProjectView> displayed = query.SortField is null
            ? selected
            : AssistantGridQueries.ApplyOrder(selected, query, project => project.Id);
        return await FetchPageAsync(selected, displayed, command);
    }

    public async Task UpdateField(ProjectFieldUpdate update)
    {
        DateTime updatedAt = DateTime.UtcNow;

        int affected = await _dbContext
            .Project.Where(p => p.Id == update.ProjectId)
            .UpdateFromQueryAsync(p => new Project
            {
                Name = update.Field == ProjectField.Name ? update.StringValue ?? p.Name : p.Name,
                Conclusions =
                    update.Field == ProjectField.Conclusions
                        ? update.StringValue ?? p.Conclusions
                        : p.Conclusions,
                FollowUpActionsRecommended =
                    update.Field == ProjectField.FollowUpActionsRecommended
                        ? update.StringValue ?? p.FollowUpActionsRecommended
                        : p.FollowUpActionsRecommended,
                DecayCorrectionDate =
                    update.Field == ProjectField.DecayCorrectionDate
                        ? update.DateValue
                        : p.DecayCorrectionDate,
                UpdatedAt = updatedAt,
            });
        if (affected == 0)
            throw new KeyNotFoundException();
    }

    public async Task UpdateProjectSeries(int projectId, IReadOnlyCollection<int> seriesIds)
    {
        Project project = await _dbContext
            .Project.IncludeOptimized(x => x.ProjectSeries)
            .SingleOrDefaultAsync(x => x.Id == projectId)
            ?? throw new KeyNotFoundException($"Project {projectId} not found");

        if (await _dbContext.Series.CountAsync(x => seriesIds.Contains(x.Id)) != seriesIds.Count)
        {
            throw new ArgumentException(
                "One or more selected series no longer exist.",
                nameof(seriesIds)
            );
        }
        _dbContext.ProjectSeries.RemoveRange(project.ProjectSeries);
        project.UpdatedAt = DateTime.UtcNow;
        project.ProjectSeries = seriesIds
            .Select(seriesId => new ProjectSeries { ProjectId = project.Id, SeriesId = seriesId })
            .ToList();

        await _dbContext.SaveChangesAsync();
    }

    public async Task<bool> IsNameAvailable(string name, int excludeId)
    {
        bool exists = await _dbContext.Project.AnyAsync(p => p.Name == name && p.Id != excludeId);
        return !exists;
    }
}

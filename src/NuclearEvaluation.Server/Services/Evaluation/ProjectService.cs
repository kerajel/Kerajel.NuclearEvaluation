using Microsoft.EntityFrameworkCore;
using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Kernel.Extensions;
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
        ILogger<ProjectService> logger) : base(dbContext)
    {
        _logger = logger;
    }

    public async Task<FetchDataResult<ProjectView>> GetProjectViews(FetchDataCommand<ProjectView> command)
    {
        try
        {
            IQueryable<ProjectView> baseQuery = _dbContext.ProjectView;
            return await ExecuteQuery(baseQuery, command);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching project views");
            return FetchDataResult<ProjectView>.Faulted(ex);
        }
    }

    public async Task UpdateField(ProjectFieldUpdate update)
    {
        DateTime updatedAt = DateTime.UtcNow;

        await _dbContext.Project
            .Where(p => p.Id == update.ProjectId)
            .UpdateFromQueryAsync(p => new Project
            {
                Name = update.Field == ProjectField.Name ? update.StringValue ?? p.Name : p.Name,
                Conclusions = update.Field == ProjectField.Conclusions ? update.StringValue ?? p.Conclusions : p.Conclusions,
                FollowUpActionsRecommended = update.Field == ProjectField.FollowUpActionsRecommended ? update.StringValue ?? p.FollowUpActionsRecommended : p.FollowUpActionsRecommended,
                DecayCorrectionDate = update.Field == ProjectField.DecayCorrectionDate ? update.DateValue : p.DecayCorrectionDate,
                UpdatedAt = updatedAt,
            });
    }

    public async Task UpdateProjectSeries(int projectId, IReadOnlyCollection<int> seriesIds)
    {
        Project project = await _dbContext.Project
            .IncludeOptimized(x => x.ProjectSeries)
            .SingleOrDefaultAsync(x => x.Id == projectId)
            ?? throw new InvalidOperationException($"Project {projectId} not found");

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

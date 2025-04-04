using Microsoft.EntityFrameworkCore;
using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Kernel.Extensions;
using NuclearEvaluation.Kernel.Models.Domain;
using NuclearEvaluation.Kernel.Models.Views;
using System.Linq.Expressions;
using Z.EntityFramework.Plus;
using NuclearEvaluation.Kernel.Data.Context;
using Microsoft.Extensions.Logging;
using NuclearEvaluation.Server.Services.DB;
using NuclearEvaluation.Server.Interfaces.Evaluation;

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

    public async Task UpdatePropertyFromView<TProperty>(
        ProjectView projectView,
        Expression<Func<ProjectView, TProperty>> projectViewProperty,
        Expression<Func<Project, TProperty>> projectProperty,
        bool updateView = true)
    {
        DateTime updatedAt = DateTime.UtcNow;

        string propertyName = projectProperty.GetPropertyName();
        TProperty? updatedValue = projectViewProperty.Compile()(projectView);

        Dictionary<string, object?> dict = new()
        {
            [propertyName] = updatedValue,
        };

        await _dbContext.Project
            .Where(p => p.Id == projectView.Id)
            .UpdateAsync(dict);

        if (updateView)
        {
            projectView.UpdatedAt = updatedAt;
        }
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
            _logger.LogError(ex, "");
            return FetchDataResult<ProjectView>.Faulted(ex);
        }

    }

    public async Task UpdateProjectSeriesFromView(ProjectView projectView)
    {
        DateTime updatedAt = DateTime.UtcNow;

        Project project = await _dbContext.Project
            .IncludeOptimized(x => x.ProjectSeries)
            .SingleOrDefaultAsync(x => x.Id == projectView.Id)
            ?? throw new InvalidOperationException();

        _dbContext.ProjectSeries.RemoveRange(project.ProjectSeries);
        project.UpdatedAt = updatedAt;
        project.ProjectSeries = projectView.ProjectSeries
            .Select(x => new ProjectSeries() { ProjectId = project.Id, SeriesId = x.SeriesId })
            .ToList();

        await _dbContext.SaveChangesAsync();
    }
}
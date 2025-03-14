using Microsoft.EntityFrameworkCore;
using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Kernel.Extensions;
using NuclearEvaluation.Kernel.Interfaces;
using NuclearEvaluation.Kernel.Models.Domain;
using NuclearEvaluation.Kernel.Models.Views;
using NuclearEvaluation.Kernel.Models.Domain;
using NuclearEvaluation.Server.Data;
using System.Linq.Expressions;
using System.Threading;
using Z.EntityFramework.Plus;
using NuclearEvaluation.SharedServices.Services;

namespace NuclearEvaluation.Server.Services;

public class ProjectService : DbServiceBase, IProjectService
{
    public ProjectService(NuclearEvaluationServerDbContext dbContext) : base(dbContext)
    {
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

    public async Task<FilterDataResponse<ProjectView>> GetProjectViews(FilterDataCommand<ProjectView> command)
    {
        IQueryable<ProjectView> baseQuery = _dbContext.ProjectView;

        return await ExecuteQuery(baseQuery, command);
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
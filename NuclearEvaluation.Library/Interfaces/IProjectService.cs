using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Kernel.Models.Domain;
using NuclearEvaluation.Kernel.Models.Views;
using System.Linq.Expressions;

namespace NuclearEvaluation.Kernel.Interfaces;

public interface IProjectService
{
    Task<FilterDataResponse<ProjectView>> GetProjectViews(FilterDataCommand<ProjectView> command);
    Task UpdateProjectSeriesFromView(ProjectView projectView);
    Task UpdatePropertyFromView<TProperty>(ProjectView projectView, Expression<Func<ProjectView, TProperty>> projectViewProperty, Expression<Func<Project, TProperty>> projectProperty, bool updateView = true);
}
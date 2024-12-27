using NuclearEvaluation.Library.Commands;
using NuclearEvaluation.Library.Models.Domain;
using NuclearEvaluation.Library.Models.Views;
using System.Linq.Expressions;

namespace NuclearEvaluation.Library.Interfaces;

public interface IProjectService
{
    Task<FilterDataResponse<ProjectView>> GetProjectViews(FilterDataCommand<ProjectView> command);
    Task UpdateProjectSeriesFromView(ProjectView projectView);
    Task UpdatePropertyFromView<TProperty>(ProjectView projectView, Expression<Func<ProjectView, TProperty>> projectViewProperty, Expression<Func<Project, TProperty>> projectProperty, bool updateView = true);
}
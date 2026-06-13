using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Shared.Contracts;
using NuclearEvaluation.Shared.Models.Views;

namespace NuclearEvaluation.Server.Interfaces.Evaluation;

public interface IProjectService
{
    Task<FetchDataResult<ProjectView>> GetProjectViews(FetchDataCommand<ProjectView> command);
    Task UpdateField(ProjectFieldUpdate update);
    Task UpdateProjectSeries(int projectId, IReadOnlyCollection<int> seriesIds);
    Task<bool> IsNameAvailable(string name, int excludeId);
}

using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Shared.Models.Views;

namespace NuclearEvaluation.Server.Interfaces.Data;

public interface ISubSampleService
{
    Task<FetchDataResult<SubSampleView>> GetSubSampleViews(FetchDataCommand<SubSampleView> command);
}
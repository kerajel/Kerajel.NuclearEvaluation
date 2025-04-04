using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Kernel.Models.Views;

namespace NuclearEvaluation.Server.Interfaces.Data;

public interface ISubSampleService
{
    Task<FetchDataResult<SubSampleView>> GetSubSampleViews(FetchDataCommand<SubSampleView> command);
}
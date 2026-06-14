using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Shared.Models.Views;

namespace NuclearEvaluation.Server.Interfaces.Data;

public interface ISampleService
{
    Task<FetchDataResult<SampleView>> GetSampleViews(FetchDataCommand<SampleView> command);
}
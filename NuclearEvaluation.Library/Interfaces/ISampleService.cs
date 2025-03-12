using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Kernel.Models.Views;

namespace NuclearEvaluation.Kernel.Interfaces;

public interface ISampleService
{
    Task<FilterDataResponse<SampleView>> GetSampleViews(FilterDataCommand<SampleView> command);
}
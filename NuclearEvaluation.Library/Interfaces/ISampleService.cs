using NuclearEvaluation.Library.Commands;
using NuclearEvaluation.Library.Models.Views;

namespace NuclearEvaluation.Library.Interfaces;

public interface ISampleService
{
    Task<FilterDataResponse<SampleView>> GetSampleViews(FilterDataCommand<SampleView> command);
}
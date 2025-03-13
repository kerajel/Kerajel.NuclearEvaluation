using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Kernel.Models.Views;

namespace NuclearEvaluation.Kernel.Interfaces;

public interface ISubSampleService
{
    Task<FilterDataResponse<SubSampleView>> GetSubSampleViews(FilterDataCommand<SubSampleView> command);
}
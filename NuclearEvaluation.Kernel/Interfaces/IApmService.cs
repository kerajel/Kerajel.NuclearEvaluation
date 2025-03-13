using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Kernel.Models.Views;

namespace NuclearEvaluation.Kernel.Interfaces;

public interface IApmService
{
    Task<FilterDataResponse<ApmView>> GetApmViews(FilterDataCommand<ApmView> command);
}
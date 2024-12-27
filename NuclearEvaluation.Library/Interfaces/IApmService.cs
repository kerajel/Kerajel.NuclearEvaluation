using NuclearEvaluation.Library.Commands;
using NuclearEvaluation.Library.Models.Views;

namespace NuclearEvaluation.Library.Interfaces;

public interface IApmService
{
    Task<FilterDataResponse<ApmView>> GetApmViews(FilterDataCommand<ApmView> command);
}
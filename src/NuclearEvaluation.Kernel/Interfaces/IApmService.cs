using Kerajel.Primitives.Models;
using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Kernel.Models.Views;

namespace NuclearEvaluation.Kernel.Interfaces;

public interface IApmService
{
    Task<FetchDataResult<ApmView>> GetApmViews(FetchDataCommand<ApmView> command);
}
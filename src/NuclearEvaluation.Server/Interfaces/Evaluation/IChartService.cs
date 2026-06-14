using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Shared.Models.Plotting;
using NuclearEvaluation.Shared.Models.Views;

namespace NuclearEvaluation.Server.Interfaces.Evaluation;

public interface IChartService
{
    Task<ILookup<string, BinCount>> GetProjectApmUraniumBinCounts(int projectId);
    Task<ILookup<string, BinCount>> GetProjectParticleUraniumBinCounts(int projectId);
    Task<ILookup<string, BinCount>> GetProjectApmUraniumBinCounts(FetchDataCommand<ApmView> command);
    Task<ILookup<string, BinCount>> GetProjectParticleUraniumBinCounts(FetchDataCommand<ParticleView> command);
}

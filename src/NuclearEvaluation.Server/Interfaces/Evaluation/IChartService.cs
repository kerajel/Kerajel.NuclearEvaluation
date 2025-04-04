using NuclearEvaluation.Kernel.Models.Plotting;

namespace NuclearEvaluation.Server.Interfaces.Evaluation;

public interface IChartService
{
    Task<ILookup<string, BinCount>> GetProjectApmUraniumBinCounts(int projectId);
    Task<ILookup<string, BinCount>> GetProjectParticleUraniumBinCounts(int projectId);
}
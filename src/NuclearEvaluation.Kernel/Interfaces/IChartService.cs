using NuclearEvaluation.Kernel.Models.Plotting;

namespace NuclearEvaluation.Kernel.Interfaces;

public interface IChartService
{
    Task<ILookup<string, BinCount>> GetProjectApmUraniumBinCounts(int projectId);
    Task<ILookup<string, BinCount>> GetProjectParticleUraniumBinCounts(int projectId);
}
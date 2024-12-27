using NuclearEvaluation.Library.Models.Plotting;

namespace NuclearEvaluation.Library.Interfaces;

public interface IChartService
{
    Task<ILookup<string, BinCount>> GetProjectApmUraniumBinCounts(int projectId);
    Task<ILookup<string, BinCount>> GetProjectParticleUraniumBinCounts(int projectId);
}
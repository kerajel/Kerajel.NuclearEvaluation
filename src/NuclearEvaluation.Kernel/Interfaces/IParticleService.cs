using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Kernel.Models.Views;

namespace NuclearEvaluation.Kernel.Interfaces;

public interface IParticleService
{
    Task<FilterDataResponse<ParticleView>> GetParticleViews(FilterDataCommand<ParticleView> command);
}
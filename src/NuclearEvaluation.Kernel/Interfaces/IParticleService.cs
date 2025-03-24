using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Kernel.Models.Views;

namespace NuclearEvaluation.Kernel.Interfaces;

public interface IParticleService
{
    Task<FilterDataResult<ParticleView>> GetParticleViews(FilterDataCommand<ParticleView> command);
}
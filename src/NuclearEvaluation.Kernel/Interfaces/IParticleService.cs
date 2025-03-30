using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Kernel.Models.Views;

namespace NuclearEvaluation.Kernel.Interfaces;

public interface IParticleService
{
    Task<FetchDataResult<ParticleView>> GetParticleViews(FetchDataCommand<ParticleView> command);
}
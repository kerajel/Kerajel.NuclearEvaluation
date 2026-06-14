using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Shared.Models.Views;

namespace NuclearEvaluation.Server.Interfaces.Data;

public interface IParticleService
{
    Task<FetchDataResult<ParticleView>> GetParticleViews(FetchDataCommand<ParticleView> command);
}
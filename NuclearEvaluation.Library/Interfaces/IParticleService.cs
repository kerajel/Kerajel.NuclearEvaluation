using NuclearEvaluation.Library.Commands;
using NuclearEvaluation.Library.Models.Views;

namespace NuclearEvaluation.Library.Interfaces;

public interface IParticleService
{
    Task<FilterDataResponse<ParticleView>> GetParticleViews(FilterDataCommand<ParticleView> command);
}
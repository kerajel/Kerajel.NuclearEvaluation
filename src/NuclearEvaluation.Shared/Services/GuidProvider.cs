using NuclearEvaluation.Kernel.Interfaces;

namespace NuclearEvaluation.Shared.Services;

public class GuidProvider : IGuidProvider
{
    public Guid NewGuid()
    {
        return Guid.NewGuid();
    }
}
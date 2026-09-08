namespace NuclearEvaluation.Server.Services.GUID;

public class GuidProvider : IGuidProvider
{
    public Guid NewGuid()
    {
        return Guid.NewGuid();
    }
}

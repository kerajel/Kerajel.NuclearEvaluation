namespace NuclearEvaluation.Server.Models.Identity;

public class ApplicationClaim
{
    public string Type { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public partial class ApplicationAuthenticationState
{
    public bool IsAuthenticated { get; set; }
    public string Name { get; set; } = string.Empty;
    public IEnumerable<ApplicationClaim> Claims { get; set; } = [];
}
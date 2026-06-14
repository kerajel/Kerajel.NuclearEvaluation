namespace NuclearEvaluation.Shared.Contracts;

/// <summary>
/// ALTCHA-style proof-of-work challenge. The client must find the <c>Number</c> in
/// [0, MaxNumber] whose SHA-256 of (Salt + Number) equals <c>Challenge</c>.
/// </summary>
public class CaptchaChallenge
{
    public string Algorithm { get; set; } = "SHA-256";
    public string Challenge { get; set; } = string.Empty;
    public string Salt { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
    public int MaxNumber { get; set; }
}

/// <summary>The solved challenge returned by the client.</summary>
public class CaptchaSolution
{
    public string Algorithm { get; set; } = "SHA-256";
    public string Challenge { get; set; } = string.Empty;
    public long Number { get; set; }
    public string Salt { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
}

public class CaptchaStatus
{
    public bool Verified { get; set; }
}

namespace NuclearEvaluation.Shared.Contracts;

public sealed class StemFileProfile
{
    public int RowCount { get; set; }
    public int MissingU234 { get; set; }
    public int MissingErU234 { get; set; }
    public int MissingU235 { get; set; }
    public int MissingErU235 { get; set; }
    public int BlankLabCode { get; set; }
    public int RepeatedIdentifierRows { get; set; }
}

public enum StemQualityCheck
{
    MissingU234 = 1,
    MissingErU234,
    MissingU235,
    MissingErU235,
    BlankLabCode,
    RepeatedIdentifier,
}

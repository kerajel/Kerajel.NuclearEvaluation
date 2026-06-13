namespace NuclearEvaluation.Shared.Contracts;

/// <summary>Form model for submitting a PMI report; the file travels as multipart content.</summary>
public class PmiReportSubmission
{
    public string ReportName { get; set; } = string.Empty;

    public DateOnly? ReportDate { get; set; }
}

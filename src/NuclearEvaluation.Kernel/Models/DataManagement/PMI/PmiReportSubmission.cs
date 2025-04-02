namespace NuclearEvaluation.Kernel.Models.DataManagement.PMI;

public class PmiReportSubmission
{
    public string AuthorId {  get; set; } = string.Empty;

    public string ReportName { get; set; } = string.Empty;

    public DateOnly? ReportDate { get; set; }

    public required Stream FileStream { get; set; }
}
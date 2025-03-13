namespace NuclearEvaluation.Server.Models.Settings;

public class StemSettings
{
    public long MaxPreviewFileSize { get; set; }
    public string ProcessingQueueName { get; set; } = string.Empty;
    public string ResponseQueueName { get; set; } = string.Empty;
}
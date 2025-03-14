namespace NuclearEvaluation.Kernel.Models.DataManagement.Stem;

public class StemSettings
{
    public long MaxPreviewFileSize { get; set; }
    public string ProcessingExchangeRoutingKey { get; set; } = string.Empty;
    public string ProcessingExchangeName { get; set; } = string.Empty;
    public string ResponseQueueName { get; set; } = string.Empty;
}
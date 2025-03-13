namespace NuclearEvaluation.Kernel.Models.Messaging.StemPreview;

public class ProcessStemPreviewMessage
{
    public Guid SessionId { get; set; }
    public Guid FileId { get; set; }
}
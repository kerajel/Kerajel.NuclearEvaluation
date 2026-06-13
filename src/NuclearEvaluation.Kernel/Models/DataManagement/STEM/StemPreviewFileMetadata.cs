namespace NuclearEvaluation.Kernel.Models.DataManagement.Stem;

public class StemPreviewFileMetadata
{
    public StemPreviewFileMetadata()
    {
    }

    public StemPreviewFileMetadata(Guid id, Guid stemSessionId, string name)
    {
        Id = id;
        StemSessionId = stemSessionId;
        Name = name;
    }

    public Guid Id { get; set; }

    public Guid StemSessionId { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsFullyUploaded { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

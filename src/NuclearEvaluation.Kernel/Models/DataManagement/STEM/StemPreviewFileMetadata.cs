using LinqToDB.Mapping;

namespace NuclearEvaluation.Kernel.Models.DataManagement.Stem;

/// <summary>File-level metadata for a staged STEM upload, held in a per-session temp table.</summary>
public class StemPreviewFileMetadata
{
    public StemPreviewFileMetadata()
    {
    }

    public StemPreviewFileMetadata(Guid id, string name)
    {
        Id = id;
        Name = name;
    }

    [PrimaryKey]
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsFullyUploaded { get; set; }

    public bool IsDeleted { get; set; }
}

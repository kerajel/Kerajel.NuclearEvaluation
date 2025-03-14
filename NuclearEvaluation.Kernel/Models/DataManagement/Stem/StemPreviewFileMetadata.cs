using LinqToDB.Mapping;

namespace NuclearEvaluation.Kernel.Models.DataManagement.Stem;

public class StemPreviewFileMetadata
{

    public StemPreviewFileMetadata(Guid id, string name)
    {
        Id = id;
        Name = name;
    }

    [PrimaryKey]
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsFullyUploaded = false;
}
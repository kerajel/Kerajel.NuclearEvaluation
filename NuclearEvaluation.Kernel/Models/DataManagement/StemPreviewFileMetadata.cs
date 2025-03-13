using LinqToDB.Mapping;

namespace NuclearEvaluation.Kernel.Models.DataManagement;

public class StemPreviewFileMetadata
{
    [PrimaryKey]
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsFullyUploaded = false;
}
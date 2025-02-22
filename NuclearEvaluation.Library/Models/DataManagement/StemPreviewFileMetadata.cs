using LinqToDB.Mapping;

namespace NuclearEvaluation.Library.Models.DataManagement;

public class StemPreviewFileMetadata
{
    [PrimaryKey, Identity]
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsFullyUploaded = false;
}
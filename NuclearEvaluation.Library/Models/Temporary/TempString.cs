using LinqToDB.Mapping;

namespace NuclearEvaluation.Library.Models.Temporary;

public class TempString
{
    [PrimaryKey, Identity]
    public int Id { get; set; }

    public string Value { get; set; } = string.Empty;
}

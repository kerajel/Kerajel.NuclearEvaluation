using CsvHelper;

namespace Kerajel.TabularDataReader.Tests.Models;

[CsvRecord]
public partial class TestRecord
{
    public decimal Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime Date { get; set; }
}

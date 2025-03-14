using CsvHelper.Configuration;
using Kerajel.TabularDataReader.Tests.Models;

namespace Kerajel.TabularDataReader.Tests.Mappings;

public sealed class TestRecordClassMap : ClassMap<TestRecord>
{
    public TestRecordClassMap()
    {
        Map(m => m.Id).Name("Id");
        Map(m => m.Name).Name("Name");
        Map(m => m.Date).Name("Date");
    }
}
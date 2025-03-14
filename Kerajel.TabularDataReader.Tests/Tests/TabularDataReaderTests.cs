using CsvHelper;
using Kerajel.TabularDataReader.Tests.Mappings;
using Kerajel.TabularDataReader.Tests.Models;
using Shouldly;

namespace Kerajel.TabularDataReader.Tests;

public partial class TabularDataReaderTests
{
    [Theory]
    [InlineData("TestData/001.SampleExcel.xlsx")]
    [InlineData("TestData/002.SampleCsv.csv")]
    public void GetCsvReader_ShouldReturnCsvReader(string filePath)
    {
        // Arrange
        using TabularDataReader sut = new();

        using FileStream fs = File.OpenRead(filePath);

        // Act
        CsvReader reader = sut.GetCsvReader(fs, filePath);

        // Assert
        reader.Context.RegisterClassMap<TestRecordClassMap>();
        TestRecord[] records = reader.GetRecords<TestRecord>().ToArray();
        records.Length.ShouldBe(3);

        records[0].ShouldBeEquivalentTo(new TestRecord { Id = 1.01m, Name = "OIP", Date = DateTime.Parse("12/23/2024") });
        records[1].ShouldBeEquivalentTo(new TestRecord { Id = 1.02m, Name = "IAEE", Date = DateTime.Parse("12/31/2024") });
        records[2].ShouldBeEquivalentTo(new TestRecord { Id = 1.03m, Name = "OIP", Date = DateTime.Parse("12/24/2024") });
    }
}
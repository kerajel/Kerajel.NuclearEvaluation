using System.Globalization;
using System.Text;
using CsvHelper;
using Kerajel.TabularDataReader.Tests.Mappings;
using Kerajel.TabularDataReader.Tests.Models;

namespace Kerajel.TabularDataReader.Tests.Tests;

public class ReaderLifetimeTests
{
    [Fact]
    public void InvalidWorkbookFailsAtOpenInsteadOfWaitingOnPipe()
    {
        using TabularDataReader reader = new();
        using MemoryStream input = new(Encoding.UTF8.GetBytes("not an Excel workbook"));
        Assert.ThrowsAny<Exception>(() => reader.GetCsvReader(input, "broken.xlsx"));
    }

    [Fact]
    public void MissingWorksheetReportsErrorInsteadOfEmptySuccess()
    {
        using TabularDataReader reader = new();
        using FileStream input = File.OpenRead("TestData/001.SampleExcel.xlsx");
        Assert.Throws<ArgumentException>(() =>
            reader.GetCsvReader(input, "sample.xlsx", "missing sheet")
        );
    }

    [Theory]
    [InlineData("TestData/001.SampleExcel.xlsx")]
    [InlineData("TestData/002.SampleCsv.csv")]
    public void EarlyDisposalReleasesInputWithoutWaitingForProducer(string path)
    {
        using FileStream input = File.OpenRead(path);
        TabularDataReader reader = new();
        CsvReader csv = reader.GetCsvReader(input, path);
        Assert.True(csv.Read());
        reader.Dispose();
        Assert.False(input.CanRead);
        reader.Dispose();
    }

    [Fact]
    public async Task SpreadsheetAsyncReadingPreservesValuesUnderDifferentCulture()
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            using TabularDataReader reader = new();
            using FileStream input = File.OpenRead("TestData/001.SampleExcel.xlsx");
            CsvReader csv = reader.GetCsvReader(input, "sample.XLSX");
            csv.Context.RegisterClassMap<TestRecordClassMap>();
            List<TestRecord> rows = [];
            await foreach (TestRecord row in csv.GetRecordsAsync<TestRecord>())
                rows.Add(row);
            Assert.Equal(3, rows.Count);
            Assert.Equal(1.01m, rows[0].Id);
            Assert.Equal(new DateTime(2024, 12, 23), rows[0].Date);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void DisposedReaderCannotBeReused()
    {
        TabularDataReader reader = new();
        reader.Dispose();
        using MemoryStream input = new();
        Assert.Throws<ObjectDisposedException>(() => reader.GetCsvReader(input, "file.csv"));
    }
}

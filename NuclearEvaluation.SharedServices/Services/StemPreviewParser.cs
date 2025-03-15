using CsvHelper.Configuration;
using CsvHelper;
using System.Globalization;
using CsvHelper.TypeConversion;
using NuclearEvaluation.Kernel.Interfaces;
using Kerajel.TabularDataReader;
using NuclearEvaluation.Kernel.Models.DataManagement.Stem;
using System.Runtime.CompilerServices;

namespace NuclearEvaluation.SharedServices.Services;

public class StemPreviewParser : IStemPreviewParser
{
    public async IAsyncEnumerable<StemPreviewEntry> Parse(
        Stream stream,
        string fileName,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        TabularDataReader reader = new TabularDataReader();
        CsvReader csvReader = reader.GetCsvReader(stream, fileName);
        csvReader.Context.RegisterClassMap<StemPreviewEntryMap>();

        try
        {
            await foreach (StemPreviewEntry entry in csvReader.GetRecordsAsync<StemPreviewEntry>(ct))
            {
                yield return entry;
            }
        }
        finally
        {
            csvReader.Dispose();
            reader.Dispose();
        }
    }

    private sealed class StemPreviewEntryMap : ClassMap<StemPreviewEntry>
    {
        public StemPreviewEntryMap()
        {
            Map(m => m.Id).Name("Identifier");
            Map(m => m.LabCode).Name("LaboratoryCode");
            Map(m => m.AnalysisDate).Name("AnalysisDate")
                    .TypeConverter<StemDateConverter>();
            Map(m => m.IsNu).Name("IsNu");
            Map(m => m.U234).Name("U234").Optional();
            Map(m => m.ErU234).Name("ErU234").Optional();
            Map(m => m.U235).Name("U235").Optional();
            Map(m => m.ErU235).Name("ErU235").Optional();
        }
    }

    private sealed class StemDateConverter : ITypeConverter
    {
        public object? ConvertFromString(string? text, IReaderRow row, MemberMapData memberMapData)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date))
            {
                return DateOnly.FromDateTime(date);
            }
            else if (double.TryParse(text, out double oaDate))
            {
                try
                {
                    DateTime validDate = DateTime.FromOADate(oaDate);
                    return DateOnly.FromDateTime(validDate);
                }
                catch
                {
                    throw new FormatException($"Invalid OADate value: {text}");
                }
            }
            throw new FormatException($"Invalid date format: {text}");
        }

        public string ConvertToString(object? value, IWriterRow row, MemberMapData memberMapData)
        {
            if (value is DateOnly dateOnly)
            {
                return dateOnly.ToString("yyyy-MM-dd");
            }
            return string.Empty;
        }
    }
}
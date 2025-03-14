using CsvHelper.Configuration;
using CsvHelper;
using Kerajel.Primitives.Models;
using System.Globalization;
using CsvHelper.TypeConversion;
using NuclearEvaluation.Kernel.Interfaces;
using NuclearEvaluation.Kernel.Models.DataManagement;

namespace NuclearEvaluation.Server.Services;

public class StemPreviewParser : IStemPreviewParser
{
    static readonly CsvConfiguration _csvConfig = new(CultureInfo.InvariantCulture)
    {
        HasHeaderRecord = true,
    };

    public async Task<OperationResult<IReadOnlyCollection<StemPreviewEntry>>> Parse(Stream stream, string fileName, CancellationToken ct = default)
    {
        return null;
        //OperationResult<string> operationResult = await TabularDataReader.Read(stream, fileName);
        //if (!operationResult.Succeeded)
        //{
        //    return new(OperationStatus.Faulted, "Error reading the file");
        //}

        //using TextReader reader = new StringReader(operationResult.Content!);
        //using CsvReader csvReader = new(reader, _csvConfig);
        //csvReader.Context.RegisterClassMap<StemPreviewEntryMap>();

        ////TODO handle mapping errors
        //StemPreviewEntry[] entries = csvReader.GetRecords<StemPreviewEntry>()
        //    .ToArray();

        //return new(OperationStatus.Succeeded, entries);
    }

    Task<OperationResult<IReadOnlyCollection<Kernel.Models.DataManagement.StemPreviewEntry>>> IStemPreviewParser.Parse(Stream stream, string fileName, CancellationToken ct)
    {
        throw new NotImplementedException();
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
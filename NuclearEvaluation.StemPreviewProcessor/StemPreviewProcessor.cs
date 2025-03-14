using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using Kerajel.Primitives.Enums;
using Kerajel.Primitives.Models;
using Kerajel.TabularDataReader;
using NuclearEvaluation.Kernel.Interfaces;
using NuclearEvaluation.Kernel.Models.DataManagement;
using NuclearEvaluation.Kernel.Models.Files;
using NuclearEvaluation.Kernel.Models.Messaging.StemPreview;
using System.Globalization;

namespace NuclearEvaluation.StemPreviewProcessor;

public class StemPreviewProcessor
{
    private readonly IEfsFileService _fileService;
    private readonly IStemPreviewEntryService _entryService;
    private readonly ILogger<StemPreviewProcessor> _logger;

    public StemPreviewProcessor(
        IEfsFileService fileService,
        IStemPreviewEntryService entryService,
        ILogger<StemPreviewProcessor> logger)
    {
        _fileService = fileService;
        _entryService = entryService;
        _logger = logger;
    }

    public async Task<OperationResult> Process(ProcessStemPreviewMessage message, CancellationToken ct)
    {
        OperationResult<GetFilePathResponse> getFilePathResult = await _fileService.GetPath(message.FileId, ct);

        if (!getFilePathResult.Succeeded)
        {
            return OperationResult.FromFaulted(getFilePathResult);
        }

        string filePath = getFilePathResult.Content!.FilePath;

        using TabularDataReader reader = new();
        using FileStream fs = File.OpenRead(filePath);
        CsvReader csvReader = reader.GetCsvReader(fs, filePath);
        csvReader.Context.RegisterClassMap<StemPreviewEntryMap>();

        //TODO handle validation errors
        IAsyncEnumerable<StemPreviewEntry> entries = csvReader.GetRecordsAsync<StemPreviewEntry>(ct);

        await _entryService.InsertStemPreviewEntries(message.SessionId, entries, ct);

        //TODO wrap in OperationResult
        //TODO clean up

        return new OperationResult(OperationStatus.Succeeded);
    }

    //TODO extract
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


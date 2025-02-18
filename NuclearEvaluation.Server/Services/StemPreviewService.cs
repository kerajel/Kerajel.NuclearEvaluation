using CsvHelper.Configuration;
using CsvHelper;
using Kerajel.Primitives.Enums;
using Kerajel.Primitives.Models;
using Kerajel.TabularDataReader.Services;
using NuclearEvaluation.Library.Interfaces;
using NuclearEvaluation.Library.Models.DataManagement;
using Polly;
using Polly.Bulkhead;
using System.Globalization;
using CsvHelper.TypeConversion;

namespace NuclearEvaluation.Server.Services;

public class StemPreviewService : IStemPreviewService
{
    static readonly AsyncBulkheadPolicy<OperationResult> bulkheadPolicy = Policy
        .BulkheadAsync<OperationResult>(
            maxParallelization: 4,
            maxQueuingActions: 128,
            onBulkheadRejectedAsync: async context =>
            {
                await Task.CompletedTask;
            });

    readonly ITempTableService _tempTableService;

    const string entryTableSuffix = "stem-entry";
    const string fileNameTableSuffix = "stem-file-name";

    public StemPreviewService(ITempTableService tempTableService)
    {
        _tempTableService = tempTableService;
    }

    // TODO: Optimize memory usage by implementing file streaming.
    // The ideal approach involves streaming the input file to a physical temporary file,
    // then having RUST process this file and output to another temporary file.
    // Subsequently, CSVHelper should read this output file in chunks, streaming parsed entries directly to the database.
    // This method conserves memory by avoiding in-memory processing of the entire file at once.
    // For simplicity, the current implementation handles everything in memory.
    public async Task<OperationResult> UploadStemPreviewFile(
        Guid stemSessionId,
        Stream stream,
        string fileName,
        CancellationToken? externalCt = default)
    {
        //TODO options
        using CancellationTokenSource internalCts = new(TimeSpan.FromMinutes(1));

        using CancellationTokenSource linkedCts = externalCt.HasValue ?
            CancellationTokenSource.CreateLinkedTokenSource(internalCts.Token, externalCt.Value) :
            internalCts;

        (string entryTable, string fileNameTable) = GetTableNames(stemSessionId);

        try
        {
            OperationResult result = await bulkheadPolicy.ExecuteAsync(
                async (CancellationToken ct) =>
                {
                    try
                    {
                        OperationResult<string> operationResult = await TabularDataReader.Read(stream, fileName);
                        if (!operationResult.Succeeded)
                        {
                            return new OperationResult(OperationStatus.Faulted, "Error reading the file");
                        }

                        using TextReader reader = new StringReader(operationResult.Content!);
                        CsvConfiguration csvConfig = new(CultureInfo.InvariantCulture)
                        {
                            HasHeaderRecord = true,
                        };

                        using CsvReader csvReader = new(reader, csvConfig);
                        csvReader.Context.RegisterClassMap<StemPreviewEntryMap>();

                        StemPreviewEntry[] entries = csvReader.GetRecords<StemPreviewEntry>()
                            .ToArray();

                        //TODO handle mapping errors
                        await _tempTableService.BulkCopyInto(entryTable, entries);

                        return new OperationResult(OperationStatus.Succeeded);
                    }
                    catch (Exception)
                    {
                        return new OperationResult(OperationStatus.Faulted, "Error processing the file");
                    }
                },
                linkedCts.Token);
            return result;
        }
        catch (BulkheadRejectedException)
        {
            return new OperationResult(OperationStatus.Faulted, "Too many concurrent uploads. Please try again later.");
        }
        catch (OperationCanceledException)
        {
            return new OperationResult(OperationStatus.Faulted, "The upload was canceled or timed out.");
        }
    }

    public (string EntryTable, string FileNameTable) GetTableNames(Guid sessionId)
    {
        return ($"{sessionId}-{entryTableSuffix}", $"{sessionId}-{fileNameTableSuffix}");
    }

    public void Dispose()
    {
        _tempTableService.Dispose();
    }

    public sealed class StemPreviewEntryMap : ClassMap<StemPreviewEntry>
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

    public class StemDateConverter : ITypeConverter
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
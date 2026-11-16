using System.Globalization;
using System.Runtime.CompilerServices;
using CsvHelper;
using Kerajel.TabularDataReader;

namespace NuclearEvaluation.Server.Services.STEM;

public class StemPreviewParser : IStemPreviewParser
{
    public async IAsyncEnumerable<StemPreviewEntry> Parse(
        Stream stream,
        string fileName,
        [EnumeratorCancellation] CancellationToken ct = default
    )
    {
        using TabularDataReader reader = new();
        CsvReader csv = reader.GetCsvReader(stream, fileName);
        await csv.ReadHeaderAsync(ct);
        IReadOnlyList<string> headers = csv.Headers;
        if (headers.Count == 0)
            yield break;

        int identifier = FindColumn(headers, "Identifier");
        int labCode = FindColumn(headers, "LaboratoryCode");
        int analysisDate = FindColumn(headers, "AnalysisDate");
        int isNu = FindColumn(headers, "IsNu");
        int u234 = FindColumn(headers, "U234", required: false);
        int erU234 = FindColumn(headers, "ErU234", required: false);
        int u235 = FindColumn(headers, "U235", required: false);
        int erU235 = FindColumn(headers, "ErU235", required: false);

        while (await csv.ReadAsync(ct))
        {
            ct.ThrowIfCancellationRequested();
            yield return new StemPreviewEntry
            {
                Id = ReadDecimal(csv.GetSpan(identifier)),
                LabCode = csv.GetString(labCode),
                AnalysisDate = ReadDate(csv.GetString(analysisDate)),
                IsNu = ReadBoolean(csv.GetString(isNu)),
                U234 = ReadOptionalDecimal(csv, u234),
                ErU234 = ReadOptionalDecimal(csv, erU234),
                U235 = ReadOptionalDecimal(csv, u235),
                ErU235 = ReadOptionalDecimal(csv, erU235),
            };
        }
    }

    // Keep the STEM template's exact header names rather than adopting v34's fuzzy fallback.
    static int FindColumn(IReadOnlyList<string> headers, string name, bool required = true)
    {
        for (int index = 0; index < headers.Count; index++)
        {
            if (headers[index] == name)
                return index;
        }
        if (required)
            throw new FormatException($"Missing required STEM column '{name}'.");
        return -1;
    }

    static decimal ReadDecimal(ReadOnlySpan<byte> value) =>
        decimal.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture);

    static decimal? ReadOptionalDecimal(CsvReader csv, int index)
    {
        if (index < 0)
            return null;

        ReadOnlySpan<byte> value = csv.GetSpan(index);
        return value.IsEmpty ? null : ReadDecimal(value);
    }

    static bool ReadBoolean(string text)
    {
        if (bool.TryParse(text, out bool value))
            return value;

        if (
            int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number)
            && number is 0 or 1
        )
            return number == 1;

        throw new FormatException("Invalid IsNu value.");
    }

    static DateOnly ReadDate(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new FormatException("AnalysisDate is required.");

        if (
            DateTime.TryParse(
                text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime date
            )
        )
            return DateOnly.FromDateTime(date);

        if (
            double.TryParse(
                text,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double oaDate
            )
        )
        {
            try
            {
                return DateOnly.FromDateTime(DateTime.FromOADate(oaDate));
            }
            catch (ArgumentException ex)
            {
                throw new FormatException("Invalid Excel serial date for AnalysisDate.", ex);
            }
        }
        throw new FormatException("Invalid AnalysisDate value.");
    }
}

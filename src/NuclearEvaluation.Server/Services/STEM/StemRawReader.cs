using System.Text.Json;
using LinqToDB.Data;

namespace NuclearEvaluation.Server.Services.STEM;

internal static class StemRawReader
{
    const string measurementsType = "DECIMAL(38, 15)";

    internal static bool IsLegacySpreadsheet(string extension) =>
        extension.Equals(".xls", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".xlsb", StringComparison.OrdinalIgnoreCase);

    internal static bool IsXlsx(string extension) =>
        extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase);

    internal static async Task AppendAsync(
        DataConnection db,
        string path,
        Guid fileId,
        string fileName,
        CancellationToken ct
    )
    {
        bool excel = IsXlsx(Path.GetExtension(path));
        string reader = excel
            ? "read_xlsx($source_path, header = false, all_varchar = true, stop_at_empty = false)"
            : "read_csv($source_path, header = false, all_varchar = true, strict_mode = true,"
                + " skip = 0)";
        DataParameter sourcePath = new("source_path", path);
        List<string> firstRow = await new CommandInfo(
            db,
            $"SELECT CAST(to_json(input_row) AS VARCHAR) FROM (SELECT * FROM {reader} LIMIT 1) input_row",
            sourcePath
        ).QueryToListAsync<string>(ct);
        if (firstRow.Count == 0)
            return;

        // Header inference trims and de-duplicates names. Use the original header values and
        // DuckDB's generated names so the exact STEM spelling and first matching column win.
        Dictionary<string, string> columns = new(StringComparer.Ordinal);
        using (JsonDocument header = JsonDocument.Parse(firstRow[0]))
        {
            foreach (JsonProperty column in header.RootElement.EnumerateObject())
            {
                if (column.Value.GetString() is string name)
                    columns.TryAdd(name, QuoteIdentifier(column.Name));
            }
        }
        string identifier = RequireColumn(columns, "Identifier");
        string labCode = RequireColumn(columns, "LaboratoryCode");
        string analysisDate = RequireColumn(columns, "AnalysisDate");
        string isNu = RequireColumn(columns, "IsNu");

        string sql = $"""
            INSERT INTO stem_entries
                (Id, LabCode, AnalysisDate, IsNu, U234, ErU234, U235, ErU235, FileId, RowOrdinal, FileName)
            SELECT
                {ReadDecimal(identifier, "Identifier", "DECIMAL(10, 2)", excel)},
                coalesce({labCode}, ''),
                {ReadDate(analysisDate)},
                {ReadBoolean(isNu)},
                {ReadOptionalDecimal(columns, "U234", excel)},
                {ReadOptionalDecimal(columns, "ErU234", excel)},
                {ReadOptionalDecimal(columns, "U235", excel)},
                {ReadOptionalDecimal(columns, "ErU235", excel)},
                $file_id,
                stem_ordinal,
                $file_name
            FROM (SELECT *, row_number() OVER () - 2 AS stem_ordinal FROM {reader})
            WHERE stem_ordinal >= 0
            """;

        await new CommandInfo(
            db,
            sql,
            sourcePath,
            new DataParameter("file_id", fileId),
            new DataParameter("file_name", fileName)
        ).ExecuteAsync(ct);
    }

    static string QuoteIdentifier(string name) => $"\"{name.Replace("\"", "\"\"")}\"";

    static string RequireColumn(Dictionary<string, string> columns, string header) =>
        columns.TryGetValue(header, out string? column)
            ? column
            : throw new FormatException($"Missing required STEM column '{header}'.");

    static string Trim(string column) =>
        $"trim({column}, concat(' ', chr(9), chr(10), chr(11), chr(12), chr(13)))";

    static string ReadOptionalDecimal(Dictionary<string, string> columns, string header, bool excel)
    {
        if (!columns.TryGetValue(header, out string? column))
            return $"CAST(NULL AS {measurementsType})";

        string text = Trim(column);
        // Parse plain numbers with 64-bit decimals, then widen to the table's full precision.
        // The lower-scale cast is exact because its pattern limits the fractional digits.
        return $$"""
            CASE WHEN {{column}} IS NULL OR {{column}} = '' THEN NULL
                ELSE coalesce(
                    CASE
                        WHEN regexp_full_match({{text}}, '[+-]?([0-9]{0,3}[.][0-9]+|[0-9]{1,3}[.]?)')
                        THEN CAST(TRY_CAST({{text}} AS DECIMAL(18, 15)) AS {{measurementsType}})
                        WHEN regexp_full_match({{text}}, '[+-]?([0-9]{0,8}[.][0-9]{1,10}|[0-9]{1,8}[.]?)')
                        THEN CAST(TRY_CAST({{text}} AS DECIMAL(18, 10)) AS {{measurementsType}})
                    END,
                    {{ReadDecimal(column, header, measurementsType, excel)}}
                )
            END
            """;
    }

    static string ReadDecimal(string column, string header, string type, bool excel)
    {
        string text = Trim(column);
        const string digits = "([0-9][0-9,]*([.][0-9]*)?|[.][0-9]+)";
        // An XLSX numeric cell may use exponent notation in the workbook XML.
        string exponent = excel ? "|[+-]?([0-9]+([.][0-9]*)?|[.][0-9]+)[Ee][+-]?[0-9]+" : "";
        return $"""
            CASE
                WHEN regexp_full_match({text}, '([+-]?{digits}|{digits}[+-]{exponent})')
                THEN CAST(
                    replace(
                        CASE WHEN right({text}, 1) IN ('+', '-')
                            THEN right({text}, 1) || left({text}, length({text}) - 1)
                            ELSE {text}
                        END,
                        ',', ''
                    )
                    AS {type}
                )
                ELSE error('Invalid {header} value.')
            END
            """;
    }

    static string ReadBoolean(string column)
    {
        string text = Trim(column);
        return $"""
            CASE
                WHEN lower({text}) = 'true' OR regexp_full_match({text}, '[+]?0*1') THEN true
                WHEN lower({text}) = 'false' OR regexp_full_match({text}, '[+-]?0+') THEN false
                ELSE error('Invalid IsNu value.')
            END
            """;
    }

    static string ReadDate(string column)
    {
        string text = Trim(column);
        string date = $"""
            coalesce(
                try_cast({text} AS TIMESTAMP),
                try_strptime({text}, [
                    '%m/%d/%Y', '%m/%d/%Y %H:%M', '%m/%d/%Y %H:%M:%S',
                    '%m/%d/%Y %I:%M %p', '%m/%d/%Y %I:%M:%S %p',
                    '%b %d, %Y', '%B %d, %Y', '%d %b %Y', '%d %B %Y'
                ])
            )
            """;
        string serial = $"try_cast({text} AS DOUBLE)";
        return $"""
            CASE
                WHEN {serial} > -657435 AND {serial} < 2958466
                    THEN DATE '1899-12-30' + CAST(trunc({serial}) AS INTEGER)
                WHEN {date} >= TIMESTAMP '0001-01-01'
                    AND {date} < TIMESTAMP '10000-01-01'
                    THEN CAST({date} AS DATE)
                ELSE error('Invalid AnalysisDate value.')
            END
            """;
    }
}

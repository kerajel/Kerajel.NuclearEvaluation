using System.Data.Common;
using System.Globalization;
using DuckDB.NET.Data;

namespace NuclearEvaluation.Server.Services.Workbench;

static class ImportReviewLargeSampleExport
{
    // Match the annual CSV schema in scripts/export-radnet-large-csv.py.
    const string ExportSql = """
        COPY (
            SELECT o.id, o.source_file_id, o.source_row, o.observed_at, o.status,
                   o.exposure_mr_h, o.dose_nsv_h, o.r02, o.r03, o.r04, o.r05,
                   o.r06, o.r07, o.r08, o.r09, s.id AS station_id, s.name AS station_name,
                   s.state, f.year, f.file_name
            FROM observations o
            JOIN source_files f ON f.id = o.source_file_id
            JOIN stations s ON s.id = f.station_id
        ) TO $path (
            FORMAT CSV, HEADER, PARTITION_BY (source_file_id), WRITE_PARTITION_COLUMNS true,
            RETURN_FILES true
        )
        """;

    internal static async Task ExportAsync(
        string snapshotPath,
        DirectoryInfo destination,
        long minimumRows,
        CancellationToken ct
    )
    {
        string parent = destination.Parent?.FullName
            ?? throw new InvalidDataException("The large RadNet CSV directory has no parent.");
        string work = Path.Combine(parent, $".radnet-large-sample-{Guid.NewGuid():N}");
        Directory.CreateDirectory(work);
        try
        {
            string partitions = Path.Combine(work, "partitions");
            string export = Path.Combine(work, "annual");
            await PopulateAsync(snapshotPath, work, partitions, export, minimumRows, ct);
            ct.ThrowIfCancellationRequested();
            try
            {
                // Publish the complete collection in a single move on the same volume.
                Directory.Move(export, destination.FullName);
            }
            catch (IOException) when (Directory.Exists(destination.FullName))
            {
                // An overlapping host process already published a collection.
            }
        }
        finally
        {
            Directory.Delete(work, recursive: true);
        }
    }

    static async Task PopulateAsync(
        string snapshotPath,
        string work,
        string partitions,
        string export,
        long minimumRows,
        CancellationToken ct
    )
    {
        // Use a private snapshot copy so the exporter and archive readers can have independent
        // DuckDB resource and external file settings.
        string copy = Path.Combine(work, "archive.duckdb");
        await using (FileStream input = File.OpenRead(snapshotPath))
        await using (FileStream output = File.Create(copy))
            await input.CopyToAsync(output, ct);

        await using DuckDBConnection connection = new(
            new DuckDBConnectionStringBuilder
            {
                DataSource = copy,
                AccessMode = DuckDBAccessMode.ReadOnly,
                Threads = 1,
                MemoryLimit = "512MB",
                TempDirectory = Path.Combine(work, "spill"),
                MaxTempDirectorySize = "2GB",
            }.ConnectionString
        );
        await connection.OpenAsync(ct);

        List<(int Id, long Rows)> sources = [];
        await using (DuckDBCommand sourceFiles = (DuckDBCommand)connection.CreateCommand())
        {
            sourceFiles.CommandText = "SELECT id, row_count FROM source_files ORDER BY id";
            await using DbDataReader reader = await sourceFiles.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                sources.Add((reader.GetInt32(0), reader.GetInt64(1)));
        }

        long expected = sources.Sum(source => source.Rows);
        if (expected < minimumRows)
            throw new InvalidDataException(
                "The RadNet archive is too small for the large CSV collection."
            );

        await using (DuckDBCommand write = (DuckDBCommand)connection.CreateCommand())
        {
            write.CommandText = ExportSql;
            write.Parameters.Add(new DuckDBParameter("path", partitions));
            if (await write.ExecuteScalarAsync(ct) is not long rows || rows != expected)
                throw new InvalidDataException(
                    "The RadNet CSV export did not contain every archive observation."
                );
        }

        Directory.CreateDirectory(export);
        foreach ((int id, long rows) in sources)
        {
            ct.ThrowIfCancellationRequested();
            string idText = id.ToString(CultureInfo.InvariantCulture);
            string target = Path.Combine(export, idText + ".csv");
            if (rows == 0)
            {
                await File.WriteAllTextAsync(target, ImportReviewLargeSample.Header + "\n", ct);
                continue;
            }

            string partition = Path.Combine(partitions, "source_file_id=" + idText);
            string[] files = Directory.GetFiles(partition, "*.csv");
            if (files.Length != 1)
                throw new InvalidDataException(
                    "The RadNet CSV export did not produce one file per source."
                );
            File.Move(files[0], target);
            Directory.Delete(partition);
        }

        Directory.Delete(partitions);
    }
}

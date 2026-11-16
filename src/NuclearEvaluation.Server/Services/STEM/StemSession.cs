using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using DuckDB.NET.Data;
using LinqToDB;
using LinqToDB.Async;
using LinqToDB.Data;
using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Kernel.Extensions;
using NuclearEvaluation.Shared.Contracts;
using NuclearEvaluation.Shared.Models.Views;

namespace NuclearEvaluation.Server.Services.STEM;

/// <summary>Owns the original uploads and one DuckDB temporary table for a preview session.</summary>
public sealed class StemSession : IAsyncDisposable
{
    readonly SemaphoreSlim _gate = new(1, 1);
    readonly SemaphoreSlim _operations;
    readonly ILogger _logger;
    readonly string _directory;
    readonly string _uploads;
    readonly string _connectionString;
    readonly Dictionary<Guid, StoredFile> _files = [];
    DataConnection? _database;
    DateTime _lastAccessUtc = DateTime.UtcNow;
    bool _disposed;

    internal StemSession(string directory, SemaphoreSlim operations, ILogger logger)
    {
        _directory = directory;
        _uploads = Path.Combine(directory, "uploads");
        _operations = operations;
        _logger = logger;
        _connectionString = new DbConnectionStringBuilder
        {
            ["Data Source"] = ":memory:",
            ["threads"] = 1,
            ["memory_limit"] = "128MiB",
            ["temp_directory"] = Path.Combine(directory, "scratch"),
            ["max_temp_directory_size"] = "256MiB",
            ["allow_community_extensions"] = false,
            ["autoinstall_known_extensions"] = false,
            ["autoload_known_extensions"] = false,
            ["default_null_order"] = "NULLS_FIRST_ON_ASC_LAST_ON_DESC",
            ["default_collation"] = "nocase",
        }.ConnectionString;
    }

    internal Task AppendFileAsync(
        Guid fileId,
        string fileName,
        Stream content,
        IStemPreviewParser compatibilityParser,
        CancellationToken ct
    ) =>
        RunExclusive(
            async db =>
            {
                // A retry after a successful request must not append the same file again.
                if (_files.ContainsKey(fileId))
                    return;

                string extension = Path.GetExtension(fileName).ToLowerInvariant();
                bool legacy = StemRawReader.IsLegacySpreadsheet(extension);
                string storedExtension =
                    legacy || StemRawReader.IsXlsx(extension) ? extension : ".csv";
                string path = Path.Combine(
                    _uploads,
                    Guid.NewGuid().ToString("N") + storedExtension
                );
                StoredFile file = new(fileId, fileName, path, legacy ? compatibilityParser : null);
                try
                {
                    await using (
                        FileStream output = new(
                            path,
                            FileMode.CreateNew,
                            FileAccess.Write,
                            FileShare.None,
                            81_920,
                            FileOptions.Asynchronous | FileOptions.SequentialScan
                        )
                    )
                    {
                        await content.CopyToAsync(output, ct);
                    }

                    try
                    {
                        await AppendRowsAsync(db, file, ct);
                    }
                    catch (DuckDBException)
                        when (file.Parser is null && !ct.IsCancellationRequested)
                    {
                        // The previous reader accepts additional date and delimiter variants.
                        // Retry after the failed native transaction has rolled back.
                        file = file with { Parser = compatibilityParser };
                        await AppendRowsAsync(db, file, ct);
                    }

                    _files.Add(fileId, file);
                }
                catch
                {
                    DeleteRawFile(path, fileId);
                    throw;
                }
            },
            ct
        );

    internal Task DeleteFileAsync(Guid fileId, CancellationToken ct = default) =>
        RunExclusive(
            async db =>
            {
                if (!_files.TryGetValue(fileId, out StoredFile? file))
                    return;

                await db.GetTable<StemPreviewEntry>()
                    .Where(row => row.FileId == fileId)
                    .DeleteAsync(ct);
                _files.Remove(fileId);
                DeleteRawFile(file.Path, fileId);
            },
            ct
        );

    internal Task<StemFileProfile?> GetFileProfileAsync(Guid fileId, CancellationToken ct) =>
        RunExclusive<StemFileProfile?>(
            async db =>
            {
                if (!_files.ContainsKey(fileId))
                {
                    return null;
                }

                IQueryable<StemPreviewEntry> rows = db.GetTable<StemPreviewEntry>()
                    .Where(row => row.FileId == fileId);
                StemFileProfile? profile = await rows.GroupBy(row => row.FileId)
                    .Select(group => new StemFileProfile
                    {
                        RowCount = group.Count(),
                        MissingU234 = group.Count(row => row.U234 == null),
                        MissingErU234 = group.Count(row => row.ErU234 == null),
                        MissingU235 = group.Count(row => row.U235 == null),
                        MissingErU235 = group.Count(row => row.ErU235 == null),
                        BlankLabCode = group.Count(row => row.LabCode.Trim() == ""),
                    })
                    .SingleOrDefaultAsync(ct);
                if (profile is null)
                {
                    return new();
                }

                profile.RepeatedIdentifierRows = await FilterQualityRows(
                        rows,
                        StemQualityCheck.RepeatedIdentifier
                    )
                    .CountAsync(ct);
                return profile;
            },
            ct,
            () => null
        );

    public Task<FetchDataResult<StemPreviewEntryView>> QueryViewsAsync(
        FetchDataCommand<StemPreviewEntryView> command,
        CancellationToken ct = default
    ) =>
        RunExclusive(
            async db =>
            {
                IQueryable<StemPreviewEntry> entries = db.GetTable<StemPreviewEntry>();
                if (command.Query?.StemFileId is Guid fileId)
                {
                    entries = entries.Where(row => row.FileId == fileId);
                }
                IQueryable<PreviewRow> rows = FilterQualityRows(entries, command.Query?.StemCheck)
                    .Select(entry => new PreviewRow
                    {
                        Id = entry.Id,
                        LabCode = entry.LabCode,
                        AnalysisDate = entry.AnalysisDate,
                        IsNu = entry.IsNu,
                        U234 = entry.U234,
                        ErU234 = entry.ErU234,
                        U235 = entry.U235,
                        ErU235 = entry.ErU235,
                        FileId = entry.FileId,
                        FileName = entry.FileName,
                        RowOrdinal = entry.RowOrdinal,
                    });
                IQueryable<PreviewRow> filtered = FilterRows(rows, command.Query?.Filter);

                int total = await filtered.CountAsync(ct);
                IOrderedQueryable<PreviewRow> ordered =
                    (IOrderedQueryable<PreviewRow>)
                        filtered.OrderByWithFallback(command.Query, x => x.Id);
                PreviewRow[] data = await ordered
                    .ThenBy(x => x.FileId)
                    .ThenBy(x => x.RowOrdinal)
                    .PageWithFallback(command.Query)
                    .ToArrayAsync(ct);

                FetchDataResult<StemPreviewEntryView> result =
                    FetchDataResult<StemPreviewEntryView>.Succeeded(data);
                result.TotalCount = total;
                return result;
            },
            ct,
            () => FetchDataResult<StemPreviewEntryView>.Succeeded([])
        );

    static IQueryable<StemPreviewEntry> FilterQualityRows(
        IQueryable<StemPreviewEntry> rows,
        StemQualityCheck? check
    ) =>
        check switch
        {
            null => rows,
            StemQualityCheck.MissingU234 => rows.Where(row => row.U234 == null),
            StemQualityCheck.MissingErU234 => rows.Where(row => row.ErU234 == null),
            StemQualityCheck.MissingU235 => rows.Where(row => row.U235 == null),
            StemQualityCheck.MissingErU235 => rows.Where(row => row.ErU235 == null),
            StemQualityCheck.BlankLabCode => rows.Where(row => row.LabCode.Trim() == ""),
            StemQualityCheck.RepeatedIdentifier => rows.Where(row =>
                rows.GroupBy(candidate => candidate.Id)
                    .Where(group => group.Count() > 1)
                    .Select(group => group.Key)
                    .Contains(row.Id)
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(check)),
        };

    static async Task AppendRowsAsync(DataConnection db, StoredFile file, CancellationToken ct)
    {
        await using DataConnectionTransaction transaction = await db.BeginTransactionAsync(ct);
        if (file.Parser is null)
        {
            await StemRawReader.AppendAsync(db, file.Path, file.Id, file.Name, ct);
        }
        else
        {
            using FileStream input = File.OpenRead(file.Path);
            await db.BulkCopyAsync(
                new BulkCopyOptions { BulkCopyType = BulkCopyType.ProviderSpecific },
                ReadCompatibilityRows(file, input, file.Parser, ct),
                cancellationToken: ct
            );
        }

        // Once committed, publish the file in the session even if the request is canceled.
        ct.ThrowIfCancellationRequested();
        await transaction.CommitAsync(CancellationToken.None);
    }

    static async IAsyncEnumerable<StemPreviewEntry> ReadCompatibilityRows(
        StoredFile file,
        Stream input,
        IStemPreviewParser parser,
        [EnumeratorCancellation] CancellationToken ct
    )
    {
        long ordinal = 0;
        await foreach (
            StemPreviewEntry entry in parser.Parse(input, file.Name, ct).WithCancellation(ct)
        )
        {
            ct.ThrowIfCancellationRequested();
            entry.FileId = file.Id;
            entry.FileName = file.Name;
            entry.RowOrdinal = ordinal++;
            yield return entry;
        }
    }

    async Task<DataConnection> GetDatabaseAsync(CancellationToken ct)
    {
        if (_database?.TryGetDbConnection()?.State == ConnectionState.Open)
            return _database;
        if (_database is not null)
        {
            await _database.DisposeAsync();
            _database = null;
        }

        Directory.CreateDirectory(_uploads);
        DataConnection db = new(new DataOptions().UseDuckDB(_connectionString));
        try
        {
            string excelExtension = Path.Combine(
                AppContext.BaseDirectory,
                "duckdb-extensions",
                "excel.duckdb_extension"
            );
            await db.ExecuteAsync($"LOAD {SqlLiteral(excelExtension)}", ct);
            await db.ExecuteAsync(
                $"SET allowed_directories = [{SqlLiteral(_uploads + Path.DirectorySeparatorChar)}]",
                ct
            );
            await db.ExecuteAsync("SET enable_external_access = false", ct);
            await db.ExecuteAsync("SET lock_configuration = true", ct);
            await db.CreateTableAsync<StemPreviewEntry>(
                tableOptions: TableOptions.IsTemporary,
                token: ct
            );

            foreach (StoredFile file in _files.Values)
                await AppendRowsAsync(db, file, ct);

            _database = db;
            return db;
        }
        catch
        {
            await db.DisposeAsync();
            throw;
        }
    }

    static string SqlLiteral(string value) =>
        "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";

    static IQueryable<PreviewRow> FilterRows(IQueryable<PreviewRow> rows, string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return rows;

        IQueryable<PreviewRow> filtered = rows.FilterWithFallback(filter);
        return filtered.Provider.CreateQuery<PreviewRow>(
            new PreviewStringSearch().Visit(filtered.Expression)
        );
    }

    Task<bool> RunExclusive(Func<DataConnection, Task> action, CancellationToken ct) =>
        RunExclusive(
            async db =>
            {
                await action(db);
                return true;
            },
            ct
        );

    async Task<T> RunExclusive<T>(
        Func<DataConnection, Task<T>> action,
        CancellationToken ct,
        Func<T>? whenExpired = null
    )
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (_disposed && whenExpired is not null)
                return whenExpired();
            ObjectDisposedException.ThrowIf(_disposed, this);

            await _operations.WaitAsync(ct);
            try
            {
                return await action(await GetDatabaseAsync(ct));
            }
            finally
            {
                _lastAccessUtc = DateTime.UtcNow;
                _operations.Release();
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    internal bool TryEvictIdle(DateTime cutoff)
    {
        if (!_gate.Wait(0))
            return false;

        try
        {
            if (_lastAccessUtc >= cutoff)
                return false;

            DeleteSession();
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync();
        try
        {
            DeleteSession();
        }
        finally
        {
            _gate.Release();
        }
    }

    void DeleteSession()
    {
        _disposed = true;
        _database?.Dispose();
        _database = null;
        _files.Clear();
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    void DeleteRawFile(string path, Guid fileId)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(
                ex,
                "Could not remove STEM upload {FileId} before session eviction.",
                fileId
            );
        }
    }

    sealed record StoredFile(Guid Id, string Name, string Path, IStemPreviewParser? Parser);

    // The ordinal is used for paging but is not part of the public response contract.
    sealed class PreviewRow : StemPreviewEntryView
    {
        public long RowOrdinal { get; init; }
    }

    // DuckDB's case-insensitive collation applies to equality and sorting, but not LIKE.
    // Normalize string searches so they behave like the existing SQL Server preview.
    sealed class PreviewStringSearch : ExpressionVisitor
    {
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (
                node.Object is { Type: var receiverType } receiver
                && receiverType == typeof(string)
                && node.Arguments.Count == 1
                && node.Arguments[0].Type == typeof(string)
                && node.Method.Name
                    is nameof(string.Contains)
                        or nameof(string.StartsWith)
                        or nameof(string.EndsWith)
            )
            {
                return node.Update(Lower(Visit(receiver)), [Lower(Visit(node.Arguments[0]))]);
            }

            return base.VisitMethodCall(node);
        }

        static Expression Lower(Expression expression) =>
            expression is ConstantExpression { Value: string value }
                ? Expression.Constant(value.ToLowerInvariant())
                : Expression.Call(expression, nameof(string.ToLower), Type.EmptyTypes);
    }
}

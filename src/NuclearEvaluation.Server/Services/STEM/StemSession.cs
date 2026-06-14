using LinqToDB;
using LinqToDB.Async;
using LinqToDB.Data;
using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Kernel.Extensions;
using NuclearEvaluation.Kernel.Models.DataManagement.Stem;
using NuclearEvaluation.Shared.Models.Views;

namespace NuclearEvaluation.Server.Services.STEM;

/// <summary>
/// One anonymous STEM preview session. It owns a single kept-open linq2db connection on which
/// it creates SQL Server *global temporary tables* (##...) for the uploaded rows and file
/// metadata. Because the connection stays open for the session's lifetime, those throwaway
/// tables survive across separate (stateless) API requests and are dropped when the session is
/// evicted — demonstrating staging large datasets without ever touching the real schema.
/// </summary>
public sealed class StemSession : IAsyncDisposable
{
    const int maxBatchSize = 10_000;
    const int bulkCopyTimeout = 60 * 5;
    static readonly TableOptions TempTableOptions = TableOptions.IsGlobalTemporaryStructure;

    readonly SemaphoreSlim _gate = new(1, 1);
    readonly DataConnection _db;
    readonly string _entryTable;
    readonly string _fileTable;

    ITable<StemPreviewEntry>? _entries;
    ITable<StemPreviewFileMetadata>? _files;

    public DateTime LastAccessUtc { get; private set; } = DateTime.UtcNow;

    public StemSession(Guid sessionId, string connectionString)
    {
        _db = new DataConnection(new DataOptions().UseSqlServer(connectionString));
        _entryTable = $"##stem_entries_{sessionId:N}";
        _fileTable = $"##stem_files_{sessionId:N}";
    }

    public async Task InsertFileMetadataAsync(StemPreviewFileMetadata fileMetadata, CancellationToken ct = default)
    {
        await RunExclusive(async () =>
        {
            await EnsureTablesAsync(ct);
            await _db.InsertAsync(fileMetadata, tableName: _fileTable, tableOptions: TempTableOptions, token: ct);
        });
    }

    public async Task BulkCopyEntriesAsync(IAsyncEnumerable<StemPreviewEntry> entries, CancellationToken ct = default)
    {
        await RunExclusive(async () =>
        {
            await EnsureTablesAsync(ct);

            BulkCopyOptions options = new()
            {
                TableName = _entryTable,
                TableOptions = TempTableOptions,
                BulkCopyTimeout = bulkCopyTimeout,
                MaxBatchSize = maxBatchSize,
            };

            await _db.BulkCopyAsync(options, entries, cancellationToken: ct);
        });
    }

    public async Task EnsureIndexAsync(CancellationToken ct = default)
    {
        await RunExclusive(async () =>
        {
            await EnsureTablesAsync(ct);
            await _db.ExecuteProcAsync("[DBO].EnsureIndexOnTempTableField",
                new { tableName = _entryTable, fieldName = nameof(StemPreviewEntry.Id) });
        });
    }

    public async Task SetFileFullyUploadedAsync(Guid fileId, CancellationToken ct = default)
    {
        await RunExclusive(async () =>
        {
            await EnsureTablesAsync(ct);
            await _files!.Where(x => x.Id == fileId).Set(x => x.IsFullyUploaded, true).UpdateAsync(ct);
        });
    }

    public async Task MarkFileDeletedAsync(Guid fileId, CancellationToken ct = default)
    {
        await RunExclusive(async () =>
        {
            await EnsureTablesAsync(ct);
            await _files!.Where(x => x.Id == fileId).Set(x => x.IsDeleted, true).UpdateAsync(ct);
        });
    }

    public async Task<FetchDataResult<StemPreviewEntryView>> QueryViewsAsync(FetchDataCommand<StemPreviewEntryView> command, CancellationToken ct = default)
    {
        return await RunExclusive(async () =>
        {
            await EnsureTablesAsync(ct);

            IQueryable<StemPreviewEntryView> baseQuery =
                from entry in _entries!
                join file in _files! on entry.FileId equals file.Id
                where file.IsFullyUploaded && !file.IsDeleted
                select new StemPreviewEntryView
                {
                    Id = entry.Id,
                    LabCode = entry.LabCode,
                    AnalysisDate = entry.AnalysisDate,
                    IsNu = entry.IsNu,
                    U234 = entry.U234,
                    ErU234 = entry.ErU234,
                    U235 = entry.U235,
                    ErU235 = entry.ErU235,
                    FileId = file.Id,
                    FileName = file.Name,
                };

            IQueryable<StemPreviewEntryView> filtered = baseQuery.FilterWithFallback(command.Query);

            int[] totals = await AsyncExtensions.ToArrayAsync(
                filtered.GroupBy(_ => 1).Select(g => g.Count()),
                ct);
            int total = totals.SingleOrDefault();

            IQueryable<StemPreviewEntryView> dataQuery = filtered
                .OrderByWithFallback(command.Query, x => x.Id)
                .PageWithFallback(command.Query);

            StemPreviewEntryView[] data = await AsyncExtensions.ToArrayAsync(dataQuery, ct);

            FetchDataResult<StemPreviewEntryView> result = FetchDataResult<StemPreviewEntryView>.Succeeded(data);
            result.TotalCount = total;
            return result;
        });
    }

    async Task EnsureTablesAsync(CancellationToken ct)
    {
        _entries ??= await _db.CreateTableAsync<StemPreviewEntry>(tableName: _entryTable, tableOptions: TempTableOptions, token: ct);
        _files ??= await _db.CreateTableAsync<StemPreviewFileMetadata>(tableName: _fileTable, tableOptions: TempTableOptions, token: ct);
    }

    async Task RunExclusive(Func<Task> action)
    {
        await _gate.WaitAsync();
        try
        {
            LastAccessUtc = DateTime.UtcNow;
            await action();
        }
        finally
        {
            _gate.Release();
        }
    }

    async Task<T> RunExclusive<T>(Func<Task<T>> action)
    {
        await _gate.WaitAsync();
        try
        {
            LastAccessUtc = DateTime.UtcNow;
            return await action();
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
            if (_entries is not null)
            {
                await _db.DropTableAsync<StemPreviewEntry>(tableName: _entryTable, tableOptions: TempTableOptions, throwExceptionIfNotExists: false);
            }
            if (_files is not null)
            {
                await _db.DropTableAsync<StemPreviewFileMetadata>(tableName: _fileTable, tableOptions: TempTableOptions, throwExceptionIfNotExists: false);
            }
        }
        catch
        {
            // best effort — the global temp tables vanish when the connection closes anyway
        }
        finally
        {
            _db.Dispose();
            _gate.Release();
            _gate.Dispose();
        }
    }
}

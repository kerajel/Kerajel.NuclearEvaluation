using System.Collections.Concurrent;
using LinqToDB;
using NuclearEvaluation.Library.Commands;
using NuclearEvaluation.Library.Enums;
using NuclearEvaluation.Library.Interfaces;
using NuclearEvaluation.Library.Models.DataManagement;
using NuclearEvaluation.Library.Models.Views;
using NuclearEvaluation.Server.Data;
using Polly;
using Polly.Bulkhead;

namespace NuclearEvaluation.Server.Services;

public class StemPreviewEntryService : DbServiceBase, IStemPreviewEntryService
{
    const string entryTableSuffix = "stem-entry";
    const string fileNameTableSuffix = "stem-file";
    const TableKind tableKind = TableKind.Temporary;

    readonly ITempTableService _tempTableService;

    static readonly ConcurrentDictionary<Guid, AsyncBulkheadPolicy> _bulkheadPolicies = new();

    static AsyncBulkheadPolicy GetBulkheadPolicy(Guid stemSessionId)
    {
        AsyncBulkheadPolicy policy = _bulkheadPolicies.GetOrAdd(stemSessionId, id =>
            Policy.BulkheadAsync(
                maxParallelization: 1,
                maxQueuingActions: 64,
                onBulkheadRejectedAsync: async context =>
                {
                    await Task.CompletedTask;
                }));
        return policy;
    }

    static async Task ExecuteWithBulkheadPolicy(Guid stemSessionId, Func<Task> action)
    {
        AsyncBulkheadPolicy policy = GetBulkheadPolicy(stemSessionId);
        await policy.ExecuteAsync(action);
    }

    static async Task<T> ExecuteWithBulkheadPolicy<T>(Guid stemSessionId, Func<Task<T>> action)
    {
        T result = default!;
        AsyncBulkheadPolicy policy = GetBulkheadPolicy(stemSessionId);
        await policy.ExecuteAsync(async () =>
        {
            result = await action();
        });
        return result;
    }

    public StemPreviewEntryService(
        NuclearEvaluationServerDbContext dbContext,
        ITempTableService tempTableService)
        : base(dbContext)
    {
        _tempTableService = tempTableService;
    }

    public async Task DeleteFileData(Guid stemSessionId, Guid fileId)
    {
        await ExecuteWithBulkheadPolicy(stemSessionId, async () =>
        {
            string entryTable = GetEntryTableName(stemSessionId);
            string fileNameTable = GetFileNameTableName(stemSessionId);

            IQueryable<StemPreviewEntry>? entryQueryable = _tempTableService.Get<StemPreviewEntry>(entryTable);
            IQueryable<StemPreviewFileMetadata>? fileQueryable = _tempTableService.Get<StemPreviewFileMetadata>(fileNameTable);

            if (entryQueryable != null)
                await entryQueryable.Where(x => x.FileId == fileId).DeleteAsync();

            if (fileQueryable != null)
                await fileQueryable.Where(x => x.Id == fileId).DeleteAsync();
        });
    }

    public async Task<FilterDataResponse<StemPreviewEntryView>> GetStemPreviewEntryViews(Guid stemSessionId, FilterDataCommand<StemPreviewEntryView> command)
    {
        return await ExecuteWithBulkheadPolicy(stemSessionId, async () =>
        {
            command.TableKind = tableKind;

            string entryTable = GetEntryTableName(stemSessionId);
            string fileNameTable = GetFileNameTableName(stemSessionId);

            IQueryable<StemPreviewEntry> entryTableQuery = _tempTableService.Get<StemPreviewEntry>(entryTable)!;
            IQueryable<StemPreviewFileMetadata> fileTableQuery = _tempTableService.Get<StemPreviewFileMetadata>(fileNameTable)!;

            IQueryable<StemPreviewEntryView> baseQuery =
                from entry in entryTableQuery
                join file in fileTableQuery on entry.FileId equals file.Id
                where file.IsFullyUploaded == true
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

            if (!command.HasOrderBy)
            {
                //the exception occurs only if I specify this orderby-thenby expression, why?
                baseQuery = baseQuery.OrderBy(x => x.Id)
                    .ThenBy(x => x.FileId);
            }

            return await ExecuteQuery(baseQuery, command);
        });
    }

    public async Task InsertStemPreviewFileMetadata(Guid stemSessionId, StemPreviewFileMetadata fileMetadata, CancellationToken ct = default(CancellationToken))
    {
        await ExecuteWithBulkheadPolicy(stemSessionId, async () =>
        {
            string fileNameTable = GetFileNameTableName(stemSessionId);
            await _tempTableService.EnsureCreated<StemPreviewFileMetadata>(fileNameTable);

            await _tempTableService.InsertWithoutIdentity(fileNameTable, fileMetadata, ct);
        });
    }

    public async Task InsertStemPreviewEntries(Guid stemSessionId, IEnumerable<StemPreviewEntry> entries, CancellationToken ct = default(CancellationToken))
    {
        await ExecuteWithBulkheadPolicy(stemSessionId, async () =>
        {
            string entryTable = GetEntryTableName(stemSessionId);
            await _tempTableService.EnsureCreated<StemPreviewEntry>(entryTable);

            await _tempTableService.BulkCopyInto(entryTable, entries, ct);
        });
    }

    public async Task RefreshIndexes(Guid stemSessionId)
    {
        await ExecuteWithBulkheadPolicy(stemSessionId, async () =>
        {
            string entryTable = GetEntryTableName(stemSessionId);
            await _tempTableService.EnsureCreated<StemPreviewEntry>(entryTable);
            await _tempTableService.EnsureIndex<StemPreviewEntry, decimal>(entryTable, e => e.Id);
        });
    }

    public async Task SetStemPreviewFileAsFullyUploaded(Guid stemSessionId, Guid fileId)
    {
        await ExecuteWithBulkheadPolicy(stemSessionId, async () =>
        {
            string fileNameTable = GetFileNameTableName(stemSessionId);
            IQueryable<StemPreviewFileMetadata> queryable = await _tempTableService.EnsureCreated<StemPreviewFileMetadata>(fileNameTable);

            await queryable
                .Where(x => x.Id == fileId)
                .Set(x => x.IsFullyUploaded, true)
                .UpdateAsync();
        });
    }

    static string GetEntryTableName(Guid sessionId)
    {
        return string.Format("{0}-{1}", sessionId, entryTableSuffix);
    }

    static string GetFileNameTableName(Guid sessionId)
    {
        return string.Format("{0}-{1}", sessionId, fileNameTableSuffix);
    }

    public async ValueTask DisposeAsync()
    {
        _ =_tempTableService?.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
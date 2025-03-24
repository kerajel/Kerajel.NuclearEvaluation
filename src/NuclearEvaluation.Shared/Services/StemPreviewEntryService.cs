using System.Collections.Concurrent;
using LinqToDB;
using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Kernel.Data.Context;
using NuclearEvaluation.Kernel.Enums;
using NuclearEvaluation.Kernel.Interfaces;
using NuclearEvaluation.Kernel.Models.DataManagement.Stem;
using NuclearEvaluation.Kernel.Models.Views;
using Polly;
using Polly.Bulkhead;

namespace NuclearEvaluation.Shared.Services;

public class StemPreviewEntryService : DbServiceBase, IStemPreviewEntryService
{
    const string entryTableSuffix = "stem-entry";
    const string fileNameTableSuffix = "stem-file";
    const TableKind tableKind = TableKind.Temporary;

    readonly ITempTableService _tempTableService;

    static readonly ConcurrentDictionary<Guid, AsyncBulkheadPolicy> _bulkheadPolicies = new();

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
            string fileNameTable = GetFileNameTableName(stemSessionId);

            IQueryable<StemPreviewFileMetadata>? fileQueryable = _tempTableService.Get<StemPreviewFileMetadata>(fileNameTable);

            if (fileQueryable != null)
            {
                await fileQueryable.Where(x => x.Id == fileId)
                    .Set(x => x.IsDeleted, true)
                    .UpdateAsync();
            }

            _ = QueueDeletionOfStemFile();

            //TODO implement messaging to dispatch this operation to a dedicated worker service
            static Task QueueDeletionOfStemFile()
            {
                return Task.Run(async () =>
                {
                    await Task.Yield();
                });
            }
        });
    }

    public async Task<FilterDataResult<StemPreviewEntryView>> GetStemPreviewEntryViews(Guid stemSessionId, FilterDataCommand<StemPreviewEntryView> command)
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
                where file.IsFullyUploaded == true && file.IsDeleted == false
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
                baseQuery = baseQuery.OrderBy(x => x.Id)
                    .ThenBy(x => x.FileId);
            }

            return await ExecuteQuery(baseQuery, command);
        });
    }

    public async Task InsertStemPreviewFileMetadata(Guid stemSessionId, StemPreviewFileMetadata fileMetadata, CancellationToken ct = default)
    {
        await ExecuteWithBulkheadPolicy(stemSessionId, async () =>
        {
            string fileNameTable = GetFileNameTableName(stemSessionId);
            await _tempTableService.EnsureCreated<StemPreviewFileMetadata>(fileNameTable);

            await _tempTableService.InsertWithoutIdentity(fileNameTable, fileMetadata, ct);
        });
    }

    public async Task InsertStemPreviewEntries(Guid stemSessionId, IAsyncEnumerable<StemPreviewEntry> entries, CancellationToken ct = default)
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

    public async ValueTask DisposeAsync()
    {
        if (_tempTableService != null)
        {
            await _tempTableService.DisposeAsync();
        }
        GC.SuppressFinalize(this);
    }
}
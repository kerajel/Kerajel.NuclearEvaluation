using LinqToDB;
using LinqToDB.Data;
using LinqToDB.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Kernel.Data.Context;
using NuclearEvaluation.Kernel.Models.DataManagement.Stem;
using NuclearEvaluation.Shared.Models.Views;
using NuclearEvaluation.Server.Interfaces.STEM;
using NuclearEvaluation.Server.Services.DB;
using System.Runtime.CompilerServices;

namespace NuclearEvaluation.Server.Services.STEM;

/// <summary>
/// Stages STEM preview rows in the persistent STAGING schema, scoped by the visitor's
/// preview session. Rows are bulk-copied for throughput and purged by the retention job.
/// </summary>
public class StemPreviewEntryService : DbServiceBase, IStemPreviewEntryService
{
    const int maxBatchSize = 10_000;
    const int bulkCopyTimeout = 60 * 5;

    public StemPreviewEntryService(NuclearEvaluationServerDbContext dbContext) : base(dbContext)
    {
    }

    public async Task<FetchDataResult<StemPreviewEntryView>> GetStemPreviewEntryViews(Guid stemSessionId, FetchDataCommand<StemPreviewEntryView> command)
    {
        IQueryable<StemPreviewEntryView> baseQuery =
            from entry in _dbContext.StemPreviewEntry
            join file in _dbContext.StemPreviewFileMetadata on entry.FileId equals file.Id
            where entry.StemSessionId == stemSessionId
                && file.IsFullyUploaded
                && !file.IsDeleted
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
            baseQuery = baseQuery.OrderBy(x => x.Id).ThenBy(x => x.FileId);
        }

        return await ExecuteQuery(baseQuery, command);
    }

    public async Task InsertStemPreviewFileMetadata(Guid stemSessionId, StemPreviewFileMetadata fileMetadata, CancellationToken ct = default)
    {
        fileMetadata.StemSessionId = stemSessionId;
        _dbContext.StemPreviewFileMetadata.Add(fileMetadata);
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task InsertStemPreviewEntries(Guid stemSessionId, IAsyncEnumerable<StemPreviewEntry> entries, CancellationToken ct = default)
    {
        BulkCopyOptions options = new()
        {
            BulkCopyTimeout = bulkCopyTimeout,
            MaxBatchSize = maxBatchSize,
            KeepIdentity = false,
        };

        using DataConnection dataConnection = _dbContext.CreateLinqToDBConnection();
        await dataConnection.BulkCopyAsync(options, WithSession(entries, stemSessionId, ct), ct);
    }

    public async Task SetStemPreviewFileAsFullyUploaded(Guid stemSessionId, Guid fileId)
    {
        await _dbContext.StemPreviewFileMetadata
            .Where(x => x.StemSessionId == stemSessionId && x.Id == fileId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsFullyUploaded, true));
    }

    public async Task DeleteFileData(Guid stemSessionId, Guid fileId)
    {
        await _dbContext.StemPreviewFileMetadata
            .Where(x => x.StemSessionId == stemSessionId && x.Id == fileId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsDeleted, true));

        await _dbContext.StemPreviewEntry
            .Where(x => x.StemSessionId == stemSessionId && x.FileId == fileId)
            .ExecuteDeleteAsync();
    }

    static async IAsyncEnumerable<StemPreviewEntry> WithSession(
        IAsyncEnumerable<StemPreviewEntry> source,
        Guid stemSessionId,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (StemPreviewEntry entry in source.WithCancellation(ct))
        {
            entry.StemSessionId = stemSessionId;
            yield return entry;
        }
    }
}

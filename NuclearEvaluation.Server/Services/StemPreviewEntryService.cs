using LinqToDB;
using NuclearEvaluation.Library.Commands;
using NuclearEvaluation.Library.Enums;
using NuclearEvaluation.Library.Extensions;
using NuclearEvaluation.Library.Interfaces;
using NuclearEvaluation.Library.Models.DataManagement;
using NuclearEvaluation.Library.Models.Views;
using NuclearEvaluation.Server.Data;

namespace NuclearEvaluation.Server.Services;

public class StemPreviewEntryService : DbServiceBase, IStemPreviewEntryService, IDisposable
{
    const string entryTableSuffix = "stem-entry";
    const string fileNameTableSuffix = "stem-file";

    const TableKind tableKind = TableKind.Temporary;

    readonly ITempTableService _tempTableService;

    public StemPreviewEntryService(NuclearEvaluationServerDbContext dbContext, ITempTableService tempTableService)
        : base(dbContext)
    {
        _tempTableService = tempTableService;
    }

    public async Task DeleteFileData(Guid stemSessionId, Guid fileId)
    {
        string entryTable = GetEntryTableName(stemSessionId);
        string fileNameTable = GetFileNameTableName(stemSessionId);

        await _tempTableService.EnsureCreated<StemPreviewEntry>(entryTable);
        await _tempTableService.EnsureCreated<StemPreviewFileMetadata>(fileNameTable);

        IQueryable<StemPreviewEntry> entryQueryable = _tempTableService.Get<StemPreviewEntry>(entryTable);
        IQueryable<StemPreviewFileMetadata> fileQueryable = _tempTableService.Get<StemPreviewFileMetadata>(fileNameTable);

        await entryQueryable.Where(x => x.FileId == fileId).DeleteAsync();
        await fileQueryable.Where(x => x.Id == fileId).DeleteAsync();
    }

    public async Task<FilterDataResponse<StemPreviewEntryView>> GetStemPreviewEntryViews(Guid stemSessionId, FilterDataCommand<StemPreviewEntryView> command)
    {
        command.TableKind = tableKind;

        string entryTable = GetEntryTableName(stemSessionId);
        string fileNameTable = GetFileNameTableName(stemSessionId);

        IQueryable<StemPreviewEntry> entryTableQuery = _tempTableService.Get<StemPreviewEntry>(entryTable);
        IQueryable<StemPreviewFileMetadata> fileTableQuery = _tempTableService.Get<StemPreviewFileMetadata>(fileNameTable);

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

        if (command.LoadDataArgs != null && command.LoadDataArgs.HasEmptyOrder())
        {
            baseQuery = baseQuery.OrderBy(x => x.Id)
                .ThenBy(x => x.FileId);
        }

        return await ExecuteQuery(baseQuery, command);
    }

    public async Task InsertStemPreviewFileMetadata(Guid stemSessionId, StemPreviewFileMetadata fileMetadata, CancellationToken ct = default)
    {
        string fileNameTable = GetFileNameTableName(stemSessionId);
        await _tempTableService.EnsureCreated<StemPreviewFileMetadata>(fileNameTable);

        await _tempTableService.InsertWithoutIdentity(fileNameTable, fileMetadata, ct);
    }

    public async Task InsertStemPreviewEntries(Guid stemSessionId, IEnumerable<StemPreviewEntry> entries, CancellationToken ct = default)
    {
        string entryTable = GetEntryTableName(stemSessionId);
        await _tempTableService.EnsureCreated<StemPreviewEntry>(entryTable);

        await _tempTableService.BulkCopyInto(entryTable, entries, ct);
    }

    public async Task RefreshIndexes(Guid stemSessionId)
    {
        string entryTable = GetEntryTableName(stemSessionId);
        await _tempTableService.EnsureCreated<StemPreviewEntry>(entryTable);
        await _tempTableService.EnsureIndex<StemPreviewEntry, decimal>(entryTable, e => e.Id);
    }

    public async Task SetStemPreviewFileAsFullyUploaded(Guid stemSessionId, Guid fileId)
    {
        string fileNameTable = GetFileNameTableName(stemSessionId);
        await _tempTableService.EnsureCreated<StemPreviewFileMetadata>(fileNameTable);

        await _tempTableService.Get<StemPreviewFileMetadata>(fileNameTable)
            .Where(x => x.Id == fileId)
            .Set(x => x.IsFullyUploaded, true)
            .UpdateAsync();
    }

    static string GetEntryTableName(Guid sessionId)
    {
        return $"{sessionId}-{entryTableSuffix}";
    }

    static string GetFileNameTableName(Guid sessionId)
    {
        return $"{sessionId}-{fileNameTableSuffix}";
    }

    public void Dispose()
    {
        if (_tempTableService != null)
        {
            _tempTableService.Dispose();
        }
        GC.SuppressFinalize(this);
    }
}
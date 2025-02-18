using LinqToDB;
using NuclearEvaluation.Library.Commands;
using NuclearEvaluation.Library.Enums;
using NuclearEvaluation.Library.Interfaces;
using NuclearEvaluation.Library.Models.DataManagement;
using NuclearEvaluation.Library.Models.Views;
using NuclearEvaluation.Server.Data;

namespace NuclearEvaluation.Server.Services;

public class StemPreviewEntryService : DbServiceBase, IStemPreviewEntryService
{
    private readonly ITempTableService _tempTableService;

    public StemPreviewEntryService(NuclearEvaluationServerDbContext _dbContext, ITempTableService tempTableService) : base(_dbContext)
    {
        _tempTableService = tempTableService;
    }

    public async Task<FilterDataResponse<StemPreviewEntryView>> GetStemPreviewEntryViews(FilterDataCommand<StemPreviewEntryView> command)
    {
        command.TableKind = TableKind.Temporary;
        string tempTableName = command.GetRequiredArgument<string>(FilterDataCommand.ArgKeys.StemPreviewTempTableName);

        IQueryable<StemPreviewEntry> tempTable = _tempTableService.Get<StemPreviewEntry>(tempTableName)
            ?? throw new Exception($"Temporary table {tempTableName} does not exist");

        IQueryable<StemPreviewEntryView> baseQuery = tempTable.Select(x => new StemPreviewEntryView
        {
            Id = x.Id,
            LabCode = x.LabCode,
            AnalysisDate = x.AnalysisDate,
            IsNu = x.IsNu,
            U234 = x.U234,
            ErU234 = x.ErU234,
            U235 = x.U235,
            ErU235 = x.ErU235,
        });

        return await ExecuteQuery(baseQuery, command);
    }
}
using LinqToDB;
using LinqToDB.Data;
using LinqToDB.EntityFrameworkCore;
using NuclearEvaluation.Library.Commands;
using NuclearEvaluation.Library.Enums;
using NuclearEvaluation.Library.Interfaces;
using NuclearEvaluation.Library.Models.DataManagement;
using NuclearEvaluation.Library.Models.Views;
using NuclearEvaluation.Server.Data;
using NuclearEvaluation.Server.Shared.DataManagement;

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
        string tempTableName = command.GetRequiredArgument<string>(FilterDataCommand.ArgKeys.StemPreviewTempTableName);

        IQueryable<StemPreviewEntryView>? baseQuery = await _tempTableService.Get<StemPreviewEntryView>(tempTableName)
            ?? throw new Exception($"Temporary table {tempTableName} does not exist");

        var sat = await ExecuteQueryAsync(baseQuery, command);

        return null;
    }
}
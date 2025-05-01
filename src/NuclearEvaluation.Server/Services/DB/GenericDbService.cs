using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Kernel.Data.Context;
using NuclearEvaluation.Server.Interfaces.DB;
using System.Linq.Dynamic.Core;

namespace NuclearEvaluation.Server.Services.DB;

public class GenericDbService : DbServiceBase, IGenericDbService
{
    public GenericDbService(NuclearEvaluationServerDbContext dbContext) : base(dbContext)
    {
    }

    public async Task<FetchDataResult<dynamic>> GetFilterOptions<T>(FetchDataCommand<T> command, string propertyName) where T : class
    {
        IQueryable<T> query = _dbContext.Set<T>().AsQueryable();
        IQueryable<T> filteredQuery = GetFilteredQuery(query, command);
        dynamic[] result = await filteredQuery.Select(propertyName).Distinct().OrderByDynamic("x => x").ToDynamicArrayAsync();
        return new()
        {
            Entries = result,
        };
    }
}
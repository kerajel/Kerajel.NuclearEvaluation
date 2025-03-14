using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Kernel.Interfaces;
using NuclearEvaluation.Server.Data;
using NuclearEvaluation.SharedServices.Services;
using System.Linq.Dynamic.Core;

namespace NuclearEvaluation.Server.Services;

public class GenericService : DbServiceBase, IGenericService
{
    public GenericService(NuclearEvaluationServerDbContext dbContext) : base(dbContext)
    {
    }

    public async Task<FilterDataResponse<dynamic>> GetFilterOptions<T>(FilterDataCommand<T> command, string propertyName) where T : class
    {
        IQueryable<T> query = _dbContext.Set<T>().AsQueryable();
        IQueryable<T> filteredQuery = GetFilteredQuery(query, command, false);
        dynamic[] result = await filteredQuery.Select(propertyName).Distinct().OrderByDynamic("x => x").ToDynamicArrayAsync();
        return new()
        {
            Entries = result,
        };
    }
} 
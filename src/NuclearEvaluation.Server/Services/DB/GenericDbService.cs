using Microsoft.EntityFrameworkCore;
using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Kernel.Data.Context;
using NuclearEvaluation.Server.Interfaces.DB;
using System.Linq.Expressions;

namespace NuclearEvaluation.Server.Services.DB;

public class GenericDbService : DbServiceBase, IGenericDbService
{
    public GenericDbService(NuclearEvaluationServerDbContext dbContext) : base(dbContext)
    {
    }

    public async Task<FetchDataResult<int>> GetFilterOptions<T>(FetchDataCommand<T> command, string propertyName) where T : class
    {
        IQueryable<T> query = _dbContext.Set<T>().AsQueryable();
        IQueryable<T> filteredQuery = GetFilteredQuery(query, command);
        Expression<Func<T, int>> selector = CreateEnumValueSelector<T>(propertyName);
        List<int> result = await filteredQuery
            .Select(selector)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync();

        return new()
        {
            Entries = result,
        };
    }

    static Expression<Func<T, int>> CreateEnumValueSelector<T>(string propertyName)
    {
        ParameterExpression parameter = Expression.Parameter(typeof(T), "x");
        MemberExpression property = Expression.PropertyOrField(parameter, propertyName);

        if (!property.Type.IsEnum)
        {
            throw new ArgumentException($"Property '{propertyName}' on '{typeof(T).Name}' is not an enum.", nameof(propertyName));
        }

        UnaryExpression enumAsInt = Expression.Convert(property, typeof(int));
        return Expression.Lambda<Func<T, int>>(enumAsInt, parameter);
    }
}

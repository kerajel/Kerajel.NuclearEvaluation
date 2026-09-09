using System.Linq.Dynamic.Core;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Shared.Models.Views;

namespace NuclearEvaluation.Server.Services.DB;

public class GenericDbService : DbServiceBase, IGenericDbService
{
    public GenericDbService(NuclearEvaluationServerDbContext dbContext)
        : base(dbContext) { }

    public async Task<FetchDataResult<int>> GetFilterOptions<T>(
        FetchDataCommand<T> command,
        string propertyName
    )
        where T : class
    {
        IQueryable<T> query = _dbContext.Set<T>().AsQueryable();
        if (command.Query?.ProjectId is int projectId)
        {
            string path = typeof(T).Name switch
            {
                nameof(SeriesView) => "ProjectSeries",
                nameof(SampleView) => "Series.ProjectSeries",
                nameof(SubSampleView) => "Sample.Series.ProjectSeries",
                nameof(ApmView) or nameof(ParticleView) => "SubSample.Sample.Series.ProjectSeries",
                _ => throw new ArgumentException("This view does not support project scoping."),
            };
            query = query.Where($"{path}.Any(ProjectId == @0)", projectId);
        }
        IQueryable<T> filteredQuery = GetFilteredQuery(query, command);
        Expression<Func<T, int>> selector = CreateEnumValueSelector<T>(propertyName);
        List<int> result = await filteredQuery
            .Select(selector)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(command.CancellationToken);

        return new() { Entries = result };
    }

    static Expression<Func<T, int>> CreateEnumValueSelector<T>(string propertyName)
    {
        ParameterExpression parameter = Expression.Parameter(typeof(T), "x");
        MemberExpression property = Expression.PropertyOrField(parameter, propertyName);

        if (!property.Type.IsEnum)
        {
            throw new ArgumentException(
                $"Property '{propertyName}' on '{typeof(T).Name}' is not an enum.",
                nameof(propertyName)
            );
        }

        UnaryExpression enumAsInt = Expression.Convert(property, typeof(int));
        return Expression.Lambda<Func<T, int>>(enumAsInt, parameter);
    }
}

using Microsoft.EntityFrameworkCore;
using NuclearEvaluation.Library.Commands;
using NuclearEvaluation.Library.Enums;
using NuclearEvaluation.Library.Extensions;
using NuclearEvaluation.Library.Models.Filters;
using NuclearEvaluation.Server.Data;
using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using System.Reflection;
using System.Collections.Concurrent;
using Z.EntityFramework.Plus;
using LinqToDB.EntityFrameworkCore;

namespace NuclearEvaluation.Server.Services;

public class DbServiceBase
{
    protected NuclearEvaluationServerDbContext _dbContext;
    private static readonly ConcurrentDictionary<Type, PropertyInfo> _keyPropertyCache = new();

    public DbServiceBase(NuclearEvaluationServerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<FilterDataResponse<T>> ExecuteQuery<T>(IQueryable<T> query, FilterDataCommand<T> cmd) where T : class
    {
        FilterDataResponse<T> result = new();

        bool enableVirtualTracking = cmd.AsNoTracking && cmd.Includes.Any();

        IQueryable<T> filteredQuery = GetFilteredQuery(query, cmd, enableVirtualTracking);

        PropertyInfo keyProperty = GetKeyProperty<T>();
        ParameterExpression param = Expression.Parameter(typeof(T), "x");
        Expression keyPropertyAccess = Expression.Property(param, keyProperty.Name);
        Expression<Func<T, object>> lambdaKeyPropertyAccess = Expression.Lambda<Func<T, object>>(Expression.Convert(keyPropertyAccess, typeof(object)), param);

        IQueryable<T> dataQuery = filteredQuery
            .OrderByWithFallback(cmd.LoadDataArgs, lambdaKeyPropertyAccess)
            .PageWithFallback(cmd.LoadDataArgs);

        foreach (dynamic include in cmd.Includes)
        {
            dataQuery = QueryIncludeOptimizedExtensions.IncludeOptimized(dataQuery, include);
        }

        if (cmd.TableKind == TableKind.Persisted)
        {
            QueryFutureEnumerable<T> futureEntries = dataQuery.Future();

            if (cmd.FetchTotalCount)
            {
                QueryFutureValue<int> futureCount = filteredQuery.DeferredCount().FutureValue();
                result.TotalCount = await futureCount.ValueAsync();
            }

            result.Entries = await futureEntries.ToArrayAsync();
        }
        else
        {
            result.TotalCount = await dataQuery.CountAsyncLinqToDB();
            result.Entries = await dataQuery.ToArrayAsyncLinqToDB();
        }

        if (enableVirtualTracking)
        {
            foreach (T entry in result.Entries)
            {
                _dbContext.Entry(entry).State = EntityState.Detached;
            }
        }

        return result;
    }

    protected IQueryable<T> GetFilteredQuery<T>(IQueryable<T> query, FilterDataCommand<T> command, bool enableVirtualTracking) where T : class
    {
        IQueryable<T> filteredQuery = enableVirtualTracking ? query.AsNoTracking() : command.AsNoTracking ? query.AsNoTracking() : query;

        PropertyInfo keyProperty = GetKeyProperty<T>();

        if (command.QueryKind == QueryKind.QueryBuilder)
        {
            IQueryable<int> qbFilter = ApplyPresetFilterBox(command);
            filteredQuery = filteredQuery.Where(entry => qbFilter.Contains((int)keyProperty.GetValue(entry, null)!));
        }

        filteredQuery = filteredQuery
            .TopLevelFilterExpressionWithFallback(command.TopLevelFilterExpression)
            .FilterWithFallback(command.LoadDataArgs);

        return filteredQuery;
    }

    protected IQueryable<int> ApplyPresetFilterBox<T>(FilterDataCommand<T> command) where T : class
    {
        IQueryable<PresetFilterQueryObject> compositeQuery = GetBasePresetFilterQuery();
        foreach ((PresetFilterEntryType entryType, string? value) in command.PresetFilterBox!.AsEnumerable())
        {
            compositeQuery = compositeQuery.FilterWithFallback(value);
        }

        PropertyInfo keyProperty = GetKeyProperty<T>();
        ParameterExpression param = Expression.Parameter(typeof(PresetFilterQueryObject), "x");
        Expression keyPropertyAccess = Expression.Property(param, keyProperty.Name);
        Expression<Func<PresetFilterQueryObject, int>> lambda = Expression.Lambda<Func<PresetFilterQueryObject, int>>(Expression.Convert(keyPropertyAccess, typeof(int)), param);

        return compositeQuery.Select(lambda).GroupBy(x => x).Select(x => x.Key);
    }

    private static PropertyInfo GetKeyProperty<T>() where T : class
    {
        return _keyPropertyCache.GetOrAdd(typeof(T), t =>
        {
            PropertyInfo[] keyProperties = t.GetProperties()
                .Where(prop => Attribute.IsDefined(prop, typeof(KeyAttribute)))
                .ToArray();

            if (keyProperties.Length > 1)
            {
                throw new InvalidOperationException("Entities with composite keys are not supported yet.");
            }
            else if (keyProperties.Length == 0)
            {
                throw new InvalidOperationException("No property is marked with the [Key] attribute.");
            }

            return keyProperties.Single();
        });
    }

    protected IQueryable<PresetFilterQueryObject> GetBasePresetFilterQuery()
    {
        return from series in _dbContext.SeriesView
               from sample in _dbContext.SampleView
                   .Where(sample => sample.SeriesId == series.Id)
                   .DefaultIfEmpty() // Left join between Series and Samples
               from subSample in _dbContext.SubSampleView
                   .Where(subSample => subSample.SampleId == sample.Id)
                   .DefaultIfEmpty() // Left join between Samples and SubSamples
               from apm in _dbContext.ApmView
                   .Where(apm => apm.SubSampleId == subSample.Id)
                   .DefaultIfEmpty() // Left join between SubSamples and Apms
               from particle in _dbContext.ParticleView
                   .Where(particle => particle.SubSampleId == subSample.Id)
                   .DefaultIfEmpty() // Left join between SubSamples and Particles
               select new PresetFilterQueryObject
               {
                   Series = series,
                   Sample = sample,
                   SubSample = subSample,
                   Apm = apm,
                   Particle = particle,
               };
    }
}
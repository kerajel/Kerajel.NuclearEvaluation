using Microsoft.EntityFrameworkCore;
using NuclearEvaluation.Library.Commands;
using NuclearEvaluation.Library.Enums;
using NuclearEvaluation.Library.Extensions;
using NuclearEvaluation.Library.Interfaces;
using NuclearEvaluation.Library.Models.Filters;
using NuclearEvaluation.Server.Data;
using System.Linq.Expressions;
using System.Reflection;
using Z.EntityFramework.Plus;

namespace NuclearEvaluation.Server.Services;

public class DbServiceBase
{
    protected NuclearEvaluationServerDbContext _dbContext;

    public DbServiceBase(NuclearEvaluationServerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<FilterDataResponse<T>> ExecuteQueryAsync<T>(
        IQueryable<T> query,
        FilterDataCommand<T> command)
    where T : class, IIdentifiable
    {
        FilterDataResponse<T> result = new();

        bool enableVirtualTracking = command.AsNoTracking && command.Includes.Any();

        IQueryable<T> filteredQuery = GetFilteredQuery(query, command, enableVirtualTracking);

        IQueryable<T> dataQuery = filteredQuery
            .TopLevelOrderExpressionWithFallback(command.TopLevelOrderExpression)
            .OrderByWithFallback(command.LoadDataArgs, x => x.Id)
            .PageWithFallback(command.LoadDataArgs);

        foreach (dynamic include in command.Includes)
        {
            dataQuery = QueryIncludeOptimizedExtensions.IncludeOptimized(dataQuery, include);
        }

        bool fetchTotalCount = command.FetchTotalCount;

        QueryFutureEnumerable<T> futureEntries = dataQuery.Future();

        if (fetchTotalCount)
        {
            QueryFutureValue<int> futureCount = filteredQuery
                .DeferredCount()
                .FutureValue();

            result.TotalCount = await futureCount.ValueAsync();
        }

        result.Entries = await futureEntries.ToArrayAsync();
        
        if (enableVirtualTracking)
        {
            foreach (T entry in result.Entries)
            {
                _dbContext.Entry(entry).State = EntityState.Detached;
            }
        }

        return result;
    }

    protected IQueryable<T> GetFilteredQuery<T>(IQueryable<T> query, FilterDataCommand<T> command, bool enableVirtualTracking) where T : class, IIdentifiable
    {
        IQueryable<T> filteredQuery = enableVirtualTracking
            ? query.AsTracking()
            : (command.AsNoTracking ? query.AsNoTracking() : query.AsTracking());

        if (command.QueryKind == QueryKind.QueryBuilder)
        {
            IQueryable<int> qbFilter = ApplyPresetFilterBox(command);
            filteredQuery = filteredQuery.Where(entry => qbFilter.Select(x => x).Contains(entry.Id));
        }

        filteredQuery = filteredQuery
             .TopLevelFilterExpressionWithFallback(command.TopLevelFilterExpression)
             .FilterWithFallback(command.LoadDataArgs);

        return filteredQuery;
    }

    protected IQueryable<PresetFilterQueryObject> GetBasePresetFilterQuery()
    {
        IQueryable<PresetFilterQueryObject> compositeQuery = from series in _dbContext.SeriesView
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

        return compositeQuery;
    }

    protected IQueryable<int> ApplyPresetFilterBox<T>(FilterDataCommand<T> command) where T : class
    {
        IQueryable<PresetFilterQueryObject> compositeQuery = GetBasePresetFilterQuery();

        foreach ((PresetFilterEntryType entryType, string? value) in command.PresetFilterBox!.AsEnumerable())
        {
            compositeQuery = compositeQuery.FilterWithFallback(value);
        }

        PropertyInfo pi = typeof(PresetFilterQueryObject).GetProperties()
            .First(prop => prop.PropertyType == typeof(T));

        ParameterExpression param = Expression.Parameter(typeof(PresetFilterQueryObject), "x");
        Expression propertyAccess = Expression.Property(param, pi);
        Expression propertyIdAccess = Expression.Property(propertyAccess, nameof(IIdentifiable.Id));
        Expression<Func<PresetFilterQueryObject, int>> lambda = Expression.Lambda<Func<PresetFilterQueryObject, int>>(propertyIdAccess, param);

        return compositeQuery
            .Select(lambda)
            .GroupBy(x => x)
            .Select(x => x.Key);
    }
}
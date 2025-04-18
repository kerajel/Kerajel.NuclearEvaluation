﻿using Kerajel.Primitives.Enums;
using LinqToDB.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Kernel.Data.Context;
using NuclearEvaluation.Kernel.Enums;
using NuclearEvaluation.Kernel.Extensions;
using NuclearEvaluation.Kernel.Models.Filters;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using System.Reflection;
using Z.EntityFramework.Plus;

namespace NuclearEvaluation.Server.Services.DB;

public class DbServiceBase
{
    static readonly ConcurrentDictionary<Type, PropertyInfo> _keyPropertyCache = new();

    protected readonly NuclearEvaluationServerDbContext _dbContext;

    public DbServiceBase(NuclearEvaluationServerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<FetchDataResult<T>> ExecuteQuery<T>(
        IQueryable<T> query,
        FetchDataCommand<T> cmd,
        CancellationToken ct = default) where T : class
    {
        FetchDataResult<T> result = FetchDataResult<T>.Succeeded([]);

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

        if (cmd.TableKind == TableKind.Persisted && false)
        {
            QueryFutureEnumerable<T> futureEntries = dataQuery.Future();

            if (cmd.FetchTotalCount)
            {
                QueryFutureValue<int> futureCount = filteredQuery.DeferredCount().FutureValue();
                result.TotalCount = await futureCount.ValueAsync(ct);
            }

            result.Entries = await futureEntries.ToArrayAsync(ct);
        }
        else
        {
            result.TotalCount = await filteredQuery.CountAsyncLinqToDB(ct);
            result.Entries = await dataQuery.ToArrayAsyncLinqToDB(ct);
        }

        if (enableVirtualTracking)
        {
            foreach (T entry in result.Entries)
            {
                _dbContext.Entry(entry).State = EntityState.Detached;
            }
        }

        if (result.Entries.IsNullOrEmpty())
        {
            result.OperationStatus = OperationStatus.NotFound;
        }

        return result;
    }

    protected IQueryable<T> GetFilteredQuery<T>(IQueryable<T> query, FetchDataCommand<T> command, bool enableVirtualTracking) where T : class
    {
        IQueryable<T> filteredQuery = enableVirtualTracking ? query.AsNoTracking() : command.AsNoTracking ? query.AsNoTracking() : query;

        PropertyInfo keyProperty = GetKeyProperty<T>();

        if (command.QueryKind == QueryKind.QueryBuilder)
        {
            IQueryable<int> qbFilter = ApplyPresetFilterBox(command);

            ParameterExpression paramEntity = Expression.Parameter(typeof(T), "x");
            MemberExpression memberExpr = Expression.Property(paramEntity, keyProperty);
            UnaryExpression convertExpr = Expression.Convert(memberExpr, typeof(object));
            Expression<Func<T, object>> keySelector = Expression.Lambda<Func<T, object>>(convertExpr, paramEntity);

            filteredQuery = filteredQuery.Join(
                  qbFilter
                , keySelector
                , filter => filter
                , (entry, filter) => entry
            );
        }

        filteredQuery = filteredQuery
            .TopLevelFilterExpressionWithFallback(command.TopLevelFilterExpression)
            .FilterWithFallback(command.LoadDataArgs);

        return filteredQuery;
    }

    protected IQueryable<int> ApplyPresetFilterBox<T>(FetchDataCommand<T> command) where T : class
    {
        IQueryable<PresetFilterQueryObject> compositeQuery = GetBasePresetFilterQuery();

        foreach ((PresetFilterEntryType entryType, string? value) in command.PresetFilterBox!.AsEnumerable())
        {
            compositeQuery = compositeQuery.FilterWithFallback(value);
        }

        PropertyInfo pi = typeof(PresetFilterQueryObject).GetProperties()
            .First(prop => prop.PropertyType == typeof(T));

        PropertyInfo keyProperty = GetKeyProperty<T>();
        ParameterExpression param = Expression.Parameter(typeof(PresetFilterQueryObject), "x");
        Expression propertyAccess = Expression.Property(param, pi);
        Expression propertyIdAccess = Expression.Property(propertyAccess, keyProperty.Name);
        Expression<Func<PresetFilterQueryObject, int>> lambda = Expression.Lambda<Func<PresetFilterQueryObject, int>>(propertyIdAccess, param);

        return compositeQuery
            .Select(lambda)
            .GroupBy(x => x)
            .Select(x => x.Key);
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
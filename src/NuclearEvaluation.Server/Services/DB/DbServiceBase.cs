using Kerajel.Primitives.Enums;
using LinqToDB.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NuclearEvaluation.Kernel.Commands;
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
        FetchDataResult<T> result = FetchDataResult<T>.Succeeded(Array.Empty<T>());

        IQueryable<T> filteredQuery = GetFilteredQuery(query, cmd);

        IQueryable<T> orderedQuery = filteredQuery;

        if (cmd.TopLevelOrderExpression is not null)
        {
            orderedQuery = orderedQuery.OrderBy(cmd.TopLevelOrderExpression);
        }

        PropertyInfo keyProperty = GetKeyProperty<T>();
        ParameterExpression param = Expression.Parameter(typeof(T), "x");
        MemberExpression keyAccess = Expression.Property(param, keyProperty.Name);
        Expression convertedKeyAccess = Expression.Convert(keyAccess, typeof(object));
        Expression<Func<T, object>> lambdaKeyPropertyAccess = Expression.Lambda<Func<T, object>>(convertedKeyAccess, param);

        IQueryable<T> dataQuery = orderedQuery
            .OrderByWithFallback(cmd.LoadDataArgs, lambdaKeyPropertyAccess)
            .PageWithFallback(cmd.LoadDataArgs);

        if (cmd.TableKind == TableKind.Temporary)
        {
            result.TotalCount = await filteredQuery.CountAsyncLinqToDB(ct);
            result.Entries = await dataQuery.ToArrayAsyncLinqToDB(ct);
        }
        else
        {
            result.TotalCount = await filteredQuery.CountAsyncEF(ct);
            result.Entries = await dataQuery.ToArrayAsyncEF(ct);
        }

        bool shouldDetach = cmd.AsNoTracking && _dbContext.Model.FindEntityType(typeof(T)) is not null;

        if (shouldDetach)
        {
            foreach (T entry in result.Entries)
                _dbContext.Entry(entry).State = EntityState.Detached;
        }

        if (result.Entries.IsNullOrEmpty())
        {
            result.OperationStatus = OperationStatus.NotFound;
        }

        return result;
    }

    protected IQueryable<T> GetFilteredQuery<T>(IQueryable<T> query, FetchDataCommand<T> command) where T : class
    {
        foreach (dynamic include in command.Includes)
        {
            query = QueryIncludeOptimizedExtensions.IncludeOptimized(query, include);
        }

        IQueryable<T> filteredQuery = query;

        PropertyInfo keyProperty = GetKeyProperty<T>();

        if (command.QueryKind == QueryKind.QueryBuilder)
        {
            IQueryable<int> qbFilter = ApplyPresetFilterBox(command);

            ParameterExpression paramEntity = Expression.Parameter(typeof(T), "x");
            MemberExpression memberExpr = Expression.Property(paramEntity, keyProperty);
            UnaryExpression convertExpr = Expression.Convert(memberExpr, typeof(object));
            Expression<Func<T, object>> keySelector = Expression.Lambda<Func<T, object>>(convertExpr, paramEntity);

            filteredQuery = filteredQuery.Join(
                qbFilter,
                keySelector,
                filter => filter,
                (entry, filter) => entry
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
        MemberExpression propertyAccess = Expression.Property(param, pi);
        MemberExpression propertyIdAccess = Expression.Property(propertyAccess, keyProperty.Name);
        Expression<Func<PresetFilterQueryObject, int>> lambda = Expression.Lambda<Func<PresetFilterQueryObject, int>>(propertyIdAccess, param);

        return compositeQuery
            .Select(lambda)
            .GroupBy(x => x)
            .Select(x => x.Key);
    }

    private static PropertyInfo GetKeyProperty<T>() where T : class
    {
        return _keyPropertyCache.GetOrAdd(typeof(T), static t =>
        {
            PropertyInfo[] keyProperties = t.GetProperties()
                .Where(prop => Attribute.IsDefined(prop, typeof(KeyAttribute)))
                .ToArray();

            if (keyProperties.Length > 1)
            {
                throw new InvalidOperationException("Entities with composite keys are not supported yet.");
            }

            if (keyProperties.Length == 0)
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
                   .DefaultIfEmpty()
               from subSample in _dbContext.SubSampleView
                   .Where(subSample => subSample.SampleId == sample.Id)
                   .DefaultIfEmpty()
               from apm in _dbContext.ApmView
                   .Where(apm => apm.SubSampleId == subSample.Id)
                   .DefaultIfEmpty()
               from particle in _dbContext.ParticleView
                   .Where(particle => particle.SubSampleId == subSample.Id)
                   .DefaultIfEmpty()
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
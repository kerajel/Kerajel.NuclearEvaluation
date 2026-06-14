using NuclearEvaluation.Shared.Contracts;
using DynamicQueryableExtensions = System.Linq.Dynamic.Core.DynamicQueryableExtensions;
using System.Linq.Expressions;
using System.Text.RegularExpressions;

namespace NuclearEvaluation.Kernel.Extensions;

public static class IQueryableExtensions
{
    static readonly Regex EqualityOperatorRegex = new(@"(?<![!<>=])==(?!=)", RegexOptions.Compiled);

    public static IQueryable<T> OrderByWithFallback<T>(
        this IQueryable<T> query,
        DataQuery? dataQuery,
        Expression<Func<T, object>> defaultOrderBy) where T : class
    {
        bool isAlreadyOrdered = IsOrdered(query);

        if (!string.IsNullOrWhiteSpace(dataQuery?.OrderBy))
        {
            if (isAlreadyOrdered)
            {
                IOrderedQueryable<T> orderedQueryWithPrimary = DynamicQueryableExtensions.ThenBy(
                    (IOrderedQueryable<T>)query,
                    dataQuery.OrderBy);
                return orderedQueryWithPrimary.ThenBy(defaultOrderBy);
            }
            else
            {
                IOrderedQueryable<T> orderedQueryWithPrimary = DynamicQueryableExtensions.OrderBy(
                    query,
                    dataQuery.OrderBy);
                return orderedQueryWithPrimary.ThenBy(defaultOrderBy);
            }
        }
        else
        {
            if (isAlreadyOrdered)
            {
                return ((IOrderedQueryable<T>)query).ThenBy(defaultOrderBy);
            }
            else
            {
                return query.OrderBy(defaultOrderBy);
            }
        }
    }

    static bool IsOrdered<T>(IQueryable<T> query) where T : class
    {
        Expression expression = query.Expression;

        while (expression is MethodCallExpression methodCall)
        {
            string methodName = methodCall.Method.Name;

            if (methodName == "OrderBy" ||
                methodName == "OrderByDescending" ||
                methodName == "ThenBy" ||
                methodName == "ThenByDescending")
            {
                return true;
            }

            if (methodCall.Arguments.Count > 0)
            {
                expression = methodCall.Arguments[0];
            }
            else
            {
                break;
            }
        }

        return false;
    }

    public static IQueryable<T> FilterWithFallback<T>(
        this IQueryable<T> query,
        DataQuery? dataQuery) where T : class
    {
        return query.FilterWithFallback(dataQuery?.Filter);
    }

    public static IQueryable<T> FilterWithFallback<T>(
        this IQueryable<T> query,
        string? filter) where T : class
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return query;
        }
        else
        {
            return DynamicQueryableExtensions.Where(query, NormalizeFilter(filter));
        }
    }

    static string NormalizeFilter(string filter)
    {
        return EqualityOperatorRegex.Replace(filter, "=");
    }

    public static IQueryable<T> TopLevelFilterExpressionWithFallback<T>(
        this IQueryable<T> query,
        Expression<Func<T, bool>>? expression) where T : class
    {
        if (expression is null)
        {
            return query;
        }
        else
        {
            return query.Where(expression);
        }
    }

    public static IQueryable<T> TopLevelOrderExpressionWithFallback<T>(
        this IQueryable<T> query,
        Expression<Func<T, object>>? expression,
        bool descending = false) where T : class
    {
        if (expression is null)
        {
            return query;
        }
        else
        {
            return descending
                ? query.OrderByDescending(expression)
                : query.OrderBy(expression);
        }
    }

    public static IQueryable<T> PageWithFallback<T>(
        this IQueryable<T> query,
        DataQuery? dataQuery,
        int take = 25) where T : class
    {
        return query.Skip(dataQuery?.Skip ?? 0).Take(dataQuery?.Top ?? take);
    }
}

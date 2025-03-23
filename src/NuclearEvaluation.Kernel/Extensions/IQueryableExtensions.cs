using LinqToDB;
using Radzen;
using System.Linq.Dynamic.Core;
using System.Linq.Expressions;

namespace NuclearEvaluation.Kernel.Extensions;

public static class IQueryableExtensions
{
    public static IQueryable<T> OrderByWithFallback<T>(
        this IQueryable<T> query,
        LoadDataArgs? args,
        Expression<Func<T, object>> defaultOrderBy) where T : class
    {
        bool isAlreadyOrdered = IsOrdered(query);

        if (args.HasOrderBy())
        {
            if (isAlreadyOrdered)
            {
                IOrderedQueryable<T> orderedQueryWithPrimary = ((IOrderedQueryable<T>)query).ThenBy(args!.OrderBy);
                return orderedQueryWithPrimary.ThenBy(defaultOrderBy);
            }
            else
            {
                IOrderedQueryable<T> orderedQueryWithPrimary = query.OrderBy(args!.OrderBy);
                return orderedQueryWithPrimary.ThenBy(defaultOrderBy);
            }
        }
        else
        {
            if (isAlreadyOrdered)
            {
                return ((IOrderedQueryable<T>)query).ThenOrBy(defaultOrderBy);
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
        LoadDataArgs? args) where T : class
    {
        if (args is null)
        {
            return query;
        }
        return query.FilterWithFallback(args.Filter);
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
            return query.Where(filter);
        }
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
        LoadDataArgs? args,
        int take = 25) where T : class
    {
        return query.Skip(args?.Skip ?? 0).Take(args?.Top ?? take);
    }
}
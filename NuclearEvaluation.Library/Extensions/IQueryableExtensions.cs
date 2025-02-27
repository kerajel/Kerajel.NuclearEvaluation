using Radzen;
using System.Linq.Dynamic.Core;
using System.Linq.Expressions;

namespace NuclearEvaluation.Library.Extensions;

public static class IQueryableExtensions
{
    public static IQueryable<T> OrderByWithFallback<T>(
        this IQueryable<T> query,
        LoadDataArgs? args,
        Expression<Func<T, object>> defaultOrderBy) where T : class
    {
        string filter = args?.OrderBy ?? string.Empty;

        bool isAlreadyOrdered = IsOrdered(query);

        if (!string.IsNullOrWhiteSpace(filter))
        {
            if (isAlreadyOrdered)
            {
                // Apply ThenBy with args.OrderBy
                IOrderedQueryable<T> orderedQueryWithPrimary = ((IOrderedQueryable<T>)query).ThenBy(args.OrderBy);

                // Apply ThenBy with defaultOrderBy
                IOrderedQueryable<T> finalOrderedQuery = orderedQueryWithPrimary.ThenBy(defaultOrderBy);

                return finalOrderedQuery;
            }
            else
            {
                // Apply primary OrderBy using Dynamic LINQ
                IOrderedQueryable<T> orderedQueryWithPrimary = query.OrderBy(args.OrderBy);

                // Apply ThenBy with defaultOrderBy
                IOrderedQueryable<T> finalOrderedQuery = orderedQueryWithPrimary.ThenBy(defaultOrderBy);

                return finalOrderedQuery;
            }
        }
        else
        {
            if (isAlreadyOrdered)
            {
                // Apply ThenBy with defaultOrderBy
                IOrderedQueryable<T> orderedQuery = ((IOrderedQueryable<T>)query).ThenBy(defaultOrderBy);

                return orderedQuery;
            }
            else
            {
                // Apply OrderBy with defaultOrderBy
                IOrderedQueryable<T> orderedQuery = query.OrderBy(defaultOrderBy);

                return orderedQuery;
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
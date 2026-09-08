using System.Globalization;
using System.Linq.Dynamic.Core;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using NuclearEvaluation.Shared.Contracts;
using NuclearEvaluation.Shared.Enums;
using DynamicQueryableExtensions = System.Linq.Dynamic.Core.DynamicQueryableExtensions;

namespace NuclearEvaluation.Kernel.Extensions;

public static class IQueryableExtensions
{
    static readonly ParsingConfig FilterParsingConfig = CreateFilterParsingConfig();

    static ParsingConfig CreateFilterParsingConfig()
    {
        ParsingConfig config = new();
        // Radzen emits these enum names and uses invariant culture for date literals.
        config.UseDefaultDynamicLinqCustomTypeProvider([
            typeof(SeriesType),
            typeof(SampleType),
            typeof(CultureInfo),
            typeof(DateTimeStyles),
        ]);
        return config;
    }

    const string EnumTypePattern =
        @"(?:NuclearEvaluation\.Shared\.Enums\.)?(?:SampleType|SeriesType)";

    // Match quoted values first so normalization never changes text entered by the user.
    static readonly Regex FilterSyntaxRegex = new(
        "\"(?:\\\\.|[^\"\\\\])*\"|'(?:\\\\.|[^'\\\\])*'"
            + @"|(?<nullCoalesce>\?\?\s*null\b)"
            + $@"|\bdefault\(\s*(?<defaultType>{EnumTypePattern}|bool|byte|short|int|long|float|double|decimal|(?:System\.)?DateTime)\s*\)"
            + $@"|\(\s*(?<enumType>{EnumTypePattern})\s*\)\s*(?<enumValue>-?\d+)\b",
        RegexOptions.Compiled,
        TimeSpan.FromSeconds(1)
    );

    public static IQueryable<T> OrderByWithFallback<T>(
        this IQueryable<T> query,
        DataQuery? dataQuery,
        Expression<Func<T, object>> defaultOrderBy
    )
        where T : class
    {
        bool isAlreadyOrdered = IsOrdered(query);

        if (!string.IsNullOrWhiteSpace(dataQuery?.OrderBy))
        {
            if (isAlreadyOrdered)
            {
                IOrderedQueryable<T> orderedQueryWithPrimary = DynamicQueryableExtensions.ThenBy(
                    (IOrderedQueryable<T>)query,
                    dataQuery.OrderBy
                );
                return orderedQueryWithPrimary.ThenBy(defaultOrderBy);
            }
            else
            {
                IOrderedQueryable<T> orderedQueryWithPrimary = DynamicQueryableExtensions.OrderBy(
                    query,
                    dataQuery.OrderBy
                );
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

    static bool IsOrdered<T>(IQueryable<T> query)
        where T : class
    {
        Expression expression = query.Expression;

        while (expression is MethodCallExpression methodCall)
        {
            string methodName = methodCall.Method.Name;

            if (
                methodName == "OrderBy"
                || methodName == "OrderByDescending"
                || methodName == "ThenBy"
                || methodName == "ThenByDescending"
            )
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
        DataQuery? dataQuery
    )
        where T : class
    {
        return query.FilterWithFallback(dataQuery?.Filter);
    }

    public static IQueryable<T> FilterWithFallback<T>(this IQueryable<T> query, string? filter)
        where T : class
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return query;
        }
        else
        {
            return DynamicQueryableExtensions.Where(
                query,
                FilterParsingConfig,
                NormalizeFilter(filter)
            );
        }
    }

    static string NormalizeFilter(string filter)
    {
        return FilterSyntaxRegex.Replace(
            filter,
            match =>
            {
                if (match.Groups["nullCoalesce"].Success)
                    return string.Empty;
                if (match.Groups["enumType"].Success)
                    return EnumLiteral(
                        match.Groups["enumType"].Value,
                        match.Groups["enumValue"].Value
                    );
                if (match.Groups["defaultType"].Success)
                {
                    string type = match.Groups["defaultType"].Value;
                    return type switch
                    {
                        "bool" => "false",
                        "DateTime" or "System.DateTime" => "DateTime(1, 1, 1)",
                        _ when type.EndsWith("Type", StringComparison.Ordinal) => EnumLiteral(
                            type,
                            "0"
                        ),
                        _ => "0",
                    };
                }
                return match.Value;
            }
        );
    }

    static string EnumLiteral(string type, string value)
    {
        string fullName = type.Contains('.') ? type : $"NuclearEvaluation.Shared.Enums.{type}";
        // Dynamic LINQ uses a function-style cast; quoting avoids collisions with property names.
        return $"\"{fullName}\"({value})";
    }

    public static IQueryable<T> TopLevelFilterExpressionWithFallback<T>(
        this IQueryable<T> query,
        Expression<Func<T, bool>>? expression
    )
        where T : class
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
        bool descending = false
    )
        where T : class
    {
        if (expression is null)
        {
            return query;
        }
        else
        {
            return descending ? query.OrderByDescending(expression) : query.OrderBy(expression);
        }
    }

    public static IQueryable<T> PageWithFallback<T>(
        this IQueryable<T> query,
        DataQuery? dataQuery,
        int take = 25
    )
        where T : class
    {
        return query
            .Skip(Math.Max(0, dataQuery?.Skip ?? 0))
            .Take(Math.Clamp(dataQuery?.Top ?? take, 1, DataQuery.MaxPageSize));
    }
}

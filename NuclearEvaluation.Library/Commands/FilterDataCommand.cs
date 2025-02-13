using NuclearEvaluation.Library.Enums;
using NuclearEvaluation.Library.Models.Filters;
using Radzen;
using System.Linq.Expressions;

namespace NuclearEvaluation.Library.Commands;

public class FilterDataCommand<T>()
{
    readonly List<dynamic> includes = [];

    readonly Dictionary<string, object?> _args = [];

    public Expression<Func<T, bool>>? TopLevelFilterExpression { get; set; }

    public Expression<Func<T, object>>? TopLevelOrderExpression { get; set; }

    public PresetFilterBox? PresetFilterBox { get; set; }

    public LoadDataArgs? LoadDataArgs { get; set; }

    public bool FetchTotalCount { get; set; } = true;

    public bool AsNoTracking { get; set; } = true;

    public void AddArgument<K>(string key, K? value)
    {
       _args.Add(key, value);
    }

    public K? TryGetArgumentOrDefault<K>(string key)
    {
        _ = _args.TryGetValue(key, out object? value);
        return value is K k ? k : default;
    }

    public K GetRequiredArgument<K>(string key)
    {
        bool success = _args.TryGetValue(key, out object? value);
        if (!success || value is not K result)
        {
            throw new Exception($"Required argument '{key}' for '{nameof(FilterDataCommand)}' was not supplied");
        }
        return result;
    }

    public IEnumerable<dynamic> Includes
    {
        get
        {
            return includes;
        }
    }

    public QueryKind QueryKind
    {
        get
        {
            if (TryGetArgumentOrDefault<bool>(FilterDataCommand.ArgKeys.EnableDecayCorrection))
            {
                return QueryKind.DecayCorrected;
            }
            if (PresetFilterBox != null && PresetFilterBox.HasFilter())
            {
                return QueryKind.QueryBuilder;
            }
            return QueryKind.Basic;
        }
    }

    public void Include<TChild>(Expression<Func<T, TChild>> queryIncludeFilter) where TChild : class
    {
        includes.Add(queryIncludeFilter);
    }
}

public class FilterDataCommand
{
    private FilterDataCommand()
    {
            
    }

    public static class ArgKeys
    {
        public const string ProjectId = "ProjectId";
        public const string EnableDecayCorrection = "UseDecayCorrection";
    }
}
using NuclearEvaluation.Kernel.Enums;
using NuclearEvaluation.Shared.Contracts;
using System.Linq.Expressions;

namespace NuclearEvaluation.Kernel.Commands;

/// <summary>
/// Server-side query envelope: the serializable <see cref="DataQuery"/> received from the
/// client plus the expression-based refinements only the server can construct.
/// </summary>
public class FetchDataCommand<T>
{
    readonly List<dynamic> includes = [];

    public Expression<Func<T, bool>>? TopLevelFilterExpression { get; set; }

    public Expression<Func<T, object>>? TopLevelOrderExpression { get; set; }

    public DataQuery? Query { get; set; }

    public bool AsNoTracking { get; set; } = true;

    public bool HasOrderBy => !string.IsNullOrWhiteSpace(Query?.OrderBy);

    public IEnumerable<dynamic> Includes => includes;

    public QueryKind QueryKind
    {
        get
        {
            if (Query?.DecayCorrected == true)
            {
                return QueryKind.DecayCorrected;
            }
            if (Query?.PresetFilterBox?.HasFilter() == true)
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

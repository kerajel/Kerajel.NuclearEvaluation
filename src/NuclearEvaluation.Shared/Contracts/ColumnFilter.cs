namespace NuclearEvaluation.Shared.Contracts;

public sealed record ColumnFilterGroup(
    string Field,
    bool MatchAny,
    IReadOnlyList<ColumnFilterCondition> Conditions
)
{
    public const int MaxConditions = 2;
}

public sealed record ColumnFilterCondition(RadNetOperator Operator, string? Value = null);

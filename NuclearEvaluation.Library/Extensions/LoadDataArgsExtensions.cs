using Radzen;

namespace NuclearEvaluation.Library.Extensions;

public static partial class LoadDataArgsExtensions
{
    public static bool HasFilter(this LoadDataArgs? args)
    {
        if (args == null)
            return false;

        return !string.IsNullOrWhiteSpace(args.Filter);
    }

    public static bool HasOrderBy(this LoadDataArgs? args)
    {
        if (args == null)
            return false;

        return !string.IsNullOrWhiteSpace(args.OrderBy) && !args.Sorts.IsNullOrEmpty();
    }
}
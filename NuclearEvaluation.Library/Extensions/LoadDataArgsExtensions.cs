using Radzen;

namespace NuclearEvaluation.Library.Extensions;

public static partial class LoadDataArgsExtensions
{
    public static bool HasFilter(this LoadDataArgs args)
    {
        return !string.IsNullOrWhiteSpace(args.Filter);
    }

    public static bool HasEmptyFilter(this LoadDataArgs args)
    {
        return string.IsNullOrWhiteSpace(args.Filter);
    }
}
using System.Globalization;
using NuclearEvaluation.Shared.Contracts;

namespace NuclearEvaluation.Client.Shared.Radiation;

internal static class RadNetPresentation
{
    internal static string EntityName(RadNetEntity entity) =>
        entity switch
        {
            RadNetEntity.Station => "Monitoring stations",
            RadNetEntity.SourceFile => "Annual source files",
            RadNetEntity.Observation => "Observations",
            RadNetEntity.Channel => "Gamma channel readings",
            _ => throw new ArgumentOutOfRangeException(nameof(entity)),
        };

    internal static string SingularName(RadNetEntity entity) =>
        entity switch
        {
            RadNetEntity.Station => "Station",
            RadNetEntity.SourceFile => "Annual file",
            RadNetEntity.Observation => "Observation",
            RadNetEntity.Channel => "Gamma reading",
            _ => throw new ArgumentOutOfRangeException(nameof(entity)),
        };

    internal static string Count(long? value) =>
        value?.ToString("N0", CultureInfo.InvariantCulture) ?? "—";

    internal static string Rate(double? value) =>
        value?.ToString("G", CultureInfo.InvariantCulture) ?? "—";

    internal static string Timestamp(DateTime? value) =>
        value?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? "—";

    internal static bool TakesValue(RadNetOperator operation) =>
        operation is not (RadNetOperator.IsPresent or RadNetOperator.IsMissing);

    internal static string OperatorName(RadNetOperator operation) =>
        operation switch
        {
            RadNetOperator.Equal => "is",
            RadNetOperator.NotEqual => "is not",
            RadNetOperator.Contains => "contains",
            RadNetOperator.GreaterThan => "greater than",
            RadNetOperator.AtLeast => "at least",
            RadNetOperator.LessThan => "less than",
            RadNetOperator.AtMost => "at most",
            RadNetOperator.IsPresent => "is present",
            RadNetOperator.IsMissing => "is missing",
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };

    internal static string? SafeArchiveUrl(string? candidate) =>
        Uri.TryCreate(candidate, UriKind.Absolute, out Uri? uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && (
            uri.Host.Equals("epa.gov", StringComparison.OrdinalIgnoreCase)
            || uri.Host.EndsWith(".epa.gov", StringComparison.OrdinalIgnoreCase)
        )
            ? uri.AbsoluteUri
            : null;
}

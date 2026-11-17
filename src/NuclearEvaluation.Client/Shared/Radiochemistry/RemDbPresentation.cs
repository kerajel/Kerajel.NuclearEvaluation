using System.Globalization;
using NuclearEvaluation.Shared.Contracts;

namespace NuclearEvaluation.Client.Shared.Radiochemistry;

internal static class RemDbPresentation
{
    internal const string PagePath = "radiochemistry";
    internal const int MaxFilterValueLength = 200;
    internal const string DataSourceUrl =
        "https://jeodpp.jrc.ec.europa.eu/ftp/jrc-opendata/REMDB/datasets/";

    internal static string FileUrl(int id) =>
        FormattableString.Invariant($"{PagePath}?view=quality&file={id}");

    internal static string? ArchiveUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && uri.Host == "jeodpp.jrc.ec.europa.eu"
            ? uri.AbsoluteUri
            : null;

    internal static string EntityName(RemDbEntity entity) =>
        entity switch
        {
            RemDbEntity.Site => "Sites",
            RemDbEntity.SamplingWindow => "Sampling windows",
            _ => "Measurements",
        };

    internal static string SingularName(RemDbEntity entity) =>
        entity switch
        {
            RemDbEntity.Site => "Site",
            RemDbEntity.SamplingWindow => "Sampling window",
            _ => "Measurement",
        };

    internal static string Display(string? value) => string.IsNullOrWhiteSpace(value) ? "—" : value;

    internal static string SiteLabel(RemDbRow row) =>
        string.IsNullOrWhiteSpace(row.SiteName)
            ? FormattableString.Invariant($"Unnamed site {row.SiteId}")
            : row.SiteName;

    internal static string Count(long? value) =>
        value?.ToString("N0", CultureInfo.InvariantCulture) ?? "—";

    internal static string Number(double? value) =>
        value?.ToString("G", CultureInfo.InvariantCulture) ?? "—";

    internal static string Date(DateOnly? value) =>
        value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "—";

    internal static string DateRange(RemDbRow row)
    {
        string start = row.SourceBegin ?? Date(row.BeganOn);
        string end = row.SourceEnd ?? Date(row.EndedOn);
        return start == end ? start : $"{start} to {end}";
    }
}

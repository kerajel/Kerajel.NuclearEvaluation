using System.Text.Json.Serialization;

namespace NuclearEvaluation.Shared.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter<RemDbEntity>))]
public enum RemDbEntity
{
    Site,
    SamplingWindow,
    Measurement,
}

public sealed record RemDbQuery(
    RemDbEntity Entity = RemDbEntity.Measurement,
    IReadOnlyList<RemDbFilterGroup>? Filters = null,
    string? SortField = null,
    bool Descending = false,
    int Skip = 0,
    int Top = 50
);

public sealed record RemDbFilterGroup(
    RemDbEntity Entity,
    bool MatchAny,
    IReadOnlyList<RemDbCondition> Conditions
);

public sealed record RemDbCondition(string Field, RadNetOperator Operator, string? Value = null);

public sealed record RemDbQueryResult(IReadOnlyList<RemDbRow> Items, long TotalCount);

public sealed record RemDbSummary(
    bool Available,
    long SiteCount,
    long SamplingWindowCount,
    long MeasurementCount,
    long SourceFileCount,
    IReadOnlyList<RemDbYearCoverage> Coverage,
    IReadOnlyList<RemDbCountryCoverage> Countries,
    IReadOnlyDictionary<string, string> Metadata
);

public sealed record RemDbYearCoverage(int Year, long MeasurementCount);

public sealed record RemDbCountryCoverage(string Country, long SiteCount);

public sealed record RemDbSourceFile(
    int Id,
    int Year,
    string ArchiveUrl,
    string ArchiveSha256,
    string FileName,
    long RowCount
);

public sealed record RemDbFilePage(IReadOnlyList<RemDbSourceFile> Items, long TotalCount);

public sealed record RemDbCoverageValue(string Value, long Count);

public sealed record RemDbFileQuality(
    RemDbSourceFile SourceFile,
    long MeasurementCount,
    long MissingActivityCount,
    long LessThanCount,
    long MissingQualifierCount,
    DateOnly? FirstBeganOn,
    DateOnly? LastEndedOn,
    IReadOnlyList<RemDbCoverageValue> Units,
    IReadOnlyList<RemDbCoverageValue> ResultTypes
);

public sealed record RemDbRow
{
    public int SiteId { get; init; }
    public string Country { get; init; } = "";
    public string SiteName { get; init; } = "";
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public long? WindowId { get; init; }
    public string? Medium { get; init; }
    public string? Treatment { get; init; }
    public DateOnly? BeganOn { get; init; }
    public DateOnly? EndedOn { get; init; }
    public string? SourceBegin { get; init; }
    public string? SourceEnd { get; init; }
    public long? MeasurementId { get; init; }
    public int? SourceFileId { get; init; }
    public int? SourceRow { get; init; }
    public int? SourceYear { get; init; }
    public string? ArchiveUrl { get; init; }
    public string? ArchiveSha256 { get; init; }
    public string? FileName { get; init; }
    public string? Laboratory { get; init; }
    public string? Apparatus { get; init; }
    public string? Nuclide { get; init; }
    public double? Activity { get; init; }
    public string? Unit { get; init; }
    public string? Qualifier { get; init; }
    public string? ValueType { get; init; }
}

public sealed record RemDbField(
    RemDbEntity Entity,
    string Key,
    string Label,
    RadNetFieldType Type,
    bool Nullable = false
);

public static class RemDbCatalog
{
    public const int MaxPageSize = 250;
    public const int MaxGroups = 12;
    public const int MaxConditions = 24;

    public static IReadOnlyList<RemDbField> Fields { get; } =
    [
        new(RemDbEntity.Site, nameof(RemDbRow.SiteId), "Site ID", RadNetFieldType.Integer),
        new(RemDbEntity.Site, nameof(RemDbRow.Country), "Country", RadNetFieldType.Text, true),
        new(RemDbEntity.Site, nameof(RemDbRow.SiteName), "Site", RadNetFieldType.Text, true),
        new(RemDbEntity.Site, nameof(RemDbRow.Latitude), "Latitude", RadNetFieldType.Number, true),
        new(
            RemDbEntity.Site,
            nameof(RemDbRow.Longitude),
            "Longitude",
            RadNetFieldType.Number,
            true
        ),
        new(
            RemDbEntity.SamplingWindow,
            nameof(RemDbRow.WindowId),
            "Sampling window ID",
            RadNetFieldType.Integer
        ),
        new(
            RemDbEntity.SamplingWindow,
            nameof(RemDbRow.Medium),
            "Medium",
            RadNetFieldType.Text,
            true
        ),
        new(
            RemDbEntity.SamplingWindow,
            nameof(RemDbRow.Treatment),
            "Treatment",
            RadNetFieldType.Text,
            true
        ),
        new(
            RemDbEntity.SamplingWindow,
            nameof(RemDbRow.BeganOn),
            "Sampling began",
            RadNetFieldType.DateTime,
            true
        ),
        new(
            RemDbEntity.SamplingWindow,
            nameof(RemDbRow.EndedOn),
            "Sampling ended",
            RadNetFieldType.DateTime,
            true
        ),
        new(
            RemDbEntity.Measurement,
            nameof(RemDbRow.MeasurementId),
            "Measurement ID",
            RadNetFieldType.Integer
        ),
        new(
            RemDbEntity.Measurement,
            nameof(RemDbRow.Nuclide),
            "Radionuclide",
            RadNetFieldType.Text,
            true
        ),
        new(
            RemDbEntity.Measurement,
            nameof(RemDbRow.Activity),
            "Reported activity",
            RadNetFieldType.Number,
            true
        ),
        new(RemDbEntity.Measurement, nameof(RemDbRow.Unit), "Unit", RadNetFieldType.Text, true),
        new(
            RemDbEntity.Measurement,
            nameof(RemDbRow.Qualifier),
            "Reported qualifier",
            RadNetFieldType.Text,
            true
        ),
        new(
            RemDbEntity.Measurement,
            nameof(RemDbRow.ValueType),
            "Reported value type",
            RadNetFieldType.Text,
            true
        ),
        new(
            RemDbEntity.Measurement,
            nameof(RemDbRow.Laboratory),
            "Reporting laboratory",
            RadNetFieldType.Text,
            true
        ),
        new(
            RemDbEntity.Measurement,
            nameof(RemDbRow.Apparatus),
            "Apparatus",
            RadNetFieldType.Text,
            true
        ),
        new(
            RemDbEntity.Measurement,
            nameof(RemDbRow.SourceFileId),
            "Source file ID",
            RadNetFieldType.Integer
        ),
        new(
            RemDbEntity.Measurement,
            nameof(RemDbRow.SourceRow),
            "Source row",
            RadNetFieldType.Integer
        ),
        new(
            RemDbEntity.Measurement,
            nameof(RemDbRow.SourceYear),
            "Release year",
            RadNetFieldType.Integer
        ),
        new(
            RemDbEntity.Measurement,
            nameof(RemDbRow.FileName),
            "Source file",
            RadNetFieldType.Text
        ),
    ];

    public static bool CanCompare(RemDbField field, RadNetOperator operation) =>
        operation switch
        {
            RadNetOperator.Equal or RadNetOperator.NotEqual => true,
            RadNetOperator.Contains => field.Type == RadNetFieldType.Text,
            RadNetOperator.GreaterThan
            or RadNetOperator.AtLeast
            or RadNetOperator.LessThan
            or RadNetOperator.AtMost => field.Type != RadNetFieldType.Text,
            RadNetOperator.IsPresent or RadNetOperator.IsMissing => field.Nullable,
            _ => false,
        };
}

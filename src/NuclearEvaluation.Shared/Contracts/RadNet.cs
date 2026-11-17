using System.Text.Json.Serialization;

namespace NuclearEvaluation.Shared.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter<RadNetEntity>))]
public enum RadNetEntity
{
    Station,
    SourceFile,
    Observation,
    Channel,
}

[JsonConverter(typeof(JsonStringEnumConverter<RadNetOperator>))]
public enum RadNetOperator
{
    Equal,
    NotEqual,
    Contains,
    GreaterThan,
    AtLeast,
    LessThan,
    AtMost,
    IsPresent,
    IsMissing,
}

[JsonConverter(typeof(JsonStringEnumConverter<RadNetFieldType>))]
public enum RadNetFieldType
{
    Text,
    Integer,
    Number,
    DateTime,
}

public sealed record RadNetQuery(
    RadNetEntity Entity = RadNetEntity.Observation,
    IReadOnlyList<RadNetFilterGroup>? Filters = null,
    string? SortField = null,
    bool Descending = false,
    int Skip = 0,
    int Top = 50
);

public sealed record RadNetFilterGroup(
    RadNetEntity Entity,
    bool MatchAny,
    IReadOnlyList<RadNetCondition> Conditions
);

public sealed record RadNetCondition(string Field, RadNetOperator Operator, string? Value = null);

public sealed record RadNetQueryResult(IReadOnlyList<RadNetRow> Items, long TotalCount);

public sealed record RadNetSummary(
    bool Available,
    long StationCount,
    long SourceFileCount,
    long ObservationCount,
    IReadOnlyList<RadNetYearCoverage> Coverage,
    IReadOnlyDictionary<string, string> Metadata
);

public sealed record RadNetYearCoverage(int Year, long StationCount, long ObservationCount);

public sealed record RadNetChannelCoverage(string Channel, long ObservationCount);

public sealed record RadNetFileQuality(
    RadNetRow SourceFile,
    long ObservationCount,
    DateTime? FirstObservedAt,
    DateTime? LastObservedAt,
    long MissingHours,
    long MissingDoseCount,
    long NonApprovedCount,
    IReadOnlyList<RadNetChannelCoverage> Channels
);

public sealed record RadNetRow
{
    public int StationId { get; init; }
    public string StationName { get; init; } = "";
    public string State { get; init; } = "";
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public int? SourceFileId { get; init; }
    public int? Year { get; init; }
    public string? ArchiveUrl { get; init; }
    public string? ArchiveSha256 { get; init; }
    public string? FileName { get; init; }
    public long? SourceRows { get; init; }
    public long? InputRows { get; init; }
    public long? RejectedRows { get; init; }
    public long? DuplicateRows { get; init; }
    public long? ObservationId { get; init; }
    public int? SourceRow { get; init; }
    public DateTime? ObservedAt { get; init; }
    public string? Status { get; init; }
    public double? ExposureMrH { get; init; }
    public double? DoseNsvH { get; init; }
    public string? Channel { get; init; }
    public double? CountRate { get; init; }
}

public sealed record RadNetField(
    RadNetEntity Entity,
    string Name,
    string Label,
    RadNetFieldType Type,
    bool Nullable = false
);

public static class RadNetCatalog
{
    public const int MaxPageSize = 250;
    public const int MaxGroups = 12;
    public const int MaxConditions = 24;

    public static IReadOnlyList<RadNetField> Fields { get; } =
    [
        new(
            RadNetEntity.Station,
            nameof(RadNetRow.StationId),
            "Station ID",
            RadNetFieldType.Integer
        ),
        new(RadNetEntity.Station, nameof(RadNetRow.StationName), "Station", RadNetFieldType.Text),
        new(RadNetEntity.Station, nameof(RadNetRow.State), "State", RadNetFieldType.Text),
        new(
            RadNetEntity.SourceFile,
            nameof(RadNetRow.SourceFileId),
            "Source file ID",
            RadNetFieldType.Integer
        ),
        new(
            RadNetEntity.SourceFile,
            nameof(RadNetRow.Year),
            "Archive year",
            RadNetFieldType.Integer
        ),
        new(
            RadNetEntity.SourceFile,
            nameof(RadNetRow.FileName),
            "Source file",
            RadNetFieldType.Text
        ),
        new(
            RadNetEntity.SourceFile,
            nameof(RadNetRow.SourceRows),
            "Observations in file",
            RadNetFieldType.Integer
        ),
        new(
            RadNetEntity.Observation,
            nameof(RadNetRow.ObservationId),
            "Observation ID",
            RadNetFieldType.Integer
        ),
        new(
            RadNetEntity.Observation,
            nameof(RadNetRow.ObservedAt),
            "Observed at (UTC)",
            RadNetFieldType.DateTime
        ),
        new(
            RadNetEntity.Observation,
            nameof(RadNetRow.SourceRow),
            "CSV row",
            RadNetFieldType.Integer
        ),
        new(RadNetEntity.Observation, nameof(RadNetRow.Status), "Status", RadNetFieldType.Text),
        new(
            RadNetEntity.Observation,
            nameof(RadNetRow.ExposureMrH),
            "Exposure rate (mR/h)",
            RadNetFieldType.Number,
            true
        ),
        new(
            RadNetEntity.Observation,
            nameof(RadNetRow.DoseNsvH),
            "Dose equivalent rate (nSv/h)",
            RadNetFieldType.Number,
            true
        ),
        new(RadNetEntity.Channel, nameof(RadNetRow.Channel), "Gamma channel", RadNetFieldType.Text),
        new(
            RadNetEntity.Channel,
            nameof(RadNetRow.CountRate),
            "Count rate (CPM)",
            RadNetFieldType.Number
        ),
    ];

    public static bool CanCompare(RadNetField field, RadNetOperator operation) =>
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

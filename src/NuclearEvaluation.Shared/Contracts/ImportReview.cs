using System.Text.Json.Serialization;

namespace NuclearEvaluation.Shared.Contracts;

public sealed record ImportReviewAvailability(ImportReviewSource? Sample, bool SampleAvailable);

public sealed record ImportReviewSource(
    int FileId,
    string State,
    string Station,
    int Year,
    string FileName,
    string ArchiveUrl,
    long ObservationCount,
    string SourceLocation,
    string ArchiveSha256 = ""
);

[JsonConverter(typeof(JsonStringEnumConverter<ImportReviewDisposition>))]
public enum ImportReviewDisposition
{
    MatchesArchive,
    Duplicate,
    Rejected,
    Changed,
    New,
}

public sealed record ImportReviewStage(
    Guid Id,
    ImportReviewSource Source,
    string Name,
    string Sha256,
    long InputRows,
    long MatchedRows,
    long DuplicateRows,
    long RejectedRows,
    long ChangedRows,
    long NewRows,
    long MissingArchiveRows,
    long RowsWithoutExposure
)
{
    public bool CanCreateSeries =>
        Source.ObservationCount > 0
        && MatchedRows == Source.ObservationCount
        && ChangedRows == 0
        && NewRows == 0
        && MissingArchiveRows == 0;
}

public sealed record ImportReviewRow(
    int SourceRow,
    string Location,
    DateTime? ObservedAt,
    string Status,
    double? ExposureMrH,
    double? DoseNsvH,
    double? R03,
    ImportReviewDisposition Disposition,
    string Reason
);

public sealed record ImportReviewQuery(
    string? SortField = null,
    bool Descending = false,
    int Skip = 0,
    int Top = 50,
    IReadOnlyList<ColumnFilterGroup>? Filters = null
);

public sealed record ImportReviewPage(IReadOnlyList<ImportReviewRow> Items, long TotalCount);

public sealed record ImportReviewCreateSeries(Guid? ProjectId = null);

public sealed record ImportReviewSeriesLink(
    WorkbenchSeries Series,
    bool Created,
    Guid? ProjectId,
    bool Linked
);

public sealed record ImportReviewField(
    string Name,
    string Label,
    RadNetFieldType Type,
    bool Nullable = false
);

public static class ImportReviewCatalog
{
    public const int MaxUploadBytes = 8 * 1024 * 1024;
    public const int MaxPageSize = 250;

    public static IReadOnlyList<ImportReviewField> Fields { get; } =
    [
        new(nameof(ImportReviewRow.SourceRow), "CSV record", RadNetFieldType.Integer),
        new(nameof(ImportReviewRow.Location), "Reported location", RadNetFieldType.Text),
        new(
            nameof(ImportReviewRow.ObservedAt),
            "Observed at (UTC)",
            RadNetFieldType.DateTime,
            true
        ),
        new(nameof(ImportReviewRow.Status), "Publication status", RadNetFieldType.Text),
        new(nameof(ImportReviewRow.ExposureMrH), "Exposure (mR/h)", RadNetFieldType.Number, true),
        new(nameof(ImportReviewRow.DoseNsvH), "Dose (nSv/h)", RadNetFieldType.Number, true),
        new(nameof(ImportReviewRow.R03), "R03 (CPM)", RadNetFieldType.Number, true),
        new(nameof(ImportReviewRow.Disposition), "Review outcome", RadNetFieldType.Text),
        new(nameof(ImportReviewRow.Reason), "Finding", RadNetFieldType.Text),
    ];

    public static bool CanCompare(ImportReviewField field, RadNetOperator operation) =>
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

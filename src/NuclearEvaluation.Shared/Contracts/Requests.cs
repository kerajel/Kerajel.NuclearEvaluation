using System.ComponentModel.DataAnnotations;

namespace NuclearEvaluation.Shared.Contracts;

/// <summary>Distinct enum option lookup for a grid column, scoped by the current query.</summary>
public class EnumFilterRequest
{
    [Required, MaxLength(100)]
    public string PropertyName { get; set; } = string.Empty;

    [Required]
    public DataQuery Query { get; set; } = new();
}

/// <summary>Updates a single editable scalar field of a project.</summary>
public class ProjectFieldUpdate : IValidatableObject
{
    [Range(1, int.MaxValue)]
    public int ProjectId { get; set; }

    [EnumDataType(typeof(ProjectField))]
    public ProjectField Field { get; set; }

    public string? StringValue { get; set; }

    public DateTime? DateValue { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (
            Field == ProjectField.Name
            && (string.IsNullOrWhiteSpace(StringValue) || StringValue.Length is < 10 or > 50)
        )
            yield return new(
                "Project name must be between 10 and 50 characters.",
                [nameof(StringValue)]
            );
        if (
            Field is ProjectField.Conclusions or ProjectField.FollowUpActionsRecommended
            && (StringValue is null || StringValue.Length > 400)
        )
            yield return new(
                "Text must be supplied and cannot exceed 400 characters.",
                [nameof(StringValue)]
            );
    }
}

public enum ProjectField
{
    Name = 1,
    Conclusions = 2,
    FollowUpActionsRecommended = 3,
    DecayCorrectionDate = 4,
}

/// <summary>Replaces the set of series associated with a project.</summary>
public class ProjectSeriesUpdate : IValidatableObject
{
    [Range(1, int.MaxValue)]
    public int ProjectId { get; set; }

    [Required, MinLength(1), MaxLength(1000)]
    public List<int> SeriesIds { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (
            SeriesIds is not null
            && (SeriesIds.Any(id => id <= 0) || SeriesIds.Distinct().Count() != SeriesIds.Count)
        )
            yield return new("Series IDs must be positive and distinct.", [nameof(SeriesIds)]);
    }
}

/// <summary>Generic success/failure envelope for operations without a payload.</summary>
public class OperationOutcome
{
    public bool IsSuccessful { get; set; } = true;

    public string? ErrorMessage { get; set; }

    public static OperationOutcome Ok() => new();

    public static OperationOutcome Fail(string? message) =>
        new() { IsSuccessful = false, ErrorMessage = message };
}

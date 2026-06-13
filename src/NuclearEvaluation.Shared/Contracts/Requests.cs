namespace NuclearEvaluation.Shared.Contracts;

/// <summary>Distinct enum option lookup for a grid column, scoped by the current query.</summary>
public class EnumFilterRequest
{
    public string PropertyName { get; set; } = string.Empty;

    public DataQuery Query { get; set; } = new();
}

/// <summary>Updates a single editable scalar field of a project.</summary>
public class ProjectFieldUpdate
{
    public int ProjectId { get; set; }

    public ProjectField Field { get; set; }

    public string? StringValue { get; set; }

    public DateTime? DateValue { get; set; }
}

public enum ProjectField
{
    Name = 1,
    Conclusions = 2,
    FollowUpActionsRecommended = 3,
    DecayCorrectionDate = 4,
}

/// <summary>Replaces the set of series associated with a project.</summary>
public class ProjectSeriesUpdate
{
    public int ProjectId { get; set; }

    public List<int> SeriesIds { get; set; } = [];
}

/// <summary>Generic success/failure envelope for operations without a payload.</summary>
public class OperationOutcome
{
    public bool IsSuccessful { get; set; } = true;

    public string? ErrorMessage { get; set; }

    public static OperationOutcome Ok() => new();

    public static OperationOutcome Fail(string? message) => new() { IsSuccessful = false, ErrorMessage = message };
}

using System.ComponentModel.DataAnnotations;
using NuclearEvaluation.Shared.Models.Filters;

namespace NuclearEvaluation.Shared.Contracts;

/// <summary>
/// Serializable query sent by the WASM client to the data endpoints.
/// Mirrors what Radzen grids produce (dynamic-LINQ filter and order strings plus paging),
/// extended with the contextual options the grids used to express as expressions.
/// </summary>
public class DataQuery : IValidatableObject
{
    public const int MaxPageSize = 500;
    public const int MaxFilterLength = 16_384;

    [MaxLength(MaxFilterLength)]
    public string? Filter { get; set; }

    [MaxLength(1024)]
    public string? OrderBy { get; set; }

    [Range(0, int.MaxValue)]
    public int? Skip { get; set; }

    [Range(1, MaxPageSize)]
    public int? Top { get; set; }

    public PresetFilterBox? PresetFilterBox { get; set; }

    /// <summary>Restricts results to entities belonging to the project.</summary>
    [Range(1, int.MaxValue)]
    public int? ProjectId { get; set; }

    /// <summary>Serve decay-corrected values where the entity supports it.</summary>
    public bool DecayCorrected { get; set; }

    /// <summary>STEM preview session whose staged entries are queried.</summary>
    public Guid? StemSessionId { get; set; }

    /// <summary>Ids ordered to the top of the result set (used by selection grids).</summary>
    [MaxLength(1000)]
    public List<int>? PriorityIds { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (DecayCorrected && ProjectId is null)
            yield return new("Decay correction requires a project.", [nameof(ProjectId)]);
        if (PriorityIds?.Any(id => id <= 0) == true)
            yield return new("Priority IDs must be positive.", [nameof(PriorityIds)]);
        if (
            PresetFilterBox is { } box
            && (
                box.Filters is null
                || box.Filters.Count > 5
                || box.Filters.Any(pair =>
                    !Enum.IsDefined(pair.Key) || pair.Value?.Length > MaxFilterLength
                )
            )
        )
            yield return new(
                "Preset filters contain an invalid type or exceed the filter size limit.",
                [nameof(PresetFilterBox)]
            );
    }
}

using NuclearEvaluation.Shared.Models.Filters;

namespace NuclearEvaluation.Shared.Contracts;

/// <summary>
/// Serializable query sent by the WASM client to the data endpoints.
/// Mirrors what Radzen grids produce (dynamic-LINQ filter and order strings plus paging),
/// extended with the contextual options the grids used to express as expressions.
/// </summary>
public class DataQuery
{
    public string? Filter { get; set; }

    public string? OrderBy { get; set; }

    public int? Skip { get; set; }

    public int? Top { get; set; }

    public PresetFilterBox? PresetFilterBox { get; set; }

    /// <summary>Restricts results to entities belonging to the project.</summary>
    public int? ProjectId { get; set; }

    /// <summary>Serve decay-corrected values where the entity supports it.</summary>
    public bool DecayCorrected { get; set; }

    /// <summary>STEM preview session whose staged entries are queried.</summary>
    public Guid? StemSessionId { get; set; }

    /// <summary>Ids ordered to the top of the result set (used by selection grids).</summary>
    public List<int>? PriorityIds { get; set; }
}

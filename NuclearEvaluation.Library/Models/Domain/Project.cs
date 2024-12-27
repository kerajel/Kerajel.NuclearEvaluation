using NuclearEvaluation.Library.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace NuclearEvaluation.Library.Models.Domain;

[Index(nameof(Name), IsUnique = true)]
public class Project : IIdentifiable
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    public List<ProjectSeries> ProjectSeries { get; set; } = [];

    public string Conclusions { get; set; } = string.Empty;

    public string FollowUpActionsRecommended { get; set; } = string.Empty;

    public DateTime? DecayCorrectionDate { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
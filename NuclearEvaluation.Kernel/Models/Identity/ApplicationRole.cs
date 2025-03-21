using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;

namespace NuclearEvaluation.Kernel.Models.Identity;

public partial class ApplicationRole : IdentityRole
{
    [JsonIgnore]
    public ICollection<ApplicationUser> Users { get; set; } = [];
}
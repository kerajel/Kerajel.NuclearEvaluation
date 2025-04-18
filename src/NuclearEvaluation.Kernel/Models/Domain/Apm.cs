﻿using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace NuclearEvaluation.Kernel.Models.Domain;

[Index(nameof(SubSampleId))]
public class Apm
{
    [Key]
    public int Id { get; set; }

    public int SubSampleId { get; set; }

    public SubSample SubSample { get; set; } = null!;

    [Precision(38, 15)]
    public decimal? U234 { get; set; }

    [Precision(38, 15)]
    public decimal? ErU234 { get; set; }

    [Precision(38, 15)]
    public decimal? U235 { get; set; }

    [Precision(38, 15)]
    public decimal? ErU235 { get; set; }

    [Precision(38, 15)]
    public decimal? U236 { get; set; }

    [Precision(38, 15)]
    public decimal? ErU236 { get; set; }

    [Precision(38, 15)]
    public decimal? U238 { get; set; }

    [Precision(38, 15)]
    public decimal? ErU238 { get; set; }

    [StringLength(4000)]
    public string Comment { get; set; } = string.Empty;
}
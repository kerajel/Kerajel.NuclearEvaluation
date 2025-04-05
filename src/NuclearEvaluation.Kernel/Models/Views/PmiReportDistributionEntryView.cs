using NuclearEvaluation.Kernel.Enums;
using NuclearEvaluation.Kernel.Models.DataManagement.PMI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuclearEvaluation.Kernel.Models.Views;

public class PmiReportDistributionEntryView
{
    public int Id { get; set; }

    public Guid PmiReportId { get; set; }

    public PmiReportView PmiReport { get; set; } = null!;

    public PmiReportDistributionChannel DistributionChannel { get; set; }

    public PmiReportDistributionStatus DistributionStatus { get; set; }
}
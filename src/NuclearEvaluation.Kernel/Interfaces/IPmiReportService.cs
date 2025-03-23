using NuclearEvaluation.Kernel.Models.DataManagement.PMI;

namespace NuclearEvaluation.Kernel.Interfaces;

public interface IPmiReportService
{
    Task Insert(PmiReport pmiReport);
}
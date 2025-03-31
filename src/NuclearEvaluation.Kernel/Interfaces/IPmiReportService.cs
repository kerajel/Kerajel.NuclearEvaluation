using Kerajel.Primitives.Models;
using NuclearEvaluation.Kernel.Models.DataManagement.PMI;

namespace NuclearEvaluation.Kernel.Interfaces;

public interface IPmiReportService
{
    Task<OperationResult<PmiReport>> Create(PmiReportSubmission reportSubmission);
}
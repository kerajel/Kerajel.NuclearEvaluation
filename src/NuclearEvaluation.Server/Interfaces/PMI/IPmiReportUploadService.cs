using Kerajel.Primitives.Models;
using NuclearEvaluation.Kernel.Models.DataManagement.PMI;

namespace NuclearEvaluation.Server.Interfaces.PMI
{
    public interface IPmiReportUploadService
    {
        Task<OperationResult<PmiReport>> Upload(PmiReportSubmission reportSubmission, CancellationToken ct = default);
    }
}
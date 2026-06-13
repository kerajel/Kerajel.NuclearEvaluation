using Kerajel.Primitives.Models;
using NuclearEvaluation.Kernel.Models.DataManagement.PMI;

namespace NuclearEvaluation.Server.Interfaces.PMI;

public interface IPmiReportUploadService
{
    Task<OperationResult<PmiReport>> Upload(string reportName, DateOnly reportDate, string fileName, Stream content, CancellationToken ct = default);
}

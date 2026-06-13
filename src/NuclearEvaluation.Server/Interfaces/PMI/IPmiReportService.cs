using Kerajel.Primitives.Models;
using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Kernel.Models.DataManagement.PMI;
using NuclearEvaluation.Shared.Models.Views;

namespace NuclearEvaluation.Server.Interfaces.PMI;

public interface IPmiReportService
{
    Task<OperationResult<PmiReport>> Create(string reportName, DateOnly reportDate, string fileName, long fileSize, CancellationToken ct);
    Task<OperationResult> Delete(Guid pmiReportId, CancellationToken ct);
    Task<FetchDataResult<PmiReportView>> GetPmiReportViews(FetchDataCommand<PmiReportView> command, CancellationToken ct = default);
    Task<bool> IsNameAvailable(string reportName, CancellationToken ct = default);
}

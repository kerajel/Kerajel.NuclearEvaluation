using Kerajel.Primitives.Enums;
using Kerajel.Primitives.Models;
using Kerajel.TabularDataReader.Services;
using NuclearEvaluation.Library.Interfaces;
using Polly;
using Polly.Bulkhead;

namespace NuclearEvaluation.Server.Services
{
    public class StemPreviewService : IStemPreviewService
    {
        private static readonly AsyncBulkheadPolicy<OperationResult> bulkheadPolicy = Policy
            .BulkheadAsync<OperationResult>(
                maxParallelization: 4,
                maxQueuingActions: 128,
                onBulkheadRejectedAsync: async context =>
                {
                    await Task.CompletedTask;
                });

        public StemPreviewService()
        {
        }

        public async Task<OperationResult> UploadStemPreviewFile(Stream stream, string fileName)
        {
            CancellationTokenSource cts = new(TimeSpan.FromMinutes(1));

            try
            {
                OperationResult result = await bulkheadPolicy.ExecuteAsync(
                    async (CancellationToken ct) =>
                    {
                        try
                        {
                            OperationResult<string> operationResult = await TabularDataReader.Read(stream, fileName);
                            if (!operationResult.Succeeded)
                            {
                                return new OperationResult(OperationStatus.Faulted, "Error reading the file");
                            }
                            return new OperationResult(OperationStatus.Succeeded);
                        }
                        catch (Exception)
                        {
                            return new OperationResult(OperationStatus.Faulted, "Error processing the file");
                        }
                    },
                    cts.Token);
                return result;
            }
            catch (BulkheadRejectedException)
            {
                return new OperationResult(OperationStatus.Faulted, "Too many concurrent uploads. Please try again later.");
            }
            catch (OperationCanceledException)
            {
                return new OperationResult(OperationStatus.Faulted, "Upload timed out waiting for a slot.");
            }
        }
    }
}
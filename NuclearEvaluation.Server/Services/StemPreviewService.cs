using Kerajel.Primitives.Enums;
using Kerajel.Primitives.Models;
using Kerajel.TabularDataReader.Services;
using NuclearEvaluation.Library.Interfaces;

namespace NuclearEvaluation.Server.Services;

public class StemPreviewService : IStemPreviewService
{
    public StemPreviewService()
    {

    }

    public async Task<OperationResult> UploadStemPreviewFile(Stream stream, string fileName)
    {
        try
        {
            OperationResult<string> operationResult = await TabularDataReader.Read(stream, fileName);
            if (!operationResult.Succeeded)
            {
                return new(OperationStatus.Faulted, "Error reading the file");
            }

            return new OperationResult(OperationStatus.Succeeded);
        }
        catch
        {
            return new OperationResult(OperationStatus.Faulted, "Error processing the file");
        }
    }
}

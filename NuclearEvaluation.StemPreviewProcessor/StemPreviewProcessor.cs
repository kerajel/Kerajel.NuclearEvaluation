using Kerajel.Primitives.Enums;
using Kerajel.Primitives.Models;
using Kerajel.TabularDataReader.Services;
using NuclearEvaluation.Kernel.Interfaces;
using NuclearEvaluation.Kernel.Models.Files;
using NuclearEvaluation.Kernel.Models.Messaging.StemPreview;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuclearEvaluation.StemPreviewProcessor;

public class StemPreviewProcessor
{
    private readonly IEfsFileService _fileService;
    private readonly ILogger<StemPreviewProcessor> _logger;

    public StemPreviewProcessor(IEfsFileService fileService, ILogger<StemPreviewProcessor> logger)
    {
        _fileService = fileService;
        _logger = logger;
    }

    public async Task<OperationResult> Process(ProcessStemPreviewMessage message, CancellationToken ct)
    {
        OperationResult<GetFilePathResponse> getFilePathResult = await _fileService.GetPath(message.FileId, ct);

        if (!getFilePathResult.Succeeded)
        {
            return OperationResult.FromFaulted(getFilePathResult);
        }

        string filePath = getFilePathResult.Content!.FilePath;

        bool isFileSupported = TabularDataReader.IsSupported(
            filePath,
            out bool isSupportedPlainText,
            out bool isSupportedSpreadSheet);

        if (!isFileSupported)
        {
            return OperationResult.Faulted($"Extension {filePath} is not supported");
        }

        if (isSupportedSpreadSheet)
        {
           string filePath
        }

        if ()
        {
            
        }
    }
}

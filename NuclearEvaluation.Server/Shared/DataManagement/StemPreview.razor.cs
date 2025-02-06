using Kerajel.Primitives.Enums;
using Kerajel.Primitives.Models;
using Kerajel.TabularDataReader.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using NuclearEvaluation.Library.Enums;
using NuclearEvaluation.Library.Extensions;
using NuclearEvaluation.Library.Interfaces;
using NuclearEvaluation.Server.Models.Upload;
using System.Diagnostics;

namespace NuclearEvaluation.Server.Shared.DataManagement;

public partial class StemPreview
{
    [Inject]
    protected IJSRuntime JsRuntime { get; set; } = null!;

    [Inject]
    protected IStemPreviewService StemPreviewService { get; set; } = null!;

    [Inject]
    protected ILogger<StemPreview> Logger { get; set; } = null!;

    //TODO options
    const long fileSizeLimit = 1024L * 1024L * 50L; // 50 MB limit

    InputFile fileInput = null!;

    List<UploadedFile> selectedFiles = [];

    async Task OnInputFileChange(InputFileChangeEventArgs e)
    {
        List<IBrowserFile> newlySelectedFiles = [.. e.GetMultipleFiles()];

        selectedFiles = selectedFiles.Where(x => x.Status != UploadStatus.Pending).ToList();

        foreach (IBrowserFile file in newlySelectedFiles)
        {
            if (file.Size <= fileSizeLimit)
            {
                UploadedFile newFile = new()
                {
                    BrowserFile = file,
                    Status = UploadStatus.Pending,
                };
                selectedFiles.Add(newFile);
            }
            else
            {
                UploadedFile newFile = new()
                {
                    BrowserFile = file,
                    Status = UploadStatus.UploadError,
                    ErrorMessage = $"Size exceeds {fileSizeLimit.AsMegabytes():F2} mb",
                };
                selectedFiles.Add(newFile);
            }
        }

        selectedFiles = [.. selectedFiles];
        await InvokeAsync(StateHasChanged);
        await Task.Yield();
    }

    private async Task ProcessUpload()
    {
        UploadedFile[] pendingFiles = selectedFiles
            .Where((UploadedFile f) => f.Status == UploadStatus.Pending)
            .ToArray();

        Stopwatch sw = Stopwatch.StartNew();

        foreach (UploadedFile file in pendingFiles)
        {
            IBrowserFile browserFile = file.BrowserFile;
            file.Status = UploadStatus.Uploading;

            Dictionary<string, object> fileLoggingParameters = new()
             {
                  { "FileName", browserFile.Name },
                  { "FileSize", browserFile.Size }
             };

            using IDisposable? fileScope = Logger.BeginScope(fileLoggingParameters);
            Logger.LogInformation("Uploading STEM preview file");

            await InvokeAsync(StateHasChanged);
            await Task.Yield();

            using Stream stream = browserFile.OpenReadStream(browserFile.Size);

            OperationResult result = await StemPreviewService.UploadStemPreviewFile(stream, browserFile.Name);

            if (result.Succeeded)
            {
                file.Status = UploadStatus.Uploaded;
            }
            else
            {
                file.Status = UploadStatus.UploadError;
                file.ErrorMessage = result.ErrorMessage;
            }

            await InvokeAsync(StateHasChanged);
            await Task.Yield();

            Logger.LogInformation("Upload completed for STEM preview file");
        }

        Logger.LogInformation("{swElapsed}", sw.Elapsed.TotalSeconds);
    }

    private async Task RemoveFile(UploadedFile file)
    {
        selectedFiles.Remove(file);
        selectedFiles = [.. selectedFiles];

        await InvokeAsync(StateHasChanged);
        await Task.Yield();
    }

    private async Task TriggerFileInputClick()
    {
        await JsRuntime.InvokeVoidAsync("clickElement", fileInput.Element);
    }
}
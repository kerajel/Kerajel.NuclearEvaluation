using Kerajel.Primitives.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using NuclearEvaluation.Library.Enums;
using NuclearEvaluation.Library.Extensions;
using NuclearEvaluation.Library.Interfaces;
using NuclearEvaluation.Server.Models.Upload;
using NuclearEvaluation.Server.Services;
using NuclearEvaluation.Server.Shared.Grids;
using Radzen;
using System.Collections.Generic;
using System.Diagnostics;

namespace NuclearEvaluation.Server.Shared.DataManagement;

public partial class StemPreview
{

    [Parameter]
    public string ComponentId { get; set; } = Guid.NewGuid().ToString();


    [Inject]
    protected IJSRuntime JsRuntime { get; set; } = null!;

    [Inject]
    protected IStemPreviewService StemPreviewService { get; set; } = null!;

    [Inject]
    protected ILogger<StemPreview> Logger { get; set; } = null!;

    [Inject]
    protected ISessionCache SessionCache { get; set; } = null!;

    protected string FilesCacheKey => $"{ComponentId}_{nameof(Files)}";

    protected List<UploadedFile>? files;

    protected List<UploadedFile> Files
    {
        get
        {
            if (files != null)
            {
                return files;
            }
            bool hasFiles = SessionCache.TryGetValue(FilesCacheKey, out List<UploadedFile>? cachedFiles);
            return hasFiles ? cachedFiles : [];
        }
        set
        {
            files = value;
            var cachedFiles = value!.Where(x => x.Status == UploadStatus.Uploaded).ToList();
            SessionCache.Add(FilesCacheKey, cachedFiles);
        }
    }

    //TODO options
    const long fileSizeLimit = 1024L * 1024L * 50L; // 50 MB limit

    InputFile fileInput = null!;



    async Task OnInputFileChange(InputFileChangeEventArgs e)
    {
        List<IBrowserFile> newlySelectedFiles = [.. e.GetMultipleFiles()];

        Files = Files.Where(x => x.Status != UploadStatus.Pending).ToList();

        foreach (IBrowserFile file in newlySelectedFiles)
        {
            if (file.Size <= fileSizeLimit)
            {
                UploadedFile newFile = new()
                {
                    BrowserFile = file,
                    Status = UploadStatus.Pending,
                };
                Files.Add(newFile);
            }
            else
            {
                UploadedFile newFile = new()
                {
                    BrowserFile = file,
                    Status = UploadStatus.UploadError,
                    ErrorMessage = $"Size exceeds {fileSizeLimit.AsMegabytes():F2} mb",
                };
                Files.Add(newFile);
            }
        }

        Files = [.. Files];
        await InvokeAsync(StateHasChanged);
        await Task.Yield();
    }

    private async Task ProcessUpload()
    {
        UploadedFile[] pendingFiles = Files
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

        Files = [.. Files];
        Logger.LogInformation("{swElapsed}", sw.Elapsed.TotalSeconds);
    }

    private async Task RemoveFile(UploadedFile file)
    {
        Files.Remove(file);
        Files = [.. Files];

        await InvokeAsync(StateHasChanged);
        await Task.Yield();
    }

    private async Task TriggerFileInputClick()
    {
        await JsRuntime.InvokeVoidAsync("clickElement", fileInput.Element);
    }
}
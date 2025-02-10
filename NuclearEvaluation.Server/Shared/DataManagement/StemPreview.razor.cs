using Kerajel.Primitives.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;
using NuclearEvaluation.Library.Enums;
using NuclearEvaluation.Library.Extensions;
using NuclearEvaluation.Library.Interfaces;
using NuclearEvaluation.Server.Models.Upload;
using Radzen;

namespace NuclearEvaluation.Server.Shared.DataManagement;

public partial class StemPreview : IDisposable
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

    [Inject]
    protected DialogService DialogService { get; set; } = null!;

    private List<UploadedFile> files = [];

    private readonly CancellationTokenSource cts = new();

    // 50 MB limit
    private const long fileSizeLimit = 1024L * 1024L * 50L;

    private InputFile fileInput = null!;

    private async Task HandleBeforeInternalNavigation(LocationChangingContext context)
    {
        bool hasInProgressOrUploaded = files.Any(
            x => x.Status == UploadStatus.Uploading
                || x.Status == UploadStatus.Uploaded
        );

        if (hasInProgressOrUploaded)
        {
            bool? userConfirm = await DialogService.Confirm(
                "You have a current STEM preview that would be lost if you leave. Are you sure?",
                  "Confirm navigation",
                  new ConfirmOptions()
                  {
                      OkButtonText = "Yes",
                      CancelButtonText = "No",
                  }
            );

            if (userConfirm == false)
            {
                context.PreventNavigation();
            }
        }
    }

    private async Task OnInputFileChange(InputFileChangeEventArgs e)
    {
        List<IBrowserFile> newlySelectedFiles = new(e.GetMultipleFiles());

        files = files.Where(x => x.Status != UploadStatus.Pending).ToList();

        foreach (IBrowserFile file in newlySelectedFiles)
        {
            if (file.Size <= fileSizeLimit)
            {
                UploadedFile newFile = new()
                {
                    BrowserFile = file,
                    Status = UploadStatus.Pending,
                };
                files.Add(newFile);
            }
            else
            {
                UploadedFile newFile = new()
                {
                    BrowserFile = file,
                    Status = UploadStatus.UploadError,
                    ErrorMessage = $"Size exceeds {fileSizeLimit.AsMegabytes():F2} mb",
                };
                files.Add(newFile);
            }
        }

        files = new List<UploadedFile>(files);
        await InvokeAsync(StateHasChanged);
        await Task.Yield();
    }

    private async Task ProcessUpload()
    {
        UploadedFile[] pendingFiles = files
            .Where((UploadedFile f) => f.Status == UploadStatus.Pending)
            .ToArray();

        foreach (UploadedFile file in pendingFiles)
        {
            IBrowserFile browserFile = file.BrowserFile;
            file.Status = UploadStatus.Uploading;

            Dictionary<string, object> fileLoggingParameters = new Dictionary<string, object>()
            {
                  { "FileName", browserFile.Name },
                  { "FileSize", browserFile.Size },
            };

            using IDisposable? fileScope = Logger.BeginScope(fileLoggingParameters);
            Logger.LogInformation("Uploading STEM preview file");

            await InvokeAsync(StateHasChanged);
            await Task.Yield();

            using Stream stream = browserFile.OpenReadStream(browserFile.Size);

            OperationResult result = await StemPreviewService.UploadStemPreviewFile(
                  stream,
                  browserFile.Name,
                  cts.Token
            );

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

        files = [.. files];
    }

    private async Task RemoveFile(UploadedFile file)
    {
        files.Remove(file);
        files = new List<UploadedFile>(files);

        await InvokeAsync(StateHasChanged);
        await Task.Yield();
    }

    private async Task TriggerFileInputClick()
    {
        await JsRuntime.InvokeVoidAsync("clickElement", fileInput.Element);
    }

    public void Dispose()
    {
        cts.Cancel();
        cts.Dispose();
        GC.SuppressFinalize(this);
    }
}
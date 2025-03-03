using Kerajel.Primitives.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;
using NuclearEvaluation.Library.Enums;
using NuclearEvaluation.Library.Extensions;
using NuclearEvaluation.Library.Interfaces;
using NuclearEvaluation.Server.Models.Upload;
using NuclearEvaluation.Server.Services;
using NuclearEvaluation.Server.Shared.Grids;
using NuclearEvaluation.Server.Shared.Misc;
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

    protected readonly Guid sessionId = Guid.NewGuid();
    protected StemPreviewEntryGrid stemPreviewEntryGrid = null!;

    List<UploadedFile> files = [];

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
                  Dialogs.YesNoConfirmOptions
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

            await InvokeAsync(StateHasChanged);
            await Task.Yield();

            await InvokeAsync(async () =>
            {
                try
                {
                    using Stream stream = browserFile.OpenReadStream(browserFile.Size);
                    OperationResult result = await StemPreviewService.UploadStemPreviewFile(
                          sessionId,
                          stream,
                          file.Id,
                          browserFile.Name,
                          file.FileCancellationTokenSource.Token
                    );
                    file.Status = result.Succeeded ? UploadStatus.Uploaded : UploadStatus.UploadError;

                    await InvokeAsync(StateHasChanged);
                    await Task.Yield();

                    if (!result.Succeeded)
                    {
                        file.ErrorMessage = result.ErrorMessage;
                    }
                    else
                    {
                        await stemPreviewEntryGrid.Refresh();
                    }
                }
                catch (Exception ex)
                {
                    file.Status = UploadStatus.UploadError;
                    file.ErrorMessage = ex.Message;
                }
                finally
                {
                    await InvokeAsync(StateHasChanged);
                    await Task.Yield();
                }
            });

            _ = await StemPreviewService.RefreshIndexes(sessionId);
        }
    }

    private async Task RemoveFile(UploadedFile file)
    {
        if (file.Status == UploadStatus.Uploaded || file.Status == UploadStatus.Uploading)
        {
            bool? confirmDelete = await DialogService.Confirm(
                $"Are you sure you want to delete '{file.BrowserFile.Name}'?",
                "Confirm Delete",
                Dialogs.YesNoConfirmOptions
            );

            if (confirmDelete != true)
            {
                return;
            }
        }

        if (file.Status == UploadStatus.Uploading)
        {
            await file.FileCancellationTokenSource.CancelAsync();
        }

        files.Remove(file);
        files = new List<UploadedFile>(files);

        await StemPreviewService.DeleteFileData(sessionId, file.Id);
        await stemPreviewEntryGrid.Refresh();

        await InvokeAsync(StateHasChanged);
        await Task.Yield();
    }

    private async Task TriggerFileInputClick()
    {
        await JsRuntime.InvokeVoidAsync("clickElement", fileInput.Element);
    }

    public void Dispose()
    {
        StemPreviewService.Dispose();
        GC.SuppressFinalize(this);
    }
}
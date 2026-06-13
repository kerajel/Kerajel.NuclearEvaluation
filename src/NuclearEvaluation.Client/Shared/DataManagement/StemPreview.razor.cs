using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;
using NuclearEvaluation.Client.Models;
using NuclearEvaluation.Client.Shared.Grids;
using NuclearEvaluation.Client.Shared.Misc;
using NuclearEvaluation.Shared;
using NuclearEvaluation.Shared.Contracts;
using NuclearEvaluation.Shared.Enums;
using Radzen;

namespace NuclearEvaluation.Client.Shared.DataManagement;

public partial class StemPreview
{
    [Parameter]
    public string ComponentId { get; set; } = Guid.NewGuid().ToString();

    [Inject]
    protected INuclearEvaluationApi Api { get; set; } = null!;

    [Inject]
    protected ILogger<StemPreview> Logger { get; set; } = null!;

    [Inject]
    protected DialogService DialogService { get; set; } = null!;

    protected readonly Guid sessionId = Guid.NewGuid();
    protected StemPreviewEntryGrid stemPreviewEntryGrid = null!;

    List<UploadedFile> files = [];

    static readonly HashSet<FileStatus> inProgressStatuses = [FileStatus.Pending, FileStatus.Uploading];

    InputFile fileInput = null!;
    int currentUploadBatchId = 0;

    static readonly long maxPreviewFileSize = UploadLimits.MaxStemPreviewFileSizeBytes;

    private async Task HandleBeforeInternalNavigation(LocationChangingContext context)
    {
        bool hasInProgressFiles = files.Any(
            x => x.Status == FileStatus.Uploading
                || x.Status == FileStatus.Uploaded);

        if (hasInProgressFiles)
        {
            bool? userConfirm = await DialogService.Confirm(
                  "You have a current STEM preview that would be lost if you leave. Are you sure?",
                  "Confirm navigation",
                  Dialogs.YesNoConfirmOptions);

            if (userConfirm == false)
            {
                context.PreventNavigation();
            }
        }
    }

    private async Task OnInputFileChange(InputFileChangeEventArgs e)
    {
        List<IBrowserFile> newlySelectedFiles = new(e.GetMultipleFiles(maximumFileCount: 100));

        files = files.Where(x => x.Status != FileStatus.Pending).ToList();

        foreach (IBrowserFile file in newlySelectedFiles)
        {
            if (file.Size <= maxPreviewFileSize)
            {
                files.Add(new UploadedFile
                {
                    BrowserFile = file,
                    Status = FileStatus.Pending,
                });
            }
            else
            {
                files.Add(new UploadedFile
                {
                    BrowserFile = file,
                    Status = FileStatus.UploadError,
                    ErrorMessage = $"Size exceeds {maxPreviewFileSize / (1024 * 1024)} MB",
                });
            }
        }

        files = new(files);

        await InvokeAsync(StateHasChanged);
        await Task.Yield();
    }

    private async Task ProcessUpload()
    {
        currentUploadBatchId++;

        UploadedFile[] pendingFiles = files
              .Where(f => f.Status == FileStatus.Pending)
              .ToArray();

        foreach (UploadedFile file in pendingFiles)
        {
            file.UploadBatchId = currentUploadBatchId;
        }

        foreach (UploadedFile file in pendingFiles)
        {
            if (file.Status != FileStatus.Pending)
            {
                continue;
            }

            using IDisposable? scope = Logger.BeginScope("Processing file {fileId} on STEM session {stemSessionId}", file.Id, sessionId);

            Logger.LogInformation("Processing started");

            IBrowserFile browserFile = file.BrowserFile;
            file.Status = FileStatus.Uploading;

            await InvokeAsync(StateHasChanged);
            await Task.Yield();

            try
            {
                await using Stream stream = browserFile.OpenReadStream(maxPreviewFileSize, file.FileCancellationTokenSource.Token);
                OperationOutcome result = await Api.UploadStemPreviewFile(
                      sessionId,
                      file.Id,
                      browserFile.Name,
                      stream,
                      file.FileCancellationTokenSource.Token);

                file.Status = result.IsSuccessful ? FileStatus.Uploaded : FileStatus.UploadError;
                if (!result.IsSuccessful)
                {
                    file.ErrorMessage = result.ErrorMessage;
                }
            }
            catch (Exception ex)
            {
                file.Status = FileStatus.UploadError;
                file.ErrorMessage = ex.Message;
            }
            finally
            {
                await InvokeAsync(StateHasChanged);
                await Task.Yield();
            }

            Logger.LogInformation("Processing complete");
        }

        await InvokeAsync(stemPreviewEntryGrid.Refresh);
    }

    private async Task RemoveFile(UploadedFile file)
    {
        FileStatus previousStatus = file.Status;

        if (previousStatus == FileStatus.Uploaded || previousStatus == FileStatus.Uploading)
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

        // Abort an in-flight (or queued) upload before its status changes, so the request
        // is cancelled rather than allowed to run to completion.
        if (previousStatus == FileStatus.Uploading || previousStatus == FileStatus.Pending)
        {
            await file.FileCancellationTokenSource.CancelAsync();
        }

        // Update the UI immediately rather than waiting for the server round-trip.
        file.Status = FileStatus.Removed;
        files.Remove(file);
        files = new(files);
        await InvokeAsync(StateHasChanged);
        await Task.Yield();

        // Best-effort server-side cleanup of any rows already staged for this file.
        try
        {
            await Api.DeleteStemPreviewFile(sessionId, file.Id);
            await stemPreviewEntryGrid.Refresh();
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to clean up STEM file {fileId} after removal.", file.Id);
        }
    }

    private bool ShowStemPreviewEntryGrid()
    {
        return files.Where(f => f.Status != FileStatus.Removed)
                .GroupBy(f => f.UploadBatchId)
                .Any(batch =>
                {
                    bool batchHasUploaded = batch.Any(f => f.Status == FileStatus.Uploaded);
                    bool batchHasInProgress = batch.Any(f => inProgressStatuses.Contains(f.Status));
                    return batchHasUploaded && !batchHasInProgress;
                });
    }
}

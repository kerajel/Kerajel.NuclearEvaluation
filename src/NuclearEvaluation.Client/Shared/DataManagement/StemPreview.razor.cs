using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Routing;
using NuclearEvaluation.Client.Models;
using NuclearEvaluation.Client.Shared.Grids;
using NuclearEvaluation.Client.Shared.Misc;
using NuclearEvaluation.Shared;
using NuclearEvaluation.Shared.Contracts;
using NuclearEvaluation.Shared.Enums;
using Radzen;

namespace NuclearEvaluation.Client.Shared.DataManagement;

public partial class StemPreview : IDisposable
{
    const string ExampleFileName = "stem-quality-example.csv";
    static readonly byte[] ExampleContent = Encoding.UTF8.GetBytes(
        """
        Identifier,LaboratoryCode,AnalysisDate,IsNu,U234,ErU234,U235,ErU235
        101,DEMO-A,2026-08-01,true,0,0.01,1.02,0.02
        101,DEMO-A,2026-08-02,true,1.01,,1.03,0.02
        102,DEMO-B,2026-08-03,false,,0.01,1.04,
        103,,2026-08-04,false,,,1.05,0.03
        104,DEMO-C,2026-08-05,true,1.02,0.02,,0.03
        """
    );

    [Parameter]
    public string ComponentId { get; set; } = Guid.NewGuid().ToString();

    [Parameter]
    public bool StartInQuality { get; set; }

    [Inject]
    protected INuclearEvaluationApi Api { get; set; } = null!;

    [Inject]
    protected ILogger<StemPreview> Logger { get; set; } = null!;

    [Inject]
    protected DialogService DialogService { get; set; } = null!;

    protected readonly Guid sessionId = Guid.NewGuid();
    StemPreviewEntryGrid? stemPreviewEntryGrid;

    List<UploadedFile> files = [];
    UploadedFile? exampleFile;
    bool showQuality;
    Guid? qualityFileId;
    StemFileProfile? qualityProfile;
    string? qualityError;
    CancellationTokenSource? profileCancellation;

    static readonly HashSet<FileStatus> inProgressStatuses =
    [
        FileStatus.Pending,
        FileStatus.Uploading,
    ];

    InputFile fileInput = null!;
    int currentUploadBatchId = 0;

    static readonly long maxPreviewFileSize = UploadLimits.MaxStemPreviewFileSizeBytes;

    protected override void OnInitialized() => showQuality = StartInQuality;

    private async Task HandleBeforeInternalNavigation(LocationChangingContext context)
    {
        bool hasInProgressFiles = files.Any(x =>
            x.Status == FileStatus.Uploading || x.Status == FileStatus.Uploaded
        );

        if (hasInProgressFiles)
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
        List<IBrowserFile> newlySelectedFiles = new(e.GetMultipleFiles(maximumFileCount: 100));

        files = files.Where(x => x.Status != FileStatus.Pending).ToList();

        foreach (IBrowserFile file in newlySelectedFiles)
        {
            bool exceedsLimit = file.Size > maxPreviewFileSize;
            files.Add(
                new UploadedFile
                {
                    BrowserFile = file,
                    Name = file.Name,
                    Size = file.Size,
                    Status = exceedsLimit ? FileStatus.UploadError : FileStatus.Pending,
                    ErrorMessage = exceedsLimit
                        ? $"Size exceeds {maxPreviewFileSize / (1024 * 1024)} MB"
                        : null,
                }
            );
        }

        files = new(files);

        await InvokeAsync(StateHasChanged);
        await Task.Yield();
    }

    private async Task ProcessUpload()
    {
        currentUploadBatchId++;

        UploadedFile[] pendingFiles = files.Where(f => f.Status == FileStatus.Pending).ToArray();

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

            await UploadFile(file, ct => file.BrowserFile!.OpenReadStream(maxPreviewFileSize, ct));
        }

        if (showQuality && qualityFileId is null)
        {
            await SelectQualityFile(FirstUploadedFileId());
        }
        else if (!showQuality && stemPreviewEntryGrid is not null)
        {
            await InvokeAsync(stemPreviewEntryGrid.Refresh);
        }
    }

    bool IsExampleStaged => exampleFile is { Status: FileStatus.Uploading or FileStatus.Uploaded };

    async Task LoadExample()
    {
        if (IsExampleStaged)
        {
            return;
        }

        if (exampleFile is not null)
        {
            files.Remove(exampleFile);
        }
        UploadedFile file = new()
        {
            Name = ExampleFileName,
            Size = ExampleContent.LongLength,
            UploadBatchId = ++currentUploadBatchId,
        };
        exampleFile = file;
        files = [.. files, file];
        await UploadFile(file, _ => new MemoryStream(ExampleContent, writable: false));
        if (showQuality && file.Status == FileStatus.Uploaded)
        {
            await SelectQualityFile(file.Id);
        }
        else if (!showQuality && stemPreviewEntryGrid is not null)
        {
            await InvokeAsync(stemPreviewEntryGrid.Refresh);
        }
    }

    async Task UploadFile(UploadedFile file, Func<CancellationToken, Stream> openStream)
    {
        file.Status = FileStatus.Uploading;
        await InvokeAsync(StateHasChanged);
        await Task.Yield();

        CancellationToken ct = file.FileCancellationTokenSource.Token;
        try
        {
            await using Stream stream = openStream(ct);
            OperationOutcome result = await Api.UploadStemPreviewFile(
                sessionId,
                file.Id,
                file.Name,
                stream,
                ct
            );
            if (file.Status != FileStatus.Removed)
            {
                file.Status = result.IsSuccessful ? FileStatus.Uploaded : FileStatus.UploadError;
                file.ErrorMessage = result.ErrorMessage;
            }
        }
        catch (Exception ex)
        {
            if (file.Status != FileStatus.Removed)
            {
                file.Status = FileStatus.UploadError;
                file.ErrorMessage = ex.Message;
            }
        }
        finally
        {
            await InvokeAsync(StateHasChanged);
            await Task.Yield();
        }
    }

    Guid? FirstUploadedFileId() =>
        files.FirstOrDefault(file => file.Status == FileStatus.Uploaded)?.Id;

    Task ToggleQuality()
    {
        showQuality = !showQuality;
        return SelectQualityFile(showQuality ? FirstUploadedFileId() : null);
    }

    Task OnQualityFileChange(ChangeEventArgs args) =>
        SelectQualityFile(Guid.TryParse(args.Value?.ToString(), out Guid fileId) ? fileId : null);

    async Task SelectQualityFile(Guid? fileId)
    {
        profileCancellation?.Cancel();
        profileCancellation = null;
        qualityFileId = fileId;
        qualityProfile = null;
        qualityError = null;
        if (fileId is null)
        {
            return;
        }

        using CancellationTokenSource cancellation = new();
        profileCancellation = cancellation;
        try
        {
            StemFileProfile? profile = await Api.GetStemFileProfile(
                sessionId,
                fileId.Value,
                cancellation.Token
            );
            if (profileCancellation == cancellation)
            {
                qualityProfile = profile;
                qualityError = profile is null ? "This upload is no longer available." : null;
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (profileCancellation == cancellation)
            {
                Logger.LogWarning(ex, "Could not load the STEM quality profile.");
                qualityError = "Could not load the quality profile. Please try again.";
            }
        }
        finally
        {
            if (profileCancellation == cancellation)
            {
                profileCancellation = null;
            }
        }
    }

    private async Task RemoveFile(UploadedFile file)
    {
        FileStatus previousStatus = file.Status;

        if (previousStatus == FileStatus.Uploaded || previousStatus == FileStatus.Uploading)
        {
            bool? confirmDelete = await DialogService.Confirm(
                $"Are you sure you want to delete '{file.Name}'?",
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

        _ = CleanupRemovedFileAsync(file);
        if (qualityFileId == file.Id)
        {
            await SelectQualityFile(FirstUploadedFileId());
        }
    }

    async Task CleanupRemovedFileAsync(UploadedFile file)
    {
        // Best-effort server-side cleanup of any rows already staged for this file.
        try
        {
            await Api.DeleteStemPreviewFile(sessionId, file.Id);
            if (!showQuality && stemPreviewEntryGrid is not null)
            {
                await InvokeAsync(stemPreviewEntryGrid.Refresh);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to clean up STEM file {fileId} after removal.", file.Id);
        }
    }

    private bool ShowStemPreviewEntryGrid()
    {
        return files
            .Where(f => f.Status != FileStatus.Removed)
            .GroupBy(f => f.UploadBatchId)
            .Any(batch =>
            {
                bool batchHasUploaded = batch.Any(f => f.Status == FileStatus.Uploaded);
                bool batchHasInProgress = batch.Any(f => inProgressStatuses.Contains(f.Status));
                return batchHasUploaded && !batchHasInProgress;
            });
    }

    public void Dispose()
    {
        profileCancellation?.Cancel();
        profileCancellation = null;
    }
}

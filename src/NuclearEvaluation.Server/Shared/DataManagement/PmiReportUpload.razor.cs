using Kerajel.Primitives.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using NuclearEvaluation.Kernel.Extensions;
using NuclearEvaluation.Kernel.Interfaces;
using NuclearEvaluation.Kernel.Models.DataManagement.PMI;
using NuclearEvaluation.Server.Shared.Generics;
using NuclearEvaluation.Shared.Validators;
using Radzen;

namespace NuclearEvaluation.Server.Shared.DataManagement;

public partial class PmiReportUpload : ComponentBase
{
    private const long MaxFileSize = 50L * 1024L * 1024L;

    [Inject]
    public ILogger<PmiReportUpload> Logger { get; set; } = null!;

    [Inject]
    protected IJSRuntime JsRuntime { get; set; } = null!;

    [Inject]
    private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = null!;

    [Inject]
    protected PmiReportSubmissionValidator PmiReportSubmissionValidator { get; set; } = null!;

    [Inject]
    protected IPmiReportService PmiReportService { get; set; } = null!;

    [Inject]
    public DialogService DialogService { get; set; } = null!;
    protected DateOnly? ReportDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    protected IBrowserFile? SelectedFile { get; set; }
    protected string? Message { get; set; }
    protected string MessageStyle { get; set; } = "margin-top: 10px;";
    protected bool IsFormValid { get; set; }
    protected InputFile? fileInput;
    protected ValidatedDateOnlyPicker<PmiReportSubmission> reportDatePicker = null!;

    protected PmiReportSubmission reportSubmission = new()
    {
        ReportDate = DateOnly.FromDateTime(DateTime.UtcNow),
    };

    protected override async Task OnInitializedAsync()
    {
        base.OnInitialized();
        IsFormValid = false;
        reportSubmission.AuthorId = await AuthenticationStateProvider.GetCurrentUserId();
    }

    protected async void TriggerFileInputClick()
    {
        await JsRuntime.InvokeVoidAsync("clickElement", fileInput!.Element);
    }

    protected async void OnFileChange(InputFileChangeEventArgs e)
    {
        IBrowserFile file = e.File;
        SelectedFile = null;
        Message = string.Empty;
        IsFormValid = false;
        reportSubmission.ReportName = Path.GetFileName(e.File.Name);
        if (file is null)
        {
            return;
        }
        if (Path.GetExtension(file.Name) != ".docx")
        {
            Message = "File must be a .docx document.";
            return;
        }
        if (file.Size > MaxFileSize)
        {
            Message = "File size exceeds 50 MB limit.";
            return;
        }
        SelectedFile = file;
        await UpdateFormValidity();
    }

    protected async Task OnSubmit()
    {
        if (!IsFormValid)
        {
            return;
        }
        OperationResult<PmiReport> createReportResult = await PmiReportService.Create(reportSubmission);
        if (!createReportResult.IsSuccessful)
        {
            Message = "There was an error - dwarfs and leprecons are already underway to figure it out";
            MessageStyle = "color: darkorange; font-weight: bold; margin-top: 10px;";
        }
        else
        {
            Message = $"{SelectedFile!.Name} has been submitted";
            MessageStyle = "margin-top: 10px;";
        }
        reportDatePicker.ReInitialize();
        SelectedFile = null;
        await InvokeAsync(StateHasChanged);
        await Task.Yield();
    }

    protected async Task OnReportNameValidationChanged()
    {
        await UpdateFormValidity();
    }

    private async Task UpdateFormValidity()
    {
        bool isReportDateValid = !reportDatePicker.HasValidationErrors;
        IsFormValid = ReportDate is not null
                       && SelectedFile is not null
                       && isReportDateValid;
        await InvokeAsync(StateHasChanged);
        await Task.Yield();
    }
}
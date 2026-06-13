using FluentValidation.Results;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using NuclearEvaluation.Client.Shared.Generics;
using NuclearEvaluation.Client.Validators;
using NuclearEvaluation.Shared;
using NuclearEvaluation.Shared.Contracts;
using Radzen;

namespace NuclearEvaluation.Client.Shared.DataManagement;

public partial class PmiReportUpload : ComponentBase
{
    private const long MaxFileSize = UploadLimits.MaxPmiReportFileSizeBytes;

    [Inject]
    protected ILogger<PmiReportUpload> Logger { get; set; } = null!;

    [Inject]
    protected IJSRuntime JsRuntime { get; set; } = null!;

    [Inject]
    protected PmiReportSubmissionValidator PmiReportSubmissionValidator { get; set; } = null!;

    [Inject]
    protected INuclearEvaluationApi Api { get; set; } = null!;

    [Inject]
    protected DialogService DialogService { get; set; } = null!;

    protected DateOnly? ReportDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    protected IBrowserFile? SelectedFile { get; set; }
    protected string? Message { get; set; }
    protected string MessageStyle { get; set; } = "margin-top: 10px;";

    protected bool IsFormValid { get; set; }
    protected InputFile? fileInput;
    protected ValidatedDateOnlyPicker<PmiReportSubmission> reportDatePicker = null!;
    protected ValidatedTextBox<PmiReportSubmission> reportNamePicker = null!;

    protected PmiReportSubmission reportSubmission = new()
    {
        ReportDate = DateOnly.FromDateTime(DateTime.UtcNow),
    };

    protected override async Task OnInitializedAsync()
    {
        IsFormValid = false;
        await UpdateFormValidity(true);
    }

    protected async void TriggerFileInputClick()
    {
        await JsRuntime.InvokeVoidAsync("clickElement", fileInput!.Element);
    }

    protected async void OnFileChange(InputFileChangeEventArgs e)
    {
        IBrowserFile file = e.File;

        if (file is null)
        {
            return;
        }

        SelectedFile = null;
        Message = string.Empty;
        IsFormValid = false;

        reportSubmission.ReportName = Path.GetFileNameWithoutExtension(e.File.Name);
        reportNamePicker.ReInitialize();
        reportDatePicker.ReInitialize();

        if (Path.GetExtension(file.Name) != UploadLimits.PmiReportExtension)
        {
            Message = "File must be a .docx document.";
            return;
        }
        if (file.Size > MaxFileSize)
        {
            Message = $"File size exceeds the {MaxFileSize / (1024 * 1024)} MB limit.";
            return;
        }
        SelectedFile = file;

        await UpdateFormValidity(true);
    }

    protected async Task OnSubmit()
    {
        if (!IsFormValid)
        {
            return;
        }

        using CancellationTokenSource cts = new(TimeSpan.FromMinutes(2));

        await using Stream stream = SelectedFile!.OpenReadStream(MaxFileSize, cts.Token);
        OperationOutcome result = await Api.UploadPmiReport(
            reportSubmission.ReportName,
            reportSubmission.ReportDate!.Value,
            SelectedFile.Name,
            stream,
            cts.Token);

        if (!result.IsSuccessful)
        {
            Message = result.ErrorMessage ?? "There was an error uploading the report.";
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

    protected async Task OnReportSubmissionChanged()
    {
        await UpdateFormValidity(false);
    }

    async Task UpdateFormValidity(bool validate = false)
    {
        await InvokeAsync(StateHasChanged);
        await Task.Yield();

        bool reportDateValid;
        bool reportNameValid;
        bool fileValid = SelectedFile is { };

        if (validate)
        {
            Task<ValidationResult> reportDateValidationTask = reportDatePicker.Validate();
            Task<ValidationResult> reportNameValidationTask = reportNamePicker.Validate();

            await Task.WhenAll(reportDateValidationTask, reportNameValidationTask);

            reportDateValid = reportDateValidationTask.Result.IsValid;
            reportNameValid = reportNameValidationTask.Result.IsValid;
        }
        else
        {
            reportDateValid = reportDatePicker.IsValid;
            reportNameValid = reportNamePicker.IsValid;
        }

        IsFormValid = reportDateValid && reportNameValid && fileValid;

        await InvokeAsync(StateHasChanged);
        await Task.Yield();
    }
}

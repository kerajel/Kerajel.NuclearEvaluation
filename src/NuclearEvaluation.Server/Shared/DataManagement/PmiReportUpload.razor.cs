using FluentValidation.Results;
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
    protected ValidatedTextBox<PmiReportSubmission> reportNamePicker = null!;

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

        if (file is null)
        {
            return;
        }

        SelectedFile = null;
        Message = string.Empty;
        IsFormValid = false;

        reportSubmission.ReportName = Path.GetFileNameWithoutExtension(e.File.Name);
        reportNamePicker.ReInitialize();

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

        await UpdateFormValidity(true);
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

        //TODO add loader

        reportDatePicker.ReInitialize();
        SelectedFile = null;
        await InvokeAsync(StateHasChanged);
        await Task.Yield();
    }

    protected async Task OnReportSubmissionChanged()
    {
        await UpdateFormValidity(false);
    }

    private async Task UpdateFormValidity(bool validate = false)
    {
        bool reportDateValid = false;
        bool reportNameValid = false;

        if (validate)
        {
            Task<ValidationResult> reportDateValidationTask = reportDatePicker.Validate();
            Task<ValidationResult> reportNameValidationTask = reportNamePicker.Validate();

            await Task.WhenAll(reportDateValidationTask, reportNameValidationTask);

            ValidationResult reportDateValidationResult = reportDateValidationTask.Result;
            ValidationResult reportNameValidationResult = reportNameValidationTask.Result;

            reportDateValid = !reportDateValidationResult.IsValid;
            reportNameValid = !reportNameValidationResult.IsValid;
        }
        else
        {
            reportDateValid = !reportDatePicker.IsValid;
            reportNameValid = !reportNamePicker.IsValid;
        }

        IsFormValid = reportDateValid && reportNameValid;

        await InvokeAsync(StateHasChanged);
        await Task.Yield();
    }
}
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using NuclearEvaluation.Kernel.Models.DataManagement.PMI;
using NuclearEvaluation.Server.Shared.Generics;
using NuclearEvaluation.Server.Validators;
using Radzen;

namespace NuclearEvaluation.Server.Shared.DataManagement;

public partial class PmiReportUpload : ComponentBase
{
    //TODO options
    private const long MaxFileSize = 50L * 1024L * 1024L; // 50 MB

    [Inject]
    public ILogger<PmiReportUpload> Logger { get; set; } = null!;

    [Inject]
    protected IJSRuntime JsRuntime { get; set; } = null!;

    [Inject]
    protected PmiReportSubmissionValidator PmiReportSubmissionValidator { get; set; } = null!;

    [Inject]
    public DialogService DialogService { get; set; } = null!;

    protected DateOnly? ReportDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    protected IBrowserFile? SelectedFile { get; set; }

    protected string? ValidationMessage { get; set; }

    protected bool IsFormValid { get; set; }

    protected InputFile? fileInput;
    protected ValidatedDateOnlyPicker<PmiReportSubmission> reportDatePicker = null!;
    protected PmiReportSubmission reportSubmission = new();

    protected override void OnInitialized()
    {
        base.OnInitialized();
        IsFormValid = false;
    }

    protected async void TriggerFileInputClick()
    {
        await JsRuntime.InvokeVoidAsync("clickElement", fileInput!.Element);
    }

    protected void OnFileChange(InputFileChangeEventArgs e)
    {
        IBrowserFile file = e.File;

        SelectedFile = null;
        ValidationMessage = string.Empty;
        IsFormValid = false;

        if (file is null)
        {
            return;
        }

        if (Path.GetExtension(file.Name) == ".docx")
        {
            ValidationMessage = "File must be a .docx document.";
            return;
        }

        if (file.Size > MaxFileSize)
        {
            ValidationMessage = "File size exceeds 50 MB limit.";
            return;
        }

        SelectedFile = file;

        UpdateFormValidity();
    }

    protected void OnSubmit()
    {
        ValidationMessage = string.Empty;

        if (ReportDate is null)
        {
            ValidationMessage = "Please select a report date.";
        }
        else if (SelectedFile is null)
        {
            ValidationMessage = "Please select a .docx file.";
        }

        UpdateFormValidity();

        if (!string.IsNullOrEmpty(ValidationMessage))
        {
            return;
        }

        Logger.LogInformation("PMI Report upload would happen here.");
    }

    private void UpdateFormValidity()
    {
        IsFormValid = (ReportDate is not null
                       && SelectedFile is not null
                       && string.IsNullOrEmpty(ValidationMessage));
    }
}
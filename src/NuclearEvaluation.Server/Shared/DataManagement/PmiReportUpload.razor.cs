using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using NuclearEvaluation.Kernel.Models.DataManagement.PMI;
using NuclearEvaluation.Server.Shared.Generics;
using NuclearEvaluation.Shared.Validators;
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

    protected string? Message { get; set; }

    protected bool IsFormValid { get; set; }

    protected InputFile? fileInput;
    protected ValidatedDateOnlyPicker<PmiReportSubmission> reportDatePicker = null!;
    protected PmiReportSubmission reportSubmission = new()
    {
        //TODO Pass DateTime provider to reflect browser local time
        ReportDate = DateOnly.FromDateTime(DateTime.UtcNow),
    };

    protected override void OnInitialized()
    {
        base.OnInitialized();
        IsFormValid = false;
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

        Logger.LogInformation("PMI Report upload would happen here.");

        Message = $"{SelectedFile!.Name} has been submitted";
        reportDatePicker.ReInitialize();
        SelectedFile = null;

        await InvokeAsync(StateHasChanged);
        await Task.Yield();
    }

    async Task OnReportNameValidationChanged()
    {
        await UpdateFormValidity();
    }

    private async Task UpdateFormValidity()
    {
        bool isReportDateValid = !reportDatePicker.HasValidationErrors;

        IsFormValid =     ReportDate is not null
                       && SelectedFile is not null
                       && isReportDateValid;

        await InvokeAsync(StateHasChanged);
        await Task.Yield();
    }
}
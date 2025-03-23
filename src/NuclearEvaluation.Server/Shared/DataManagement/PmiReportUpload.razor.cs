using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using NuclearEvaluation.Kernel.Enums;
using NuclearEvaluation.Kernel.Extensions;
using NuclearEvaluation.Kernel.Helpers;
using NuclearEvaluation.Kernel.Interfaces;
using NuclearEvaluation.Kernel.Models.DataManagement.PMI;
using NuclearEvaluation.Server.Shared.Generics;
using NuclearEvaluation.Shared.Validators;
using Radzen;
using System.Security.Claims;
using System.Transactions;

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

    protected bool IsFormValid { get; set; }

    protected InputFile? fileInput;
    protected ValidatedDateOnlyPicker<PmiReportSubmission> reportDatePicker = null!;
    protected PmiReportSubmission reportSubmission = new()
    {
        //TODO Pass DateTime provider to reflect browser local time
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

        PmiReport pmiReport = PreparePmiReport();

        await PmiReportService.Insert(pmiReport);

        Logger.LogInformation("PMI Report upload would happen here.");

        Message = $"{SelectedFile!.Name} has been submitted";
        reportDatePicker.ReInitialize();
        SelectedFile = null;

        await InvokeAsync(StateHasChanged);
        await Task.Yield();
    }

    private PmiReport PreparePmiReport()
    {
        PmiReport pmiReport = new()
        {
            //TODO add name picker
            Name = reportSubmission.ReportName,
            AuthorId = reportSubmission.AuthorId,
            CreatedDate = reportSubmission.ReportDate!.Value,
            Status = PmiReportStatus.DistributionPending,
        };

        foreach (PmiReportDistributionChannel channel in Enum.GetValues<PmiReportDistributionChannel>())
        {
            PmiReportDistributionEntry entry = new()
            {
                PmiReport = pmiReport,
                PmiReportDistributionChannel = channel,
                PmiReportDistributionStatus = PmiReportDistributionStatus.Pending,
            };
            pmiReport.PmiReportDistributionEntries.Add(entry);
        }

        return pmiReport;
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

    protected string GetCurrentUserId()
    {
        var authState = AuthenticationStateProvider.GetAuthenticationStateAsync().Result;
        var user = authState.User;
        if (user.Identity?.IsAuthenticated ?? false)
        {
            return user.FindFirst(c => c.Type.Equals(ClaimTypes.NameIdentifier))?.Value ?? string.Empty;
        }
        return string.Empty;
    }
}
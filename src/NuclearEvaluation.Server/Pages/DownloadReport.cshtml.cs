namespace NuclearEvaluation.Server.Pages;

using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

public class DownloadReportViewModel : PageModel
{
    public Guid ReportId { get; set; }

    public void OnGet(Guid id)
    {
        ReportId = id;
    }
}

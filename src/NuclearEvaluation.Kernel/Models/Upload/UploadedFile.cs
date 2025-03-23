using Microsoft.AspNetCore.Components.Forms;
using NuclearEvaluation.Kernel.Enums;

namespace NuclearEvaluation.Kernel.Models.Upload;

public class UploadedFile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int UploadBatchId { get; set; }
    public IBrowserFile BrowserFile { get; set; } = null!;
    public FileStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    public CancellationTokenSource FileCancellationTokenSource { get; set; } = new();
}
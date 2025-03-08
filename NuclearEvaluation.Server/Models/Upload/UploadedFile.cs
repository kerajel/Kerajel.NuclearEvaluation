using Microsoft.AspNetCore.Components.Forms;
using NuclearEvaluation.Library.Enums;

namespace NuclearEvaluation.Server.Models.Upload;

public class UploadedFile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public IBrowserFile BrowserFile { get; set; } = null!;
    public FileStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    public CancellationTokenSource FileCancellationTokenSource { get; set; } = new();
}
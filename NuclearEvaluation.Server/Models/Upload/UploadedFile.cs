using Microsoft.AspNetCore.Components.Forms;
using NuclearEvaluation.Library.Enums;

namespace NuclearEvaluation.Server.Models.Upload;

public class UploadedFile
{
    public int FileId { get; set; }
    public IBrowserFile BrowserFile { get; set; } = null!;
    public UploadStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
}
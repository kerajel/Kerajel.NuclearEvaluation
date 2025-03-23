using Microsoft.AspNetCore.Components;
using NuclearEvaluation.Kernel.Models.Upload;

namespace NuclearEvaluation.Server.Shared.Grids;

public partial class FileUploadGrid
{
    [Parameter]
    public IEnumerable<UploadedFile> Data { get; set; } = Enumerable.Empty<UploadedFile>();

    [Parameter]
    public EventCallback<UploadedFile> OnRemoveFile { get; set; }

    private async Task RemoveFile(UploadedFile file)
    {
        if (OnRemoveFile.HasDelegate)
        {
            await OnRemoveFile.InvokeAsync(file);
        }
    }
}
using Microsoft.AspNetCore.Components;
using NuclearEvaluation.Client.Models;

namespace NuclearEvaluation.Client.Shared.Grids;

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
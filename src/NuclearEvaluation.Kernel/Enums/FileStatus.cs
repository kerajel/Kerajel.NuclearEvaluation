namespace NuclearEvaluation.Kernel.Enums;

public enum FileStatus : byte
{
    Pending,
    Uploading,
    Uploaded,
    UploadError,
    Removed,
    Deleting,
}
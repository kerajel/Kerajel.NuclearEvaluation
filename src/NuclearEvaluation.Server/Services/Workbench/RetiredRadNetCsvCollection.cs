using System.Globalization;

namespace NuclearEvaluation.Server.Services.Workbench;

internal static class RetiredRadNetCsvCollection
{
    const string ExportHeader =
        "id,source_file_id,source_row,observed_at,status,exposure_mr_h,dose_nsv_h,r02,r03,r04,r05,"
        + "r06,r07,r08,r09,station_id,station_name,state,year,file_name";

    internal static void Remove(string contentRoot, ILogger logger)
    {
        DirectoryInfo directory = new(Path.Combine(contentRoot, "App_Data", "radnet-large-sample"));
        if (!directory.Exists)
            return;

        try
        {
            if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                logger.LogWarning("The retired RadNet CSV directory is a link.");
                return;
            }

            FileSystemInfo[] entries = directory.GetFileSystemInfos();
            if (entries.Any(entry => !IsGeneratedCsv(entry)))
            {
                logger.LogWarning("The retired RadNet CSV directory contains unexpected entries.");
                return;
            }

            foreach (FileSystemInfo entry in entries)
                entry.Delete();
            directory.Delete();
            logger.LogInformation("Removed {Count} retired RadNet CSV files.", entries.Length);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(error, "Could not remove the retired RadNet CSV collection.");
        }
    }

    static bool IsGeneratedCsv(FileSystemInfo entry)
    {
        if (
            entry is not FileInfo file
            || (file.Attributes & FileAttributes.ReparsePoint) != 0
            || file.Extension != ".csv"
            || !int.TryParse(
                Path.GetFileNameWithoutExtension(file.Name),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int sourceId
            )
            || sourceId <= 0
        )
            return false;

        using StreamReader reader = file.OpenText();
        return reader.ReadLine() == ExportHeader;
    }
}

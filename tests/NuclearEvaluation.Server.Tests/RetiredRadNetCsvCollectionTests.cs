using Microsoft.Extensions.Logging.Abstractions;
using NuclearEvaluation.Server.Services.Workbench;

namespace NuclearEvaluation.Server.Tests;

public sealed class RetiredRadNetCsvCollectionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RemoveOnlyDeletesTheGeneratedCollection(bool hasUnexpectedCsv)
    {
        DirectoryInfo root = Directory.CreateTempSubdirectory();
        try
        {
            DirectoryInfo appData = root.CreateSubdirectory("App_Data");
            DirectoryInfo collection = appData.CreateSubdirectory("radnet-large-sample");
            string snapshot = Path.Combine(appData.FullName, "radnet.duckdb");
            string annualSample = Path.Combine(appData.FullName, "radnet-import-sample.csv");
            File.WriteAllText(snapshot, "snapshot");
            File.WriteAllText(annualSample, "annual sample");
            string generated = Path.Combine(collection.FullName, "12025.csv");
            File.WriteAllText(
                generated,
                "id,source_file_id,source_row,observed_at,status,exposure_mr_h,dose_nsv_h,r02,r03,r04,r05,"
                    + "r06,r07,r08,r09,station_id,station_name,state,year,file_name\n"
            );
            string unexpected = Path.Combine(collection.FullName, "22025.csv");
            if (hasUnexpectedCsv)
                File.WriteAllText(unexpected, "different header\n");

            RetiredRadNetCsvCollection.Remove(root.FullName, NullLogger.Instance);

            Assert.Equal(hasUnexpectedCsv, Directory.Exists(collection.FullName));
            Assert.Equal(hasUnexpectedCsv, File.Exists(generated));
            Assert.Equal(hasUnexpectedCsv, File.Exists(unexpected));
            Assert.Equal("snapshot", File.ReadAllText(snapshot));
            Assert.Equal("annual sample", File.ReadAllText(annualSample));
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }
}

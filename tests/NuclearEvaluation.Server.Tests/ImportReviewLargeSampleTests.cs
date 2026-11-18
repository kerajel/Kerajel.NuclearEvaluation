using NuclearEvaluation.Server.Services.Workbench;
using NuclearEvaluation.Shared.Contracts;

namespace NuclearEvaluation.Server.Tests;

public sealed class ImportReviewLargeSampleTests
{
    const string Header =
        "id,source_file_id,source_row,observed_at,status,exposure_mr_h,dose_nsv_h,r02,r03,r04,r05,"
        + "r06,r07,r08,r09,station_id,station_name,state,year,file_name";

    [Fact]
    public async Task StagesSeparateCsvsAndQueriesFilteredPagesWithSourceFiles()
    {
        DirectoryInfo directory = Directory.CreateTempSubdirectory("ne-large-csv-test-");
        try
        {
            await using ImportReviewLargeSample sample = new(directory.FullName);
            CancellationToken ct = CancellationToken.None;
            Assert.False(sample.IsAvailable);
            Assert.Null(await sample.QueryAsync(new(), ct));
            Assert.Null(await sample.StageAsync(ct));

            string alpha = Path.Combine(directory.FullName, "12024.csv");
            await File.WriteAllLinesAsync(
                alpha,
                [
                    Header,
                    "1,12024,9,2024-01-01 00:00:00,APPROVED,0.2,,10,15,,,,,,,1,Alpha,CO,2024,alpha-2024.csv",
                    "2,12024,17,2024-01-01 01:00:00,APPROVED,0.3,,10,16,,,,,,,1,Alpha,CO,2024,alpha-2024.csv",
                    "4,12024,21,2024-01-01 02:00:00,APPROVED,0.05,,10,18,,,,,,,1,Alpha,CO,2024,alpha-2024.csv",
                ]
            );
            string beta = Path.Combine(directory.FullName, "22024.csv");
            await File.WriteAllLinesAsync(
                beta,
                [
                    Header,
                    "3,22024,4,2024-01-02 00:00:00,APPROVED,0.4,,10,17,,,,,,,2,Beta,AZ,2024,beta-2024.csv",
                ]
            );
            string empty = Path.Combine(directory.FullName, "32024.csv");
            await File.WriteAllLinesAsync(empty, [Header]);
            Assert.True(sample.IsAvailable);

            ImportReviewLargeStage?[] stages = await Task.WhenAll(
                sample.StageAsync(ct),
                sample.StageAsync(ct)
            );
            ImportReviewLargeStage stage = Assert.IsType<ImportReviewLargeStage>(stages[0]);
            Assert.Equal(stage, stages[1]);
            Assert.Equal(directory.Name, stage.Name);
            Assert.Equal(directory.EnumerateFiles().Sum(file => file.Length), stage.Bytes);
            Assert.Equal(4, stage.RowCount);
            Assert.Equal(3, stage.SourceFileCount);

            RadNetQueryResult page = Assert.IsType<RadNetQueryResult>(
                await sample.QueryAsync(
                    new(
                        Filters:
                        [
                            new(
                                RadNetEntity.Station,
                                false,
                                [new(nameof(RadNetRow.State), RadNetOperator.Equal, "CO")]
                            ),
                            new(
                                RadNetEntity.Observation,
                                false,
                                [new(nameof(RadNetRow.ExposureMrH), RadNetOperator.AtLeast, "0.1")]
                            ),
                        ],
                        SortField: nameof(RadNetRow.ExposureMrH),
                        Descending: true,
                        Skip: 1,
                        Top: 1
                    ),
                    ct
                )
            );
            Assert.Equal(2, page.TotalCount);
            RadNetRow row = Assert.Single(page.Items);
            Assert.Equal(0.2, row.ExposureMrH);
            Assert.Equal("alpha-2024.csv", row.FileName);
            Assert.Equal(9, row.SourceRow);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}

using DuckDB.NET.Data;
using NuclearEvaluation.Server.Services.RemDb;
using NuclearEvaluation.Shared.Contracts;

namespace NuclearEvaluation.Server.Tests;

public sealed class RemDbSnapshotTests
{
    static readonly CancellationToken Cancellation = CancellationToken.None;

    [Fact]
    public async Task SiteFiltersMatchOneSamplingWindowAndMeasurement()
    {
        using SnapshotFixture fixture = new();
        RemDbQuery query = new(
            RemDbEntity.Site,
            [
                new(
                    RemDbEntity.Site,
                    false,
                    [new(nameof(RemDbRow.Country), RadNetOperator.Equal, "fr")]
                ),
                new(
                    RemDbEntity.SamplingWindow,
                    false,
                    [new(nameof(RemDbRow.Medium), RadNetOperator.Equal, "Water")]
                ),
                new(
                    RemDbEntity.Measurement,
                    true,
                    [
                        new(nameof(RemDbRow.Nuclide), RadNetOperator.Equal, "Cs-137"),
                        new(nameof(RemDbRow.Nuclide), RadNetOperator.Equal, "Co-60"),
                    ]
                ),
                new(
                    RemDbEntity.Measurement,
                    false,
                    [new(nameof(RemDbRow.Qualifier), RadNetOperator.Equal, "<")]
                ),
            ]
        );

        RemDbQueryResult result = Assert.IsType<RemDbQueryResult>(
            await fixture.Snapshot.QueryAsync(query, Cancellation)
        );
        Assert.Equal(1, result.TotalCount);
        RemDbRow row = Assert.Single(result.Items);
        Assert.Equal("Second site", row.SiteName);
        Assert.Null(row.WindowId);
    }

    [Fact]
    public async Task MeasurementPagingPreservesPreciseSourceTimeAndProvenance()
    {
        using SnapshotFixture fixture = new();
        RemDbQuery query = new(
            RemDbEntity.Measurement,
            [
                new(
                    RemDbEntity.SamplingWindow,
                    false,
                    [new(nameof(RemDbRow.BeganOn), RadNetOperator.AtLeast, "2020-02-01")]
                ),
                new(
                    RemDbEntity.Measurement,
                    false,
                    [new(nameof(RemDbRow.Activity), RadNetOperator.IsPresent)]
                ),
            ],
            nameof(RemDbRow.MeasurementId),
            Descending: true,
            Skip: 1,
            Top: 1
        );
        RemDbQueryResult result = Assert.IsType<RemDbQueryResult>(
            await fixture.Snapshot.QueryAsync(query, Cancellation)
        );
        Assert.Equal(2, result.TotalCount);
        RemDbRow row = Assert.Single(result.Items);
        Assert.Equal(402, row.MeasurementId);
        Assert.Equal(201, row.WindowId);
        Assert.Equal(12, row.SourceRow);
        Assert.Equal(10, row.SourceFileId);
        Assert.Equal(2020, row.SourceYear);
        Assert.Equal("archive-A-digest", row.ArchiveSha256);
        Assert.Equal("measurements-a.txt", row.FileName);
        Assert.Equal(new DateOnly(2020, 2, 1), row.BeganOn);
        Assert.Equal("2020-02-01 01:20:00", row.SourceBegin);
        Assert.Null(row.SourceEnd);
        Assert.Equal("<", row.Qualifier);
        Assert.Equal(0.4, row.Activity);
    }

    [Fact]
    public async Task SummaryAndQualityDescribeReportedRowsWithoutInferringQualifiers()
    {
        using SnapshotFixture fixture = new();
        RemDbSummary summary = await fixture.Snapshot.SummaryAsync(Cancellation);
        Assert.True(summary.Available);
        Assert.Equal(2, summary.SiteCount);
        Assert.Equal(3, summary.SamplingWindowCount);
        Assert.Equal(6, summary.MeasurementCount);
        Assert.Equal(2, summary.SourceFileCount);
        Assert.Equal([new(2020, 4), new(2021, 2)], summary.Coverage);
        Assert.Equal(new("FR", 2), Assert.Single(summary.Countries));

        RemDbFilePage page = Assert.IsType<RemDbFilePage>(
            await fixture.Snapshot.FilesAsync(1, 1, Cancellation)
        );
        Assert.Equal(2, page.TotalCount);
        Assert.Equal(10, Assert.Single(page.Items).Id);

        RemDbFileQuality quality = Assert.IsType<RemDbFileQuality>(
            await fixture.Snapshot.FileQualityAsync(10, Cancellation)
        );
        Assert.Equal(4, quality.MeasurementCount);
        Assert.Equal(1, quality.MissingActivityCount);
        Assert.Equal(2, quality.LessThanCount);
        Assert.Equal(2, quality.MissingQualifierCount);
        Assert.Equal(new DateOnly(2020, 1, 1), quality.FirstBeganOn);
        Assert.Equal(new DateOnly(2020, 1, 2), quality.LastEndedOn);
        Assert.Equal(new("Bq/L", 4), Assert.Single(quality.Units));
        Assert.Equal(new("Reported", 3), Assert.Single(quality.ResultTypes));
        Assert.Null(await fixture.Snapshot.FileQualityAsync(99, Cancellation));
    }

    [Fact]
    public void ValidateFieldScopesAndTypedValues()
    {
        Assert.Throws<ArgumentException>(() =>
            RemDbQueryPlan.Compile(new(SortField: "m.id; DROP TABLE measurements"))
        );
        Assert.Throws<ArgumentException>(() =>
            RemDbQueryPlan.Compile(new(RemDbEntity.Site, SortField: nameof(RemDbRow.SourceYear)))
        );
        Assert.Throws<ArgumentException>(() =>
            RemDbQueryPlan.Compile(
                new(
                    Filters:
                    [
                        new(
                            RemDbEntity.Site,
                            false,
                            [new(nameof(RemDbRow.Unit), RadNetOperator.Equal, "Bq/L")]
                        ),
                    ]
                )
            )
        );
        Assert.Throws<ArgumentException>(() =>
            RemDbQueryPlan.Compile(
                new(
                    Filters:
                    [
                        new(
                            RemDbEntity.Measurement,
                            false,
                            [new(nameof(RemDbRow.Activity), RadNetOperator.AtLeast, "NaN")]
                        ),
                    ]
                )
            )
        );
        RemDbQueryPlan plan = RemDbQueryPlan.Compile(
            new(
                Filters:
                [
                    new(
                        RemDbEntity.Site,
                        false,
                        [new(nameof(RemDbRow.SiteName), RadNetOperator.Equal, "' OR TRUE")]
                    ),
                ]
            )
        );
        Assert.Equal("' OR TRUE", plan.Arguments["p0"]);
        Assert.DoesNotContain("OR TRUE", plan.PageSql);
    }

    sealed class SnapshotFixture : IDisposable
    {
        readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("remdb-test-");

        public SnapshotFixture()
        {
            string file = Path.Combine(_directory.FullName, "fixture.duckdb");
            Snapshot = new(file);
            using DuckDBConnection connection = new(
                new DuckDBConnectionStringBuilder
                {
                    DataSource = file,
                    Threads = 1,
                }.ConnectionString
            );
            connection.Open();
            using DuckDBCommand command = (DuckDBCommand)connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE sites(id INTEGER, country VARCHAR, name VARCHAR, latitude DOUBLE, longitude DOUBLE);
                CREATE TABLE sampling_windows(id BIGINT, site_id INTEGER, medium VARCHAR, treatment VARCHAR,
                    began_on DATE, ended_on DATE, source_begin VARCHAR, source_end VARCHAR);
                CREATE TABLE source_files(id INTEGER, year INTEGER, archive_url VARCHAR,
                    archive_sha256 VARCHAR, file_name VARCHAR, row_count BIGINT);
                CREATE TABLE measurements(id BIGINT, window_id BIGINT, source_file_id INTEGER, source_row INTEGER,
                    laboratory VARCHAR, apparatus VARCHAR, nuclide VARCHAR, activity DOUBLE,
                    unit VARCHAR, qualifier VARCHAR, value_type VARCHAR);
                CREATE TABLE snapshot_metadata(key VARCHAR, value VARCHAR);
                INSERT INTO snapshot_metadata VALUES ('source_url', 'https://example.com/catalog');
                INSERT INTO sites VALUES (1, 'FR', 'First site', NULL, NULL), (2, 'FR', 'Second site', 48, NULL);
                INSERT INTO sampling_windows VALUES
                    (101, 1, 'Water', NULL, '2020-01-01', '2020-01-02', '2020-01-01 00:10:00', '2020-01-02 01:20:00'),
                    (102, 1, 'Air', NULL, '2020-02-01', '2020-02-02', '2020-02-01 00:00:00', '2020-02-02 00:00:00'),
                    (201, 2, 'Water', 'Filtered', '2020-02-01', NULL, '2020-02-01 01:20:00', '');
                INSERT INTO source_files VALUES
                    (10, 2020, 'https://example.com/archive-a.zip', 'archive-A-digest', 'measurements-a.txt', 4),
                    (20, 2021, 'https://example.com/archive-b.zip', 'archive-B-digest', 'measurements-b.txt', 2);
                INSERT INTO measurements VALUES
                    (101, 101, 10, 3, 'L1', 'A1', 'I-131', 2.0, 'Bq/L', '<', 'Reported'),
                    (102, 101, 10, 4, 'L1', 'A1', 'Cs-137', 1.0, 'Bq/L', NULL, 'Reported'),
                    (103, 101, 10, 5, 'L1', 'A1', 'Co-60', NULL, 'Bq/L', '', NULL),
                    (502, 102, 20, 2, 'L2', NULL, 'Cs-137', 1.7, 'Bq/m³', '<', NULL),
                    (402, 201, 10, 12, 'L2', 'A2', 'Cs-137', 0.4, 'Bq/L', '<', 'Reported'),
                    (404, 201, 20, 3, 'L2', NULL, 'Cs-137', NULL, 'Bq/L', NULL, NULL);
                """;
            command.ExecuteNonQuery();
        }

        public RemDbSnapshot Snapshot { get; }

        public void Dispose() => _directory.Delete(recursive: true);
    }
}

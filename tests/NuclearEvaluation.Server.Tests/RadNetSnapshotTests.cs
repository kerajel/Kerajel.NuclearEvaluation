using System.Text;
using DuckDB.NET.Data;
using NuclearEvaluation.Server.Services.RadNet;
using NuclearEvaluation.Server.Services.Workbench;
using NuclearEvaluation.Shared.Contracts;

namespace NuclearEvaluation.Server.Tests;

public sealed class RadNetSnapshotTests
{
    [Fact]
    public async Task StationMatchesOneFileObservationAndChannel()
    {
        using SnapshotFixture fixture = new();
        RadNetQuery request = new(
            RadNetEntity.Station,
            [
                new(
                    RadNetEntity.Station,
                    false,
                    [new(nameof(RadNetRow.State), RadNetOperator.Equal, "ca")]
                ),
                new(
                    RadNetEntity.SourceFile,
                    false,
                    [new(nameof(RadNetRow.Year), RadNetOperator.Equal, "2024")]
                ),
                new(
                    RadNetEntity.Observation,
                    true,
                    [
                        new(nameof(RadNetRow.Status), RadNetOperator.Equal, "approved"),
                        new(nameof(RadNetRow.Status), RadNetOperator.Equal, "provisional"),
                    ]
                ),
                new(
                    RadNetEntity.Channel,
                    false,
                    [
                        new(nameof(RadNetRow.Channel), RadNetOperator.Equal, "R03"),
                        new(nameof(RadNetRow.CountRate), RadNetOperator.GreaterThan, "100"),
                    ]
                ),
            ]
        );

        RadNetQueryResult result = Assert.IsType<RadNetQueryResult>(
            await fixture.Snapshot.QueryAsync(request, CancellationToken.None)
        );
        Assert.Equal(1, result.TotalCount);
        Assert.Equal("Alpha", Assert.Single(result.Items).StationName);
    }

    [Fact]
    public async Task ChannelResultsPagePresentReadingsAndPreserveSourceProvenance()
    {
        using SnapshotFixture fixture = new();
        RadNetQuery request = new(
            RadNetEntity.Channel,
            [
                new(
                    RadNetEntity.Observation,
                    false,
                    [new(nameof(RadNetRow.ObservationId), RadNetOperator.Equal, "101")]
                ),
            ],
            nameof(RadNetRow.CountRate),
            Descending: true,
            Skip: 1,
            Top: 1
        );

        RadNetQueryResult result = Assert.IsType<RadNetQueryResult>(
            await fixture.Snapshot.QueryAsync(request, CancellationToken.None)
        );
        Assert.Equal(2, result.TotalCount);
        RadNetRow row = Assert.Single(result.Items);
        Assert.Equal("R03", row.Channel);
        Assert.Equal(10.5, row.CountRate);
        Assert.Equal(2, row.SourceRow);
        Assert.Equal("alpha-2024.csv", row.FileName);
        Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), row.ObservedAt);
    }

    [Fact]
    public async Task SummaryAndQualityDescribeOnlyTheRequestedFile()
    {
        using SnapshotFixture fixture = new();
        CancellationToken ct = CancellationToken.None;
        RadNetSummary summary = await fixture.Snapshot.SummaryAsync(ct);
        Assert.True(summary.Available);
        Assert.Equal(3, summary.StationCount);
        Assert.Equal(8, summary.ObservationCount);
        Assert.Equal(2, summary.Coverage.Count);
        Assert.Equal(6, summary.Coverage[0].ObservationCount);

        RadNetFileQuality quality = Assert.IsType<RadNetFileQuality>(
            await fixture.Snapshot.FileQualityAsync(11, ct)
        );
        Assert.Equal(3, quality.ObservationCount);
        Assert.Equal(1, quality.MissingHours);
        Assert.Equal(1, quality.MissingDoseCount);
        Assert.Equal(1, quality.NonApprovedCount);
        Assert.Equal(
            3,
            quality.Channels.Single(channel => channel.Channel == "R03").ObservationCount
        );
        Assert.Equal(1, quality.SourceFile.DuplicateRows);
        Assert.Null(await fixture.Snapshot.FileQualityAsync(99, ct));
    }

    [Fact]
    public async Task WorkbenchProjectsCountOverlappingSeriesOnceAndPageTheSameScope()
    {
        using SnapshotFixture fixture = new();
        using WorkbenchArchive archive = new(fixture.Snapshot);
        CancellationToken ct = CancellationToken.None;
        WorkbenchCatalog catalog = await archive.CatalogAsync(ct);
        WorkbenchStation alpha = Assert.Single(catalog.Stations, row => row.Name == "Alpha");
        WorkbenchStation beta = Assert.Single(catalog.Stations, row => row.Name == "Beta");
        WorkbenchSeries whole = Series(alpha, alpha.FirstDay, alpha.LastDay);
        WorkbenchSeries subset = Series(alpha, new(2024, 1, 1), new(2024, 1, 1));
        WorkbenchSeries peer = Series(beta, new(2024, 1, 1), new(2024, 1, 1));
        WorkbenchSeries[] selection = [whole, subset, peer];

        WorkbenchProjectStats stats = await archive.ProjectStatsAsync(selection, ct);
        RadNetQueryResult page = await archive.ProjectObservationsAsync(selection, 1, 2, ct);

        Assert.Equal(6, stats.ObservationCount);
        Assert.Equal(6, stats.WithR03);
        Assert.Equal([5, 1], stats.Months.Select(month => month.ObservationCount));
        Assert.Equal(stats.ObservationCount, page.TotalCount);
        Assert.Equal([103L, 102L], page.Items.Select(row => row.ObservationId));
        Assert.All(page.Items, row => Assert.Equal("alpha-2024.csv", row.FileName));
    }

    [Fact]
    public async Task WorkbenchProjectObservationQueryFiltersAndSortsBeforePagingWithinSeries()
    {
        using SnapshotFixture fixture = new();
        using WorkbenchArchive archive = new(fixture.Snapshot);
        CancellationToken ct = CancellationToken.None;
        WorkbenchCatalog catalog = await archive.CatalogAsync(ct);
        WorkbenchStation alpha = Assert.Single(catalog.Stations, row => row.Name == "Alpha");
        WorkbenchStation beta = Assert.Single(catalog.Stations, row => row.Name == "Beta");
        DateOnly day = new(2024, 1, 1);
        WorkbenchSeries[] selection =
        [
            Series(alpha, day, day),
            Series(alpha, day, day),
            Series(beta, day, day),
        ];
        RadNetQuery query = new(
            RadNetEntity.Observation,
            [
                new(
                    RadNetEntity.Station,
                    true,
                    [
                        new(nameof(RadNetRow.StationName), RadNetOperator.Equal, "Alpha"),
                        new(nameof(RadNetRow.StationName), RadNetOperator.Equal, "Gamma"),
                    ]
                ),
                new(
                    RadNetEntity.SourceFile,
                    false,
                    [
                        new(nameof(RadNetRow.Year), RadNetOperator.AtLeast, "2024"),
                        new(nameof(RadNetRow.Year), RadNetOperator.AtMost, "2025"),
                    ]
                ),
                new(
                    RadNetEntity.Observation,
                    true,
                    [
                        new(nameof(RadNetRow.DoseNsvH), RadNetOperator.AtLeast, "80"),
                        new(nameof(RadNetRow.DoseNsvH), RadNetOperator.IsMissing),
                    ]
                ),
            ],
            SortField: nameof(RadNetRow.DoseNsvH),
            Descending: true,
            Top: 1
        );
        RadNetQueryResult first = await archive.ProjectObservationsAsync(selection, query, ct);
        RadNetQueryResult second = await archive.ProjectObservationsAsync(
            selection,
            query with
            {
                Skip = 1,
            },
            ct
        );
        RadNetQueryResult pastEnd = await archive.ProjectObservationsAsync(
            selection,
            query with
            {
                Skip = 2,
            },
            ct
        );

        Assert.Equal(2, first.TotalCount);
        Assert.Equal(first.TotalCount, second.TotalCount);
        Assert.Equal(first.TotalCount, pastEnd.TotalCount);
        Assert.Equal(103, Assert.Single(first.Items).ObservationId);
        Assert.Equal(102, Assert.Single(second.Items).ObservationId);
        Assert.Empty(pastEnd.Items);
    }

    [Fact]
    public async Task WorkbenchSeriesCountAndFilesIncludeTheLastDayWithoutLeakingOtherYears()
    {
        using SnapshotFixture fixture = new();
        using WorkbenchArchive archive = new(fixture.Snapshot);
        CancellationToken ct = CancellationToken.None;
        WorkbenchCatalog catalog = await archive.CatalogAsync(ct);
        WorkbenchStation alpha = Assert.Single(catalog.Stations, row => row.Name == "Alpha");
        DateOnly day = new(2024, 1, 1);
        WorkbenchSeries subset = Series(alpha, day, day);

        Assert.Equal(4, alpha.ObservationCount);
        Assert.Equal(3, await archive.CountAsync(alpha, day, day, ct));
        WorkbenchFilePage first = await archive.FilesAsync(subset, 0, 10, ct);
        WorkbenchSeriesFile file = Assert.Single(first.Items);
        Assert.Equal(3, file.ObservationCount);
        Assert.Equal(11, file.Id);
        WorkbenchFilePage pastEnd = await archive.FilesAsync(subset, 10, 10, ct);
        Assert.Empty(pastEnd.Items);
        Assert.Equal(1, pastEnd.TotalCount);
    }

    static WorkbenchSeries Series(WorkbenchStation station, DateOnly from, DateOnly to) =>
        new(
            Guid.NewGuid(),
            station.Name,
            station.State,
            station.Name,
            from,
            to,
            WorkbenchReview.Pending,
            "",
            0,
            1
        );

    [Fact]
    public async Task WorkbenchBuilderKeepsAllFivePredicatesOnTheSameBranch()
    {
        using SnapshotFixture fixture = new();
        WorkbenchQueryEngine engine = new(fixture.Snapshot);
        WorkbenchQuerySource source = QuerySource();
        CancellationToken ct = CancellationToken.None;
        WorkbenchQuery query = new(
            WorkbenchQueryEntity.Series,
            [
                new(
                    WorkbenchQueryEntity.Project,
                    false,
                    [
                        new(
                            nameof(WorkbenchQueryRow.ProjectName),
                            RadNetOperator.Equal,
                            "Coverage audit"
                        ),
                    ]
                ),
                new(
                    WorkbenchQueryEntity.Series,
                    false,
                    [new(nameof(WorkbenchQueryRow.State), RadNetOperator.Equal, "CA")]
                ),
                new(
                    WorkbenchQueryEntity.File,
                    false,
                    [new(nameof(WorkbenchQueryRow.Year), RadNetOperator.Equal, "2024")]
                ),
                new(
                    WorkbenchQueryEntity.Observation,
                    false,
                    [new(nameof(WorkbenchQueryRow.Status), RadNetOperator.Equal, "approved")]
                ),
                new(
                    WorkbenchQueryEntity.Channel,
                    false,
                    [
                        new(nameof(WorkbenchQueryRow.Channel), RadNetOperator.Equal, "R03"),
                        new(nameof(WorkbenchQueryRow.CountRate), RadNetOperator.GreaterThan, "100"),
                    ]
                ),
            ],
            nameof(WorkbenchQueryRow.SeriesName),
            MinimumMatchingObservations: 1
        );

        WorkbenchQueryPage series = await engine.QueryAsync(query, source, ct);
        Assert.Equal(2, series.TotalCount);
        Assert.Equal(["Alpha full", "Alpha slice"], series.Items.Select(row => row.SeriesName));
        Assert.All(series.Items, row => Assert.Equal(1L, row.MatchingObservations));

        WorkbenchQueryPage observations = await engine.QueryAsync(
            query with
            {
                Entity = WorkbenchQueryEntity.Observation,
                SortField = null,
            },
            source,
            ct
        );
        Assert.Equal(1, observations.TotalCount);
        Assert.Equal(103L, Assert.Single(observations.Items).ObservationId);
        WorkbenchQueryPage channels = await engine.QueryAsync(
            query with
            {
                Entity = WorkbenchQueryEntity.Channel,
                SortField = nameof(WorkbenchQueryRow.CountRate),
            },
            source,
            ct
        );
        WorkbenchQueryRow reading = Assert.Single(channels.Items);
        Assert.Equal(1, channels.TotalCount);
        Assert.Equal(("R03", 130d, 4), (reading.Channel, reading.CountRate, reading.SourceRow));

        WorkbenchQueryPage files = await engine.QueryAsync(
            query with
            {
                Entity = WorkbenchQueryEntity.File,
                SortField = null,
            },
            source,
            ct
        );
        Assert.Equal(11, Assert.Single(files.Items).FileId);
        WorkbenchQueryPage projects = await engine.QueryAsync(
            query with
            {
                Entity = WorkbenchQueryEntity.Project,
                SortField = null,
            },
            source,
            ct
        );
        Assert.Equal("Coverage audit", Assert.Single(projects.Items).ProjectName);

        Assert.Equal(
            0,
            (
                await engine.QueryAsync(query with { MinimumMatchingObservations = 2 }, source, ct)
            ).TotalCount
        );

        WorkbenchQueryFilterGroup[] eitherStatus = query
            .Filters!.Select(group =>
                group.Entity == WorkbenchQueryEntity.Observation
                    ? group with
                    {
                        MatchAny = true,
                        Conditions =
                        [
                            .. group.Conditions,
                            new(nameof(WorkbenchQueryRow.Status), RadNetOperator.Equal, "REJECTED"),
                        ],
                    }
                    : group
            )
            .ToArray();
        WorkbenchQueryPage eitherStatusResults = await engine.QueryAsync(
            query with
            {
                Entity = WorkbenchQueryEntity.Observation,
                Filters = eitherStatus,
                SortField = null,
            },
            source,
            ct
        );
        Assert.Equal(3, eitherStatusResults.TotalCount);
        Assert.Equal(
            [102L, 103L, 202L],
            eitherStatusResults.Items.Select(row => row.ObservationId).Order()
        );
    }

    [Fact]
    public async Task WorkbenchBuilderKeepsEmptyProjectsAndUnlinkedSeriesInUnfilteredResults()
    {
        using SnapshotFixture fixture = new();
        WorkbenchQueryEngine engine = new(fixture.Snapshot);
        WorkbenchQuerySource source = QuerySource();
        CancellationToken ct = CancellationToken.None;

        WorkbenchQueryPage projects = await engine.QueryAsync(
            new(WorkbenchQueryEntity.Project),
            source,
            ct
        );
        Assert.Equal(3, projects.TotalCount);
        Assert.Contains(projects.Items, row => row.ProjectName == "Empty project");

        WorkbenchQueryPage series = await engine.QueryAsync(
            new(WorkbenchQueryEntity.Series),
            source,
            ct
        );
        Assert.Equal(4, series.TotalCount);
        Assert.Contains(series.Items, row => row.SeriesName == "Gamma unrestricted");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            engine.QueryAsync(
                new(WorkbenchQueryEntity.Project, SortField: nameof(WorkbenchQueryRow.CountRate)),
                source,
                ct
            )
        );
    }

    [Fact]
    public async Task WorkbenchResultFiltersDoNotReduceTheObservationsThatQualifyASeries()
    {
        using SnapshotFixture fixture = new();
        WorkbenchQueryEngine engine = new(fixture.Snapshot);
        WorkbenchQuerySource source = QuerySource();
        CancellationToken ct = CancellationToken.None;
        WorkbenchQuery query = new(
            WorkbenchQueryEntity.Observation,
            [
                new(
                    WorkbenchQueryEntity.Project,
                    false,
                    [
                        new(
                            nameof(WorkbenchQueryRow.ProjectName),
                            RadNetOperator.Equal,
                            "Coverage audit"
                        ),
                    ]
                ),
                new(
                    WorkbenchQueryEntity.Observation,
                    false,
                    [new(nameof(WorkbenchQueryRow.Status), RadNetOperator.Equal, "APPROVED")]
                ),
                new(
                    WorkbenchQueryEntity.Channel,
                    false,
                    [
                        new(nameof(WorkbenchQueryRow.Channel), RadNetOperator.Equal, "R03"),
                        new(nameof(WorkbenchQueryRow.CountRate), RadNetOperator.GreaterThan, "100"),
                    ]
                ),
            ],
            MinimumMatchingObservations: 2,
            ResultFilters:
            [
                new(nameof(WorkbenchQueryRow.SourceRow), RadNetOperator.AtLeast, "4"),
                new(nameof(WorkbenchQueryRow.Station), RadNetOperator.Equal, "Alpha"),
            ]
        );

        WorkbenchQueryPage observation = await engine.QueryAsync(query, source, ct);
        Assert.Equal(1, observation.TotalCount);
        Assert.Equal(103L, Assert.Single(observation.Items).ObservationId);
        Assert.Equal(
            2,
            (await engine.QueryAsync(query with { ResultFilters = null }, source, ct)).TotalCount
        );

        WorkbenchQueryPage files = await engine.QueryAsync(
            query with
            {
                Entity = WorkbenchQueryEntity.File,
                ResultFilters =
                [
                    new(nameof(WorkbenchQueryRow.Year), RadNetOperator.Equal, "2024"),
                    new(nameof(WorkbenchQueryRow.State), RadNetOperator.Equal, "CA"),
                ],
            },
            source,
            ct
        );
        Assert.Equal(1, files.TotalCount);
        Assert.Equal(11, Assert.Single(files.Items).FileId);

        WorkbenchQueryPage channels = await engine.QueryAsync(
            query with
            {
                Entity = WorkbenchQueryEntity.Channel,
                SortField = nameof(WorkbenchQueryRow.ObservedAt),
                ResultFilters =
                [
                    new(nameof(WorkbenchQueryRow.CountRate), RadNetOperator.GreaterThan, "200"),
                    new(nameof(WorkbenchQueryRow.FileName), RadNetOperator.Contains, "2025"),
                ],
            },
            source,
            ct
        );
        Assert.Equal(1, channels.TotalCount);
        Assert.Equal(104L, Assert.Single(channels.Items).ObservationId);
    }

    [Fact]
    public async Task WorkbenchResultFiltersApplyAheadOfPagingAndValidateTheSelectedLevel()
    {
        using SnapshotFixture fixture = new();
        WorkbenchQueryEngine engine = new(fixture.Snapshot);
        WorkbenchQuerySource source = QuerySource();
        CancellationToken ct = CancellationToken.None;
        WorkbenchQuery query = new(
            WorkbenchQueryEntity.Series,
            SortField: nameof(WorkbenchQueryRow.MatchingObservations),
            Descending: true,
            Top: 1,
            ResultFilters:
            [
                new(nameof(WorkbenchQueryRow.MatchingObservations), RadNetOperator.AtLeast, "3"),
            ]
        );

        WorkbenchQueryPage page = await engine.QueryAsync(query, source, ct);
        Assert.Equal(3, page.TotalCount);
        Assert.Equal("Alpha full", Assert.Single(page.Items).SeriesName);
        Assert.Equal(4L, Assert.Single(page.Items).MatchingObservations);
        Assert.Equal(3, (await engine.QueryAsync(query with { Skip = 1 }, source, ct)).TotalCount);

        WorkbenchQueryPage projects = await engine.QueryAsync(
            new(
                WorkbenchQueryEntity.Project,
                ResultFilters:
                [
                    new(nameof(WorkbenchQueryRow.ProjectSeriesCount), RadNetOperator.AtLeast, "2"),
                ]
            ),
            source,
            ct
        );
        Assert.Equal("Coverage audit", Assert.Single(projects.Items).ProjectName);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            engine.QueryAsync(
                query with
                {
                    ResultFilters =
                    [
                        new(
                            nameof(WorkbenchQueryRow.ProjectName),
                            RadNetOperator.Equal,
                            "Coverage audit"
                        ),
                    ],
                },
                source,
                ct
            )
        );
    }

    [Fact]
    public async Task WorkbenchColumnGroupsCombineWithoutChangingSeriesQualification()
    {
        using SnapshotFixture fixture = new();
        WorkbenchQueryEngine engine = new(fixture.Snapshot);
        WorkbenchQuerySource source = QuerySource();
        CancellationToken ct = CancellationToken.None;
        WorkbenchQuery query = new(
            WorkbenchQueryEntity.Observation,
            SortField: nameof(WorkbenchQueryRow.ObservedAt),
            Descending: true,
            MinimumMatchingObservations: 4,
            Top: 1,
            ResultFilters: [new(nameof(WorkbenchQueryRow.Station), RadNetOperator.Equal, "Alpha")],
            ResultFilterGroups:
            [
                new(
                    nameof(WorkbenchQueryRow.SourceRow),
                    true,
                    [new(RadNetOperator.Equal, "2"), new(RadNetOperator.Equal, "4")]
                ),
            ]
        );

        WorkbenchQueryPage first = await engine.QueryAsync(query, source, ct);
        Assert.Equal(3, first.TotalCount);
        Assert.Equal(104L, Assert.Single(first.Items).ObservationId);
        WorkbenchQueryPage next = await engine.QueryAsync(query with { Skip = 1 }, source, ct);
        Assert.Equal(3, next.TotalCount);
        Assert.Equal(103L, Assert.Single(next.Items).ObservationId);

        WorkbenchQueryPage series = await engine.QueryAsync(
            new(
                WorkbenchQueryEntity.Series,
                ResultFilterGroups:
                [
                    new(
                        nameof(WorkbenchQueryRow.SeriesName),
                        true,
                        [new(RadNetOperator.Contains, "Alpha"), new(RadNetOperator.Equal, "Beta")]
                    ),
                    new(
                        nameof(WorkbenchQueryRow.SeriesObservationCount),
                        false,
                        [new(RadNetOperator.AtLeast, "3"), new(RadNetOperator.LessThan, "4")]
                    ),
                ]
            ),
            source,
            ct
        );
        Assert.Equal(["Alpha slice", "Beta"], series.Items.Select(row => row.SeriesName));
    }

    static WorkbenchQuerySource QuerySource()
    {
        WorkbenchSeries full = new(
            Guid.NewGuid(),
            "Alpha full",
            "CA",
            "Alpha",
            new(2024, 1, 1),
            new(2025, 12, 31),
            WorkbenchReview.Pending,
            "",
            4,
            1
        );
        WorkbenchSeries slice = full with
        {
            Id = Guid.NewGuid(),
            Name = "Alpha slice",
            To = new(2024, 1, 1),
            ObservationCount = 3,
        };
        WorkbenchSeries beta = full with
        {
            Id = Guid.NewGuid(),
            Name = "Beta",
            Station = "Beta",
            ObservationCount = 3,
        };
        WorkbenchSeries gamma = full with
        {
            Id = Guid.NewGuid(),
            Name = "Gamma unrestricted",
            State = "TX",
            Station = "Gamma",
            ObservationCount = 1,
        };
        WorkbenchProject main = new(Guid.NewGuid(), "Coverage audit", 1, 3);
        WorkbenchProject other = new(Guid.NewGuid(), "Another project", 1, 1);
        WorkbenchProject empty = new(Guid.NewGuid(), "Empty project", 1, 0);
        return new(
            [main, other, empty],
            [full, slice, beta, gamma],
            [
                new(main.Id, full.Id),
                new(main.Id, slice.Id),
                new(main.Id, beta.Id),
                new(other.Id, full.Id),
            ]
        );
    }

    [Fact]
    public void UnknownFieldsAndInvalidValuesCannotEnterTheQueryPlan()
    {
        Assert.Throws<ArgumentException>(() =>
            RadNetQueryPlan.Compile(new(SortField: "o.id; DROP TABLE stations"))
        );
        Assert.Throws<ArgumentException>(() =>
            RadNetQueryPlan.Compile(
                new(
                    Filters:
                    [
                        new(
                            RadNetEntity.SourceFile,
                            false,
                            [new(nameof(RadNetRow.Year), RadNetOperator.Equal, "2024 or true")]
                        ),
                    ]
                )
            )
        );
        Assert.Throws<ArgumentException>(() =>
            RadNetQueryPlan.Compile(
                new(
                    Filters:
                    [
                        new(
                            RadNetEntity.Station,
                            false,
                            [new(nameof(RadNetRow.CountRate), RadNetOperator.Equal, "0")]
                        ),
                    ]
                )
            )
        );
    }

    [Fact]
    public async Task ImportReviewKeepsSourceRowsAndCanPageAcrossSelectedOutcomes()
    {
        using SnapshotFixture fixture = new();
        using ImportReviewStore store = new(fixture.Snapshot);
        Guid workspace = Guid.NewGuid();
        ImportReviewStage stage = await StageImportAsync(store, workspace, ImportCsv);

        Assert.Equal(
            (5L, 3L, 1L, 1L),
            (stage.InputRows, stage.MatchedRows, stage.DuplicateRows, stage.RejectedRows)
        );
        Assert.Equal(5, stage.RowsWithoutExposure);
        Assert.True(stage.CanCreateSeries);
        Assert.Equal(stage.Id, (await StageImportAsync(store, workspace, ImportCsv)).Id);
        Assert.Null(await store.FindAsync(Guid.NewGuid(), stage.Id, CancellationToken.None));

        ImportReviewQuery query = new(
            SortField: nameof(ImportReviewRow.SourceRow),
            Top: 1,
            Filters:
            [
                new(
                    nameof(ImportReviewRow.Disposition),
                    true,
                    [
                        new(RadNetOperator.Equal, nameof(ImportReviewDisposition.Duplicate)),
                        new(RadNetOperator.Equal, nameof(ImportReviewDisposition.Rejected)),
                    ]
                ),
            ]
        );
        ImportReviewPage? first = await store.QueryAsync(
            workspace,
            stage.Id,
            query,
            CancellationToken.None
        );
        ImportReviewPage? next = await store.QueryAsync(
            workspace,
            stage.Id,
            query with
            {
                Skip = 1,
            },
            CancellationToken.None
        );
        Assert.NotNull(first);
        Assert.NotNull(next);
        Assert.Equal(2, first.TotalCount);
        ImportReviewRow duplicate = Assert.Single(first.Items);
        Assert.Equal(5, duplicate.SourceRow);
        Assert.Equal(ImportReviewDisposition.Duplicate, duplicate.Disposition);
        Assert.Equal(6, Assert.Single(next.Items).SourceRow);
    }

    [Fact]
    public async Task ImportReviewBlocksChangedMeasurementAndPreservesStageOnMalformedInput()
    {
        using SnapshotFixture fixture = new();
        using ImportReviewStore store = new(fixture.Snapshot);
        Guid workspace = Guid.NewGuid();
        ImportReviewStage stage = await StageImportAsync(
            store,
            workspace,
            ImportCsv.Replace("10.5", "11.5")
        );

        Assert.Equal(1, stage.ChangedRows);
        Assert.Equal(0, stage.MissingArchiveRows);
        Assert.False(stage.CanCreateSeries);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            StageImportAsync(store, workspace, "invalid,header\n1,2")
        );
        Assert.Equal(stage, await store.FindAsync(workspace, stage.Id, CancellationToken.None));
    }

    static async Task<ImportReviewStage> StageImportAsync(
        ImportReviewStore store,
        Guid workspace,
        string csv
    )
    {
        ImportReviewSource source = new(
            11,
            "CA",
            "Alpha",
            2024,
            "alpha-2024.csv",
            "https://example.com/alpha.zip",
            3,
            "CA: ALPHA",
            "abc"
        );
        using MemoryStream stream = new(Encoding.UTF8.GetBytes(csv));
        return await store.StageAsync(
            workspace,
            source,
            "alpha-2024.csv",
            stream,
            CancellationToken.None
        );
    }

    const string ImportCsv = """
        LOCATION_NAME,SAMPLE COLLECTION TIME,STATUS,DOSE EQUIVALENT RATE (nSv/h),GAMMA COUNT RATE R03 (CPM),GAMMA COUNT RATE R09 (CPM)
        CA: ALPHA,01/01/2024 00:00:00,APPROVED,70,10.5,400
        CA: ALPHA,01/01/2024 02:00:00,REJECTED,,120,
        CA: ALPHA,01/01/2024 03:00:00,APPROVED,90,130,
        CA: ALPHA,01/01/2024 03:00:00,REJECTED,,800,
        CA: ALPHA,01/01/2024 04:00:00,APPROVED,,,
        """;

    sealed class SnapshotFixture : IDisposable
    {
        readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("radnet-test-");

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
                CREATE TABLE stations(id INTEGER, state VARCHAR, name VARCHAR, latitude DOUBLE, longitude DOUBLE);
                CREATE TABLE source_files(id INTEGER, station_id INTEGER, year INTEGER, archive_url VARCHAR,
                    file_name VARCHAR, row_count BIGINT, archive_sha256 VARCHAR, source_rows BIGINT,
                    rejected_rows BIGINT, duplicate_rows BIGINT);
                CREATE TABLE observations(id BIGINT, source_file_id INTEGER, observed_at TIMESTAMP,
                    status VARCHAR, exposure_mr_h DOUBLE, dose_nsv_h DOUBLE, r02 DOUBLE, r03 DOUBLE,
                    r04 DOUBLE, r05 DOUBLE, r06 DOUBLE, r07 DOUBLE, r08 DOUBLE, r09 DOUBLE, source_row INTEGER);
                CREATE TABLE snapshot_metadata(key VARCHAR, value VARCHAR);
                INSERT INTO snapshot_metadata VALUES ('source_url', 'https://example.com/catalog');
                INSERT INTO stations VALUES (1, 'CA', 'Alpha', NULL, NULL), (2, 'CA', 'Beta', NULL, NULL),
                    (3, 'TX', 'Gamma', NULL, NULL);
                INSERT INTO source_files VALUES
                    (11, 1, 2024, 'https://example.com/alpha.zip', 'alpha-2024.csv', 3, 'abc', 4, 0, 1),
                    (12, 1, 2025, 'https://example.com/alpha.zip', 'alpha-2025.csv', 1, 'abc', 1, 0, 0),
                    (21, 2, 2024, 'https://example.com/beta.zip', 'beta-2024.csv', 2, 'def', 2, 0, 0),
                    (22, 2, 2025, 'https://example.com/beta.zip', 'beta-2025.csv', 1, 'def', 1, 0, 0),
                    (31, 3, 2024, 'https://example.com/gamma.zip', 'gamma-2024.csv', 1, 'ghi', 1, 0, 0);
                INSERT INTO observations(id, source_file_id, observed_at, status, r03, r09, source_row, dose_nsv_h) VALUES
                    (101, 11, '2024-01-01 00:00:00', 'APPROVED', 10.5, 400, 2, 70),
                    (102, 11, '2024-01-01 02:00:00', 'REJECTED', 120, NULL, 3, NULL),
                    (103, 11, '2024-01-01 03:00:00', 'APPROVED', 130, NULL, 4, 90),
                    (104, 12, '2025-01-01 00:00:00', 'APPROVED', 500, NULL, 2, 100),
                    (201, 21, '2024-01-01 00:00:00', 'APPROVED', 20, 330, 2, NULL),
                    (202, 21, '2024-01-01 01:00:00', 'REJECTED', 400, NULL, 3, NULL),
                    (203, 22, '2025-01-01 00:00:00', 'APPROVED', 600, NULL, 2, NULL),
                    (301, 31, '2024-01-01 00:00:00', 'APPROVED', 700, NULL, 2, NULL);
                """;
            command.ExecuteNonQuery();
        }

        public RadNetSnapshot Snapshot { get; }

        public void Dispose() => _directory.Delete(recursive: true);
    }
}

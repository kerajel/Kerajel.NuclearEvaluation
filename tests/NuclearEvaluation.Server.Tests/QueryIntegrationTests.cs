using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Kernel.Data.Context;
using NuclearEvaluation.Kernel.Data.Seed;
using NuclearEvaluation.Server.Services.Data;
using NuclearEvaluation.Server.Services.DB;
using NuclearEvaluation.Server.Services.Evaluation;
using NuclearEvaluation.Shared.Contracts;
using NuclearEvaluation.Shared.Enums;
using NuclearEvaluation.Shared.Models.Domain;
using NuclearEvaluation.Shared.Models.Filters;
using NuclearEvaluation.Shared.Models.Plotting;
using NuclearEvaluation.Shared.Models.QueryBuilder;
using NuclearEvaluation.Shared.Models.Views;
using Radzen;

namespace NuclearEvaluation.Server.Tests;

public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("NUCLEAR_TEST_SQL")))
            Skip =
                "Set NUCLEAR_TEST_SQL to a disposable local SQL Server. Tests create and delete their own database.";
    }
}

public sealed class SqlFixture : IAsyncLifetime
{
    string? _connectionString;
    public int ProjectId { get; private set; }
    public int SeriesId { get; private set; }

    public NuclearEvaluationServerDbContext Open() =>
        new(
            new DbContextOptionsBuilder<NuclearEvaluationServerDbContext>()
                .UseSqlServer(_connectionString)
                .Options
        );

    public async Task InitializeAsync()
    {
        string? server = Environment.GetEnvironmentVariable("NUCLEAR_TEST_SQL");
        if (string.IsNullOrWhiteSpace(server))
            return;
        _connectionString = new SqlConnectionStringBuilder(server)
        {
            InitialCatalog = "NuclearEvaluationTests_" + Guid.NewGuid().ToString("N"),
        }.ConnectionString;
        await using NuclearEvaluationServerDbContext db = Open();
        await db.Database.MigrateAsync();
        // Install SQL object definitions only; never run the large, destructive sandbox seed.
        foreach (
            string batch in SeedScript
                .ReadBatches()
                .Where(batch => batch.Contains("CREATE OR ALTER"))
        )
            await db.Database.ExecuteSqlRawAsync(batch);

        Series series = new()
        {
            SeriesType = SeriesType.Regular,
            CreatedAt = new DateTime(2024, 1, 1),
        };
        Series other = new()
        {
            SeriesType = SeriesType.Medium,
            CreatedAt = new DateTime(2024, 1, 1),
        };
        Sample sample = new()
        {
            Series = series,
            ExternalCode = "001",
            SampleClass = "PIC",
            SamplingDate = new DateTime(2020, 1, 1),
        };
        Sample otherSample = new()
        {
            Series = other,
            ExternalCode = "002",
            SampleClass = "QC",
            SamplingDate = new DateTime(2020, 1, 1),
        };
        SubSample subSample = new()
        {
            Sample = sample,
            ExternalCode = "001",
            ScreeningDate = new DateTime(2024, 1, 1),
        };
        Project project = new()
        {
            Name = "Integration project",
            CreatedAt = new DateTime(2024, 1, 1),
            UpdatedAt = new DateTime(2024, 1, 1),
            DecayCorrectionDate = new DateTime(2024, 1, 1),
            ProjectSeries = [new() { Series = series }],
        };
        db.AddRange(project, otherSample);
        foreach (decimal? value in new decimal?[] { null, 0.5m, 1m, 2m, 8.999m, 9m })
        {
            db.Add(new Apm { SubSample = subSample, U234 = value });
            db.Add(
                new Particle
                {
                    SubSample = subSample,
                    U234 = value,
                    AnalysisDate = new DateTime(2024, 1, 1),
                }
            );
        }
        await db.SaveChangesAsync();
        ProjectId = project.Id;
        SeriesId = series.Id;
    }

    public async Task DisposeAsync()
    {
        if (_connectionString is null)
            return;
        await using NuclearEvaluationServerDbContext db = Open();
        await db.Database.EnsureDeletedAsync();
    }
}

public class QueryIntegrationTests(SqlFixture database) : IClassFixture<SqlFixture>
{
    [SqlServerFact]
    public async Task SeedProducesConsistentChronologyAndCodes()
    {
        await using NuclearEvaluationServerDbContext source = database.Open();
        string connection = new SqlConnectionStringBuilder(source.Database.GetConnectionString())
        {
            InitialCatalog = "NuclearEvaluationSeedTests_" + Guid.NewGuid().ToString("N"),
        }.ConnectionString;
        await using NuclearEvaluationServerDbContext db = new(
            new DbContextOptionsBuilder<NuclearEvaluationServerDbContext>()
                .UseSqlServer(connection)
                .Options
        );
        try
        {
            await db.Database.MigrateAsync();
            await db.Database.OpenConnectionAsync(); // Keep the seed's local temp tables across batches.
            // Both a new database and a reset must use the same identity ranges.
            for (int seedRun = 0; seedRun < 2; seedRun++)
            {
                foreach (string batch in SeedScript.ReadBatches())
                {
                    // Exercise the real script, with only its dataset size reduced.
                    string smallBatch = batch
                        .Replace("@SeriesTarget INT = 100000", "@SeriesTarget INT = 20")
                        .Replace("@ProjectTarget INT = 33333", "@ProjectTarget INT = 5");
                    await db.Database.ExecuteSqlRawAsync(smallBatch);
                }
                Assert.Equal(20, await db.Series.CountAsync());
                Assert.Equal(5, await db.Project.CountAsync());
                Assert.False(await db.Sample.AnyAsync(x => x.SamplingDate < x.Series.CreatedAt));
                Assert.False(
                    await db.SubSample.AnyAsync(x => x.ScreeningDate < x.Sample.SamplingDate)
                );
                Assert.False(
                    await db.Particle.AnyAsync(x =>
                        x.AnalysisDate < x.SubSample.ScreeningDate
                        || x.AnalysisDate > DateTime.UtcNow
                    )
                );
                Assert.False(
                    await db.Project.AnyAsync(x =>
                        x.UpdatedAt < x.CreatedAt || x.UpdatedAt > DateTime.UtcNow
                    )
                );
                Assert.False(
                    await db
                        .Sample.GroupBy(x => new { x.SeriesId, x.ExternalCode })
                        .AnyAsync(x => x.Count() > 1)
                );
                Assert.False(
                    await db
                        .SubSample.GroupBy(x => new { x.SampleId, x.ExternalCode })
                        .AnyAsync(x => x.Count() > 1)
                );
                Assert.Equal(10000, await db.Series.MinAsync(x => x.Id));
                Assert.Equal(10019, await db.Series.MaxAsync(x => x.Id));
                Assert.Equal(1, await db.Sample.MinAsync(x => x.Id));
                Assert.Equal(1, await db.SubSample.MinAsync(x => x.Id));
                Assert.Equal(1, await db.Apm.MinAsync(x => x.Id));
                Assert.Equal(1, await db.Particle.MinAsync(x => x.Id));
                Assert.Equal(1, await db.Project.MinAsync(x => x.Id));
            }
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
            await db.Database.EnsureDeletedAsync();
        }
    }

    [SqlServerFact]
    public async Task QueryBuilderSupportsEnumAndTextFiltersAcrossEntities()
    {
        await using NuclearEvaluationServerDbContext db = database.Open();
        CompositeFilterDescriptor[] descriptors =
        [
            new()
            {
                Property = "Sample.ExternalCode",
                FilterOperator = FilterOperator.Equals,
                FilterValue = "001",
            },
            new()
            {
                Property = "Sample.SampleType",
                FilterOperator = FilterOperator.Equals,
                FilterValue = SampleType.Qc,
            },
        ];
        string filter = descriptors.ToFilterString<SampleViewPresetFilter>(
            LogicalFilterOperator.Or,
            FilterCaseSensitivity.Default
        );
        PresetFilterBox preset = new();
        preset.Set(PresetFilterEntryType.Sample, filter);
        FetchDataResult<SeriesView> result = await new SeriesService(
            db,
            NullLogger<SeriesView>.Instance
        ).GetSeriesViews(new() { Query = new() { PresetFilterBox = preset } });
        Assert.True(result.IsSuccessful, $"Filter: {filter}\n{result.Exception}");
        Assert.Equal(2, result.TotalCount);

        preset.Set(
            PresetFilterEntryType.Sample,
            new[] { descriptors[1] }.ToFilterString<SampleViewPresetFilter>(
                LogicalFilterOperator.And,
                FilterCaseSensitivity.Default
            )
        );
        FetchDataResult<SampleView> samples = await new SampleService(
            db,
            NullLogger<SampleService>.Instance
        ).GetSampleViews(new() { Query = new() { PresetFilterBox = preset } });
        Assert.True(samples.IsSuccessful, samples.Exception?.ToString());
        Assert.Equal(SampleType.Qc, Assert.Single(samples.Entries).SampleType);
    }

    [SqlServerFact]
    public async Task QueryBuilderSupportsScalarDefaultsAndDirectEnumFilters()
    {
        await using NuclearEvaluationServerDbContext db = database.Open();
        (PresetFilterEntryType EntryType, string Filter)[] filters =
        [
            (
                PresetFilterEntryType.Series,
                BuildFilter<SeriesViewPresetFilter>(
                    "Series.SeriesType",
                    FilterOperator.Equals,
                    SeriesType.Regular
                )
            ),
            (
                PresetFilterEntryType.Series,
                BuildFilter<SeriesViewPresetFilter>("Series.IsDu", FilterOperator.Equals, false)
            ),
            (
                PresetFilterEntryType.Series,
                BuildFilter<SeriesViewPresetFilter>("Series.Id", FilterOperator.GreaterThan, 0)
            ),
            (
                PresetFilterEntryType.Series,
                BuildFilter<SeriesViewPresetFilter>(
                    "Series.CreatedAt",
                    FilterOperator.LessThan,
                    new DateTime(2025, 1, 1)
                )
            ),
            (
                PresetFilterEntryType.Sample,
                BuildFilter<SampleViewPresetFilter>(
                    "Sample.SamplingDate",
                    FilterOperator.GreaterThan,
                    new DateTime(2019, 1, 1)
                )
            ),
            (
                PresetFilterEntryType.Apm,
                BuildFilter<ApmViewPresetFilter>("Apm.U234", FilterOperator.GreaterThan, 1m)
            ),
        ];
        foreach ((PresetFilterEntryType entryType, string filter) in filters)
        {
            PresetFilterBox preset = new();
            preset.Set(entryType, filter);
            FetchDataResult<SeriesView> result = await new SeriesService(
                db,
                NullLogger<SeriesView>.Instance
            ).GetSeriesViews(new() { Query = new() { PresetFilterBox = preset } });
            Assert.True(result.IsSuccessful, $"Filter: {filter}\n{result.Exception}");
            Assert.NotEmpty(result.Entries);
        }
        FetchDataResult<SampleView> samples = await new SampleService(
            db,
            NullLogger<SampleService>.Instance
        ).GetSampleViews(
            new()
            {
                Query = new()
                {
                    Filter = BuildFilter<SampleView>(
                        "SampleType",
                        FilterOperator.Equals,
                        SampleType.Qc
                    ),
                },
            }
        );
        Assert.True(samples.IsSuccessful, samples.Exception?.ToString());
        Assert.Equal(SampleType.Qc, Assert.Single(samples.Entries).SampleType);
    }

    static string BuildFilter<T>(string property, FilterOperator operation, object value)
    {
        CompositeFilterDescriptor[] filters =
        [
            new()
            {
                Property = property,
                FilterOperator = operation,
                FilterValue = value,
            },
        ];
        return filters.ToFilterString<T>(LogicalFilterOperator.And, FilterCaseSensitivity.Default);
    }

    [SqlServerFact]
    public async Task UpgradePreservesExistingRowsAndCorrectsComputedType()
    {
        await using NuclearEvaluationServerDbContext source = database.Open();
        string connection = new SqlConnectionStringBuilder(source.Database.GetConnectionString())
        {
            InitialCatalog = "NuclearEvaluationUpgradeTests_" + Guid.NewGuid().ToString("N"),
        }.ConnectionString;
        await using NuclearEvaluationServerDbContext db = new(
            new DbContextOptionsBuilder<NuclearEvaluationServerDbContext>()
                .UseSqlServer(connection)
                .Options
        );
        try
        {
            await db.GetService<IMigrator>().MigrateAsync("20260613110533_InitialCreate");
            Series series = new()
            {
                CreatedAt = new DateTime(2024, 1, 1),
                SeriesType = SeriesType.Regular,
            };
            db.Add(series);
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO [DATA].[Sample] (SeriesId, ExternalCode, SamplingDate, SampleClass) VALUES ({series.Id}, N'001', '2024-01-01', N'PIC')"
            );
            // Existing, non-schema-bound views must also survive the computed-column update.
            string view = SeedScript
                .ReadBatches()
                .Single(batch => batch.Contains("CREATE OR ALTER VIEW [DATA].[SampleView]"));
            await db.Database.ExecuteSqlRawAsync(view);
            await db.Database.MigrateAsync();
            Sample sample = await db.Sample.SingleAsync();
            Assert.Equal("001", sample.ExternalCode);
            Assert.Equal(SampleType.Pic, sample.SampleType);
            Assert.Equal(series.Id, sample.SeriesId);
            Assert.Single(await db.SampleView.ToListAsync());
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }

    [SqlServerFact]
    public async Task ModelMatchesMigrationSnapshot()
    {
        await using NuclearEvaluationServerDbContext db = database.Open();
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [SqlServerFact]
    public async Task ProjectIncludesWorkWithFilteringPagingAndNoTracking()
    {
        await using NuclearEvaluationServerDbContext db = database.Open();
        FetchDataCommand<ProjectView> command = new()
        {
            Query = new() { Filter = $"Id == {database.ProjectId}", Top = 1 },
        };
        command.Include(x => x.ProjectSeries);
        FetchDataResult<ProjectView> result = await new ProjectService(
            db,
            NullLogger<ProjectService>.Instance
        ).GetProjectViews(command);
        Assert.True(result.IsSuccessful, result.Exception?.ToString());
        Assert.Equal(
            database.SeriesId,
            Assert.Single(Assert.Single(result.Entries).ProjectSeries).SeriesId
        );
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [SqlServerFact]
    public async Task CountsAndEnumOptionsRespectProjectScope()
    {
        await using NuclearEvaluationServerDbContext db = database.Open();
        FetchDataCommand<SeriesView> command = new()
        {
            Query = new() { ProjectId = database.ProjectId },
        };
        SeriesCountsView counts = await new SeriesService(
            db,
            NullLogger<SeriesView>.Instance
        ).GetSeriesCounts(command);
        Assert.Equal(1, counts.SeriesCount);
        Assert.Equal(1, counts.SampleCount);
        Assert.Equal(1, counts.SubSampleCount);
        Assert.Equal(6, counts.ApmCount);
        Assert.Equal(6, counts.ParticleCount);
        FetchDataResult<int> options = await new GenericDbService(db).GetFilterOptions(
            command,
            nameof(SeriesView.SeriesType)
        );
        Assert.Equal([(int)SeriesType.Regular], options.Entries);
    }

    [SqlServerFact]
    public async Task DecayCorrectedQueriesStillApplyPresetFilters()
    {
        await using NuclearEvaluationServerDbContext db = database.Open();
        PresetFilterBox filters = new();
        filters.Set(PresetFilterEntryType.Sample, "Sample.ExternalCode == \"002\"");
        DataQuery query = new()
        {
            ProjectId = database.ProjectId,
            DecayCorrected = true,
            PresetFilterBox = filters,
        };
        FetchDataResult<ParticleView> particles = await new ParticleService(
            db,
            NullLogger<ParticleService>.Instance
        ).GetParticleViews(new() { Query = query });
        Assert.True(particles.IsSuccessful || particles.NotFound, particles.Exception?.ToString());
        Assert.Empty(particles.Entries);
        ILookup<string, BinCount> charts = await new ChartService(db).GetProjectApmUraniumBinCounts(
            new FetchDataCommand<ApmView> { Query = query }
        );
        Assert.Empty(charts);
    }

    [SqlServerFact]
    public async Task HistogramUsesConsistentBinsIncludingZeroCountsAndNineBoundary()
    {
        await using NuclearEvaluationServerDbContext db = database.Open();
        BinCount[] bins = (
            await new ChartService(db).GetProjectApmUraniumBinCounts(
                new FetchDataCommand<ApmView> { Query = new() { ProjectId = database.ProjectId } }
            )
        )["U234"]
            .ToArray();
        Assert.Equal(
            ["n.m.", "< 1", "1-2", "2-3", "3-4", "4-5", "5-6", "6-7", "7-8", "8-9", "≥ 9"],
            bins.Select(x => x.Name)
        );
        Assert.Equal([1, 1, 1, 1, 0, 0, 0, 0, 0, 1, 1], bins.Select(x => x.Count));
    }

    [SqlServerFact]
    public async Task DecayMovesForwardDownAndBackwardUp()
    {
        await using NuclearEvaluationServerDbContext db = database.Open();
        decimal later = await db
            .Database.SqlQueryRaw<decimal>(
                "SELECT [DATA].[CalculateDecayCorrection](10, '3024-01-01', '2024-01-01', 'U234') AS Value"
            )
            .SingleAsync();
        decimal earlier = await db
            .Database.SqlQueryRaw<decimal>(
                "SELECT [DATA].[CalculateDecayCorrection](10, '1024-01-01', '2024-01-01', 'U234') AS Value"
            )
            .SingleAsync();
        Assert.InRange(later, 9.97m, 9.98m);
        Assert.InRange(earlier, 10.02m, 10.03m);
        ProjectDecayCorrectedParticleView[] corrected = await db
            .ProjectDecayCorrectedParticleView.Where(x =>
                x.ProjectId == database.ProjectId && x.U234 == 9m
            )
            .ToArrayAsync();
        Assert.Single(corrected); // Analysis date equals target; sampling date is four years earlier.
    }
}

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NuclearEvaluation.Kernel.Data.Context;

namespace NuclearEvaluation.Kernel.Data.Migrations;

[DbContext(typeof(NuclearEvaluationServerDbContext))]
[Migration("20260908120000_CorrectDecayReferenceDates")]
public sealed class CorrectDecayReferenceDates : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // SQL CASE yields int unless explicitly cast; EF reads this enum as a byte.
        migrationBuilder.AlterColumn<byte>(
            name: "SampleType",
            schema: "DATA",
            table: "Sample",
            type: "tinyint",
            nullable: false,
            computedColumnSql: "CAST(CASE WHEN SampleClass LIKE 'PIC%' THEN 3 WHEN SampleClass LIKE '%QC%' THEN 4 ELSE 2 END AS tinyint)",
            oldClrType: typeof(byte),
            oldType: "tinyint",
            oldComputedColumnSql: "CASE WHEN SampleClass LIKE 'PIC%' THEN 3 WHEN SampleClass LIKE '%QC%' THEN 4 ELSE 2 END"
        );
        migrationBuilder.Sql(
            """
            CREATE OR ALTER FUNCTION [DATA].[CalculateDecayCorrection]
            (
                @RawValue DECIMAL(38,15),
                @DecayCorrectionDate DATETIME2,
                @AnalysisDate DATETIME2,
                @Isotope NVARCHAR(10)
            )
            RETURNS DECIMAL(38,15)
            AS
            BEGIN
                IF @DecayCorrectionDate IS NULL OR @RawValue IS NULL
                    RETURN @RawValue;

                -- Values are treated as amounts/activities at the reference date. A later target
                -- date decreases them; an earlier target date back-corrects them upward.
                DECLARE @t FLOAT = DATEDIFF_BIG(SECOND, @AnalysisDate, @DecayCorrectionDate) / (365.25 * 86400.0);

                DECLARE @lambda FLOAT;

                IF @Isotope = 'U234'
                    SET @lambda = LOG(2) / 245500.0;
                ELSE IF @Isotope = 'U235'
                    SET @lambda = LOG(2) / 703800000.0;
                ELSE IF @Isotope = 'U236'
                    SET @lambda = LOG(2) / 23420000.0;
                ELSE IF @Isotope = 'U238'
                    SET @lambda = LOG(2) / 4468000000.0;
                ELSE
                    RETURN @RawValue;

                DECLARE @DecayFactor FLOAT = EXP(-@lambda * @t);

                DECLARE @CorrectedValue DECIMAL(38,15) = CAST(@RawValue * @DecayFactor AS DECIMAL(38,15));

                RETURN @CorrectedValue;
            END;
            """
        );
        migrationBuilder.Sql(
            """
            CREATE OR ALTER VIEW [DATA].[SeriesView]
            AS
            WITH sampleData AS (
                SELECT
                     [SeriesId]
                    ,COUNT(*) AS [SampleCount]
                    ,STRING_AGG(CONVERT(NVARCHAR(MAX), [ExternalCode]), ',') WITHIN GROUP (ORDER BY [ExternalCode] ASC) AS [SampleExternalCodes]
                FROM [DATA].[Sample]
                GROUP BY [SeriesId]
            )
            SELECT
                 [s].[Id]
                ,[s].[SeriesType]
                ,[s].[CreatedAt]
                ,[s].[SgasComment]
                ,[s].[IsDu]
                ,[s].[WorkingPaperLink]
                ,[s].[IsNu]
                ,[s].[AnalysisCompleteDate]
                ,ISNULL([sd].[SampleCount], 0) AS [SampleCount]
                ,ISNULL([sd].[SampleExternalCodes], '') AS [SampleExternalCodes]
            FROM [DATA].[Series] AS [s]
            LEFT JOIN sampleData AS [sd]
                ON [s].[Id] = [sd].[SeriesId];
            """
        );
        migrationBuilder.Sql(
            """
            CREATE OR ALTER VIEW [EVALUATION].[ProjectView]
            AS
            WITH seriesData AS (
                SELECT
                     [ProjectId] AS [Id]
                    ,STRING_AGG(CONVERT(NVARCHAR(MAX), [SeriesId]), ',') WITHIN GROUP (ORDER BY [SeriesId] ASC) AS [SeriesIds]
                    ,SUM([sv].[SampleCount]) AS [SampleCount]
                FROM [EVALUATION].[ProjectSeries] AS [ps]
                INNER JOIN [DATA].[SeriesView] AS [sv] ON [ps].[SeriesId] = [sv].[Id]
                GROUP BY [ps].[ProjectId]
            )
                SELECT
                 [p].[Id]
                ,[p].[Name]
                ,[p].[Conclusions]
                ,[p].[FollowUpActionsRecommended]
                ,[p].[CreatedAt]
                ,[p].[UpdatedAt]
                ,[p].[DecayCorrectionDate]
                ,ISNULL([sd].[SeriesIds], N'') AS [SeriesIds]
                ,ISNULL([sd].[SampleCount], 0) AS [SampleCount]
            FROM [EVALUATION].[Project] AS [p]
            LEFT JOIN seriesData AS [sd]
                ON [p].[Id] = [sd].[Id];
            """
        );
        migrationBuilder.Sql(
            """
            CREATE OR ALTER VIEW [EVALUATION].[ProjectDecayCorrectedParticleView]
            AS
            SELECT
                  [pr].[Id] AS [ProjectId]
                , [x].[Id]
                , [x].[SubSampleId]
                , [x].[ParticleExternalId]
                , [x].[AnalysisDate]
                , [x].[IsNu]
                , [x].[LaboratoryCode]
                , [DATA].[CalculateDecayCorrection](
                      CAST([x].[U234] AS DECIMAL(38,15)),
                      [pr].[DecayCorrectionDate],
                      [x].[AnalysisDate],
                      'U234') AS [U234]
                , [DATA].[CalculateDecayCorrection](
                      CAST([x].[ErU234] AS DECIMAL(38,15)),
                      [pr].[DecayCorrectionDate],
                      [x].[AnalysisDate],
                      'U234') AS [ErU234]
                , [DATA].[CalculateDecayCorrection](
                      CAST([x].[U235] AS DECIMAL(38,15)),
                      [pr].[DecayCorrectionDate],
                      [x].[AnalysisDate],
                      'U235') AS [U235]
                , [DATA].[CalculateDecayCorrection](
                      CAST([x].[ErU235] AS DECIMAL(38,15)),
                      [pr].[DecayCorrectionDate],
                      [x].[AnalysisDate],
                      'U235') AS [ErU235]
                , [x].[Comment]
            FROM [DATA].[Particle] AS [x]
            INNER JOIN [DATA].[SubSample] AS [ss] ON [x].[SubSampleId] = [ss].[Id]
            INNER JOIN [DATA].[Sample] AS [s] ON [ss].[SampleId] = [s].[Id]
            INNER JOIN [EVALUATION].[ProjectSeries] AS [ps] ON [s].[SeriesId] = [ps].[SeriesId]
            INNER JOIN [EVALUATION].[Project] AS [pr] ON [ps].[ProjectId] = [pr].[Id];
            """
        );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // SQL CASE yields int unless explicitly cast; EF reads this enum as a byte.
        migrationBuilder.AlterColumn<byte>(
            name: "SampleType",
            schema: "DATA",
            table: "Sample",
            type: "tinyint",
            nullable: false,
            computedColumnSql: "CASE WHEN SampleClass LIKE 'PIC%' THEN 3 WHEN SampleClass LIKE '%QC%' THEN 4 ELSE 2 END",
            oldClrType: typeof(byte),
            oldType: "tinyint",
            oldComputedColumnSql: "CAST(CASE WHEN SampleClass LIKE 'PIC%' THEN 3 WHEN SampleClass LIKE '%QC%' THEN 4 ELSE 2 END AS tinyint)"
        );
        migrationBuilder.Sql(
            """
            CREATE OR ALTER FUNCTION [DATA].[CalculateDecayCorrection]
            (
                @RawValue DECIMAL(38,15),
                @DecayCorrectionDate DATETIME2,
                @AnalysisDate DATETIME2,
                @Isotope NVARCHAR(10)
            )
            RETURNS DECIMAL(38,15)
            AS
            BEGIN
                IF @DecayCorrectionDate IS NULL OR @RawValue IS NULL
                    RETURN @RawValue;

                DECLARE @t FLOAT = DATEDIFF(DAY, @AnalysisDate, @DecayCorrectionDate) / 365.25;

                DECLARE @lambda FLOAT;

                IF @Isotope = 'U234'
                    SET @lambda = LOG(2) / 245500.0;
                ELSE IF @Isotope = 'U235'
                    SET @lambda = LOG(2) / 703800000.0;
                ELSE IF @Isotope = 'U236'
                    SET @lambda = LOG(2) / 23420000.0;
                ELSE IF @Isotope = 'U238'
                    SET @lambda = LOG(2) / 4468000000.0;
                ELSE
                    RETURN @RawValue;

                DECLARE @DecayFactor FLOAT = EXP(@lambda * @t);

                DECLARE @CorrectedValue DECIMAL(38,15) = CAST(@RawValue * @DecayFactor AS DECIMAL(38,15));

                RETURN @CorrectedValue;
            END;
            """
        );
        migrationBuilder.Sql(
            """
            CREATE OR ALTER VIEW [DATA].[SeriesView]
            AS
            WITH sampleData AS (
                SELECT
                     [SeriesId]
                    ,COUNT(*) AS [SampleCount]
                    ,STRING_AGG([ExternalCode], ',') WITHIN GROUP (ORDER BY [ExternalCode] ASC) AS [SampleExternalCodes]
                FROM [DATA].[Sample]
                GROUP BY [SeriesId]
            )
            SELECT
                 [s].[Id]
                ,[s].[SeriesType]
                ,[s].[CreatedAt]
                ,[s].[SgasComment]
                ,[s].[IsDu]
                ,[s].[WorkingPaperLink]
                ,[s].[IsNu]
                ,[s].[AnalysisCompleteDate]
                ,ISNULL([sd].[SampleCount], 0) AS [SampleCount]
                ,ISNULL([sd].[SampleExternalCodes], '') AS [SampleExternalCodes]
            FROM [DATA].[Series] AS [s]
            LEFT JOIN sampleData AS [sd]
                ON [s].[Id] = [sd].[SeriesId];
            """
        );
        migrationBuilder.Sql(
            """
            CREATE OR ALTER VIEW [EVALUATION].[ProjectView]
            AS
            WITH seriesData AS (
                SELECT
                     [ProjectId] AS [Id]
                    ,STRING_AGG([SeriesId], ',') WITHIN GROUP (ORDER BY [SeriesId] ASC) AS [SeriesIds]
                    ,SUM([sv].[SampleCount]) AS [SampleCount]
                FROM [EVALUATION].[ProjectSeries] AS [ps]
                INNER JOIN [DATA].[SeriesView] AS [sv] ON [ps].[SeriesId] = [sv].[Id]
                GROUP BY [ps].[ProjectId]
            )
                SELECT
                 [p].[Id]
                ,[p].[Name]
                ,[p].[Conclusions]
                ,[p].[FollowUpActionsRecommended]
                ,[p].[CreatedAt]
                ,[p].[UpdatedAt]
                ,[p].[DecayCorrectionDate]
                ,ISNULL([sd].[SeriesIds], N'') AS [SeriesIds]
                ,ISNULL([sd].[SampleCount], 0) AS [SampleCount]
            FROM [EVALUATION].[Project] AS [p]
            LEFT JOIN seriesData AS [sd]
                ON [p].[Id] = [sd].[Id];
            """
        );
        migrationBuilder.Sql(
            """
            CREATE OR ALTER VIEW [EVALUATION].[ProjectDecayCorrectedParticleView]
            AS
            SELECT
                  [pr].[Id] AS [ProjectId]
                , [x].[Id]
                , [x].[SubSampleId]
                , [x].[ParticleExternalId]
                , [x].[AnalysisDate]
                , [x].[IsNu]
                , [x].[LaboratoryCode]
                , [DATA].[CalculateDecayCorrection](
                      CAST([x].[U234] AS DECIMAL(38,15)),
                      [pr].[DecayCorrectionDate],
                      [s].[SamplingDate],
                      'U234') AS [U234]
                , [DATA].[CalculateDecayCorrection](
                      CAST([x].[ErU234] AS DECIMAL(38,15)),
                      [pr].[DecayCorrectionDate],
                      [s].[SamplingDate],
                      'U234') AS [ErU234]
                , [DATA].[CalculateDecayCorrection](
                      CAST([x].[U235] AS DECIMAL(38,15)),
                      [pr].[DecayCorrectionDate],
                      [s].[SamplingDate],
                      'U235') AS [U235]
                , [DATA].[CalculateDecayCorrection](
                      CAST([x].[ErU235] AS DECIMAL(38,15)),
                      [pr].[DecayCorrectionDate],
                      [s].[SamplingDate],
                      'U235') AS [ErU235]
                , [x].[Comment]
            FROM [DATA].[Particle] AS [x]
            INNER JOIN [DATA].[SubSample] AS [ss] ON [x].[SubSampleId] = [ss].[Id]
            INNER JOIN [DATA].[Sample] AS [s] ON [ss].[SampleId] = [s].[Id]
            INNER JOIN [EVALUATION].[ProjectSeries] AS [ps] ON [s].[SeriesId] = [ps].[SeriesId]
            INNER JOIN [EVALUATION].[Project] AS [pr] ON [ps].[ProjectId] = [pr].[Id];
            """
        );
    }
}

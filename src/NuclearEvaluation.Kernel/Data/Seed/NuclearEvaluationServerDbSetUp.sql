DROP TABLE IF EXISTS #tempComments;
DROP TABLE IF EXISTS #tempURLs;
DROP TABLE IF EXISTS #activityNotes;
DROP TABLE IF EXISTS #trackingNumbers;
DROP TABLE IF EXISTS #followUpActionsRecommended;
DROP TABLE IF EXISTS #conclusions;
DROP TABLE IF EXISTS #projectNames;
DROP TABLE IF EXISTS #seedNumbers1000;
DROP TABLE IF EXISTS #projectIds;

GO

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
GO

-- SeriesView
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
GO

-- SampleView
CREATE OR ALTER VIEW [DATA].[SampleView]
AS
WITH subSampleData AS (
	SELECT
		SampleId,
		COUNT(*) AS SubSampleCount
		FROM [DATA].SubSample ss 
		GROUP BY SampleId
)
SELECT    [x].Id
        , [x].SeriesId
		, CONCAT([x].SeriesId, N'-', [x].ExternalCode) AS [Sequence]
        , [x].ExternalCode
        , [x].SamplingDate
		, [x].SampleType
		, [x].SampleClass
		, [x].Latitude
        , [x].Longitude
		, ISNULL([ssd].SubSampleCount, 0) AS SubSampleCount
FROM [DATA].[Sample] [x]
LEFT JOIN subSampleData AS [ssd]
	ON [ssd].SampleId = x.[Id];
GO

-- SubSampleView
CREATE OR ALTER VIEW [DATA].[SubSampleView]
AS
SELECT    [x].Id
        , [x].SampleId
		, CONCAT([s].SeriesId, N'-', [s].ExternalCode, N'-', [x].ExternalCode) AS [Sequence]
        , [x].ExternalCode
        , [x].ScreeningDate
        , [x].IsFromLegacySystem
        , [x].UploadResultDate
        , [x].ActivityNotes
        , [x].TrackingNumber
FROM [DATA].[SubSample] [x]
INNER JOIN [DATA].[Sample] [s] ON [x].SampleId = [s].[Id]
GO

-- ParticleView
CREATE OR ALTER VIEW [DATA].[ParticleView]
AS
SELECT    [x].Id
        , [x].SubSampleId
        , [x].ParticleExternalId
        , [x].AnalysisDate
        , [x].IsNu
        , [x].LaboratoryCode
        , [x].U234
        , [x].ErU234
        , [x].U235
        , [x].ErU235
        , [x].Comment
FROM [DATA].[Particle] [x];
GO

-- ApmView
CREATE OR ALTER VIEW [DATA].[ApmView]
AS
SELECT    [x].Id
        , [x].SubSampleId
        , [x].U234
        , [x].ErU234
        , [x].U235
        , [x].ErU235
        , [x].U236
        , [x].ErU236
        , [x].U238
        , [x].ErU238
        , [x].Comment
FROM [DATA].[Apm] [x];
GO

-- ProjectView
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
GO

-- ProjectViewSeriesView
CREATE OR ALTER VIEW [EVALUATION].[ProjectViewSeriesView]
AS
SELECT
    [ps].[ProjectId] AS [ProjectId],
    [ps].[SeriesId] AS [SeriesId]
FROM [EVALUATION].[ProjectSeries] AS [ps]
GO

-- ProjectDecayCorrectedParticleView
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
GO

-- ProjectDecayCorrectedApmView
CREATE OR ALTER VIEW [EVALUATION].[ProjectDecayCorrectedApmView]
AS
SELECT
      [pr].[Id] AS [ProjectId]
    , [x].[Id]
    , [x].[SubSampleId]
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
    , [DATA].[CalculateDecayCorrection](
          CAST([x].[U236] AS DECIMAL(38,15)),
          [pr].[DecayCorrectionDate],
          [s].[SamplingDate],
          'U236') AS [U236]
    , [DATA].[CalculateDecayCorrection](
          CAST([x].[ErU236] AS DECIMAL(38,15)),
          [pr].[DecayCorrectionDate],
          [s].[SamplingDate],
          'U236') AS [ErU236]
    , [DATA].[CalculateDecayCorrection](
          CAST([x].[U238] AS DECIMAL(38,15)),
          [pr].[DecayCorrectionDate],
          [s].[SamplingDate],
          'U238') AS [U238]
    , [DATA].[CalculateDecayCorrection](
          CAST([x].[ErU238] AS DECIMAL(38,15)),
          [pr].[DecayCorrectionDate],
          [s].[SamplingDate],
          'U238') AS [ErU238]
    , [x].[Comment]
FROM [DATA].[Apm] AS [x]
INNER JOIN [DATA].[SubSample] AS [ss] 
    ON [x].[SubSampleId] = [ss].[Id]
INNER JOIN [DATA].[Sample] AS [s] 
    ON [ss].[SampleId] = [s].[Id]
INNER JOIN [EVALUATION].[ProjectSeries] AS [ps] 
    ON [s].[SeriesId] = [ps].[SeriesId]
INNER JOIN [EVALUATION].[Project] AS [pr] 
    ON [ps].[ProjectId] = [pr].[Id];
GO

CREATE TABLE #tempComments 
(
	ID INT IDENTITY,
    Comment NVARCHAR(200)
);

CREATE TABLE #tempURLs
(	
	ID INT IDENTITY,
    URL NVARCHAR(200)
);

CREATE TABLE #activityNotes
(
	ID INT IDENTITY,
    Note NVARCHAR(200)
);

CREATE TABLE #trackingNumbers
(
	ID INT IDENTITY,
    TrackingNumber NVARCHAR(200)
);

CREATE TABLE #followUpActionsRecommended
(
	ID INT IDENTITY,
    Action NVARCHAR(200)
);

CREATE TABLE #conclusions
(
	ID INT IDENTITY,
    Conclusion NVARCHAR(200)
);

CREATE TABLE #projectNames
(
	ID INT IDENTITY,
    ProjectName NVARCHAR(200)
);

CREATE TABLE #seedNumbers1000
(
    n INT NOT NULL PRIMARY KEY
);

;WITH digits AS (
    SELECT v
    FROM (VALUES (0),(1),(2),(3),(4),(5),(6),(7),(8),(9)) AS d(v)
)
INSERT INTO #seedNumbers1000 (n)
SELECT ROW_NUMBER() OVER (ORDER BY hundreds.v, tens.v, ones.v)
FROM digits AS hundreds
CROSS JOIN digits AS tens
CROSS JOIN digits AS ones;

INSERT INTO #tempComments (Comment)
VALUES 
    ('Corrective action needed'), ('Sample approved'), ('Recheck results'),
    ('Valid range exceeded'), ('Test sample again'), ('Precision is key'),
    ('Error in calculation'), ('Reagent issue'), ('Calibration required'),
    ('Maintain equipment'), ('Results are consistent'), ('Check for contamination'),
    ('Urgent retesting'), ('Outlier detected'), ('Sample deteriorated'),
    ('Possible mislabel'), ('Retest old batch'), ('Verify with second method'),
    ('Control sample failed'), ('Report to supervisor'), ('Compliance issue'),
    ('Procedure updated'), ('New protocol applied'), ('Batch accepted'),
    ('Specs are within range'), ('Data logged'), ('Repeat with new kit'),
    ('Temperature anomaly'), ('Expected range shift'), ('Confirm test method'),
    ('Quality control pass'), ('Cross-check required'), ('Specimen integrity checked'),
    ('Record findings'), ('Batch recall needed'), ('Equipment malfunction'),
    ('Review procedure'), ('Hold for further analysis'), ('Reagents replaced'),
    ('Sample condition good'), ('Awaiting confirmation'), ('Final approval pending'),
    ('Validation complete'), ('Testing phase complete'), ('Reformulation required'),
    ('Stability test needed'), ('Preparation phase'), ('Assay development'), 
    ('Quality checks in place'), ('Ready for next stage');

INSERT INTO #tempURLs (URL)
VALUES 
    ('/resources/Q1D2C3'), ('/validate/R4T5Y6'), ('/results/S7G8H9'),
    ('/batch/B2N3M4'), ('/analysis/A1S2D3'), ('/info/I4F5G6'),
    ('/data/D7H8J9'), ('/report/R1K2L3'), ('/summary/S4F5G6'),
    ('/documentation/D8J9K0'), ('/access/A1S2D3'), ('/files/F4G5H6'),
    ('/query/Q7R8S9'), ('/export/E1X2Y3'), ('/review/R4E5V6'),
    ('/download/D7O8W9'), ('/archive/A1R2C3'), ('/feedback/F4E5B6'),
    ('/logs/L7O8G9'), ('/support/S1P2O3');

INSERT INTO #activityNotes (Note)
VALUES 
    ('Analysis completed'), ('Awaiting review'), ('Data entered'), ('Samples received'),
    ('Quality check done'), ('Processing stage'), ('Sent for testing'),
    ('Results pending'), ('Batch processing'), ('Error in sample handling'),
    ('Re-analysis required'), ('Sample in quarantine'), ('Batch passed'),
    ('Awaiting more samples'), ('Under temperature control'), ('Sterilization completed'),
    ('Cross-contamination check'), ('Batch sorted'), ('Awaiting shipment'),
    ('Shipment delayed'), ('Ready for dispatch'), ('Dispatched to lab'),
    ('Received at lab'), ('Initial tests done'), ('Further testing required'),
    ('Results inconclusive'), ('Results validated'), ('Sample compromised'),
    ('Retest scheduled'), ('Retest completed'), ('Record updated'),
    ('Data analysis started'), ('Data analysis completed'), ('Report issued'),
    ('Report sent to client'), ('Client feedback received'), ('Revised after feedback'),
    ('Sample archived'), ('Archive checked'), ('Sample discarded'),
    ('Discrepancy noted'), ('Investigation started'), ('Investigation completed'),
    ('Corrective action taken'), ('Follow-up scheduled'), ('Follow-up completed'),
    ('Maintenance required'), ('Maintenance completed'), ('Calibration required'),
    ('Calibration completed');

INSERT INTO #trackingNumbers (TrackingNumber)
VALUES 
    ('TN0001'), ('TN0002'), ('TN0003'), ('TN0004'),
    ('TN0005'), ('TN0006'), ('TN0007'), ('TN0008'),
    ('TN0009'), ('TN0010'), ('TN0011'), ('TN0012'),
    ('TN0013'), ('TN0014'), ('TN0015'), ('TN0016'),
    ('TN0017'), ('TN0018'), ('TN0019'), ('TN0020'),
    ('TN0021'), ('TN0022'), ('TN0023'), ('TN0024'),
    ('TN0025'), ('TN0026'), ('TN0027'), ('TN0028'),
    ('TN0029'), ('TN0030'), ('TN0031'), ('TN0032'),
    ('TN0033'), ('TN0034'), ('TN0035'), ('TN0036'),
    ('TN0037'), ('TN0038'), ('TN0039'), ('TN0040'),
    ('TN0041'), ('TN0042'), ('TN0043'), ('TN0044'),
    ('TN0045'), ('TN0046'), ('TN0047'), ('TN0048'),
    ('TN0049'), ('TN0050');

INSERT INTO #followUpActionsRecommended (Action)
VALUES 
    ('Increase sampling frequency'), ('Update SOPs'), ('Review all related batches'),
    ('Conduct staff retraining'), ('Review equipment settings'), ('Increase QA checks'),
    ('Audit recent batches'), ('Implement new software tools'), ('Review supplier contracts'),
    ('Conduct risk assessment'), ('Update training manuals'), ('Revise quality control parameters'),
    ('Schedule additional maintenance'), ('Enhance data logging procedures'), ('Adjust calibration intervals'),
    ('Revalidate processes'), ('Improve storage conditions'), ('Expand testing protocols'),
    ('Reassess workflow integration'), ('Modify sample handling methods'), ('Update safety guidelines'),
    ('Enhance security measures'), ('Increase monitoring frequency'), ('Develop contingency plans'),
    ('Create backup systems'), ('Reinforce emergency procedures'), ('Initiate process optimization'),
    ('Improve client communication'), ('Expand team capabilities'), ('Upgrade software versions'),
    ('Strengthen network security'), ('Enhance user training'), ('Update regulatory compliance'),
    ('Reorganize management structure'), ('Reevaluate operational strategies'), ('Streamline data access'),
    ('Augment performance metrics'), ('Review disaster recovery plans'), ('Enhance system redundancy'),
    ('Upgrade hardware components'), ('Expand research and development efforts');

INSERT INTO #conclusions (Conclusion)
VALUES 
    ('All parameters met'), ('Process within specifications'), ('No deviations found'),
    ('Minor issues detected'), ('Major errors identified'), ('Compliance fully achieved'),
    ('Partial compliance observed'), ('Reevaluation recommended'), ('Process stable and reliable'),
    ('Improvements necessary'), ('No further action needed'), ('Follow-up required'),
    ('Batch exceeds expectations'), ('Batch fails to meet criteria'), ('Satisfactory outcome achieved'),
    ('Unsatisfactory performance noted'), ('Reformulation advised'), ('Redesign of process needed'),
    ('System integrity confirmed'), ('Vulnerabilities detected'), ('High performance confirmed'),
    ('Suboptimal results observed'), ('Efficiency verified'), ('Inefficiency issues present'),
    ('Technology upgrade required'), ('Outdated procedures in use'), ('Best practices followed'),
    ('Non-conformance reported'), ('Audit confirms compliance'), ('Audit reveals discrepancies'),
    ('Quality standards maintained'), ('Quality standards not met'), ('Operational excellence confirmed'),
    ('Operational flaws detected'), ('Safety standards achieved'), ('Safety concerns raised'),
    ('Environmental impact acceptable'), ('Environmental concerns detected'), ('Sustainability goals met'),
    ('Further sustainability efforts required');

INSERT INTO #projectNames (ProjectName)
VALUES
    ('Isotope Synthesis'), ('Uranium Enrichment'), ('Plutonium Refinement'),
    ('Fission Research'), ('Fusion Experiment'), ('Radiation Shielding'),
    ('Nuclear Reactor Development'), ('Neutron Activation Study'), ('Alpha Particle Emission'),
    ('Gamma Ray Detection'), ('Beta Decay Analysis'), ('Nuclear Fuel Cycle'),
    ('Radioactive Waste Management'), ('Nuclear Chain Reaction'), ('Thermal Neutron Analysis'),
    ('Fast Neutron Detection'), ('Radioisotope Production'), ('Heavy Water Reactor'),
    ('Thorium Cycle Development'), ('Nuclear Safeguards Initiative'), ('Critical Mass Evaluation'),
    ('Nuclear Containment'), ('Tritium Production'), ('Radiation Protection Program'),
    ('Decay Heat Removal'), ('Radioactive Tracer Studies'), ('Nuclear Forensics Investigation'),
    ('Nuclear Decommissioning Plan'), ('Ionization Chamber Project'), ('Particle Accelerator Experiment'),
    ('Actinide Chemistry'), ('Radiochemistry Advancement'), ('Nuclear Fusion Plasma Study'),
    ('Fission Product Analysis'), ('Reactor Safety Enhancement'), ('Neutron Flux Measurement'),
    ('Spent Fuel Management'), ('Transuranic Element Study'), ('Radiation Dose Measurement'),
    ('Fission Fragment Study'), ('Nuclear Material Accountability'), ('Gamma Spectroscopy'),
    ('Radionuclide Migration'), ('Nuclear Reactor Core Simulation'), ('Fusion Reactor Prototype'),
    ('Nuclear Reactor Neutronics'), ('Reactor Pressure Vessel Study'), ('Nuclear Reactor Coolant System'),
    ('Neutron Diffusion Modeling'), ('Nuclear Energy Policy Development'), ('Nuclear Radiation Monitoring'),
    ('Advanced Fuel Fabrication'), ('Fusion Ignition Study'), ('Nuclear Power Plant Optimization'),
    ('Nuclear Waste Repository Project'), ('Molten Salt Reactor Development'), ('Fast Breeder Reactor Study'),
    ('High-Level Waste Disposal'), ('Subcritical Reactor Design'), ('Low Enriched Uranium Initiative'),
    ('Nuclear Criticality Safety'), ('Fuel Reprocessing Strategy'), ('Uranium Isotope Separation'),
    ('Gamma Ray Spectroscopy'), ('Proton-Neutron Interaction Study'), ('Fusion Reaction Efficiency'),
    ('Decay Constant Measurement'), ('Actinide Separation Process'), ('Tritium Handling and Storage'),
    ('Plutonium Isotope Research'), ('Nuclear Transmutation Study'), ('Nuclear Reactor Control Systems'),
    ('Radioactive Contamination Analysis'), ('Nuclear Data Compilation'), ('Nuclear Reactor Design Optimization'),
    ('Accelerator-Driven System Development'), ('Fusion Energy Research');

-- Clear existing domain and evaluation data so this script is idempotent
-- and can be re-run by the nightly sandbox reset. FK-safe deletion order.
-- (STEM preview data lives in throwaway temp tables, not real tables, so it is not touched here.)
DECLARE @DeleteBatchSize INT = 1000;

WHILE 1 = 1
BEGIN
    DELETE TOP (@DeleteBatchSize) FROM [DATA].[Apm];
    IF @@ROWCOUNT = 0 BREAK;
END;

WHILE 1 = 1
BEGIN
    DELETE TOP (@DeleteBatchSize) FROM [DATA].[Particle];
    IF @@ROWCOUNT = 0 BREAK;
END;

WHILE 1 = 1
BEGIN
    DELETE TOP (@DeleteBatchSize) FROM [DATA].[SubSample];
    IF @@ROWCOUNT = 0 BREAK;
END;

WHILE 1 = 1
BEGIN
    DELETE TOP (@DeleteBatchSize) FROM [DATA].[Sample];
    IF @@ROWCOUNT = 0 BREAK;
END;

WHILE 1 = 1
BEGIN
    DELETE TOP (@DeleteBatchSize) FROM [EVALUATION].[ProjectSeries];
    IF @@ROWCOUNT = 0 BREAK;
END;

IF OBJECT_ID('[EVALUATION].[PresetFilterEntry]', 'U') IS NOT NULL
BEGIN
    WHILE 1 = 1
    BEGIN
        DELETE TOP (@DeleteBatchSize) FROM [EVALUATION].[PresetFilterEntry];
        IF @@ROWCOUNT = 0 BREAK;
    END;
END;

IF OBJECT_ID('[EVALUATION].[PresetFilter]', 'U') IS NOT NULL
BEGIN
    WHILE 1 = 1
    BEGIN
        DELETE TOP (@DeleteBatchSize) FROM [EVALUATION].[PresetFilter];
        IF @@ROWCOUNT = 0 BREAK;
    END;
END;

WHILE 1 = 1
BEGIN
    DELETE TOP (@DeleteBatchSize) FROM [EVALUATION].[Project];
    IF @@ROWCOUNT = 0 BREAK;
END;

WHILE 1 = 1
BEGIN
    DELETE TOP (@DeleteBatchSize) FROM [DATA].[Series];
    IF @@ROWCOUNT = 0 BREAK;
END;

DBCC CHECKIDENT ('[DATA].[Series]', RESEED, 9999);
DBCC CHECKIDENT ('[DATA].[Sample]', RESEED, 0);
DBCC CHECKIDENT ('[DATA].[SubSample]', RESEED, 0);
DBCC CHECKIDENT ('[DATA].[Particle]', RESEED, 0);
DBCC CHECKIDENT ('[DATA].[Apm]', RESEED, 0);
GO

-- Seed Series
DECLARE @SeriesTarget INT = 100000;
DECLARE @SeedBatchSize INT = 1000;
DECLARE @SeriesOffset INT = 0;

WHILE @SeriesOffset < @SeriesTarget
BEGIN
    INSERT INTO [DATA].[Series] (
         [SeriesType]
        ,[CreatedAt]
        ,[SgasComment]
        ,[IsDu]
        ,[WorkingPaperLink]
        ,[IsNu]
        ,[AnalysisCompleteDate])
    SELECT
         CAST(ABS(CHECKSUM(NEWID())) % 4 + 1 AS TINYINT)
        ,DATEADD(day, CAST(ABS(CHECKSUM(NEWID())) % 20 - 10 AS INT), GETDATE())
        ,(SELECT Comment FROM #tempComments WHERE ID = (numbers.n % (SELECT MAX(ID) FROM #tempComments)) + 1)
        ,CAST(numbers.n % 2 AS BIT)
        ,(SELECT URL FROM #tempURLs WHERE ID = (numbers.n % (SELECT MAX(ID) FROM #tempURLs)) + 1)
        ,CAST((numbers.n + 1) % 2 AS BIT)
        ,CASE WHEN numbers.n % 3 = 0 THEN DATEADD(day, CAST(ABS(CHECKSUM(NEWID())) % 365 - 182 AS INT), GETDATE()) ELSE NULL END
    FROM (
        SELECT @SeriesOffset + n AS n
        FROM #seedNumbers1000
        WHERE n <= CASE
            WHEN @SeriesTarget - @SeriesOffset < @SeedBatchSize THEN @SeriesTarget - @SeriesOffset
            ELSE @SeedBatchSize
        END
    ) AS numbers;

    SET @SeriesOffset += @SeedBatchSize;
END;
GO

-- Seed Samples
DECLARE @ParentBatchSize INT = 1000;
DECLARE @SeriesStartId INT;
DECLARE @SeriesMaxId INT;

SELECT
    @SeriesStartId = MIN([Id]),
    @SeriesMaxId = MAX([Id])
FROM [DATA].[Series];

WHILE @SeriesStartId IS NOT NULL AND @SeriesStartId <= @SeriesMaxId
BEGIN
    ;WITH numbers AS (
        SELECT n
        FROM #seedNumbers1000
        WHERE n <= 5
    ),
    seriesSamples AS (
        SELECT
             [s].[Id] AS SeriesId
            ,CAST(ABS(CHECKSUM(NEWID())) % 5 + 1 AS INT) AS SampleCount
        FROM [DATA].[Series] s
        WHERE [s].[Id] >= @SeriesStartId
          AND [s].[Id] < @SeriesStartId + @ParentBatchSize
    ),
    expandedSeriesSamples AS (
        SELECT
             [ss].SeriesId
            ,n.n AS SampleIndex
        FROM seriesSamples ss
        JOIN numbers n ON n.n <= ss.SampleCount
    )
    INSERT INTO [DATA].[Sample] (
        [SeriesId],
        [ExternalCode],
        [SamplingDate],
        [SampleClass],
        [Latitude],
        [Longitude]
    )
    SELECT
         [ess].SeriesId
        ,RIGHT('EX' + CAST(NEWID() AS NVARCHAR(MAX)), 3)
        ,DATEADD(day, CAST(ABS(CHECKSUM(NEWID())) % 20 - 10 AS INT), GETDATE())
        ,CASE
            WHEN ABS(CHECKSUM(NEWID())) % 100 < 30 THEN 'pic' + LEFT(CONVERT(VARCHAR(36), NEWID()), 2)
            WHEN ABS(CHECKSUM(NEWID())) % 100 < 70 THEN LEFT(CONVERT(VARCHAR(36), NEWID()), 3) + 'qc'
            ELSE LEFT(CONVERT(VARCHAR(36), NEWID()), 5)
         END
        ,CASE WHEN ABS(CHECKSUM(NEWID())) % 10 = 0 THEN NULL ELSE CAST(-90 + (180 * RAND(CHECKSUM(NEWID()))) AS DECIMAL(11,8)) END
        ,CASE WHEN ABS(CHECKSUM(NEWID())) % 10 = 0 THEN NULL ELSE CAST(-180 + (360 * RAND(CHECKSUM(NEWID()))) AS DECIMAL(11,8)) END
    FROM
        expandedSeriesSamples ess;

    SET @SeriesStartId += @ParentBatchSize;
END;
GO

-- Seed SubSamples
DECLARE @ParentBatchSize INT = 1000;
DECLARE @SampleStartId INT;
DECLARE @SampleMaxId INT;

SELECT
    @SampleStartId = MIN([Id]),
    @SampleMaxId = MAX([Id])
FROM [DATA].[Sample];

WHILE @SampleStartId IS NOT NULL AND @SampleStartId <= @SampleMaxId
BEGIN
    ;WITH numbers AS (
        SELECT n
        FROM #seedNumbers1000
        WHERE n <= 5
    ),
    sampleSubSamples AS (
        SELECT
             [s].[Id] AS SampleId
            ,CAST(ABS(CHECKSUM(NEWID())) % 3 + 1 AS INT) AS SubSampleCount
        FROM [DATA].[Sample] s
        WHERE [s].[Id] >= @SampleStartId
          AND [s].[Id] < @SampleStartId + @ParentBatchSize
    ),
    expandedSubSamples AS (
        SELECT
             [ss].SampleId
            ,n.n AS SubSampleIndex
        FROM sampleSubSamples ss
        JOIN numbers n ON n.n <= ss.SubSampleCount
    )
    INSERT INTO [DATA].[SubSample] (
         [SampleId]
        ,[ExternalCode]
        ,[ScreeningDate]
        ,[IsFromLegacySystem]
        ,[UploadResultDate]
        ,[ActivityNotes]
        ,[TrackingNumber])
    SELECT
         [ess].SampleId
        ,RIGHT('SX' + CAST(NEWID() AS NVARCHAR(MAX)), 3)
        ,DATEADD(day, CAST(ABS(CHECKSUM(NEWID())) % 20 - 10 AS INT), GETDATE())
        ,CAST(CAST(ABS(CHECKSUM(NEWID())) % 2 AS INT) AS BIT)
        ,CASE WHEN CAST(ABS(CHECKSUM(NEWID())) % 10 AS INT) > 1 THEN DATEADD(day, CAST(ABS(CHECKSUM(NEWID())) % 30 - 15 AS INT), GETDATE()) ELSE NULL END
        ,(SELECT Note FROM #activityNotes WHERE ID = (CAST(ess.SampleId AS INT) % (SELECT COUNT(*) FROM #activityNotes)) + 1)
        ,(SELECT TrackingNumber FROM #trackingNumbers WHERE ID = (CAST(ess.SampleId AS INT) % (SELECT COUNT(*) FROM #trackingNumbers)) + 1)
    FROM
        expandedSubSamples ess;

    SET @SampleStartId += @ParentBatchSize;
END;
GO

-- Seed Particle
DECLARE @ParentBatchSize INT = 1000;
DECLARE @SubSampleStartId INT;
DECLARE @SubSampleMaxId INT;

SELECT
    @SubSampleStartId = MIN([Id]),
    @SubSampleMaxId = MAX([Id])
FROM [DATA].[SubSample];

WHILE @SubSampleStartId IS NOT NULL AND @SubSampleStartId <= @SubSampleMaxId
BEGIN
    ;WITH numbers AS (
        SELECT n
        FROM #seedNumbers1000
        WHERE n <= 5
    ),
    sampleSubSamples AS (
        SELECT
             [s].[Id] AS SubSampleId
            ,CAST(ABS(CHECKSUM(NEWID())) % 3 + 1 AS INT) AS ParticleCount
        FROM [DATA].[SubSample] s
        WHERE [s].[Id] >= @SubSampleStartId
          AND [s].[Id] < @SubSampleStartId + @ParentBatchSize
    ),
    expandedParticles AS (
        SELECT
             [ss].SubSampleId
            ,n.n AS ParticleIndex
        FROM sampleSubSamples ss
        JOIN numbers n ON n.n <= ss.ParticleCount
    )
    INSERT INTO [DATA].[Particle] (
         [SubSampleId],
         [ParticleExternalId],
         [AnalysisDate],
         [IsNu],
         [LaboratoryCode],
         [U234],
         [ErU234],
         [U235],
         [ErU235],
         [Comment]
    )
    SELECT
         essp.SubSampleId,
         CAST((RAND(CHECKSUM(NEWID())) * 3200.23) AS DECIMAL(10,2)),
         DATEADD(day, CAST(RAND(CHECKSUM(NEWID())) * 30 - 15 AS INT), GETDATE()),
	     CAST((essp.SubSampleId + 1) % 2 AS BIT),
         LEFT(NEWID(), 10),
         CASE WHEN RAND(CHECKSUM(NEWID())) < 0.8 THEN CAST(RAND(CHECKSUM(NEWID())) * 10 AS DECIMAL(38,15)) ELSE NULL END,
         CASE WHEN RAND(CHECKSUM(NEWID())) < 0.8 THEN CAST(RAND(CHECKSUM(NEWID())) * 1 AS DECIMAL(38,15)) ELSE NULL END,
         CASE WHEN RAND(CHECKSUM(NEWID())) < 0.8 THEN CAST(RAND(CHECKSUM(NEWID())) * 10 AS DECIMAL(38,15)) ELSE NULL END,
         CASE WHEN RAND(CHECKSUM(NEWID())) < 0.8 THEN CAST(RAND(CHECKSUM(NEWID())) * 1 AS DECIMAL(38,15)) ELSE NULL END,
         (SELECT Comment FROM #tempComments WHERE ID = (essp.SubSampleId % (SELECT MAX(ID) FROM #tempComments)) + 1)
    FROM
        expandedParticles essp;

    SET @SubSampleStartId += @ParentBatchSize;
END;
GO

-- Seed APM
DECLARE @ParentBatchSize INT = 1000;
DECLARE @SubSampleStartId INT;
DECLARE @SubSampleMaxId INT;

SELECT
    @SubSampleStartId = MIN([Id]),
    @SubSampleMaxId = MAX([Id])
FROM [DATA].[SubSample];

WHILE @SubSampleStartId IS NOT NULL AND @SubSampleStartId <= @SubSampleMaxId
BEGIN
    ;WITH numbers AS (
        SELECT n
        FROM #seedNumbers1000
        WHERE n <= 5
    ),
    sampleSubSamples AS (
        SELECT
             [s].[Id] AS SubSampleId
            ,CAST(ABS(CHECKSUM(NEWID())) % 5 + 1 AS INT) AS ParticleCount
        FROM [DATA].[SubSample] s
        WHERE [s].[Id] >= @SubSampleStartId
          AND [s].[Id] < @SubSampleStartId + @ParentBatchSize
    ),
    expandedParticles AS (
        SELECT
             [ss].SubSampleId
            ,n.n AS ParticleIndex
        FROM sampleSubSamples ss
        JOIN numbers n ON n.n <= ss.ParticleCount
    )
    INSERT INTO [DATA].[Apm] (
        [SubSampleId],
        [U234],
        [ErU234],
        [U235],
        [ErU235],
        [U236],
        [ErU236],
        [U238],
        [ErU238],
        [Comment]
    )
    SELECT
        essapm.SubSampleId,
        CASE WHEN RAND(CHECKSUM(NEWID())) < 0.8 THEN CAST(RAND(CHECKSUM(NEWID())) * 10 AS DECIMAL(38,15)) ELSE NULL END,
        CASE WHEN RAND(CHECKSUM(NEWID())) < 0.8 THEN CAST(RAND(CHECKSUM(NEWID())) * 1 AS DECIMAL(38,15)) ELSE NULL END,
        CASE WHEN RAND(CHECKSUM(NEWID())) < 0.8 THEN CAST(RAND(CHECKSUM(NEWID())) * 10 AS DECIMAL(38,15)) ELSE NULL END,
        CASE WHEN RAND(CHECKSUM(NEWID())) < 0.8 THEN CAST(RAND(CHECKSUM(NEWID())) * 1 AS DECIMAL(38,15)) ELSE NULL END,
        CASE WHEN RAND(CHECKSUM(NEWID())) < 0.8 THEN CAST(RAND(CHECKSUM(NEWID())) * 10 AS DECIMAL(38,15)) ELSE NULL END,
        CASE WHEN RAND(CHECKSUM(NEWID())) < 0.8 THEN CAST(RAND(CHECKSUM(NEWID())) * 1 AS DECIMAL(38,15)) ELSE NULL END,
        CASE WHEN RAND(CHECKSUM(NEWID())) < 0.8 THEN CAST(RAND(CHECKSUM(NEWID())) * 10 AS DECIMAL(38,15)) ELSE NULL END,
        CASE WHEN RAND(CHECKSUM(NEWID())) < 0.8 THEN CAST(RAND(CHECKSUM(NEWID())) * 1 AS DECIMAL(38,15)) ELSE NULL END,
        (SELECT Comment FROM #tempComments WHERE ID = (essapm.SubSampleId % (SELECT MAX(ID) FROM #tempComments)) + 1)
    FROM
        expandedParticles essapm;

    SET @SubSampleStartId += @ParentBatchSize;
END;
GO

-- Seed Project
DECLARE @ProjectTarget INT = 33333;
DECLARE @SeedBatchSize INT = 1000;
DECLARE @ProjectOffset INT = 0;

SET IDENTITY_INSERT [EVALUATION].[Project] ON;

WHILE @ProjectOffset < @ProjectTarget
BEGIN
    INSERT INTO [EVALUATION].[Project] (
         [Id]
        ,[Name]
        ,[Conclusions]
        ,[FollowUpActionsRecommended]
        ,[CreatedAt]
        ,[UpdatedAt]
    )
    SELECT
         numbers.n
        ,((SELECT ProjectName FROM #projectNames WHERE ID = (numbers.n % (SELECT MAX(ID) FROM #projectNames)) + 1) + ' ' + LEFT(CONVERT(NVARCHAR(36), NEWID()), 7))
        ,(SELECT Conclusion FROM #conclusions WHERE ID = (numbers.n % (SELECT MAX(ID) FROM #conclusions)) + 1)
        ,(SELECT [Action] FROM #followUpActionsRecommended WHERE ID = (numbers.n % (SELECT MAX(ID) FROM #followUpActionsRecommended)) + 1)
        ,DATEADD(day, CAST(ABS(CHECKSUM(NEWID())) % 20 - 10 AS INT), GETDATE()) AS [CreatedAt]
        ,DATEADD(day, CAST(ABS(CHECKSUM(NEWID())) % 20 - 10 AS INT), GETDATE()) AS [UpdatedAt]
    FROM (
        SELECT @ProjectOffset + n AS n
        FROM #seedNumbers1000
        WHERE n <= CASE
            WHEN @ProjectTarget - @ProjectOffset < @SeedBatchSize THEN @ProjectTarget - @ProjectOffset
            ELSE @SeedBatchSize
        END
    ) AS numbers;

    SET @ProjectOffset += @SeedBatchSize;
END;

SET IDENTITY_INSERT [EVALUATION].[Project] OFF;

DBCC CHECKIDENT ('[EVALUATION].[Project]', RESEED, 33333);
GO

-- Seed ProjectSeries
CREATE TABLE #projectIds
(
    ProjectNumber INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    ProjectId INT NOT NULL
);

INSERT INTO #projectIds (ProjectId)
SELECT [Id]
FROM [EVALUATION].[Project]
ORDER BY [Id];

DECLARE @ProjectCount INT = (SELECT COUNT(*) FROM #projectIds);
DECLARE @ParentBatchSize INT = 1000;
DECLARE @SeriesStartId INT;
DECLARE @SeriesMinId INT;
DECLARE @SeriesMaxId INT;

SELECT
    @SeriesStartId = MIN([Id]),
    @SeriesMinId = MIN([Id]),
    @SeriesMaxId = MAX([Id])
FROM [DATA].[Series];

WHILE @ProjectCount > 0 AND @SeriesStartId IS NOT NULL AND @SeriesStartId <= @SeriesMaxId
BEGIN
    ;WITH cte AS (
        SELECT
            [s].[Id] AS [SeriesId],
            (([s].[Id] - @SeriesMinId) % @ProjectCount) + 1 AS ProjectNumber
        FROM [DATA].[Series] AS [s]
        WHERE [s].[Id] >= @SeriesStartId
          AND [s].[Id] < @SeriesStartId + @ParentBatchSize
    )
    INSERT INTO [EVALUATION].ProjectSeries (ProjectId, SeriesId)
    SELECT [p].[ProjectId], [c].[SeriesId]
    FROM cte AS [c]
    INNER JOIN #projectIds AS [p]
        ON [p].[ProjectNumber] = [c].[ProjectNumber];

    SET @SeriesStartId += @ParentBatchSize;
END;
GO

-- Best-effort database tuning. These are environment-specific (and may be disallowed on
-- shared hosting), so they are wrapped so a failure can never abort the rest of the script
-- (which also defines the temp-table index proc the STEM preview feature depends on).
BEGIN TRY
    IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = DB_NAME() AND snapshot_isolation_state = 1)
        ALTER DATABASE CURRENT SET ALLOW_SNAPSHOT_ISOLATION ON;
END TRY
BEGIN CATCH END CATCH;
GO

BEGIN TRY
    IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = DB_NAME() AND is_read_committed_snapshot_on = 1)
        ALTER DATABASE CURRENT SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;
END TRY
BEGIN CATCH END CATCH;
GO

BEGIN TRY
    DECLARE @recoverySql NVARCHAR(MAX) = N'ALTER DATABASE ' + QUOTENAME(DB_NAME()) + N' SET RECOVERY SIMPLE;';
    EXEC sp_executesql @recoverySql;
END TRY
BEGIN CATCH END CATCH;
GO

CREATE OR ALTER PROCEDURE [DBO].EnsureIndexOnTempTableField
    @tableName NVARCHAR(128),
    @fieldName NVARCHAR(128)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @tempIndexName NVARCHAR(128) = 'IDX_' + @fieldName + '_On_' + REPLACE(@tableName, '##', '');
    DECLARE @dbName NVARCHAR(128) = 'tempdb';
    DECLARE @sql NVARCHAR(MAX);

    IF NOT EXISTS (
        SELECT 1
        FROM [TEMPDB].SYS.TABLES AS t
        WHERE t.name = @tableName
          AND t.is_ms_shipped = 0
    )
    BEGIN
        RAISERROR(N'The specified table ''%s'' does not exist in tempdb.', 16, 1, @tableName);
        RETURN;
    END

    IF NOT EXISTS (
        SELECT 1
        FROM [TEMPDB].SYS.COLUMNS AS c
        INNER JOIN [TEMPDB].SYS.TABLES AS t ON c.object_id = t.object_id
        WHERE t.name = @tableName
          AND c.name = @fieldName
    )
    BEGIN
        RAISERROR(N'The specified field ''%s'' does not exist in the table ''%s''.', 16, 1, @fieldName, @tableName);
        RETURN;
    END

    IF NOT EXISTS (
        SELECT 1
        FROM [TEMPDB].SYS.INDEXES AS i
        INNER JOIN [TEMPDB].SYS.INDEX_COLUMNS AS ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
        INNER JOIN [TEMPDB].SYS.TABLES AS t ON i.object_id = t.object_id
        INNER JOIN [TEMPDB].SYS.COLUMNS AS c ON ic.column_id = c.column_id AND t.object_id = c.object_id
        WHERE t.name = @tableName
          AND c.name = @fieldName
          AND i.name = @tempIndexName
    )
    BEGIN
        SET @sql = 'CREATE NONCLUSTERED INDEX ' + QUOTENAME(@tempIndexName) +
                   ' ON [TEMPDB].[DBO].' + QUOTENAME(@tableName) + ' (' + QUOTENAME(@fieldName) + ')';
        EXEC sp_executesql @sql;
    END
END;

GO

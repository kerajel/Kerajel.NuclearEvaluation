using System.Data.Common;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Kerajel.Primitives.Models;
using LinqToDB.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.Core;
using NuclearEvaluation.Kernel.Commands;
using NuclearEvaluation.Kernel.Models.DataManagement.Stem;
using NuclearEvaluation.Server.Controllers;
using NuclearEvaluation.Server.Interfaces.STEM;
using NuclearEvaluation.Server.Services.EFS;
using NuclearEvaluation.Server.Services.Sandbox;
using NuclearEvaluation.Server.Services.STEM;
using NuclearEvaluation.Shared.Contracts;
using NuclearEvaluation.Shared.Models.Views;
using Radzen;
using FileInfo = System.IO.FileInfo;

namespace NuclearEvaluation.Server.Tests;

public class StemPreviewDuckDbTests
{
    [Fact]
    public async Task UploadsCanBeFilteredSortedAndPagedAcrossFiles()
    {
        using UploadFiles files = new();
        await using StemSessionManager sessions = files.CreateSessions();
        StemPreviewEntryService entries = new(sessions, new StemPreviewParser());
        StemPreviewService uploads = new(entries, NullLogger<StemPreviewService>.Instance);
        Guid session = Guid.NewGuid();
        Guid firstFile = Guid.NewGuid();
        Guid secondFile = Guid.NewGuid();

        await UploadSuccessfully(
            uploads,
            session,
            firstFile,
            "first.csv",
            """
            42.25,LAB-A,46025,1,2.123456789012345
            43,LAB-A,2026-01-01,false,9
            44,LAB-A,2026-01-03,false,
            """
        );
        string firstRawPath = Assert.Single(RawFiles(sessions, ".csv"));
        string unavailablePath = firstRawPath + ".unavailable";
        File.Move(firstRawPath, unavailablePath);
        try
        {
            await UploadSuccessfully(
                uploads,
                session,
                secondFile,
                "second.csv",
                """
                42.25,LAB-B,2026-01-02,false,3
                45,OTHER,2026-01-04,true,6
                """
            );
            await UploadSuccessfully(
                uploads,
                session,
                secondFile,
                "retry.csv",
                "46,LAB-B,2026-01-04,true,1"
            );
            // The second upload neither replayed the first file nor appended the retry.
            Assert.Equal(5, (await Query(entries, session)).TotalCount);
        }
        finally
        {
            File.Move(unavailablePath, firstRawPath);
        }

        string filter = Filter(
            (nameof(StemPreviewEntryView.LabCode), FilterOperator.Contains, "lab-"),
            (
                nameof(StemPreviewEntryView.AnalysisDate),
                FilterOperator.GreaterThanOrEquals,
                new DateOnly(2026, 1, 2)
            ),
            (nameof(StemPreviewEntryView.U234), FilterOperator.GreaterThan, 2m)
        );
        DataQuery page = new()
        {
            Filter = filter,
            OrderBy = "Id asc, AnalysisDate desc",
            Top = 1,
        };
        FetchDataResult<StemPreviewEntryView> first = await Query(entries, session, page);
        Assert.Equal(2, first.TotalCount);
        StemPreviewEntryView row = Assert.Single(first.Entries);
        Assert.Equal(
            (42.25m, "LAB-A", new DateOnly(2026, 1, 3), true),
            (row.Id, row.LabCode, row.AnalysisDate, row.IsNu)
        );
        Assert.Equal(
            (firstFile, "first.csv", 2.123456789012345m),
            (row.FileId, row.FileName, row.U234!.Value)
        );

        page.Skip = 1;
        FetchDataResult<StemPreviewEntryView> second = await Query(entries, session, page);
        Assert.Equal(2, second.TotalCount);
        Assert.Equal(secondFile, Assert.Single(second.Entries).FileId);

        FetchDataResult<StemPreviewEntryView> nulls = await Query(
            entries,
            session,
            new()
            {
                Filter = Filter((nameof(StemPreviewEntryView.U234), FilterOperator.IsNull, null)),
            }
        );
        Assert.Equal(44m, Assert.Single(nulls.Entries).Id);

        Assert.True((await uploads.DeleteFileData(session, firstFile)).IsSuccessful);
        Assert.False(File.Exists(firstRawPath));
        Assert.Single(RawFiles(sessions, ".csv"));
        FetchDataResult<StemPreviewEntryView> afterDeletion = await Query(entries, session);
        Assert.Equal(2, afterDeletion.TotalCount);
        Assert.All(afterDeletion.Entries, entry => Assert.Equal(secondFile, entry.FileId));
    }

    [Fact]
    public async Task NativeDecimalFastPathsPreservePrecisionAndLegacyNumberFormats()
    {
        using UploadFiles files = new();
        await using StemSessionManager sessions = files.CreateSessions();
        IStemPreviewParser compatibilityParser = Substitute.For<IStemPreviewParser>();
        StemPreviewEntryService entries = new(sessions, compatibilityParser);
        StemPreviewService uploads = new(entries, NullLogger<StemPreviewService>.Instance);
        Guid session = Guid.NewGuid();

        await UploadSuccessfully(
            uploads,
            session,
            Guid.NewGuid(),
            "decimals.csv",
            """
            1,LAB-A,2026-01-01,true,0.1234567890123456
            2,LAB-A,2026-01-01,true,123456.1234567890
            3,LAB-A,2026-01-01,true,999.9999999999999995
            4,LAB-A,2026-01-01,true,99999999.9999999999
            5,LAB-A,2026-01-01,true,100000000.123456789012345
            6,LAB-A,2026-01-01,true,"1,234.123456789012345-"
            7,LAB-A,2026-01-01,true,
            """
        );

        Assert.Empty(compatibilityParser.ReceivedCalls());
        Assert.Equal(
            [
                0.123456789012346m,
                123456.123456789m,
                1000m,
                99999999.9999999999m,
                100000000.123456789012345m,
                -1234.123456789012345m,
                null,
            ],
            (await Query(entries, session)).Entries.Select(row => row.U234)
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MultipartUploadLogsOneSafeSummaryEvenWhenHttp200ContainsFailure(bool fail)
    {
        const string fileName = "private-workbook.csv";
        const string labCode = "private-lab-code";
        ILogger<StemUploadLog> logger = Substitute.For<
            ILogger<StemUploadLog>,
            ILogger<StemPreviewService>
        >();
        IStemPreviewEntryService entries = Substitute.For<IStemPreviewEntryService>();
        if (fail)
        {
            entries
                .AppendFileData(
                    Arg.Any<Guid>(),
                    Arg.Any<Guid>(),
                    fileName,
                    Arg.Any<Stream>(),
                    Arg.Any<CancellationToken>()
                )
                .Returns(
                    Task.FromException(
                        new FormatException($"Invalid value {labCode} in {fileName}")
                    )
                );
        }
        StemPreviewService uploads = new(entries, (ILogger<StemPreviewService>)logger);
        IStorageQuotaService quota = Substitute.For<IStorageQuotaService>();
        quota.CanAccept(Arg.Any<long>()).Returns(true);
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddControllers().AddApplicationPart(typeof(StemController).Assembly);
        builder.Services.AddSingleton<IStemPreviewService>(uploads);
        builder.Services.AddSingleton(quota);
        builder.Services.AddSingleton(logger);
        builder.Services.AddScoped<StemUploadLog>();
        await using WebApplication app = builder.Build();
        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        IServiceProvider services = scope.ServiceProvider;
        ControllerActionDescriptor upload = services
            .GetRequiredService<IActionDescriptorCollectionProvider>()
            .ActionDescriptors.Items.OfType<ControllerActionDescriptor>()
            .Single(action =>
                action.ControllerTypeInfo.AsType() == typeof(StemController)
                && action.ActionName == nameof(StemController.Upload)
            );

        Guid fileId = Guid.NewGuid();
        byte[] csv = Encoding.UTF8.GetBytes(
            $"Identifier,LaboratoryCode,AnalysisDate,IsNu\n42,{labCode},2026-01-03,true\n"
        );
        using MultipartFormDataContent multipart = new();
        multipart.Add(new StringContent(fileId.ToString()), "fileId");
        multipart.Add(new ByteArrayContent(csv), "file", fileName);
        using MemoryStream body = new(await multipart.ReadAsByteArrayAsync());
        using MemoryStream response = new();
        DefaultHttpContext http = new() { RequestServices = services };
        http.Request.Method = HttpMethods.Post;
        http.Request.ContentType = multipart.Headers.ContentType!.ToString();
        http.Request.ContentLength = body.Length;
        http.Request.Body = body;
        http.Response.Body = response;
        RouteData route = new();
        route.Values["sessionId"] = Guid.NewGuid().ToString();

        IActionInvokerFactory invokers = services.GetRequiredService<IActionInvokerFactory>();
        await invokers.CreateInvoker(new ActionContext(http, route, upload))!.InvokeAsync();

        response.Position = 0;
        OperationOutcome envelope = Assert.IsType<OperationOutcome>(
            await JsonSerializer.DeserializeAsync<OperationOutcome>(
                response,
                JsonSerializerOptions.Web
            )
        );
        Assert.Equal(!fail, envelope.IsSuccessful);
        Assert.Equal(StatusCodes.Status200OK, http.Response.StatusCode);
        ICall log = Assert.Single(logger.ReceivedCalls(), call =>
            call.GetMethodInfo().Name == nameof(ILogger.Log)
        );
        object?[] arguments = log.GetArguments();
        Assert.Equal(
            fail ? LogLevel.Warning : LogLevel.Information,
            Assert.IsType<LogLevel>(arguments[0])
        );
        Assert.Null(arguments[3]);
        IReadOnlyList<KeyValuePair<string, object?>> state = Assert.IsAssignableFrom<
            IReadOnlyList<KeyValuePair<string, object?>>
        >(arguments[2]);
        Dictionary<string, object?> fields = state.ToDictionary(StringComparer.Ordinal);
        Assert.Equal(fail ? "failed" : "succeeded", fields["Outcome"]);
        Assert.Equal(StatusCodes.Status200OK, Assert.IsType<int>(fields["StatusCode"]));
        Assert.Equal(fileId, Assert.IsType<Guid>(fields["FileId"]));
        Assert.Equal(csv.LongLength, Assert.IsType<long>(fields["FileBytes"]));
        if (fail)
            Assert.Equal(nameof(FormatException), fields["ErrorKind"]);
        Assert.True(Assert.IsType<double>(fields["MvcElapsedMs"]) >= 0);
        string? message = state.ToString();
        Assert.DoesNotContain(fileName, message);
        Assert.DoesNotContain(labCode, message);
    }

    [Fact]
    public async Task ClosingTheConnectionRebuildsTheSingleTempTableFromTheRetainedFiles()
    {
        using UploadFiles files = new();
        await using StemSessionManager sessions = files.CreateSessions();
        StemPreviewEntryService entries = new(sessions, new StemPreviewParser());
        StemPreviewService uploads = new(entries, NullLogger<StemPreviewService>.Instance);
        Guid session = Guid.NewGuid();
        Guid firstFile = Guid.NewGuid();
        Guid secondFile = Guid.NewGuid();
        await UploadSuccessfully(
            uploads,
            session,
            firstFile,
            "first.csv",
            "41,LAB-A,2026-01-01,true,1"
        );
        await UploadSuccessfully(
            uploads,
            session,
            secondFile,
            "second.csv",
            "42,LAB-B,2026-01-02,false,2"
        );
        string[] retained = RawFiles(sessions, ".csv").Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(2, retained.Length);

        StemSession live = Assert.IsType<StemSession>(sessions.TryGet(session));
        FieldInfo? databaseField = typeof(StemSession).GetField(
            "_database",
            BindingFlags.Instance | BindingFlags.NonPublic
        );
        Assert.NotNull(databaseField);
        DataConnection database = Assert.IsType<DataConnection>(databaseField.GetValue(live));
        DbConnection? connection = database.TryGetDbConnection();
        Assert.NotNull(connection);

        await using (DbCommand command = connection.CreateCommand())
        {
            command.CommandText =
                "SELECT table_name, temporary FROM duckdb_tables() WHERE NOT internal";
            await using DbDataReader tables = await command.ExecuteReaderAsync();
            Assert.True(await tables.ReadAsync());
            Assert.Equal("stem_entries", tables.GetString(0));
            Assert.True(tables.GetBoolean(1));
            Assert.False(await tables.ReadAsync());
        }
        await connection.CloseAsync();

        FetchDataResult<StemPreviewEntryView> rebuilt = await Query(entries, session);
        Assert.Equal(2, rebuilt.TotalCount);
        Assert.Equal([firstFile, secondFile], rebuilt.Entries.Select(row => row.FileId));
        Assert.NotSame(database, databaseField.GetValue(live));
        Assert.Equal(retained, RawFiles(sessions, ".csv").Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task ModernExcelUsesTheNativeReaderAndKeepsTheOriginalWorkbook()
    {
        using UploadFiles files = new();
        await using StemSessionManager sessions = files.CreateSessions();
        IStemPreviewParser legacyParser = Substitute.For<IStemPreviewParser>();
        StemPreviewEntryService entries = new(sessions, legacyParser);
        StemPreviewService uploads = new(entries, NullLogger<StemPreviewService>.Instance);
        Guid session = Guid.NewGuid();
        using MemoryStream workbook = CreateWorkbook();

        OperationResult result = await uploads.UploadStemPreviewFile(
            session,
            workbook,
            Guid.NewGuid(),
            "sample.xlsx"
        );

        Assert.True(result.IsSuccessful, result.Exception?.ToString());
        Assert.Empty(legacyParser.ReceivedCalls());
        string rawPath = Assert.Single(RawFiles(sessions, ".xlsx"));
        Assert.Equal(workbook.ToArray(), File.ReadAllBytes(rawPath));
        Assert.Collection(
            (await Query(entries, session)).Entries,
            row =>
            {
                Assert.Equal(42.25m, row.Id);
                Assert.Equal("001", row.LabCode);
                Assert.Equal(new DateOnly(2026, 1, 3), row.AnalysisDate);
                Assert.True(row.IsNu);
                Assert.Equal(2.125m, row.U234);
            },
            row =>
            {
                Assert.Equal(43m, row.Id);
                Assert.Equal(new DateOnly(2026, 1, 4), row.AnalysisDate);
                Assert.False(row.IsNu);
                Assert.Null(row.U234);
            }
        );
    }

    [Theory]
    [InlineData("sample.tsv", "\t")]
    [InlineData("sample.psv", "|")]
    [InlineData("sample.dat", ";")]
    public async Task NativeTextReaderKeepsTheExistingDelimiterSupport(
        string fileName,
        string separator
    )
    {
        using UploadFiles files = new();
        await using StemSessionManager sessions = files.CreateSessions();
        IStemPreviewParser legacyParser = Substitute.For<IStemPreviewParser>();
        StemPreviewEntryService entries = new(sessions, legacyParser);
        StemPreviewService uploads = new(entries, NullLogger<StemPreviewService>.Instance);
        Guid session = Guid.NewGuid();
        string header = string.Join(
            separator,
            "Identifier",
            "LaboratoryCode",
            "AnalysisDate",
            "IsNu"
        );
        string line = string.Join(separator, "41", "001", "2026-01-03", "true");
        using MemoryStream stream = new(Encoding.UTF8.GetBytes($"{header}\n{line}\n"));

        OperationResult result = await uploads.UploadStemPreviewFile(
            session,
            stream,
            Guid.NewGuid(),
            fileName
        );

        Assert.True(result.IsSuccessful, result.Exception?.ToString());
        Assert.Empty(legacyParser.ReceivedCalls());
        StemPreviewEntryView row = Assert.Single((await Query(entries, session)).Entries);
        Assert.Equal(41m, row.Id);
        Assert.Equal("001", row.LabCode);
        Assert.Null(row.U234);
    }

    [Fact]
    public async Task FailedUploadsAreRolledBackAndSessionsStayIsolated()
    {
        using UploadFiles files = new();
        await using StemSessionManager sessions = files.CreateSessions();
        StemPreviewEntryService entries = new(sessions, new StemPreviewParser());
        StemPreviewService uploads = new(entries, NullLogger<StemPreviewService>.Instance);
        Guid session = Guid.NewGuid();
        Guid otherSession = Guid.NewGuid();

        Guid goodFile = Guid.NewGuid();
        await UploadSuccessfully(
            uploads,
            session,
            goodFile,
            "good.csv",
            "77,LAB-C,2026-01-01,true,1"
        );
        await UploadSuccessfully(
            uploads,
            otherSession,
            goodFile,
            "elsewhere.csv",
            "77,LAB-D,2026-01-01,false,1"
        );
        OperationResult malformed = await Upload(
            uploads,
            session,
            Guid.NewGuid(),
            "bad.csv",
            """
            78,LAB-C,2026-01-01,false,2
            79,LAB-C,not-a-date,false,3
            """
        );
        Assert.False(malformed.IsSuccessful);
        Assert.Equal(2, RawFiles(sessions, ".csv").Length);

        Assert.Equal("good.csv", Assert.Single((await Query(entries, session)).Entries).FileName);
        Assert.Equal(
            "elsewhere.csv",
            Assert.Single((await Query(entries, otherSession)).Entries).FileName
        );
        await sessions.EvictAsync(session);
        Assert.Equal(0, (await Query(entries, session)).TotalCount);
        Assert.Equal(1, (await Query(entries, otherSession)).TotalCount);
        Assert.Single(RawFiles(sessions, ".csv"));
    }

    [Fact]
    public async Task CanceledAppendDoesNotLeaveRowsForRetry()
    {
        using UploadFiles files = new();
        await using StemSessionManager sessions = files.CreateSessions();
        IStemPreviewParser parser = Substitute.For<IStemPreviewParser>();
        StemPreviewEntryService entries = new(sessions, parser);
        Guid session = Guid.NewGuid();
        Guid file = Guid.NewGuid();

        using CancellationTokenSource cancel = new();
        parser
            .Parse(Arg.Any<Stream>(), "retry.xls", cancel.Token)
            .Returns(_ => Rows(81, cancel.Cancel, cancel.Token));
        using MemoryStream first = new([1]);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            entries.AppendFileData(session, file, "retry.xls", first, cancel.Token)
        );
        Assert.Empty(RawFiles(sessions, ".xls"));
        Assert.Empty((await Query(entries, session)).Entries);

        parser
            .Parse(Arg.Any<Stream>(), "retry.xls", Arg.Any<CancellationToken>())
            .Returns(_ => Rows(82));
        using MemoryStream second = new([2]);
        await entries.AppendFileData(session, file, "retry.xls", second);

        Assert.Equal(82m, Assert.Single((await Query(entries, session)).Entries).Id);
        string rawPath = Assert.Single(RawFiles(sessions, ".xls"));
        Assert.Equal((byte)2, Assert.Single(File.ReadAllBytes(rawPath)));
    }

    [Fact]
    public async Task ConfiguredStorageRetainsLiveUploadsThroughTheSweepAndEvictsIdleSessions()
    {
        DirectoryInfo site = Directory.CreateTempSubdirectory("stem-storage-test-");
        try
        {
            IHostEnvironment environment = Substitute.For<IHostEnvironment>();
            environment.ContentRootPath.Returns(site.FullName);
            IOptions<SandboxSettings> settings = Options.Create(
                new SandboxSettings { StorageDirectory = "App_Data/NuclearEvaluationStorage" }
            );
            EfsFileService files = new(settings, environment);
            StorageQuotaService quota = new(files, settings);
            await using StemSessionManager sessions = new(settings, environment);
            StemPreviewEntryService entries = new(sessions, new StemPreviewParser());
            StemPreviewService uploads = new(entries, NullLogger<StemPreviewService>.Instance);
            Guid session = Guid.NewGuid();
            string storagePath = Path.Combine(
                site.FullName,
                "App_Data",
                "NuclearEvaluationStorage"
            );

            Assert.True(quota.CanAccept(1024));
            await UploadSuccessfully(
                uploads,
                session,
                Guid.NewGuid(),
                "sample.csv",
                "42,LAB-A,2026-01-03,true,2"
            );

            Assert.Equal(storagePath, Path.GetDirectoryName(sessions.StorageDirectory));
            string rawPath = Assert.Single(RawFiles(sessions, ".csv"));
            Assert.Equal(
                "Identifier,LaboratoryCode,AnalysisDate,IsNu,U234\n42,LAB-A,2026-01-03,true,2\n",
                File.ReadAllText(rawPath)
            );
            Assert.True(files.GetTotalSizeBytes() >= new FileInfo(rawPath).Length);

            DirectoryInfo storage = new(storagePath);
            DirectoryInfo orphan = storage.CreateSubdirectory("stem-orphan");
            DateTime cutoff = DateTime.UtcNow.AddHours(-1);
            orphan.LastWriteTimeUtc = cutoff.AddHours(-1);
            Directory.SetLastWriteTimeUtc(sessions.StorageDirectory, cutoff.AddHours(-1));

            Assert.Equal(
                1,
                EfsFileService.PurgeOlderThan(storage, cutoff, sessions.StorageDirectory)
            );
            Assert.False(Directory.Exists(orphan.FullName));
            Assert.Equal(42m, Assert.Single((await Query(entries, session)).Entries).Id);
            Assert.True(File.Exists(rawPath));

            await sessions.EvictIdleAsync(TimeSpan.Zero);
            Assert.Null(sessions.TryGet(session));
            Assert.False(File.Exists(rawPath));
        }
        finally
        {
            site.Delete(recursive: true);
        }
    }

    static string Filter(
        params (string Property, FilterOperator Operator, object? Value)[] conditions
    ) =>
        conditions
            .Select(condition => new CompositeFilterDescriptor
            {
                Property = condition.Property,
                FilterOperator = condition.Operator,
                FilterValue = condition.Value,
            })
            .ToFilterString<StemPreviewEntryView>(
                LogicalFilterOperator.And,
                FilterCaseSensitivity.Default
            );

    static async IAsyncEnumerable<StemPreviewEntry> Rows(
        decimal id,
        Action? afterFirstRow = null,
        [EnumeratorCancellation] CancellationToken ct = default
    )
    {
        await Task.Yield();
        yield return new() { Id = id, AnalysisDate = new(2026, 1, 1) };
        afterFirstRow?.Invoke();
        ct.ThrowIfCancellationRequested();
    }

    static Task<FetchDataResult<StemPreviewEntryView>> Query(
        StemPreviewEntryService service,
        Guid session,
        DataQuery? query = null
    ) => service.GetStemPreviewEntryViews(session, new() { Query = query });

    static async Task UploadSuccessfully(
        StemPreviewService service,
        Guid session,
        Guid fileId,
        string name,
        string rows
    )
    {
        OperationResult result = await Upload(service, session, fileId, name, rows);
        Assert.True(result.IsSuccessful, result.Exception?.ToString());
    }

    static async Task<OperationResult> Upload(
        StemPreviewService service,
        Guid session,
        Guid fileId,
        string name,
        string rows
    )
    {
        using MemoryStream stream = new(
            Encoding.UTF8.GetBytes($"Identifier,LaboratoryCode,AnalysisDate,IsNu,U234\n{rows}\n")
        );
        return await service.UploadStemPreviewFile(session, stream, fileId, name);
    }

    static string[] RawFiles(StemSessionManager sessions, string extension) =>
        Directory.Exists(sessions.StorageDirectory)
            ? Directory.GetFiles(
                sessions.StorageDirectory,
                $"*{extension}",
                SearchOption.AllDirectories
            )
            : [];

    static MemoryStream CreateWorkbook()
    {
        MemoryStream workbook = new();
        using (ZipArchive archive = new(workbook, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddXml(
                archive,
                "[Content_Types].xml",
                """
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                  <Default Extension="xml" ContentType="application/xml"/>
                  <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
                  <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
                </Types>
                """
            );
            AddXml(
                archive,
                "_rels/.rels",
                """
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
                </Relationships>
                """
            );
            AddXml(
                archive,
                "xl/workbook.xml",
                """
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"
                          xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                  <sheets><sheet name="Data" sheetId="1" r:id="rId1"/></sheets>
                </workbook>
                """
            );
            AddXml(
                archive,
                "xl/_rels/workbook.xml.rels",
                """
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
                </Relationships>
                """
            );
            AddXml(
                archive,
                "xl/worksheets/sheet1.xml",
                """
                <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
                  <dimension ref="A1:E3"/>
                  <sheetData>
                    <row r="1">
                      <c r="A1" t="inlineStr"><is><t>Identifier</t></is></c>
                      <c r="B1" t="inlineStr"><is><t>LaboratoryCode</t></is></c>
                      <c r="C1" t="inlineStr"><is><t>AnalysisDate</t></is></c>
                      <c r="D1" t="inlineStr"><is><t>IsNu</t></is></c>
                      <c r="E1" t="inlineStr"><is><t>U234</t></is></c>
                    </row>
                    <row r="2">
                      <c r="A2"><v>42.25</v></c>
                      <c r="B2" t="inlineStr"><is><t>001</t></is></c>
                      <c r="C2"><v>46025</v></c>
                      <c r="D2" t="b"><v>1</v></c>
                      <c r="E2"><v>2.125</v></c>
                    </row>
                    <row r="3">
                      <c r="A3"><v>43</v></c>
                      <c r="B3" t="inlineStr"><is><t>LAB-A</t></is></c>
                      <c r="C3" t="inlineStr"><is><t>2026-01-04</t></is></c>
                      <c r="D3" t="b"><v>0</v></c>
                    </row>
                  </sheetData>
                </worksheet>
                """
            );
        }
        workbook.Position = 0;
        return workbook;
    }

    static void AddXml(ZipArchive archive, string path, string xml)
    {
        using StreamWriter writer = new(archive.CreateEntry(path).Open());
        writer.Write(xml);
    }

    sealed class UploadFiles : IDisposable
    {
        readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("stem-duckdb-test-");

        public StemSessionManager CreateSessions() =>
            new(Options.Create(new SandboxSettings()), _directory);

        public void Dispose() => _directory.Delete(recursive: true);
    }
}

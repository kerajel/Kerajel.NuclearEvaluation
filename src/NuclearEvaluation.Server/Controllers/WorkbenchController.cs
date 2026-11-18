using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Data.SqlClient;
using NuclearEvaluation.Server.Services.Workbench;
using NuclearEvaluation.Shared.Contracts;

namespace NuclearEvaluation.Server.Controllers;

[ApiController]
[Route("api/workbench")]
public sealed class WorkbenchController(
    WorkbenchStore store,
    WorkbenchArchive archive,
    WorkbenchQueryEngine queryEngine,
    ImportReviewStore imports,
    ImportReviewSample importSample,
    ImportReviewLargeSample largeSample,
    IHostEnvironment environment,
    ILogger<WorkbenchController> logger
) : ControllerBase, IAsyncActionFilter
{
    const string WorkspaceCookie = "ne-workbench";

    [HttpGet]
    public async Task<ActionResult<WorkbenchCatalog>> Catalog(CancellationToken ct)
    {
        WorkbenchCatalog catalog = await archive.CatalogAsync(ct);
        if (!catalog.Archive.Available || catalog.Stations.Count == 0)
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "The RadNet snapshot is unavailable."
            );

        Guid? current = ReadWorkspaceCookie();
        Guid workspace = await store.OpenWorkspaceAsync(current, catalog.Stations, ct);
        if (current != workspace)
        {
            Response.Cookies.Append(
                WorkspaceCookie,
                workspace.ToString("N"),
                new CookieOptions
                {
                    HttpOnly = true,
                    Secure = Request.IsHttps || !environment.IsDevelopment(),
                    SameSite = SameSiteMode.Strict,
                    IsEssential = true,
                    Path = "/api/workbench",
                }
            );
        }
        return catalog;
    }

    [HttpGet("series")]
    public Task<ActionResult<WorkbenchSeriesPage>> SeriesPage(
        [FromQuery] string? search = null,
        [FromQuery] string? state = null,
        [FromQuery] WorkbenchReview? review = null,
        [FromQuery] WorkbenchSeriesSort sort = WorkbenchSeriesSort.Name,
        [FromQuery] bool descending = false,
        [FromQuery] int skip = 0,
        [FromQuery] int top = 25,
        CancellationToken ct = default
    ) => SearchSeriesAsync(new(search, state, review, sort, descending, skip, top), ct);

    [HttpPost("series/query")]
    [RequestSizeLimit(16 * 1024)]
    public Task<ActionResult<WorkbenchSeriesPage>> SearchSeries(
        [FromBody] WorkbenchSeriesSearch query,
        CancellationToken ct
    ) => SearchSeriesAsync(query, ct);

    async Task<ActionResult<WorkbenchSeriesPage>> SearchSeriesAsync(
        WorkbenchSeriesSearch query,
        CancellationToken ct
    )
    {
        ValidatePage(query.Skip, query.Top);
        string? search = string.IsNullOrWhiteSpace(query.Search)
            ? null
            : CleanText(query.Search, 100, "Search text");
        string? state = string.IsNullOrWhiteSpace(query.State)
            ? null
            : query.State.Trim().ToUpperInvariant();
        if (state is not null && (state.Length != 2 || !state.All(char.IsAsciiLetterUpper)))
            throw new ArgumentException("Choose a two-letter state code.");
        if (query.Review.HasValue && !Enum.IsDefined(query.Review.Value))
            throw new ArgumentException("Choose a supported review status.");
        return await store.ListSeriesAsync(
            RequireWorkspace(),
            search,
            state,
            query.Review,
            query.Sort,
            query.Descending,
            query.Skip,
            query.Top,
            ct,
            query.ColumnFilters
        );
    }

    [HttpPost("query")]
    [RequestSizeLimit(64 * 1024)]
    public async Task<ActionResult<WorkbenchQueryPage>> Query(
        [FromBody] WorkbenchQuery query,
        CancellationToken ct
    )
    {
        Guid workspace = RequireWorkspace();
        WorkbenchQuerySource source = await store.ReadQuerySourceAsync(workspace, ct);
        return await queryEngine.QueryAsync(query, source, ct);
    }

    [HttpGet("import-review")]
    public async Task<ImportReviewAvailability> ImportAvailability(CancellationToken ct)
    {
        RequireWorkspace();
        ImportReviewSource? source = await importSample.FindSourceAsync(ct);
        return new(source, source is not null && importSample.IsAvailable, largeSample.IsAvailable);
    }

    [HttpPost("import-review/large/stage")]
    public async Task<ActionResult<ImportReviewLargeStage>> StageLargeSample(CancellationToken ct)
    {
        RequireWorkspace();
        try
        {
            if (await largeSample.StageAsync(ct) is { } stage)
                return stage;
        }
        catch (InvalidDataException error)
        {
            logger.LogWarning(error, "Could not stage the RadNet CSV collection.");
        }

        return Problem(
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "The RadNet CSV collection is unavailable on this host."
        );
    }

    [HttpPost("import-review/large/query")]
    [RequestSizeLimit(32 * 1024)]
    public async Task<ActionResult<RadNetQueryResult>> LargeSampleRows(
        [FromBody] RadNetQuery query,
        CancellationToken ct
    )
    {
        RequireWorkspace();
        RadNetQueryResult? page = await largeSample.QueryAsync(query, ct);
        if (page is not null)
            return page;

        return Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Stage the RadNet CSV collection before browsing it."
        );
    }

    [HttpPost("import-review/sample")]
    public async Task<ActionResult<ImportReviewStage>> StageSample(CancellationToken ct)
    {
        Guid workspace = RequireWorkspace();
        await store.EnsureWorkspaceAsync(workspace, ct);
        ImportReviewSource? source = await importSample.FindSourceAsync(ct);
        if (source is null)
            return ImportSourceUnavailable();

        await using Stream? csv = await importSample.OpenVerifiedAsync(ct);
        if (csv is null)
        {
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "The verified example CSV is unavailable on this host."
            );
        }

        return await imports.StageAsync(
            workspace,
            source,
            Path.GetFileName(source.FileName),
            csv,
            ct
        );
    }

    [HttpPost("import-review/upload")]
    [RequestSizeLimit(ImportReviewCatalog.MaxUploadBytes + 64 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = ImportReviewCatalog.MaxUploadBytes)]
    public async Task<ActionResult<ImportReviewStage>> StageUpload(
        [FromForm] IFormFile file,
        CancellationToken ct
    )
    {
        Guid workspace = RequireWorkspace();
        await store.EnsureWorkspaceAsync(workspace, ct);
        string name = Path.GetFileName(file.FileName.Replace('\\', '/'));
        if (
            !name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
            || name.Length > 160
            || file.Length is <= 0 or > ImportReviewCatalog.MaxUploadBytes
        )
            throw new ArgumentException("Choose a RadNet CSV of up to 8 MiB.");

        ImportReviewSource? source = await importSample.FindSourceAsync(ct);
        if (source is null)
            return ImportSourceUnavailable();

        await using Stream csv = file.OpenReadStream();
        return await imports.StageAsync(workspace, source, name, csv, ct);
    }

    [HttpPost("import-review/{id:guid}/query")]
    [RequestSizeLimit(32 * 1024)]
    public async Task<ActionResult<ImportReviewPage>> ImportRows(
        Guid id,
        [FromBody] ImportReviewQuery query,
        CancellationToken ct
    )
    {
        ImportReviewPage? page = await imports.QueryAsync(RequireWorkspace(), id, query, ct);
        return page is null ? NotFound() : page;
    }

    [HttpPost("import-review/{id:guid}/series")]
    public async Task<ActionResult<ImportReviewSeriesLink>> CreateImportSeries(
        Guid id,
        [FromBody] ImportReviewCreateSeries request,
        CancellationToken ct
    )
    {
        Guid workspace = RequireWorkspace();
        ImportReviewStage? stage = await imports.FindAsync(workspace, id, ct);
        if (stage is null)
            return NotFound();

        ImportReviewSource? current = await importSample.FindSourceAsync(ct);
        if (!stage.CanCreateSeries || current is null || current != stage.Source)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Create a Series from a complete match to the current archive."
            );
        }

        return await store.LinkImportSeriesAsync(
            workspace,
            current,
            stage.Sha256,
            request.ProjectId,
            ct
        );
    }

    ObjectResult ImportSourceUnavailable() =>
        Problem(
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "The Colorado Springs 2025 reference file is unavailable in the local RadNet snapshot."
        );

    [HttpPost("series")]
    public async Task<ActionResult<WorkbenchSeries>> CreateSeries(
        [FromBody] WorkbenchSeriesDraft draft,
        CancellationToken ct
    ) => StatusCode(StatusCodes.Status201Created, await SaveSeriesAsync(null, draft, ct));

    [HttpPut("series/{id:guid}")]
    public async Task<ActionResult<WorkbenchSeries>> UpdateSeries(
        Guid id,
        [FromBody] WorkbenchSeriesDraft draft,
        CancellationToken ct
    )
    {
        ValidateVersion(draft.Version);
        return await SaveSeriesAsync(id, draft, ct);
    }

    [HttpDelete("series/{id:guid}")]
    public async Task<IActionResult> DeleteSeries(
        Guid id,
        [FromQuery] long version,
        CancellationToken ct
    )
    {
        ValidateVersion(version);
        await store.DeleteSeriesAsync(RequireWorkspace(), id, version, ct);
        return NoContent();
    }

    [HttpGet("series/{id:guid}/files")]
    public async Task<ActionResult<WorkbenchFilePage>> Files(
        Guid id,
        [FromQuery] int skip = 0,
        [FromQuery] int top = 25,
        CancellationToken ct = default
    )
    {
        ValidatePage(skip, top);
        WorkbenchSeries row = await store.GetSeriesAsync(RequireWorkspace(), id, ct);
        return await archive.FilesAsync(row, skip, top, ct);
    }

    [HttpGet("projects")]
    public async Task<ActionResult<IReadOnlyList<WorkbenchProject>>> Projects(
        CancellationToken ct
    ) => Ok(await store.ListProjectsAsync(RequireWorkspace(), ct));

    [HttpPost("projects")]
    public async Task<ActionResult<WorkbenchProjectDetails>> CreateProject(
        [FromBody] WorkbenchProjectDraft draft,
        CancellationToken ct
    )
    {
        WorkbenchProject project = await store.CreateProjectAsync(
            RequireWorkspace(),
            CleanText(draft.Name, 100, "Project name"),
            ct
        );
        return CreatedAtAction(
            nameof(Project),
            new { id = project.Id },
            new WorkbenchProjectDetails(project, [], new(0, 0, 0, []))
        );
    }

    [HttpGet("projects/{id:guid}")]
    public async Task<ActionResult<WorkbenchProjectDetails>> Project(
        Guid id,
        CancellationToken ct
    ) => await ProjectDetailsAsync(await store.GetProjectAsync(RequireWorkspace(), id, ct), ct);

    [HttpPost("projects/{id:guid}/series/{seriesId:guid}")]
    public Task<WorkbenchProjectDetails> AddSeries(
        Guid id,
        Guid seriesId,
        [FromQuery] long version,
        CancellationToken ct
    ) => SetMembershipAsync(id, seriesId, version, add: true, ct);

    [HttpDelete("projects/{id:guid}/series/{seriesId:guid}")]
    public Task<WorkbenchProjectDetails> RemoveSeries(
        Guid id,
        Guid seriesId,
        [FromQuery] long version,
        CancellationToken ct
    ) => SetMembershipAsync(id, seriesId, version, add: false, ct);

    [HttpGet("projects/{id:guid}/observations")]
    public async Task<ActionResult<RadNetQueryResult>> ProjectObservations(
        Guid id,
        [FromQuery] int skip = 0,
        [FromQuery] int top = 25,
        [FromQuery] long? version = null,
        CancellationToken ct = default
    )
    {
        ValidatePage(skip, top);
        WorkbenchProjectSnapshot project = await GetObservationProjectAsync(id, version, ct);
        return await archive.ProjectObservationsAsync(project.Series, skip, top, ct);
    }

    [HttpPost("projects/{id:guid}/observations/query")]
    [RequestSizeLimit(32 * 1024)]
    public async Task<ActionResult<RadNetQueryResult>> QueryProjectObservations(
        Guid id,
        [FromBody] RadNetQuery query,
        [FromQuery] long? version,
        CancellationToken ct
    )
    {
        WorkbenchProjectSnapshot project = await GetObservationProjectAsync(id, version, ct);
        return await archive.ProjectObservationsAsync(project.Series, query, ct);
    }

    async Task<WorkbenchProjectSnapshot> GetObservationProjectAsync(
        Guid id,
        long? version,
        CancellationToken ct
    )
    {
        WorkbenchProjectSnapshot project = await store.GetProjectAsync(RequireWorkspace(), id, ct);
        if (version is not null && version != project.Project.Version)
            throw new WorkbenchConflictException();
        return project;
    }

    async Task<WorkbenchProjectDetails> SetMembershipAsync(
        Guid id,
        Guid seriesId,
        long version,
        bool add,
        CancellationToken ct
    )
    {
        ValidateVersion(version);
        WorkbenchProjectSnapshot project = await store.SetMembershipAsync(
            RequireWorkspace(),
            id,
            seriesId,
            version,
            add,
            ct
        );
        return await ProjectDetailsAsync(project, ct);
    }

    async Task<WorkbenchProjectDetails> ProjectDetailsAsync(
        WorkbenchProjectSnapshot project,
        CancellationToken ct
    ) => new(project.Project, project.Series, await archive.ProjectStatsAsync(project.Series, ct));

    async Task<WorkbenchSeries> SaveSeriesAsync(
        Guid? id,
        WorkbenchSeriesDraft draft,
        CancellationToken ct
    )
    {
        Guid workspace = RequireWorkspace();
        string name = CleanText(draft.Name, 100, "Series name");
        string notes = draft.Notes.Trim();
        if (notes.Length > 1000)
            throw new ArgumentException("Use at most 1,000 characters for notes.");
        if (!Enum.IsDefined(draft.Review))
            throw new ArgumentException("Choose a supported review status.");

        WorkbenchCatalog catalog = await archive.CatalogAsync(ct);
        WorkbenchStation station = catalog.Stations.FirstOrDefault(entry =>
            entry.State.Equals(draft.State.Trim(), StringComparison.OrdinalIgnoreCase)
            && entry.Name.Equals(draft.Station.Trim(), StringComparison.OrdinalIgnoreCase)
        ) ?? throw new ArgumentException("Choose a station from the archive.");
        if (draft.From > draft.To || draft.From < station.FirstDay || draft.To > station.LastDay)
            throw new ArgumentException(
                "Choose an ordered date range within the station's coverage."
            );

        long count = await archive.CountAsync(station, draft.From, draft.To, ct);
        return await store.SaveSeriesAsync(
            workspace,
            id,
            draft with
            {
                Name = name,
                State = station.State,
                Station = station.Name,
                Notes = notes,
            },
            count,
            ct
        );
    }

    Guid? ReadWorkspaceCookie() =>
        Request.Cookies.TryGetValue(WorkspaceCookie, out string? value)
        && Guid.TryParseExact(value, "N", out Guid id)
        && id != Guid.Empty
            ? id
            : null;

    Guid RequireWorkspace() => ReadWorkspaceCookie() ?? throw new WorkbenchCookieException();

    static string CleanText(string source, int length, string label)
    {
        string value = source.Trim();
        if (value.Length == 0 || value.Length > length)
            throw new ArgumentException($"{label} must be between 1 and {length} characters.");
        return value;
    }

    static void ValidatePage(int skip, int top)
    {
        if (skip < 0 || top is < 1 or > RadNetCatalog.MaxPageSize)
            throw new ArgumentException($"Choose a page of 1 to {RadNetCatalog.MaxPageSize} rows.");
    }

    static void ValidateVersion(long? version)
    {
        if (version is null or <= 0)
            throw new ArgumentException("Supply the version of the record you are editing.");
    }

    [NonAction]
    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next
    )
    {
        Response.Headers.CacheControl = "no-store";
        if (!store.IsConfigured)
        {
            context.Result = Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Workbench storage is not configured."
            );
            return;
        }

        ActionExecutedContext result = await next();
        if (result.Exception is not { } exception || result.ExceptionHandled)
            return;

        (int code, string? message) = exception switch
        {
            WorkbenchCookieException => (409, "Open the workbench before using its data."),
            WorkbenchConflictException => (409, "This record changed. Reload it and try again."),
            ArgumentException or FormatException or InvalidDataException => (
                400,
                exception.Message
            ),
            KeyNotFoundException => (404, "This record no longer exists in your workbench."),
            TimeoutException or SqlException => (
                503,
                "Workbench storage is temporarily unavailable."
            ),
            _ => (0, null),
        };
        if (message is null)
            return;
        if (code == 503)
            logger.LogWarning(exception, "Workbench storage could not complete the request.");
        result.Result = Problem(statusCode: code, title: message);
        result.ExceptionHandled = true;
    }
}

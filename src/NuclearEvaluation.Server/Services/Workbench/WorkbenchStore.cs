using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using NuclearEvaluation.Shared.Contracts;

namespace NuclearEvaluation.Server.Services.Workbench;

public sealed class WorkbenchStore(IConfiguration configuration) : IDisposable
{
    readonly string? _connectionString = configuration.GetConnectionString("Workbench")
        ?? configuration.GetConnectionString("NuclearEvaluationServerDbConnection");
    readonly SemaphoreSlim _schemaLock = new(1);
    volatile bool _schemaReady;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_connectionString);

    const string SeriesFields = """
        s.Id, s.Name, s.[State], s.Station, s.FromDay, s.ToDay,
        s.Review, s.Notes, s.ObservationCount, s.Version
        """;

    const string SelectProjects = """
        SELECT p.Id, p.Name, p.Version, COUNT(link.SeriesId)
        FROM dbo.WorkbenchProjects p
        LEFT JOIN dbo.WorkbenchProjectSeries link ON link.WorkspaceId = p.WorkspaceId AND link.ProjectId = p.Id
        WHERE p.WorkspaceId = @workspace
        GROUP BY p.Id, p.Name, p.Version
        ORDER BY p.Name, p.Id;
        """;

    const string Schema = """
        IF OBJECT_ID(N'dbo.WorkbenchWorkspaces', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.WorkbenchWorkspaces (
                Id uniqueidentifier NOT NULL PRIMARY KEY,
                CreatedAt datetime2 NOT NULL DEFAULT SYSUTCDATETIME()
            );
            CREATE TABLE dbo.WorkbenchSeries (
                WorkspaceId uniqueidentifier NOT NULL REFERENCES dbo.WorkbenchWorkspaces (Id),
                Id uniqueidentifier NOT NULL,
                Name nvarchar(100) NOT NULL,
                [State] char(2) NOT NULL,
                Station nvarchar(100) NOT NULL,
                FromDay date NOT NULL,
                ToDay date NOT NULL,
                Review int NOT NULL,
                Notes nvarchar(1000) NOT NULL,
                ObservationCount bigint NOT NULL,
                Version bigint NOT NULL DEFAULT 1,
                PRIMARY KEY (WorkspaceId, Id),
                CHECK (FromDay <= ToDay),
                CHECK (Review BETWEEN 0 AND 2),
                CHECK (ObservationCount >= 0)
            );
            CREATE TABLE dbo.WorkbenchProjects (
                WorkspaceId uniqueidentifier NOT NULL REFERENCES dbo.WorkbenchWorkspaces (Id),
                Id uniqueidentifier NOT NULL,
                Name nvarchar(100) NOT NULL,
                Version bigint NOT NULL DEFAULT 1,
                PRIMARY KEY (WorkspaceId, Id)
            );
            CREATE TABLE dbo.WorkbenchProjectSeries (
                WorkspaceId uniqueidentifier NOT NULL,
                ProjectId uniqueidentifier NOT NULL,
                SeriesId uniqueidentifier NOT NULL,
                PRIMARY KEY (WorkspaceId, ProjectId, SeriesId),
                FOREIGN KEY (WorkspaceId, ProjectId) REFERENCES dbo.WorkbenchProjects (WorkspaceId, Id)
                    ON DELETE CASCADE,
                FOREIGN KEY (WorkspaceId, SeriesId) REFERENCES dbo.WorkbenchSeries (WorkspaceId, Id)
                    ON DELETE CASCADE
            );
            CREATE INDEX IX_WorkbenchProjectSeries_Series
                ON dbo.WorkbenchProjectSeries (WorkspaceId, SeriesId, ProjectId);
        END;

        IF COL_LENGTH(N'dbo.WorkbenchWorkspaces', N'StarterProjectsSeeded') IS NULL
            ALTER TABLE dbo.WorkbenchWorkspaces ADD StarterProjectsSeeded bit NOT NULL
                CONSTRAINT DF_WorkbenchWorkspaces_StarterProjectsSeeded DEFAULT (0);

        IF OBJECT_ID(N'dbo.WorkbenchImportedSeries', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.WorkbenchImportedSeries (
                WorkspaceId uniqueidentifier NOT NULL,
                SourceFileId int NOT NULL,
                SeriesId uniqueidentifier NOT NULL,
                PRIMARY KEY (WorkspaceId, SourceFileId),
                FOREIGN KEY (WorkspaceId, SeriesId) REFERENCES dbo.WorkbenchSeries (WorkspaceId, Id)
                    ON DELETE CASCADE
            );
            CREATE INDEX IX_WorkbenchImportedSeries_Series
                ON dbo.WorkbenchImportedSeries (WorkspaceId, SeriesId);
        END;
        """;

    const string StarterProjects = """
        IF NOT EXISTS (
            SELECT 1 FROM dbo.WorkbenchWorkspaces WHERE Id = @workspace AND StarterProjectsSeeded = 0
        )
            RETURN;

        DECLARE @projects TABLE (
            Id uniqueidentifier PRIMARY KEY, Name nvarchar(100), States varchar(32), Stations nvarchar(100)
        );
        INSERT @projects (Id, Name, States, Stations) VALUES
            (NEWID(), N'California monitoring', ',CA,', NULL),
            (NEWID(), N'Great Lakes monitoring', ',IL,MI,MN,OH,WI,',
                N',Chicago,Cleveland,Detroit,Duluth,Milwaukee,Toledo,'),
            (NEWID(), N'Pacific Northwest monitoring', ',ID,OR,WA,', NULL);

        DELETE candidate
        FROM @projects candidate
        WHERE EXISTS (
            SELECT 1 FROM dbo.WorkbenchProjects p
            WHERE p.WorkspaceId = @workspace
                AND p.Name COLLATE Latin1_General_100_CI_AS = candidate.Name
        );

        DECLARE @members TABLE (ProjectId uniqueidentifier, SeriesId uniqueidentifier);
        WITH stations AS (
            SELECT s.Id, s.[State], s.Station, s.ObservationCount,
                ROW_NUMBER() OVER (
                    PARTITION BY s.[State], s.Station ORDER BY s.ObservationCount DESC, s.Id
                ) AS preference
            FROM dbo.WorkbenchSeries s
            WHERE s.WorkspaceId = @workspace AND s.ObservationCount > 0
        )
        INSERT @members (ProjectId, SeriesId)
        SELECT p.Id, picked.Id
        FROM @projects p
        CROSS APPLY (
            SELECT TOP (4) s.Id
            FROM stations s
            WHERE s.preference = 1 AND CHARINDEX(',' + RTRIM(s.[State]) + ',', p.States) > 0
                AND (p.Stations IS NULL OR CHARINDEX(N',' + s.Station + N',', p.Stations) > 0)
            ORDER BY s.ObservationCount DESC, s.[State], s.Station, s.Id
        ) picked;

        INSERT dbo.WorkbenchProjects (WorkspaceId, Id, Name)
        SELECT @workspace, p.Id, p.Name
        FROM @projects p
        WHERE EXISTS (SELECT 1 FROM @members m WHERE m.ProjectId = p.Id);

        INSERT dbo.WorkbenchProjectSeries (WorkspaceId, ProjectId, SeriesId)
        SELECT @workspace, ProjectId, SeriesId FROM @members;

        UPDATE dbo.WorkbenchWorkspaces SET StarterProjectsSeeded = 1 WHERE Id = @workspace;
        """;

    internal async Task<Guid> OpenWorkspaceAsync(
        Guid? requested,
        IReadOnlyList<WorkbenchStation> stations,
        CancellationToken ct
    )
    {
        await using SqlConnection connection = await OpenAsync(ct);
        if (requested is { } existing)
        {
            await using SqlCommand lookup = WorkspaceCommand(
                connection,
                null,
                "SELECT StarterProjectsSeeded FROM dbo.WorkbenchWorkspaces WHERE Id = @workspace;",
                existing
            );
            if (await lookup.ExecuteScalarAsync(ct) is bool seeded)
            {
                if (!seeded)
                {
                    await using SqlTransaction seed = await BeginWorkspaceAsync(
                        connection,
                        existing,
                        "Exclusive",
                        ct
                    );
                    await SeedProjectsAsync(connection, seed, existing, ct);
                    await seed.CommitAsync(ct);
                }
                return existing;
            }
        }

        Guid workspace = Guid.NewGuid();
        await using SqlTransaction transaction = (SqlTransaction)
            await connection.BeginTransactionAsync(ct);
        await using (
            SqlCommand create = WorkspaceCommand(
                connection,
                transaction,
                "INSERT dbo.WorkbenchWorkspaces (Id) VALUES (@workspace);",
                workspace
            )
        )
        {
            await create.ExecuteNonQueryAsync(ct);
        }

        SeedSeries[] rows = stations
            .Select(station => new SeedSeries(Guid.NewGuid(), station))
            .ToArray();
        Guid firstSeries = (
            rows.FirstOrDefault(row =>
                row.Station.State == "CA"
                && row.Station.Name.Contains("SACRAMENTO", StringComparison.OrdinalIgnoreCase)
            ) ?? rows.MaxBy(row => row.Station.ObservationCount)!
        ).Id;
        await using SqlCommand series = WorkspaceCommand(
            connection,
            transaction,
            """
            INSERT dbo.WorkbenchSeries (
                WorkspaceId, Id, Name, [State], Station, FromDay, ToDay, Review, Notes, ObservationCount
            )
            SELECT @workspace, Id, Station, [State], Station, FromDay, ToDay, 0, N'', ObservationCount
            FROM OPENJSON(@stations) WITH (
                Id uniqueidentifier '$.Id',
                Station nvarchar(100) '$.Station.Name',
                [State] char(2) '$.Station.State',
                FromDay date '$.Station.FirstDay',
                ToDay date '$.Station.LastDay',
                ObservationCount bigint '$.Station.ObservationCount'
            );
            """,
            workspace
        );
        series.Parameters.Add("@stations", SqlDbType.NVarChar, -1).Value = JsonSerializer.Serialize(
            rows
        );
        await series.ExecuteNonQueryAsync(ct);

        await using (
            SqlCommand project = WorkspaceCommand(
                connection,
                transaction,
                """
                INSERT dbo.WorkbenchProjects (WorkspaceId, Id, Name)
                    VALUES (@workspace, @project, N'Monitoring coverage review');
                INSERT dbo.WorkbenchProjectSeries (WorkspaceId, ProjectId, SeriesId)
                    VALUES (@workspace, @project, @series);
                """,
                workspace
            )
        )
        {
            project.Parameters.Add("@project", SqlDbType.UniqueIdentifier).Value = Guid.NewGuid();
            project.Parameters.Add("@series", SqlDbType.UniqueIdentifier).Value = firstSeries;
            await project.ExecuteNonQueryAsync(ct);
        }
        await SeedProjectsAsync(connection, transaction, workspace, ct);
        await transaction.CommitAsync(ct);
        return workspace;
    }

    static async Task SeedProjectsAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        Guid workspace,
        CancellationToken ct
    )
    {
        await using SqlCommand seed = WorkspaceCommand(
            connection,
            transaction,
            StarterProjects,
            workspace
        );
        await seed.ExecuteNonQueryAsync(ct);
    }

    internal async Task<WorkbenchSeriesPage> ListSeriesAsync(
        Guid workspace,
        string? search,
        string? state,
        WorkbenchReview? review,
        WorkbenchSeriesSort sort,
        bool descending,
        int skip,
        int top,
        CancellationToken ct,
        IReadOnlyList<ColumnFilterGroup>? columnFilters = null
    )
    {
        string sortField = SeriesColumn(sort);
        string order = descending ? "DESC" : "ASC";
        List<SqlParameter> parameters = [];
        string columnPredicate = SeriesColumnPredicate(columnFilters ?? [], parameters);
        string predicate =
            """
                FROM dbo.WorkbenchSeries s
                WHERE s.WorkspaceId = @workspace
                    AND (@search IS NULL OR CHARINDEX(@search, s.Name COLLATE Latin1_General_100_CI_AS) > 0
                        OR CHARINDEX(@search, s.Station COLLATE Latin1_General_100_CI_AS) > 0
                        OR CHARINDEX(@search, s.Notes COLLATE Latin1_General_100_CI_AS) > 0)
                    AND (@state IS NULL OR s.[State] = @state)
                    AND (@review IS NULL OR s.Review = @review)
                """ + columnPredicate;
        await using SqlConnection connection = await OpenAsync(ct);
        await using SqlTransaction transaction = await BeginWorkspaceAsync(
            connection,
            workspace,
            "Shared",
            ct
        );
        await using SqlCommand query = WorkspaceCommand(
            connection,
            transaction,
            $"""
            SELECT COUNT_BIG(*), COALESCE(SUM(s.ObservationCount), CONVERT(bigint, 0)) {predicate};
            SELECT {SeriesFields} {predicate}
            ORDER BY {sortField} {order}, s.Id ASC
            OFFSET @skip ROWS FETCH NEXT @top ROWS ONLY;
            """,
            workspace
        );
        query.Parameters.Add("@search", SqlDbType.NVarChar, 100).Value = (object?)search
            ?? DBNull.Value;
        query.Parameters.Add("@state", SqlDbType.Char, 2).Value = (object?)state ?? DBNull.Value;
        query.Parameters.Add("@review", SqlDbType.Int).Value = review is { } status
            ? (int)status
            : DBNull.Value;
        query.Parameters.Add("@skip", SqlDbType.Int).Value = skip;
        query.Parameters.Add("@top", SqlDbType.Int).Value = top;
        foreach (SqlParameter parameter in parameters)
            query.Parameters.Add(parameter);

        await using SqlDataReader reader = await query.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);
        long total = reader.GetInt64(0);
        long observations = reader.GetInt64(1);
        await reader.NextResultAsync(ct);
        List<WorkbenchSeries> rows = await ReadSeriesAsync(reader, ct);
        return new(rows, total, observations);
    }

    internal static void ValidateSeriesColumnFilters(
        WorkbenchSeriesSort sort,
        IReadOnlyList<ColumnFilterGroup> groups
    )
    {
        _ = SeriesColumn(sort);
        _ = SeriesColumnPredicate(groups, []);
    }

    static string SeriesColumn(WorkbenchSeriesSort field) =>
        field switch
        {
            WorkbenchSeriesSort.Name => "s.Name",
            WorkbenchSeriesSort.State => "s.[State]",
            WorkbenchSeriesSort.Station => "s.Station",
            WorkbenchSeriesSort.From => "s.FromDay",
            WorkbenchSeriesSort.To => "s.ToDay",
            WorkbenchSeriesSort.Review => "s.Review",
            WorkbenchSeriesSort.ObservationCount => "s.ObservationCount",
            WorkbenchSeriesSort.Notes => "s.Notes",
            _ => throw new ArgumentException("Choose a supported Series column."),
        };

    static string SeriesColumnPredicate(
        IReadOnlyList<ColumnFilterGroup> groups,
        List<SqlParameter> parameters
    )
    {
        HashSet<WorkbenchSeriesSort> fields = [];
        List<string> predicates = [];
        foreach (ColumnFilterGroup group in groups)
        {
            WorkbenchSeriesColumn? field = WorkbenchSeriesColumn.All.FirstOrDefault(column =>
                string.Equals(column.Field, group?.Field, StringComparison.OrdinalIgnoreCase)
            );
            if (
                group is null
                || field is null
                || !fields.Add(field.Sort)
                || group.Conditions is not { Count: >= 1 and <= ColumnFilterGroup.MaxConditions }
            )
                throw new ArgumentException("Choose one or two conditions for each Series column.");

            predicates.Add(
                " AND ("
                    + string.Join(
                        group.MatchAny ? " OR " : " AND ",
                        group.Conditions.Select(condition =>
                            SeriesCondition(field, condition, parameters)
                        )
                    )
                    + ")"
            );
        }
        return string.Concat(predicates);
    }

    static string SeriesCondition(
        WorkbenchSeriesColumn field,
        ColumnFilterCondition condition,
        List<SqlParameter> parameters
    )
    {
        if (condition is null || condition.Value is not { Length: > 0 and <= 200 } value)
            throw new ArgumentException($"Enter a value for {field.Label}.");
        bool review = field.Sort == WorkbenchSeriesSort.Review;
        string operation = condition.Operator switch
        {
            RadNetOperator.Equal => "=",
            RadNetOperator.NotEqual => "<>",
            RadNetOperator.Contains when field.Type == RadNetFieldType.Text && !review =>
                "contains",
            RadNetOperator.GreaterThan when field.Type != RadNetFieldType.Text => ">",
            RadNetOperator.AtLeast when field.Type != RadNetFieldType.Text => ">=",
            RadNetOperator.LessThan when field.Type != RadNetFieldType.Text => "<",
            RadNetOperator.AtMost when field.Type != RadNetFieldType.Text => "<=",
            _ => throw new ArgumentException($"That comparison is unavailable for {field.Label}."),
        };
        string name = "@column" + parameters.Count.ToString(CultureInfo.InvariantCulture);
        (SqlDbType Type, object Value) parameter = field.Type switch
        {
            RadNetFieldType.Text when !review => (SqlDbType.NVarChar, value),
            RadNetFieldType.Text
                when Enum.TryParse(value, true, out WorkbenchReview status)
                    && Enum.IsDefined(status) => (SqlDbType.Int, (int)status),
            RadNetFieldType.Integer
                when long.TryParse(
                    value,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out long count
                ) => (SqlDbType.BigInt, count),
            RadNetFieldType.DateTime
                when DateOnly.TryParseExact(
                    value,
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out DateOnly day
                ) => (SqlDbType.Date, day.ToDateTime(TimeOnly.MinValue)),
            _ => throw new ArgumentException($"Enter a valid value for {field.Label}."),
        };
        parameters.Add(new SqlParameter(name, parameter.Type) { Value = parameter.Value });
        string column = SeriesColumn(field.Sort);
        if (parameter.Type == SqlDbType.NVarChar)
            column += " COLLATE Latin1_General_100_CI_AS";
        return operation == "contains"
            ? $"CHARINDEX({name}, {column}) > 0"
            : $"{column} {operation} {name}";
    }

    internal async Task<WorkbenchSeries> GetSeriesAsync(
        Guid workspace,
        Guid id,
        CancellationToken ct
    )
    {
        await using SqlConnection connection = await OpenAsync(ct);
        return await GetSeriesAsync(connection, null, workspace, id, ct);
    }

    internal async Task EnsureWorkspaceAsync(Guid workspace, CancellationToken ct)
    {
        await using SqlConnection connection = await OpenAsync(ct);
        await using SqlCommand lookup = WorkspaceCommand(
            connection,
            null,
            "SELECT 1 FROM dbo.WorkbenchWorkspaces WHERE Id = @workspace;",
            workspace
        );
        if (await lookup.ExecuteScalarAsync(ct) is null)
            throw new WorkbenchCookieException();
    }

    internal async Task<WorkbenchSeries> SaveSeriesAsync(
        Guid workspace,
        Guid? seriesId,
        WorkbenchSeriesDraft draft,
        long observationCount,
        CancellationToken ct
    )
    {
        Guid id = seriesId ?? Guid.NewGuid();
        await using SqlConnection connection = await OpenAsync(ct);
        await using SqlTransaction transaction = await BeginWorkspaceAsync(
            connection,
            workspace,
            "Exclusive",
            ct
        );
        if (seriesId is not null)
            CheckVersion(
                (await GetSeriesAsync(connection, transaction, workspace, id, ct)).Version,
                draft.Version
            );

        await WriteSeriesAsync(
            connection,
            transaction,
            workspace,
            id,
            draft,
            observationCount,
            seriesId is null,
            ct
        );
        if (seriesId is not null)
            await BumpLinkedProjectsAsync(connection, transaction, workspace, id, ct);

        WorkbenchSeries result = await GetSeriesAsync(connection, transaction, workspace, id, ct);
        await transaction.CommitAsync(ct);
        return result;
    }

    internal async Task<ImportReviewSeriesLink> LinkImportSeriesAsync(
        Guid workspace,
        ImportReviewSource source,
        string sha256,
        Guid? projectId,
        CancellationToken ct
    )
    {
        DateOnly from = new(source.Year, 1, 1);
        DateOnly to = new(source.Year, 12, 31);
        await using SqlConnection connection = await OpenAsync(ct);
        await using SqlTransaction transaction = await BeginWorkspaceAsync(
            connection,
            workspace,
            "Exclusive",
            ct
        );
        WorkbenchProjectSnapshot? project = projectId is { } id
            ? await GetProjectAsync(connection, transaction, workspace, id, ct)
            : null;
        WorkbenchSeries? mapped = await GetImportedSeriesAsync(
            connection,
            transaction,
            workspace,
            source.FileId,
            ct
        );
        bool reused =
            mapped is not null
            && string.Equals(mapped.State, source.State, StringComparison.Ordinal)
            && string.Equals(mapped.Station, source.Station, StringComparison.Ordinal)
            && mapped.From == from
            && mapped.To == to
            && mapped.ObservationCount == source.ObservationCount;

        // A Series with a changed scope belongs to the workspace. Leave its edits in place
        // and replace the import mapping with a Series that covers the accepted source rows.
        WorkbenchSeries series = reused
            ? mapped!
            : await CreateImportedSeriesAsync(
                connection,
                transaction,
                workspace,
                source,
                sha256,
                from,
                to,
                ct
            );

        bool linked = project is not null && project.Series.All(row => row.Id != series.Id);
        if (linked)
            await ChangeMembershipAsync(
                connection,
                transaction,
                workspace,
                project!.Project.Id,
                series.Id,
                add: true,
                ct
            );

        await transaction.CommitAsync(ct);
        return new(series, !reused, projectId, linked);
    }

    static async Task<WorkbenchSeries?> GetImportedSeriesAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        Guid workspace,
        int fileId,
        CancellationToken ct
    )
    {
        await using SqlCommand command = WorkspaceCommand(
            connection,
            transaction,
            $"""
            SELECT {SeriesFields}
            FROM dbo.WorkbenchImportedSeries source
            JOIN dbo.WorkbenchSeries s ON s.WorkspaceId = source.WorkspaceId AND s.Id = source.SeriesId
            WHERE source.WorkspaceId = @workspace AND source.SourceFileId = @file;
            """,
            workspace
        );
        command.Parameters.Add("@file", SqlDbType.Int).Value = fileId;
        await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? ReadSeries(reader) : null;
    }

    static async Task<WorkbenchSeries> CreateImportedSeriesAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        Guid workspace,
        ImportReviewSource source,
        string sha256,
        DateOnly from,
        DateOnly to,
        CancellationToken ct
    )
    {
        Guid series = Guid.NewGuid();
        string suffix = FormattableString.Invariant($", {source.State} · RadNet {source.Year}");
        string name =
            source.Station[..Math.Min(source.Station.Length, 100 - suffix.Length)] + suffix;
        string notes = FormattableString.Invariant(
            $"EPA RadNet annual CSV {source.FileName} (source file {source.FileId}). Matched the archive during import review. CSV SHA-256 {sha256}."
        );
        WorkbenchSeriesDraft draft = new(
            name,
            source.State,
            source.Station,
            from,
            to,
            WorkbenchReview.Pending,
            notes
        );
        await WriteSeriesAsync(
            connection,
            transaction,
            workspace,
            series,
            draft,
            source.ObservationCount,
            insert: true,
            ct
        );
        await using SqlCommand command = WorkspaceCommand(
            connection,
            transaction,
            """
            UPDATE dbo.WorkbenchImportedSeries SET SeriesId = @series
            WHERE WorkspaceId = @workspace AND SourceFileId = @file;
            IF @@ROWCOUNT = 0
                INSERT dbo.WorkbenchImportedSeries (WorkspaceId, SourceFileId, SeriesId)
                VALUES (@workspace, @file, @series);
            """,
            workspace
        );
        command.Parameters.Add("@file", SqlDbType.Int).Value = source.FileId;
        command.Parameters.Add("@series", SqlDbType.UniqueIdentifier).Value = series;
        await command.ExecuteNonQueryAsync(ct);
        return await GetSeriesAsync(connection, transaction, workspace, series, ct);
    }

    static async Task WriteSeriesAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        Guid workspace,
        Guid series,
        WorkbenchSeriesDraft draft,
        long observationCount,
        bool insert,
        CancellationToken ct
    )
    {
        string sql = insert
            ? """
                INSERT dbo.WorkbenchSeries (
                    WorkspaceId, Id, Name, [State], Station, FromDay, ToDay, Review, Notes, ObservationCount
                )
                VALUES (@workspace, @id, @name, @state, @station, @from, @to, @review, @notes, @count);
                """
            : """
                UPDATE dbo.WorkbenchSeries
                SET Name = @name, [State] = @state, Station = @station,
                    FromDay = @from, ToDay = @to, Review = @review, Notes = @notes, ObservationCount = @count,
                    Version = Version + 1
                WHERE WorkspaceId = @workspace AND Id = @id;
                """;
        await using SqlCommand change = WorkspaceCommand(connection, transaction, sql, workspace);
        change.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = series;
        change.Parameters.Add("@name", SqlDbType.NVarChar, 100).Value = draft.Name;
        change.Parameters.Add("@state", SqlDbType.Char, 2).Value = draft.State;
        change.Parameters.Add("@station", SqlDbType.NVarChar, 100).Value = draft.Station;
        change.Parameters.Add("@from", SqlDbType.Date).Value = draft.From.ToDateTime(
            TimeOnly.MinValue
        );
        change.Parameters.Add("@to", SqlDbType.Date).Value = draft.To.ToDateTime(TimeOnly.MinValue);
        change.Parameters.Add("@review", SqlDbType.Int).Value = (int)draft.Review;
        change.Parameters.Add("@notes", SqlDbType.NVarChar, 1000).Value = draft.Notes;
        change.Parameters.Add("@count", SqlDbType.BigInt).Value = observationCount;
        await change.ExecuteNonQueryAsync(ct);
    }

    internal async Task DeleteSeriesAsync(
        Guid workspace,
        Guid id,
        long version,
        CancellationToken ct
    )
    {
        await using SqlConnection connection = await OpenAsync(ct);
        await using SqlTransaction transaction = await BeginWorkspaceAsync(
            connection,
            workspace,
            "Exclusive",
            ct
        );
        CheckVersion(
            (await GetSeriesAsync(connection, transaction, workspace, id, ct)).Version,
            version
        );
        await BumpLinkedProjectsAsync(connection, transaction, workspace, id, ct);
        await using SqlCommand change = WorkspaceCommand(
            connection,
            transaction,
            "DELETE dbo.WorkbenchSeries WHERE WorkspaceId = @workspace AND Id = @id;",
            workspace
        );
        change.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = id;
        await change.ExecuteNonQueryAsync(ct);
        await transaction.CommitAsync(ct);
    }

    internal async Task<IReadOnlyList<WorkbenchProject>> ListProjectsAsync(
        Guid workspace,
        CancellationToken ct
    )
    {
        await using SqlConnection connection = await OpenAsync(ct);
        await using SqlCommand query = WorkspaceCommand(
            connection,
            null,
            SelectProjects,
            workspace
        );
        await using SqlDataReader reader = await query.ExecuteReaderAsync(ct);
        return await ReadProjectsAsync(reader, ct);
    }

    internal async Task<WorkbenchQuerySource> ReadQuerySourceAsync(
        Guid workspace,
        CancellationToken ct
    )
    {
        await using SqlConnection connection = await OpenAsync(ct);
        await using SqlTransaction transaction = await BeginWorkspaceAsync(
            connection,
            workspace,
            "Shared",
            ct
        );
        await using SqlCommand command = WorkspaceCommand(
            connection,
            transaction,
            $"""
            {SelectProjects}
            SELECT {SeriesFields} FROM dbo.WorkbenchSeries s WHERE s.WorkspaceId = @workspace;
            SELECT ProjectId, SeriesId FROM dbo.WorkbenchProjectSeries WHERE WorkspaceId = @workspace;
            """,
            workspace
        );
        await using SqlDataReader reader = await command.ExecuteReaderAsync(ct);
        List<WorkbenchProject> projects = await ReadProjectsAsync(reader, ct);
        await reader.NextResultAsync(ct);
        List<WorkbenchSeries> series = await ReadSeriesAsync(reader, ct);
        await reader.NextResultAsync(ct);
        List<WorkbenchQueryMembership> memberships = [];
        while (await reader.ReadAsync(ct))
            memberships.Add(new(reader.GetGuid(0), reader.GetGuid(1)));
        return new(projects, series, memberships);
    }

    static async Task<List<WorkbenchProject>> ReadProjectsAsync(
        DbDataReader reader,
        CancellationToken ct
    )
    {
        List<WorkbenchProject> projects = [];
        while (await reader.ReadAsync(ct))
            projects.Add(
                new(reader.GetGuid(0), reader.GetString(1), reader.GetInt64(2), reader.GetInt32(3))
            );
        return projects;
    }

    internal async Task<WorkbenchProject> CreateProjectAsync(
        Guid workspace,
        string name,
        CancellationToken ct
    )
    {
        Guid id = Guid.NewGuid();
        await using SqlConnection connection = await OpenAsync(ct);
        await using SqlTransaction transaction = await BeginWorkspaceAsync(
            connection,
            workspace,
            "Exclusive",
            ct
        );
        await using SqlCommand create = WorkspaceCommand(
            connection,
            transaction,
            """
            INSERT dbo.WorkbenchProjects (WorkspaceId, Id, Name) VALUES (@workspace, @id, @name);
            """,
            workspace
        );
        create.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = id;
        create.Parameters.Add("@name", SqlDbType.NVarChar, 100).Value = name;
        await create.ExecuteNonQueryAsync(ct);
        await transaction.CommitAsync(ct);
        return new(id, name, 1, 0);
    }

    internal async Task<WorkbenchProjectSnapshot> GetProjectAsync(
        Guid workspace,
        Guid id,
        CancellationToken ct
    )
    {
        await using SqlConnection connection = await OpenAsync(ct);
        await using SqlTransaction transaction = await BeginWorkspaceAsync(
            connection,
            workspace,
            "Shared",
            ct
        );
        return await GetProjectAsync(connection, transaction, workspace, id, ct);
    }

    internal async Task<WorkbenchProjectSnapshot> SetMembershipAsync(
        Guid workspace,
        Guid project,
        Guid series,
        long version,
        bool add,
        CancellationToken ct
    )
    {
        await using SqlConnection connection = await OpenAsync(ct);
        await using SqlTransaction transaction = await BeginWorkspaceAsync(
            connection,
            workspace,
            "Exclusive",
            ct
        );
        WorkbenchProjectSnapshot current = await GetProjectAsync(
            connection,
            transaction,
            workspace,
            project,
            ct
        );
        bool linked = current.Series.Any(row => row.Id == series);
        if (linked == add)
            return current;
        CheckVersion(current.Project.Version, version);
        if (add)
            await GetSeriesAsync(connection, transaction, workspace, series, ct);

        await ChangeMembershipAsync(connection, transaction, workspace, project, series, add, ct);
        WorkbenchProjectSnapshot result = await GetProjectAsync(
            connection,
            transaction,
            workspace,
            project,
            ct
        );
        await transaction.CommitAsync(ct);
        return result;
    }

    static async Task ChangeMembershipAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        Guid workspace,
        Guid project,
        Guid series,
        bool add,
        CancellationToken ct
    )
    {
        const string insert = """
            INSERT dbo.WorkbenchProjectSeries (WorkspaceId, ProjectId, SeriesId)
            VALUES (@workspace, @project, @series);
            """;
        const string delete = """
            DELETE dbo.WorkbenchProjectSeries
            WHERE WorkspaceId = @workspace AND ProjectId = @project AND SeriesId = @series;
            """;
        const string versionUpdate = """
            UPDATE dbo.WorkbenchProjects SET Version = Version + 1
            WHERE WorkspaceId = @workspace AND Id = @project;
            """;
        await using SqlCommand change = WorkspaceCommand(
            connection,
            transaction,
            (add ? insert : delete) + versionUpdate,
            workspace
        );
        change.Parameters.Add("@project", SqlDbType.UniqueIdentifier).Value = project;
        change.Parameters.Add("@series", SqlDbType.UniqueIdentifier).Value = series;
        await change.ExecuteNonQueryAsync(ct);
    }

    async Task<SqlConnection> OpenAsync(CancellationToken ct)
    {
        SqlConnection connection = new(_connectionString);
        try
        {
            await connection.OpenAsync(ct);
            if (!_schemaReady)
                await EnsureSchemaAsync(connection, ct);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    async Task EnsureSchemaAsync(SqlConnection connection, CancellationToken ct)
    {
        await _schemaLock.WaitAsync(ct);
        try
        {
            if (_schemaReady)
                return;

            await using SqlTransaction transaction = (SqlTransaction)
                await connection.BeginTransactionAsync(ct);
            await LockAsync(
                connection,
                transaction,
                "NuclearEvaluation.Workbench.Schema",
                "Exclusive",
                ct
            );
            await using SqlCommand create = new(Schema, connection, transaction);
            await create.ExecuteNonQueryAsync(ct);
            await transaction.CommitAsync(ct);
            _schemaReady = true;
        }
        finally
        {
            _schemaLock.Release();
        }
    }

    static async Task<SqlTransaction> BeginWorkspaceAsync(
        SqlConnection connection,
        Guid workspace,
        string mode,
        CancellationToken ct
    )
    {
        SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(ct);
        try
        {
            await LockAsync(
                connection,
                transaction,
                $"NuclearEvaluation.Workbench.{workspace:N}",
                mode,
                ct
            );
            await using SqlCommand lookup = WorkspaceCommand(
                connection,
                transaction,
                "SELECT 1 FROM dbo.WorkbenchWorkspaces WHERE Id = @workspace;",
                workspace
            );
            if (await lookup.ExecuteScalarAsync(ct) is null)
                throw new WorkbenchCookieException();
            return transaction;
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    static async Task LockAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string resource,
        string mode,
        CancellationToken ct
    )
    {
        await using SqlCommand command = new(
            """
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock
                @Resource = @resource, @LockMode = @mode, @LockOwner = 'Transaction', @LockTimeout = 5000;
            SELECT @result;
            """,
            connection,
            transaction
        );
        command.Parameters.Add("@resource", SqlDbType.NVarChar, 255).Value = resource;
        command.Parameters.Add("@mode", SqlDbType.VarChar, 32).Value = mode;
        object? result = await command.ExecuteScalarAsync(ct);
        if (result is not int value || value < 0)
            throw new TimeoutException("The workbench is busy. Try the request again.");
    }

    static SqlCommand WorkspaceCommand(
        SqlConnection connection,
        SqlTransaction? transaction,
        string sql,
        Guid workspace
    )
    {
        SqlCommand command = new(sql, connection, transaction);
        command.Parameters.Add("@workspace", SqlDbType.UniqueIdentifier).Value = workspace;
        return command;
    }

    static async Task<WorkbenchSeries> GetSeriesAsync(
        SqlConnection connection,
        SqlTransaction? transaction,
        Guid workspace,
        Guid id,
        CancellationToken ct
    )
    {
        await using SqlCommand query = WorkspaceCommand(
            connection,
            transaction,
            $"""
            SELECT {SeriesFields}
            FROM dbo.WorkbenchSeries s
            WHERE s.WorkspaceId = @workspace AND s.Id = @id;
            """,
            workspace
        );
        query.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = id;
        await using SqlDataReader reader = await query.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            throw new KeyNotFoundException();
        return ReadSeries(reader);
    }

    static async Task<WorkbenchProjectSnapshot> GetProjectAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        Guid workspace,
        Guid id,
        CancellationToken ct
    )
    {
        await using SqlCommand query = WorkspaceCommand(
            connection,
            transaction,
            $"""
            SELECT p.Id, p.Name, p.Version FROM dbo.WorkbenchProjects p
            WHERE p.WorkspaceId = @workspace AND p.Id = @id;
            SELECT {SeriesFields}
            FROM dbo.WorkbenchProjectSeries link
            JOIN dbo.WorkbenchSeries s ON s.WorkspaceId = link.WorkspaceId AND s.Id = link.SeriesId
            WHERE link.WorkspaceId = @workspace AND link.ProjectId = @id
            ORDER BY s.Name, s.Id;
            """,
            workspace
        );
        query.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = id;
        await using SqlDataReader reader = await query.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            throw new KeyNotFoundException();

        Guid projectId = reader.GetGuid(0);
        string name = reader.GetString(1);
        long version = reader.GetInt64(2);
        await reader.NextResultAsync(ct);
        List<WorkbenchSeries> series = await ReadSeriesAsync(reader, ct);
        return new(new(projectId, name, version, series.Count), series);
    }

    static async Task BumpLinkedProjectsAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        Guid workspace,
        Guid series,
        CancellationToken ct
    )
    {
        await using SqlCommand update = WorkspaceCommand(
            connection,
            transaction,
            """
            UPDATE p SET Version = Version + 1
            FROM dbo.WorkbenchProjects p
            JOIN dbo.WorkbenchProjectSeries link ON link.WorkspaceId = p.WorkspaceId AND link.ProjectId = p.Id
            WHERE link.WorkspaceId = @workspace AND link.SeriesId = @series;
            """,
            workspace
        );
        update.Parameters.Add("@series", SqlDbType.UniqueIdentifier).Value = series;
        await update.ExecuteNonQueryAsync(ct);
    }

    static async Task<List<WorkbenchSeries>> ReadSeriesAsync(
        DbDataReader reader,
        CancellationToken ct
    )
    {
        List<WorkbenchSeries> rows = [];
        while (await reader.ReadAsync(ct))
            rows.Add(ReadSeries(reader));
        return rows;
    }

    static WorkbenchSeries ReadSeries(DbDataReader reader) =>
        new(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            DateOnly.FromDateTime(reader.GetDateTime(4)),
            DateOnly.FromDateTime(reader.GetDateTime(5)),
            (WorkbenchReview)reader.GetInt32(6),
            reader.GetString(7),
            reader.GetInt64(8),
            reader.GetInt64(9)
        );

    static void CheckVersion(long current, long? expected)
    {
        if (current != expected)
            throw new WorkbenchConflictException();
    }

    public void Dispose() => _schemaLock.Dispose();

    sealed record SeedSeries(Guid Id, WorkbenchStation Station);
}

internal sealed record WorkbenchQuerySource(
    IReadOnlyList<WorkbenchProject> Projects,
    IReadOnlyList<WorkbenchSeries> Series,
    IReadOnlyList<WorkbenchQueryMembership> Memberships
);

internal sealed record WorkbenchQueryMembership(Guid ProjectId, Guid SeriesId);

internal sealed record WorkbenchProjectSnapshot(
    WorkbenchProject Project,
    IReadOnlyList<WorkbenchSeries> Series
);

internal sealed class WorkbenchConflictException : Exception;

internal sealed class WorkbenchCookieException : Exception;

# Implementation

Nuclear Evaluation combines a Blazor WebAssembly interface, a typed ASP.NET Core API, and SQL Server.
The browser manages interaction and component state, while the server executes relational queries,
processes uploads, and maintains the shared workspace.

## Query execution

Grid controls produce a `DataQuery` containing filters, ordering, pagination, and project scope.
The server translates it into an EF Core query and returns a `DataResult` with rows and a total count.
Read queries use `AsNoTracking` and typed `Include` expressions. Request cancellation flows through
to database execution.

Project membership and saved presets apply consistently to grids, counts, enum options, and charts.
Histogram aggregation runs in SQL over all matching records. Grid pages contain up to 500 rows.
Filter parsing preserves quoted values, and saved descriptors support defined scalar, array,
and list types.

## Spreadsheet processing

`TabularDataReader` exposes Excel worksheets and delimited text through a common CSV reader.
Worksheet conversion pulls one row at a time, and parsed rows are bulk-copied into SQL staging tables.
Each STEM preview session owns a connection and temporary tables shared across its API requests.

The upload lifecycle includes cancellation, cleanup of partial files and staged rows, and deletion
of rows belonging to a removed file. Idle sessions are evicted by the maintenance service.
The file input supports multiple uploads and includes populated examples and a blank template.

## Browser state

Sequence checks ensure that the latest grid request and validation result determine the displayed
state. Save controls remain disabled during validation. Grid results are cached for ten minutes,
with up to 64 entries in persistent browser storage, and refreshed from the API.

Query Builder edits remain separate from the applied query. Changing filter checkboxes or
conditions keeps the current result set until Apply query is selected. Loading a saved preset
applies it to the results immediately.

Project navigation reloads data for the selected ID. Project loading, counts, and charts expose
request failures with retry controls. The home page provides direct entry points into data
management, evaluation, and STEM preview.

## API and workspace lifecycle

Request validation checks page sizes, IDs, project fields, and preset descriptors. Missing project
updates return HTTP 404; unexpected API failures return Problem Details and are logged on the server.
Grid queries use the `DataResult` envelope for rows, totals, and errors.

The anonymous workspace uses proof-of-work verification, per-IP rate limits, and upload limits.
The WASM solver yields periodically to keep the interface responsive. A configured `Captcha:Secret`
keeps verification cookies valid across host restarts; otherwise, the host generates a process key.

The application runs on a single host. Preview sessions last until idle eviction or host restart.
Upload disk usage is cached for quota checks, and maintenance purges expired files and refreshes
the generated dataset on the configured schedule. EF Core migrations manage schema updates
separately from data initialization and resets.

## Calculation and data model

Decay correction uses each particle's analysis date and each APM record's sample date. Charts use
ordered bins with zero-count categories included. The [calculation model](calculation-notes.md)
defines the formula, isotope constants, and bin boundaries.

The generated dataset contains related projects, series, samples, subsamples, and measurements.
Series IDs start at 10000; sample, subsample, APM, and particle IDs start at 1. These ranges
are consistent for initial seeding and sandbox resets. External codes are sequential within
each parent, and event dates follow their processing sequence.

## Automated verification

The .NET test suite contains 90 tests:

| Test project | Tests | Coverage |
|---|---:|---|
| TabularDataReader | 12 | CSV and Excel parsing, culture handling, error propagation, and resource lifetime |
| Client | 42 | Components, HTTP client behavior, query validation, filter serialization, and asynchronous request ordering |
| Server | 36 | Captcha, upload cleanup, SQL query translation, charts, decay calculations, migrations, and seed consistency |

SQL integration tests create disposable databases. The seed test executes the full script with
dataset targets reduced to 20 series and 5 projects, covering both initial seeding and reset. CI runs the SQL tests alongside the component
and reader tests; local runs enable them through `NUCLEAR_TEST_SQL`.

The browser suite covers navigation, project editing, query building, uploads, and responsive layout.
Its scenarios and expected behavior are documented in the [test checklist](testing/test-cases.md).
See the [README](../README.md#tests) for commands to run each suite.

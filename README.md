# Nuclear Evaluation

A public workspace for managing and analyzing environmental radiation measurements. Create
editable Series from the EPA RadNet archive, group them into Projects, and follow published
observations back to their source files. Radiochemistry also provides access to the European
Commission's Radioactivity Environmental Monitoring Database (REMdb).

Built with Blazor WebAssembly, ASP.NET Core, SQL Server, and read-only DuckDB snapshots.

**[Try the live demo](https://nuclearevaluation.com/)**

### What to try

- **Data Workbench** opens the Series grid with editable metadata and paged source files,
  observations, and channels. Manage Project membership to update its chart, distinct
  observation count, and observation grid. Query Builder applies filters from Projects through
  gamma channels along the same branch, with an optional minimum count of distinct matching
  observations per Series. Column filters accept two conditions with AND or OR and refine
  the returned grid without changing those qualifying counts. Try the
  **California · 2025 R03 coverage review** preset.
- **Radiation Monitoring** opens the station-to-channel hierarchy, a Query Builder with
  browser-local presets, and File Quality. Each observation identifies its original CSV record.
- **Radiochemistry** browses REMdb sites, sampling windows, and radionuclide results from 40 annual
  releases. Use Query Builder to join conditions across the hierarchy, or File Quality to see
  published units, value types, missing values, and reporting-limit qualifiers.
- **Fleet Explorer** shows reactor capacity, state rankings, and monthly generation for two
  selected plants. See [the source snapshot](docs/fleet-data.md).
- **Generation Calendar** shows monthly or daily-average generation by state, with a plant-level
  breakdown for the selected month of the 2024 snapshot and CSV exports for both measures.
- **Energy & Emissions** compares published lifecycle factors for six electricity sources and
  estimates totals for a generation amount you choose. Fleet and Calendar links can fill that amount.
- **Sources & Methods** links to the public datasets and explains their units and processing.

### Design choices

The browser owns component state and calls typed HTTP APIs. DuckDB serves each archive's filters,
file quality, counts, and pagination. Gamma channels are projected from the observation's
published count-rate columns. SQL Server persists each browser's Series, Projects, and memberships
in separate workbench tables. Project queries pass those Series ranges to DuckDB and count their
union, so overlapping ranges never double-count a published observation. The legacy isotope
and STEM workflows are available as opt-in fixtures for tests.

[Implementation notes](docs/implementation.md) describe the legacy fixture architecture and test
coverage.

## Architecture

| Project | Type | Role |
|---|---|---|
| `NuclearEvaluation.Client` | Blazor WebAssembly | All UI (pages, Radzen components, grids, charts, query builder) |
| `NuclearEvaluation.Server` | ASP.NET Core | Web API controllers + hosts the WASM bundle; sandbox/captcha/rate-limiting |
| `NuclearEvaluation.Shared` | Class library | Domain/view models, enums, query-builder filters, and the `INuclearEvaluationApi` contract (referenced by both client and server) |
| `NuclearEvaluation.Kernel` | Class library | EF Core `DbContext`, migrations, query execution, embedded seed script |
| `Kerajel.Primitives` | Class library | Vendored helper types (`OperationResult`, `Debouncer`, …) |
| `Kerajel.TabularDataReader` | Class library | Compatibility reader for legacy Excel and inputs DuckDB cannot parse with the existing STEM rules |

The client calls the host over HTTP and JSON after the human check. RadNet and REMdb each use a
typed field catalog and query contract. With fixtures enabled, the legacy grids use `DataQuery`
over SQL Server or embedded DuckDB for STEM Preview.

## Running locally with Docker

The repository ships a `docker-compose.yml` that runs SQL Server 2022 and the app together.

```bash
docker compose up --build
```

Then open <http://localhost:8080>. Development mode skips the proof-of-work captcha.

For legacy fixture development, run `NUCLEAR_TEST_FIXTURES=true docker compose up --build`.
The app migrates and seeds SQL Server in this mode. The database and uploaded files persist in
named volumes (`mssql-data`, `app-storage`).

## Running locally without Docker

With the .NET 11 SDK installed, run:

```bash
dotnet run --project src/NuclearEvaluation.Server
```

To run the legacy fixtures, set `Sandbox__UseTestFixtures=true` and configure
`ConnectionStrings__NuclearEvaluationServerDbConnection` for a disposable SQL Server database.

Data Workbench uses `ConnectionStrings__Workbench` or the existing SQL Server connection string.
Install the RadNet snapshot and provision the workbench tables on that same database. The app
initializes its isolated tables on first use when its connection has DDL permissions.

### Build the EPA RadNet snapshot

Build the snapshot outside the repository with Python 3.11 or later and DuckDB 1.5.6, the same
DuckDB release used by the server. The default download covers the available 2016–2025 station
files. Keep several GB free for the archive cache, staged CSVs, and database.

```bash
mkdir -p "$HOME/.local/share/nuclear-evaluation/radnet"
python3 -m venv "$HOME/.local/share/nuclear-evaluation/radnet/venv"
radnet_dir="$HOME/.local/share/nuclear-evaluation/radnet"
"$radnet_dir/venv/bin/pip" install duckdb==1.5.6
"$radnet_dir/venv/bin/python" scripts/import-radnet.py \
  --cache "$radnet_dir/archives" --output "$radnet_dir/radnet.duckdb"
RadNet__SnapshotPath="$radnet_dir/radnet.duckdb" dotnet run --project src/NuclearEvaluation.Server
```

The server also looks for `App_Data/radnet.duckdb` under its content root. The file lives outside
`wwwroot`. Place the snapshot on the host separately from the application publish. The Radiation
workspace shows an unavailable state until the snapshot is installed.

The importer retains the archive URL, SHA-256, member path, and source record number. It uses
the published UTC timestamps and units, leaves missing values empty, and keeps one record per
station and timestamp. Ties favor approved records, then the most complete record, then the
first source record. Imported counts exclude rows with unparseable or wrong-year timestamps or
no usable measurement. Source-file quality shows the input, duplicate, and rejected row counts.
The public data comes from the [EPA archive index](https://www.epa.gov/radnet/radnet-csv-file-downloads)
under [EPA's data terms](https://edg.epa.gov/EPA_Data_License.html).

### Try Import Review

**Data Workbench → Import Review → Full archive** opens the installed RadNet DuckDB snapshot
directly. The 2016–2025 snapshot contains 10,058,284 observations. DuckDB filters, sorts, and
pages them on the server. Each result shows its original CSV name and record number.

**Annual CSV** uses the Colorado Springs 2025 CSV from the same EPA download. Extract it from
the archive cache and set `ImportReview__SamplePath` when starting the server, or place the file
at `App_Data/radnet-import-sample.csv` under the server's content root.

```bash
unzip -p "$radnet_dir"/archives/*-co_colorado_springs_2025-2008.zip \
  'co_colorado_springs_2025-2008/CO_COLORADO_SPRINGS_2025.csv' \
  > "$radnet_dir/CO_COLORADO_SPRINGS_2025.csv"
export ImportReview__SamplePath="$radnet_dir/CO_COLORADO_SPRINGS_2025.csv"
```

Stage the supplied sample or upload a copy of this annual file to inspect its source records,
including duplicates, rejected rows, and differences from the archive. A complete match can be
saved as a Series and linked to a Project.

### Build the JRC REMdb snapshot

Use Python 3.11 or later, DuckDB 1.5.6, and a workspace outside the repository with several GB free.

```bash
remdb_dir="$HOME/.local/share/nuclear-evaluation/remdb"
mkdir -p "$remdb_dir"
python3 -m venv "$remdb_dir/venv"
"$remdb_dir/venv/bin/pip" install duckdb==1.5.6
"$remdb_dir/venv/bin/python" scripts/import-remdb.py \
  --archive-cache "$remdb_dir/archives" --output "$remdb_dir/remdb.duckdb"
RemDb__SnapshotPath="$remdb_dir/remdb.duckdb" dotnet run --project src/NuclearEvaluation.Server
```

The server also reads `App_Data/remdb.duckdb` relative to its content root. Install the snapshot
outside `wwwroot` and separately from the app publish. Radiochemistry shows an unavailable state
until the snapshot is installed.

The importer uses the bulk 1984–2006 and 2007–2011 archives, then the individual 2012–2023 releases.
It checks annual CSV headers and published row count footers and retains the archive URL, SHA-256,
member path, and CSV record number. Sites and sampling windows group published fields. Windows
use sampling timestamps at their published precision. Activity values, units, reporting-limit
qualifiers, and value types are retained for inspection. The 1984–2011 files omit the separate
qualifier column. The 1986 source splits some results across consecutive rows. The importer
rejoins the recognized pairs and cites the first row.

Source attribution: European Commission, Joint Research Centre (JRC), Radioactivity Environmental
Monitoring Database (REMdb). [Official archive](https://jeodpp.jrc.ec.europa.eu/ftp/jrc-opendata/REMDB/datasets/) ·
[CC BY 4.0 reuse notice](https://jeodpp.jrc.ec.europa.eu/ftp/jrc-opendata/REMDB/datasets/copyright.txt).

### Site assistant and MCP

Run the assistant host on loopback:

```bash
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://127.0.0.1:8081 Assistant__Enabled=true \
  dotnet run --project src/NuclearEvaluation.Server --no-launch-profile
```

Open <http://127.0.0.1:8081> and use **Assistant** in the bottom-right corner. The popup follows
the active page and tab. In Radiochemistry, ask “Show Cs-137 results for Austrian cow's milk in
2023 below the reporting limit.” The assistant fills the editor and runs the query. On the
Generation Calendar, try “Show Illinois in February as a daily average.”

In Development, the app connects to its MCP server over Streamable HTTP at
`http://127.0.0.1:8081/mcp`. It caches
the catalog from `describe_site_assistant` on first use and sends each proposed action through
`validate_site_assistant_action` before applying it. `get_site_help` supplies the site's help
text. If you change the port, set `Assistant__McpUrl` to the matching loopback URL.

For natural-language requests across the site, set an [OpenAI API project key](https://developers.openai.com/api/docs/quickstart)
as `OPENAI_API_KEY` in the server process environment or as `OpenAI:ApiKey` in the server project's
.NET user secrets, then restart the host. `OpenAI:Model` overrides the default `gpt-5-mini`.
The local parser handles common Series questions in legacy fixture mode without an API key.

In Production, the server enables the chat when an API key is configured. The human check protects
chat and status requests, and chat accepts up to five requests per client IP and 60 across the app
in each rolling minute per server process. The production app calls its MCP tools in-process.
The HTTP MCP endpoint is available only in enabled local Development. Set
`Assistant__Enabled=false` on the host to turn off production chat.

## Abuse protection & ephemeral data

The `Sandbox` configuration section governs per-IP request limits and these additional controls
for legacy fixtures:

- **Upload rate** — per-IP daily cap.
- **Upload caps** — 64 MiB per STEM file (`UploadLimits`). Upload admission checks the global storage
  threshold against retained uploads and temporary DuckDB spill files before accepting a new file.
  `Sandbox:StemPreviewMaxConcurrentOperations` bounds simultaneous DuckDB operations (two by default).
- **Ephemeral data** — a background service purges expired upload folders, evicts idle STEM
  sessions (closing their DuckDB connections and deleting their uploads), and resets SQL Server to seed once per
  interval (tracked in `DBO.SandboxState` so it survives app-pool recycling).

Successful human checks are remembered for 30 days, including across server restarts. Without a
configured `Captcha:Secret`, the server persists private data protection keys in
`App_Data/CaptchaKeys` under the app content root. The IIS worker needs write access to this
directory; preserve it across deployments and restrict access to the application identity.
`Captcha:KeyDirectory` overrides the location. An existing private `Captcha:Secret` remains
supported and keeps its current cookies valid. Difficulty and cookie lifetime can also be set
under `Captcha`.

When the host blocks writes outside the site directory, set `Sandbox__StorageDirectory` to
`App_Data/NuclearEvaluationStorage` on the host. Relative paths resolve against the app content
root, and the IIS worker needs write access. Keep this directory outside `wwwroot` because it
holds temporary uploads and DuckDB spill files. Docker uses the sibling storage volume by default.

## Tests

```bash
dotnet test
```

- `NuclearEvaluation.Client.Tests` — bUnit component tests with a mocked API (no database).
- `Kerajel.TabularDataReader.Tests` — CSV/Excel parsing, culture, error propagation, and resource lifetime.
- `NuclearEvaluation.Server.Tests` — captcha validation, upload cleanup, embedded DuckDB preview tests, and optional SQL Server integration tests.

The SQL tests create and delete a uniquely named database.
CI runs them against its disposable SQL Server service. To enable them locally, set `NUCLEAR_TEST_SQL`
to a connection string whose login can create and delete test databases. Without it, those tests
are explicitly skipped.

To keep build output outside the checkout:

```bash
dotnet test --configuration Release --artifacts-path /tmp/nuclear-evaluation-build
```


End-to-end browser regression tests live in `tests/e2e`. Start the app with fixtures enabled
against a disposable local or staging database, then run:

```bash
cd tests/e2e
npm ci
npx playwright install chromium
npm test
```

The suite uses `http://localhost:8080` by default. Override it with `E2E_BASE_URL` when testing another disposable target. Use `E2E_WORKERS` to tune browser parallelism, or `npm run test:serial` for one-worker debugging.

For repeatable agent and CI runs, use the repository script. It creates an isolated Compose project, assigns random host ports so it can run beside the development stack, and removes its database and storage volumes when finished:

```bash
./scripts/run-e2e.sh
```

The Docker e2e profile defaults to two Playwright workers. Override `E2E_WORKERS` when needed:

```bash
E2E_WORKERS=4 ./scripts/run-e2e.sh
```

The checked-in Playwright Codex agents can explore the running app, turn the scenario checklist in `docs/testing/test-cases.md` into executable tests, and investigate failures. Their seed test is `tests/e2e/specs/seed.spec.ts`. Generated tests run through the same Playwright configuration and CI job as hand-written tests.

## Production deployment

Production site: <https://nuclearevaluation.com/>.

Production is hosted on **smarterasp.net** (shared Windows hosting) and is published directly —
Docker is for local development only. Do not deploy the repository root as a Docker build unless
the hosting account is explicitly configured for containers.

### GitHub Actions

The CI workflow publishes a self-contained Windows x64 release after `build-and-test` passes. It
retains the publish output as an artifact for 14 days and uses Web Deploy to sync the release to IIS.
The release preserves the IIS `web.config`, production `appsettings*.json`, and files that exist
only on the server. Update production configuration through the host. After the sync, the workflow
checks the live commit marker and `/api/captcha/status`.

The GitHub `production` environment allows deployments from `master`. Configure
`SMARTERASP_WEBDEPLOY_URL` and `SMARTERASP_WEBDEPLOY_SITE` as environment variables and
`SMARTERASP_WEBDEPLOY_USERNAME` and `SMARTERASP_WEBDEPLOY_PASSWORD` as environment secrets. The
[SmarterASP.NET Web Deploy panel](https://www.smarterasp.net/support/kb/a2097/where-can-i-find-my-web-deploy-login-info.aspx)
provides the current connection settings.

In **Actions → CI → Run workflow**, choose `master` and `plan` to preview the sync. Choose `deploy`
to release manually. Set the repository variable `AUTO_DEPLOY_PRODUCTION` to `true` after verifying
the first deployment to enable releases on subsequent pushes to `master`. Remove the variable to
pause automatic releases.

### Manual publish

For a direct publish, build the self-contained Windows x64 host, which bundles the .NET runtime
and WASM client:

```bash
dotnet publish src/NuclearEvaluation.Server -c Release -r win-x64 --self-contained true -o ./publish
```

Deploy the contents of `./publish` and supply `ConnectionStrings:NuclearEvaluationServerDbConnection`
and, optionally, `Captcha:Secret` via the host's configuration.

Publish the server and its bundled WASM client together. EF Core migrations manage the database
schema; sandbox initialization and scheduled resets manage the example data.

SmarterASP.NET Auto Build requires the .NET 11 SDK on the provider's build system. Use the direct
publish above if that SDK is unavailable. SmarterASP.NET's Railpack flow restores before nested
project folders are copied into the build context, so ordinary `ProjectReference` dependencies
fail later at `dotnet publish --no-restore`.

Create the upload archive with the repository script instead:

```powershell
.\scripts\package-smarterasp-autobuild.ps1
```

The script creates an ignored `artifacts/smarterasp/*.zip` source archive shaped specifically for
SmarterASP.NET Auto Build. The archive root contains exactly one detected ASP.NET Core project,
no root `Dockerfile`, the server source, flattened library source folders without their `.csproj`
files, a prebuilt client `wwwroot`, `appsettings.json`, and a custom `web.config` for ASP.NET Core
Module hosting. It also validates the package with `dotnet restore` followed by
`dotnet publish --no-restore`, matching the provider's build order.

Recommended SmarterASP.NET publish settings for the Auto Build project:

- `RuntimeIdentifier=win-x64`
- `SelfContained=true`
- `ServerGarbageCollection=false`
- `IsTransformWebConfigDisabled=true` when supplying a custom `web.config`
- `StaticWebAssetsEnabled=false` when the client `wwwroot` is already included in the archive

These properties are injected into the generated upload package by
`scripts/package-smarterasp-autobuild.ps1`; keep the repository itself Docker-first.
Before publishing this version, [switch the SmarterASP.NET IIS application pool to 64-bit](https://www.smarterasp.net/support/kb/a2249/how-to-change-the-application-pool-to-64-bit-pool.aspx).
The package uses native DuckDB and must run in a 64-bit process.

On startup the app applies EF Core migrations before accepting traffic. The full sandbox seed and
reset work runs in the background so shared-hosting startup timeouts do not block the site launch.

## License

MIT Licensed.

# Nuclear Evaluation

A personal web application for exploring nuclear-material evaluation data: projects, series,
samples, isotope measurements, charts, and spreadsheet previews.

Built with Blazor WebAssembly, ASP.NET Core, and SQL Server, it brings relational data exploration,
reusable queries, isotope charts, and spreadsheet processing into one interactive workspace.

**[Try the live demo](https://nuclearevaluation.com/)**

### What to try

- **Fleet Explorer:** select a U.S. nuclear plant from the map or list, view capacity by reactor,
  and compare two plants' monthly electricity generation. See [the source snapshot](docs/fleet-data.md).
- **Energy & Emissions:** compare published lifecycle greenhouse gas estimates across six electricity
  sources and inspect the estimates for each lifecycle phase.
- **Data Management:** browse and filter series, expand their samples, and inspect matching totals.
- **Evaluation:** open a project, change its series membership, and explore isotope distributions.
- **Query Builder:** combine filters across related entities and save reusable presets.
- **STEM Preview:** upload Excel or delimited text files, inspect their rows, and remove individual
  files. Open **Quality** to profile a file and inspect the rows that match a check.

The evaluation workspace is shared and anonymous. It uses **generated example data**, and changes
are periodically reset. Uploads are temporary. The fleet and emissions sections use published data.
See [the calculation model](docs/calculation-notes.md) for decay correction, reference dates, and
histogram definitions.

### Design choices

The browser owns component state and calls a typed HTTP API. SQL Server handles data management,
evaluation queries, and histogram aggregation. STEM Preview retains the original uploads for the
session and appends each file to a DuckDB temporary table. DuckDB handles preview filtering,
sorting, counts, and pagination.

The application runs as a single host with an anonymous shared workspace.
[Implementation notes](docs/implementation.md) describe query execution, upload processing,
browser state, and automated test coverage.

## Architecture

| Project | Type | Role |
|---|---|---|
| `NuclearEvaluation.Client` | Blazor WebAssembly | All UI (pages, Radzen components, grids, charts, query builder) |
| `NuclearEvaluation.Server` | ASP.NET Core | Web API controllers + hosts the WASM bundle; sandbox/captcha/rate-limiting |
| `NuclearEvaluation.Shared` | Class library | Domain/view models, enums, query-builder filters, and the `INuclearEvaluationApi` contract (referenced by both client and server) |
| `NuclearEvaluation.Kernel` | Class library | EF Core `DbContext`, migrations, query execution, embedded seed script |
| `Kerajel.Primitives` | Class library | Vendored helper types (`OperationResult`, `Debouncer`, …) |
| `Kerajel.TabularDataReader` | Class library | Compatibility reader for legacy Excel and inputs DuckDB cannot parse with the existing STEM rules |

The client talks to the server only through the typed `INuclearEvaluationApi` (HTTP + JSON).
Grids translate Radzen `LoadDataArgs` into a serializable `DataQuery`; the server runs it against
SQL Server for evaluation data and embedded DuckDB for STEM Preview.

```
Browser ──HTTP/JSON──> NuclearEvaluation.Server (API + WASM host) ──EF Core / linq2db──> SQL Server
   │  (WASM: NuclearEvaluation.Client)                 │
   └─ proof-of-work captcha gate                       ├─ embedded DuckDB (STEM Preview)
                                                       └─ SandboxMaintenanceService (purge + nightly reset)
```

## Running locally with Docker

The repository ships a `docker-compose.yml` that runs SQL Server 2022 and the app together.

```bash
docker compose up --build
```

Then open <http://localhost:8080>. On first run the app applies EF Core migrations and seeds
the database; the proof-of-work captcha appears once, after which a cookie remembers you.

SQL Server data and uploaded files persist in named Docker volumes (`mssql-data`, `app-storage`).

## Running locally without Docker

Requirements: .NET SDK 11.0 and a reachable SQL Server instance.

1. Put your connection string in `src/NuclearEvaluation.Server/appsettings.Development.json`
   (git-ignored):

   ```json
   {
     "ConnectionStrings": {
       "NuclearEvaluationServerDbConnection": "Server=localhost;Database=NuclearEvaluation;Trusted_Connection=True;TrustServerCertificate=True;"
     }
   }
   ```

2. Run the host (it migrates and seeds on startup):

   ```bash
   dotnet run --project src/NuclearEvaluation.Server
   ```

To manage the schema by hand instead:

```bash
cd src/NuclearEvaluation.Server
dotnet tool restore
dotnet ef database update --project ../NuclearEvaluation.Kernel --startup-project .
```

The setup/seed SQL lives at `src/NuclearEvaluation.Kernel/Data/Seed/NuclearEvaluationServerDbSetUp.sql`
and is embedded into the Kernel assembly so the app can run it for both first-time setup and the
nightly reset.

## Abuse protection & ephemeral data

Because the site is public and anonymous, the `Sandbox` configuration section governs:

- **Rate limiting** — per-IP request window plus a stricter per-IP daily cap on uploads.
- **Upload caps** — 64 MiB per STEM file (`UploadLimits`). Upload admission checks the global storage
  threshold against retained uploads and temporary DuckDB spill files before accepting a new file.
  `Sandbox:StemPreviewMaxConcurrentOperations` bounds simultaneous DuckDB operations (two by default).
- **Ephemeral data** — a background service purges expired upload folders, evicts idle STEM
  sessions (closing their DuckDB connections and deleting their uploads), and resets SQL Server to seed once per
  interval (tracked in `DBO.SandboxState` so it survives app-pool recycling).

Set a private `Captcha:Secret` and the connection string on the production host. If no captcha
secret is configured, the server generates a random key for that process, so verification cookies
stop working when it restarts. Difficulty and cookie lifetime can also be set under `Captcha`.

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


End-to-end browser regression tests live in `tests/e2e`. Start the app first against a disposable local or staging database, then run:

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

For a direct publish, build the self-contained Windows x64 host, which bundles the .NET runtime
and WASM client:

```bash
dotnet publish src/NuclearEvaluation.Server -c Release -r win-x64 --self-contained true -o ./publish
```

Deploy the contents of `./publish` and supply `ConnectionStrings:NuclearEvaluationServerDbConnection`
and `Captcha:Secret` via the host's configuration.

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

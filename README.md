# Nuclear Evaluation

A .NET 10 web application for exploring nuclear-material evaluation data across projects,
series, particle samples, sub-samples, and APM (Alpha Particle Measurement) records. It brings
uranium-isotope analysis, decay correction, grids, charts, STEM upload previews, and reusable
query presets into one evaluation workspace.

The public sandbox is anonymous by design. A self-hosted proof-of-work captcha, rate limits,
upload caps, and scheduled resets keep the demo available without user accounts.

## Architecture

| Project | Type | Role |
|---|---|---|
| `NuclearEvaluation.Client` | Blazor WebAssembly | All UI (pages, Radzen components, grids, charts, query builder) |
| `NuclearEvaluation.Server` | ASP.NET Core | Web API controllers + hosts the WASM bundle; sandbox/captcha/rate-limiting |
| `NuclearEvaluation.Shared` | Class library | View models, enums, query-builder filters, and the `INuclearEvaluationApi` contract (referenced by both client and server) |
| `NuclearEvaluation.Kernel` | Class library | EF Core `DbContext`, domain entities, migrations, query execution, embedded seed script |
| `Kerajel.Primitives` | Class library | Vendored helper types (`OperationResult`, `Debouncer`, …) |
| `Kerajel.TabularDataReader` | Class library | Vendored delimited-text/Excel reader used by STEM preview parsing |

The client talks to the server only through the typed `INuclearEvaluationApi` (HTTP + JSON).
Grids translate Radzen `LoadDataArgs` into a serializable `DataQuery`; the server maps that
onto EF Core queries.

```
Browser ──HTTP/JSON──> NuclearEvaluation.Server (API + WASM host) ──EF Core / linq2db──> SQL Server
   │  (WASM: NuclearEvaluation.Client)                 │
   └─ proof-of-work captcha gate                       └─ SandboxMaintenanceService (purge + nightly reset)
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

Requirements: .NET SDK 10.0 and a reachable SQL Server instance.

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
dotnet ef database update --project src/NuclearEvaluation.Kernel --startup-project src/NuclearEvaluation.Server
```

The setup/seed SQL lives at `src/NuclearEvaluation.Kernel/Data/Seed/NuclearEvaluationServerDbSetUp.sql`
and is embedded into the Kernel assembly so the app can run it for both first-time setup and the
nightly reset.

## Abuse protection & ephemeral data

Because the site is public and anonymous, the `Sandbox` configuration section governs:

- **Rate limiting** — per-IP request window plus a stricter per-IP daily cap on uploads.
- **Upload caps** — ~64 MB per file (`UploadLimits`) and a global storage ceiling that blocks
  uploads once exceeded.
- **Ephemeral data** — a background service purges expired upload folders, evicts idle STEM
  sessions (dropping their throwaway temp tables), and resets the database to seed once per
  interval (tracked in `DBO.SandboxState` so it survives app-pool recycling).

The proof-of-work captcha secret and difficulty live under the `Captcha` section. Override
both `Captcha:Secret` and the connection string in production.

## Tests

```bash
dotnet test
```

- `NuclearEvaluation.Client.Tests` — bUnit component tests with a mocked API (no database).
- `Kerajel.TabularDataReader.Tests` — CSV/Excel reader tests.


End-to-end browser regression tests live in `tests/e2e`. Start the app first against a disposable local or staging database, then run:

```bash
cd tests/e2e
npm install
npx playwright install chromium
npm test
```

The suite uses `http://localhost:8080` by default. Override it with `E2E_BASE_URL` when testing another disposable target. Use `E2E_WORKERS` to tune browser parallelism, or `npm run test:serial` for one-worker debugging.

Or run the browser suite through Docker Compose, which starts the app dependencies and uses a dedicated Playwright test container:

```bash
docker compose --profile e2e up --build --abort-on-container-exit --exit-code-from e2e e2e
```

The Docker e2e profile defaults to 2 Playwright workers, which keeps the local SQL Server
container steadier while still running tests in parallel. Override with `E2E_WORKERS` for a
faster stress run, for example:

```powershell
$env:E2E_WORKERS = "4"
docker compose --profile e2e up --build --abort-on-container-exit --exit-code-from e2e e2e
```
## Production deployment

Production is hosted on **smarterasp.net** (shared Windows hosting) and is published directly —
Docker is for local development only. Do not deploy the repository root as a Docker build unless
the hosting account is explicitly configured for containers.

For a direct .NET publish, publish the host project, which bundles the WASM client:

```bash
dotnet publish src/NuclearEvaluation.Server -c Release -o ./publish
```

Deploy the contents of `./publish` and supply `ConnectionStrings:NuclearEvaluationServerDbConnection`
and `Captcha:Secret` via the host's configuration.

For SmarterASP.NET Auto Build, use a source archive shaped as a single ASP.NET Core project at
the archive root. The root must contain the `.csproj` that SmarterASP.NET should detect and build;
avoid placing a `Dockerfile` at that archive root. The archive should include the server source,
the referenced library source folders, the prebuilt client `wwwroot`, `appsettings.json`, and a
`web.config` for ASP.NET Core Module hosting.

Recommended SmarterASP.NET publish settings for the Auto Build project:

- `RuntimeIdentifier=win-x86`
- `SelfContained=true`
- `ServerGarbageCollection=false`
- `IsTransformWebConfigDisabled=true` when supplying a custom `web.config`
- `StaticWebAssetsEnabled=false` when the client `wwwroot` is already included in the archive

On startup the app applies EF Core migrations before accepting traffic. The full sandbox seed and
reset work runs in the background so shared-hosting startup timeouts do not block the site launch.

## License

MIT Licensed.

# Nuclear Evaluation

A .NET 9 web application for exploring nuclear-material evaluation data: particle samples,
sub-samples, and APM (Alpha Particle Measurement) records organised into series and projects,
with uranium-isotope analysis, decay correction, and a query builder. UI built with
[Radzen](https://blazor.radzen.com/) Blazor components.

Originally a server-side Blazor app, it is now a **Blazor WebAssembly client + ASP.NET Core
Web API**, deployed as a single site. It runs **anonymously** — no accounts — behind a
self-hosted proof-of-work captcha, with abuse limits and an ephemeral, self-resetting dataset.

## Architecture

| Project | Type | Role |
|---|---|---|
| `NuclearEvaluation.Client` | Blazor WebAssembly | All UI (pages, Radzen components, grids, charts, query builder) |
| `NuclearEvaluation.Server` | ASP.NET Core | Web API controllers + hosts the WASM bundle; sandbox/captcha/rate-limiting |
| `NuclearEvaluation.Shared` | Class library | View models, enums, query-builder filters, and the `INuclearEvaluationApi` contract (referenced by both client and server) |
| `NuclearEvaluation.Kernel` | Class library | EF Core `DbContext`, domain entities, migrations, query execution, embedded seed script |
| `Kerajel.Primitives` | Class library | Vendored helper types (`OperationResult`, `Debouncer`, …) |
| `Kerajel.TabularDataReader` | Class library | Vendored CSV/Excel reader used by STEM preview parsing |

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

Requirements: .NET SDK 9.0 and a reachable SQL Server instance.

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

## Production deployment

Production is hosted on **smarterasp.net** (shared Windows hosting) and is published directly —
Docker is for local development only. Publish the host project, which bundles the WASM client:

```bash
dotnet publish src/NuclearEvaluation.Server -c Release -o ./publish
```

Deploy the contents of `./publish` and supply `ConnectionStrings:NuclearEvaluationServerDbConnection`
and `Captcha:Secret` via the host's configuration.

## License

MIT Licensed.

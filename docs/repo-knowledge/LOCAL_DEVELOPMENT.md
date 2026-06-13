# Local Development

## Required Runtimes and Tools

| Tool | Version | Notes |
|---|---|---|
| .NET SDK | 9.0 | All projects target `net9.0` |
| SQL Server | 2022 (or any recent) | Provided by docker-compose, or bring your own |
| Docker | recent | Optional, but the simplest way to run the full stack |

## Option A — Docker (recommended)

```
docker compose up --build
```

This starts SQL Server 2022 and the app. Open <http://localhost:8080>. On first run the app
applies migrations and seeds the database, then shows the proof-of-work captcha once.

## Option B — Run the host directly

1. Create `src/NuclearEvaluation.Server/appsettings.Development.json` (git-ignored) with a
   connection string:

   ```json
   {
     "ConnectionStrings": {
       "NuclearEvaluationServerDbConnection": "Server=localhost;Database=NuclearEvaluation;Trusted_Connection=True;TrustServerCertificate=True;"
     }
   }
   ```

2. `dotnet run --project src/NuclearEvaluation.Server` (migrates and seeds on startup).

Only the Server project needs to run; it serves the WASM client. For hot reload of the client UI
you can also run `dotnet watch --project src/NuclearEvaluation.Client`, but the API must be served
by the Server.

## Database

EF Core migrations live in `src/NuclearEvaluation.Kernel/Data/Migrations` (single `InitialCreate`).
Apply them manually with:

```
dotnet ef database update --project src/NuclearEvaluation.Kernel --startup-project src/NuclearEvaluation.Server
```

Views and seed data come from the embedded `Data/Seed/NuclearEvaluationServerDbSetUp.sql`, which is
idempotent (it clears and reseeds), so it doubles as the nightly sandbox reset.

## STEM sample files

Template/sample files live in `src/NuclearEvaluation.Client/wwwroot/files/stem-preview/` and are
served as static assets for the STEM preview feature.

## Configuration

`src/NuclearEvaluation.Server/appsettings.json` holds non-secret defaults (logging, the `Sandbox`
section). Secrets — the connection string and `Captcha:Secret` — belong in
`appsettings.Development.json` (local) or environment variables (Docker/production).

| Section | Purpose |
|---|---|
| `ConnectionStrings:NuclearEvaluationServerDbConnection` | SQL Server connection |
| `Sandbox:*` | Rate limits, upload retention, storage ceiling, reset cadence |
| `Captcha:Secret`, `Captcha:MaxNumber` | Proof-of-work signing key and difficulty |

## Running Tests

```
dotnet test
```

- `tests/NuclearEvaluation.Client.Tests` — bUnit component tests; the API is mocked with
  NSubstitute, so no database is required.
- `tests/Kerajel.TabularDataReader.Tests` — CSV/Excel reader tests.

## Logging

Serilog writes to the console and a rolling daily file at `logs/log-.txt` (3-day retention).

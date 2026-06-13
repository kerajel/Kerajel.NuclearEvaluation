# Operations

## CI/CD

`.github/workflows/ci.yml` builds the solution in Release and runs the tests on every push and
pull request to `master`, with a SQL Server service container available for tests that need it.

## Runtime Environment

- **Framework**: .NET 9.0
- **Single deployable**: `NuclearEvaluation.Server` hosts the WASM client and the API.
- **Web server**: Kestrel (behind IIS on smarterasp.net in production).
- Max request body is sized to the per-file upload cap (`UploadLimits` + headroom).

## Hosting model

- **Local**: `docker compose up` runs SQL Server + the app.
- **Production**: published directly to **smarterasp.net** (shared Windows hosting). There is no
  Docker, message broker, or background worker process in production — all background work runs
  in-process as `IHostedService`.

```
dotnet publish src/NuclearEvaluation.Server -c Release -o ./publish
```

Deploy `./publish` and configure `ConnectionStrings:NuclearEvaluationServerDbConnection` and
`Captcha:Secret` through the host.

## Abuse Protection & Ephemeral Data (the "Sandbox")

Configured under the `Sandbox` section:

| Setting | Default | Effect |
|---|---|---|
| `RateLimitPermitPerWindow` / `RateLimitWindowSeconds` | 300 / 60s | Global per-IP request limit |
| `MaxUploadsPerIpPerDay` | 50 | Per-IP daily upload cap |
| `MaxStorageBytes` | 2 GB | Global storage ceiling; uploads rejected once exceeded |
| `UploadRetentionHours` | 24 | Uploaded files, STEM staging, and PMI reports older than this are purged |
| `ResetEnabled` / `ResetIntervalHours` | true / 24 | Database is reset to seed on this interval |
| `SweepIntervalMinutes` | 30 | How often the maintenance sweep runs |
| `SeedOnStartup` | true | Migrate + seed when the app starts if empty |

`SandboxMaintenanceService` performs the sweep; reset cadence is tracked in `DBO.SandboxState`
so it survives app-pool recycling. If the reseed is too heavy for shared hosting, set
`Sandbox:ResetEnabled=false` — the upload purge still runs.

## Captcha

A self-hosted proof-of-work captcha (no third-party service). The server signs SHA-256 search
challenges with `Captcha:Secret`; a solved challenge yields a 30-day signed cookie. Set a strong
`Captcha:Secret` in production.

## File Storage

Uploaded files are written under `NuclearEvaluationStorage/` (parent of the app directory). In
Docker this is the `app-storage` volume. Files are purged on the retention schedule.

## Logging

Serilog — console + rolling daily file `logs/log-.txt` (3-day retention).

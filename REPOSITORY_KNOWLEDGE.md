# Kerajel.NuclearEvaluation — Repository Knowledge

## What This Repository Is

A .NET 9 solution for nuclear-material evaluation: it tracks particle samples, sub-samples,
and APM (Alpha Particle Measurement) data across series and projects, supports uranium-isotope
analysis with decay correction, a query builder, and PMI (Post-Measurement Investigation)
report uploads.

The application is a **Blazor WebAssembly client backed by an ASP.NET Core Web API**, deployed
as a single site. It runs **anonymously** (no accounts) behind a self-hosted proof-of-work
captcha, with per-IP rate limiting, upload quotas, and an ephemeral dataset that resets to seed
on a schedule.

## High-Level Component Map

| Project | Type | Role |
|---|---|---|
| `NuclearEvaluation.Client` | Blazor WebAssembly | All UI: pages, Radzen components, grids, charts, query builder, captcha gate |
| `NuclearEvaluation.Server` | ASP.NET Core | Web API controllers + WASM host; captcha, rate limiting, sandbox maintenance |
| `NuclearEvaluation.Shared` | Class library | View models, enums, query-builder filters, `INuclearEvaluationApi` contract (`DataQuery`/`DataResult`) |
| `NuclearEvaluation.Kernel` | Class library | EF Core `DbContext`, domain entities, migrations, query execution, embedded seed script |
| `Kerajel.Primitives` | Class library | Vendored helpers (`OperationResult`, `Debouncer`, …) |
| `Kerajel.TabularDataReader` | Class library | Vendored CSV/Excel reader for STEM preview parsing |

Tests: `NuclearEvaluation.Client.Tests` (bUnit, mocked API) and `Kerajel.TabularDataReader.Tests`.

## Where to Start

- **Local development setup**: [docs/repo-knowledge/LOCAL_DEVELOPMENT.md](docs/repo-knowledge/LOCAL_DEVELOPMENT.md)
- **Architecture and data flow**: [docs/repo-knowledge/ARCHITECTURE.md](docs/repo-knowledge/ARCHITECTURE.md)
- **Engineering conventions**: [docs/repo-knowledge/ENGINEERING_GUIDE.md](docs/repo-knowledge/ENGINEERING_GUIDE.md)
- **Operations and CI/CD**: [docs/repo-knowledge/OPERATIONS.md](docs/repo-knowledge/OPERATIONS.md)
- **The README** at the repo root covers Docker, local run, and deployment.

## History

This started as a server-side Blazor app with ASP.NET Core Identity and a RabbitMQ/Hangfire PMI
distribution pipeline. In the June 2026 overhaul it became anonymous and was split into a WASM
client + Web API; Identity and the distribution pipeline were removed, the external
`Kerajel.*` libraries were vendored in, and Docker + abuse protection + a captcha were added.
The git history preserves the removed code.

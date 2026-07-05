# Momentum — personal daily-life "life OS" (backend)

A modular-monolith ASP.NET Core backend: one deployable app, one Postgres database,
one Identity, with modules (Habits, Tasks, Journal, ...) isolated in code via an
`IModule` plugin pattern, not in infrastructure.

**Status: Phase 1 — Foundation.** Solution scaffold, `SharedKernel`, `Infrastructure`
(DbContext + Identity core), the module registration pattern, Serilog JSON logging,
Swagger with JWT, Docker + docker-compose. Zero business modules yet — those land in
Phase 2 (Platform: Auth/Settings/Notifications/Integrations), Phase 3 (Habits/Tasks/
Journal/Today, fully implemented, with tests), and Phase 4 (Tier 2/3 stubs).

## Solution layout

```
src/
  Momentum.Api             Program.cs, DI wiring, middleware, module registration loop
  Momentum.SharedKernel     Entity base classes, IModule, ICurrentUser, exceptions, paging, Result
  Momentum.Infrastructure   AppDbContext (Identity + EF Core/Npgsql), Identity core, health checks
  Momentum.Modules/         (empty in Phase 1 — modules land here starting Phase 2)
tests/
  Momentum.IntegrationTests WebApplicationFactory<Program> + Testcontainers Postgres
  Momentum.UnitTests        Plain unit tests (no I/O)
```

### The module pattern

Every module implements `Momentum.SharedKernel.Modules.IModule` (`AddServices`,
`MapEndpoints`). `Momentum.Api`'s `Program.cs` never references a module type by
name — `ModuleAssemblyScanner` finds every `Momentum.Modules.*.dll` in the build
output and instantiates whatever implements `IModule` in it. `AppDbContext` uses the
same scanner to apply each module's `IEntityTypeConfiguration<T>` classes.

**Adding a new module** (Phase 2+) means: create `src/Momentum.Modules/<Name>/`,
add a `ProjectReference` to it from `Momentum.Api.csproj` (so its DLL is copied to
the output folder — nothing works without this one line), add it to
`Momentum.sln`, and add its `.csproj` to the `Dockerfile`'s dependency-copy layer.
No other file changes. `Modules__Enabled` (comma list, env-overridable) can
allow-list which discovered modules actually run; leave unset to run all of them.

## ⚠️ Target framework: you'll need the .NET 10 SDK

This solution targets `net10.0` per the locked stack decision. If `dotnet --list-sdks`
doesn't show a `10.x` entry, install it before doing anything below — `net8.0`/`net9.0`
SDKs cannot build `net10.0` projects.

## Prerequisites

- .NET 10 SDK
- Docker + Docker Compose (for the Postgres container and/or running the API in
  a container)
- Access to your Nexus mirror's NuGet feed (or `nuget.org` for local dev if Nexus
  isn't reachable yet)

## NuGet source (air-gapped by design)

`NuGet.Config` at the repo root has no source baked in — it reads `%NUGET_SOURCE_URL%`
(and optional `%NUGET_SOURCE_USERNAME%`/`%NUGET_SOURCE_PASSWORD%`, expanded by NuGet
itself, not by your shell). Before restoring:

```bash
export NUGET_SOURCE_URL="https://nexus.yourcompany.internal/repository/nuget-group/index.json"
# or, for local dev with no Nexus available:
export NUGET_SOURCE_URL="https://api.nuget.org/v3/index.json"

dotnet restore Momentum.sln
```

If your feed requires auth, don't edit `NuGet.Config` — run this once locally (it
writes to your user-level NuGet config, not the repo):

```bash
dotnet nuget update source Mirror --source "$NUGET_SOURCE_URL" \
  --username "$NUGET_SOURCE_USERNAME" --password "$NUGET_SOURCE_PASSWORD" \
  --store-password-in-clear-text --configfile NuGet.Config
```

Package versions are pinned centrally in `Directory.Packages.props`, targeting
.NET 10 GA + contemporaneous ecosystem releases as of mid-2026. If your mirror only
has older versions, that's the one file to edit — `dotnet restore` will report
`NU1102` for anything it can't find.

## Local dev (no Docker)

1. Start a local Postgres (or point `ConnectionStrings__Default` at any reachable
   instance). `appsettings.Development.json` defaults to
   `Host=localhost;Port=5432;Database=momentum;Username=momentum;Password=momentum`.
2. Restore + generate the initial migration (not committed yet — see below):
   ```bash
   dotnet ef migrations add InitialCreate \
     --project src/Momentum.Infrastructure --startup-project src/Momentum.Api
   ```
3. Run:
   ```bash
   dotnet run --project src/Momentum.Api
   ```
   Migrations apply automatically on startup (`Database__MigrateOnStartup=true`,
   the default — see "Migrations" below). Swagger UI: **http://localhost:8080/swagger**.

## Docker Compose

```bash
cp .env.example .env
# edit .env: at minimum set NUGET_SOURCE_URL and a real Jwt__Secret

docker compose up --build
```

- API: http://localhost:8080/swagger
- `GET /health/live` — liveness (no dependencies)
- `GET /health/ready` — readiness (checks Postgres)
- `GET /metrics` — Prometheus scrape endpoint
- Postgres is exposed on `5432` for local inspection; remove that port mapping for
  anything beyond a dev box.

## Migrations

**Guarded startup migration** (`Database__MigrateOnStartup=true`, the default): the
API calls `Database.MigrateAsync()` once at boot, inside a scope, before accepting
traffic. Chosen over a separate init step for now because this is a single-instance
personal app — simplest possible ops. If/when this runs as more than one replica,
flip `Database__MigrateOnStartup=false` and run migrations as a separate step
instead (a one-off container/K8s Job running
`dotnet ef database update --project src/Momentum.Infrastructure --startup-project src/Momentum.Api`,
or a dedicated CI stage before deploy) so replicas never race each other applying
schema changes.

No migrations are committed yet — generating one requires the EF Core design-time
tooling (`dotnet tool install --global dotnet-ef`), which needs the .NET SDK this
sandbox couldn't install (see "Known limitations" below). Run the command in step 2
above once you have the SDK, then commit the generated `Migrations/` folder under
`src/Momentum.Infrastructure/`.

## Configuration reference

All env-overridable (`Section__Key` syntax), see `.env.example` for a filled-in template:

| Variable | Purpose |
|---|---|
| `ConnectionStrings__Default` | Npgsql connection string |
| `Jwt__Secret` / `Jwt__Issuer` / `Jwt__Audience` / `Jwt__AccessMinutes` / `Jwt__RefreshDays` | JWT signing + lifetime |
| `Cors__Origins` | Comma-separated allowed origins (empty = no cross-origin requests allowed) |
| `Telegram__BotToken` | Notifications module (Phase 2) |
| `Modules__Enabled` | Comma-separated allow-list of module names; empty = all discovered modules |
| `Database__MigrateOnStartup` | `true`/`false`, see "Migrations" |

## Observability

- **Logs**: Serilog, structured JSON to stdout (compact JSON format — feed to Loki
  or any JSON-aware collector). Request logging via `UseSerilogRequestLogging()`.
- **Metrics**: `prometheus-net.AspNetCore`, scraped at `/metrics`.
- **Health**: `/health/live` (self-check only) and `/health/ready` (Postgres via
  `AspNetCore.HealthChecks.NpgSql`) — wire both into your orchestrator's liveness/
  readiness probes.

## Error format

Every error response is RFC 7807 `ProblemDetails`, produced by one global
`IExceptionHandler` (`Momentum.Api.ExceptionHandling.AppExceptionHandler`).
Module code throws `Momentum.SharedKernel.Exceptions.*` (`NotFoundException`,
`ConflictException`, `ForbiddenAccessException`, `AppValidationException` — the
last one raised automatically by `ValidationEndpointFilter<T>` from a
FluentValidation failure); anything else becomes a generic 500 with no internal
detail leaked.

## User isolation (non-negotiable, per the brief)

- Every module entity extends `Momentum.SharedKernel.Entities.UserOwnedEntity`
  (`UserId` + `Entity`'s `Id`/`CreatedAtUtc`/`UpdatedAtUtc`).
- `AppDbContext` applies a **global EF query filter** to every `IUserOwned` entity
  type in the model, scoped to the current request's user — this is defense in
  depth, not a substitute for explicit filtering.
- Module query/service code must still filter by the authenticated user's id
  explicitly, obtained via `Momentum.SharedKernel.Security.ICurrentUser` (never
  HttpContext or Identity types directly).
- A dedicated integration test proving user A cannot read/write user B's rows
  lands in Phase 2 alongside Auth.

## Known limitations of this delivery

This solution was authored in a network-isolated sandbox with no route to
`nuget.org`, Microsoft's .NET CDN, `ports.ubuntu.com` (this box is arm64, and
Ubuntu only ships `.NET` for arm64 there), or any Docker registry — so none of this
was compiled, migrated, or run in that sandbox. Every file was written and manually
reviewed for correctness, but you are the first compiler this code meets. Before
trusting it:

```bash
dotnet restore Momentum.sln
dotnet build Momentum.sln
dotnet ef migrations add InitialCreate --project src/Momentum.Infrastructure --startup-project src/Momentum.Api
dotnet test Momentum.sln          # needs a Docker daemon for Testcontainers
docker compose up --build
```

Report anything that doesn't compile/run and it'll get fixed directly rather than
re-guessed.

## What's next

- **Phase 2**: `Platform.Auth` (register/login/refresh/logout/me), `Platform.Settings`,
  `Platform.Notifications` (Telegram-first), `Platform.Integrations` shell, plus the
  user-isolation integration test.
- **Phase 3**: Habits, Tasks, Journal, Today — fully implemented, with tests.
- **Phase 4**: Entities + migrations for every Tier 2/3 module (Focus, Health,
  Finance, Learning, Goals, Calendar, Reviews, Insights, Inbox), endpoints stubbed
  `501 Not Implemented`.

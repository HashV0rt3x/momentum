# Claude Code brief — {{APP_NAME}} · Daily-Life App (modular-monolith backend)

> Working name: pick one and find/replace `{{APP_NAME}}` → **Niyyah / Rihla / Cadence / Momentum**.

## Goal
Build the backend for a personal daily-life "life OS": a **modular monolith** where a
user plans, tracks, and reflects each day. Ship the **daily loop (v1)** fully working,
and scaffold the remaining modules as stubs (folder + entity + empty controller +
"not implemented") so they can be filled in later with no restructuring.

## Locked decisions
- **ASP.NET Core Web API, .NET 10 (LTS)** — drop to .NET 8 LTS if that's what our Nexus
  mirror / approved runtime serves.
- **Multi-user, ASP.NET Core Identity + JWT** (access + rotating refresh tokens).
- **PostgreSQL + EF Core (Npgsql).**
- **Modular monolith:** one deployable app, one DB, one Identity — modules isolated in
  code, not infrastructure.

## Solution structure
```
src/
  {{APP_NAME}}.Api            // Program.cs, DI, middleware, module registration
  {{APP_NAME}}.SharedKernel   // base entity, User ref, ProblemDetails, paging, results
  {{APP_NAME}}.Modules/
    Platform.Auth/            // Identity, JWT, refresh tokens
    Platform.Settings/
    Platform.Notifications/   // Telegram-first, worker dispatch
    Platform.Integrations/    // Google Calendar, Notion, Telegram, screen-time client
    Habits/
    Tasks/
    Journal/
    Today/                    // aggregation only, owns ~no tables
    Focus/                    // sessions + screen-time ingestion
    Health/
    Finance/
    Learning/
    Goals/
    Calendar/
    Reviews/
    Insights/                 // read-only cross-module analytics
    Inbox/                    // quick capture
  {{APP_NAME}}.Infrastructure // DbContext, migrations, EF config, background host
tests/
```
Each module folder owns: `Domain/` (entities, enums), `Contracts/` (DTOs, validators),
`Endpoints/` (controllers or minimal-API group), and its own EF entity configs.
Every module registers itself via an `IModule` interface (`MapEndpoints`, `AddServices`)
so `Program.cs` just loops over enabled modules.

## Module registry (build order = top to bottom)

**Tier 0 — Platform (build first)**
| Module | Key entities | Key endpoints |
|---|---|---|
| Auth & Users | User, RefreshToken | `/auth/register\|login\|refresh\|logout\|me` |
| Settings | UserSettings, ModuleToggle | `GET/PUT /settings` |
| Notifications | Reminder, Channel, DeliveryLog | `/reminders` CRUD + worker |
| Integrations | Integration, SyncState | `/integrations`, OAuth callbacks |

**Tier 1 — Daily loop (v1, fully implemented)**
| Module | Key entities | Key endpoints |
|---|---|---|
| Habits | Habit, HabitSchedule, HabitLog | `/habits`, `PUT /habits/{id}/log`, `/habits/summary` |
| Tasks | Task, Project, Tag | `/tasks`, `/tasks/today`, `PATCH /tasks/reorder`, `/projects` |
| Journal | JournalEntry (mood, energy) | `/journal`, `GET /journal?from=&to=` |
| Today | (aggregation) | `GET /today`, `GET /today/evening` |

**Tier 2 — Trackers (stub now, fill later)**
| Module | Key entities | Key endpoints |
|---|---|---|
| Focus & Time | FocusSession, ScreenTimeLog, FocusTarget | `/focus/sessions`, `POST /screen-time`, `/focus/summary` |
| Health | Workout, WorkoutSet, BodyMetric | `/health/workouts`, `/health/metrics`, `/health/summary` |
| Finance | Account, Transaction, Category, Budget, Subscription, FxRate | `/finance/transactions`, `/finance/budgets`, `/finance/summary` |
| Learning | Skill, SkillProgress, Course, ReadingItem | `/learning/skills`, `/learning/reading`, `/learning/summary` |

**Tier 3 — Growth (stub now, fill later)**
| Module | Key entities | Key endpoints |
|---|---|---|
| Goals | Goal, Milestone, GoalLink | `/goals`, `/goals/{id}/milestones`, `/goals/{id}/progress` |
| Calendar | Event, CalendarSource | `/calendar/events`, `GET /calendar?from=&to=` |
| Reviews | Review (rollup snapshot) | `POST /reviews/weekly`, `GET /reviews` |
| Insights | (read-only) | `/insights/trends`, `/insights/correlations` |
| Inbox | InboxItem | `POST /inbox`, `GET /inbox`, `POST /inbox/{id}/promote` |

Seed each new user with default habits (your devotional list + gym/swim/cycle) and an
empty task/journal set.

## Cross-cutting (SharedKernel + Infrastructure)
- **User isolation is a hard rule:** every entity has `UserId`; every query is filtered
  by the authenticated user; a dedicated integration test proves user A can't touch
  user B's rows.
- **Errors:** RFC 7807 `ProblemDetails`, global exception handler, **FluentValidation**.
- **Security:** short-lived access token, rotating refresh (store hash only), HTTPS/HSTS,
  strict CORS (configurable), rate limiting on `/auth/*`; secrets via env, never source.
- **Observability:** **Serilog** JSON → stdout (Loki), **prometheus-net** `/metrics`,
  health checks `/health/live` + `/health/ready` (Npgsql) wired for K8s probes.
- **Background work:** one `IHostedService` worker for reminders, digests, and syncs.
- **Docs:** Swagger/OpenAPI with JWT bearer configured.
- **PDF (later):** **QuestPDF** (pure .NET, no Chromium — air-gapped friendly) for
  exporting a week/review to A4.

## Delivery / air-gapped
- Multi-stage **Dockerfile** (sdk build → aspnet runtime), non-root, minimal final image.
- **NuGet.Config** with a **configurable package source** for our **Nexus mirror** (don't
  hardcode nuget.org) so `dotnet restore` works offline.
- **docker-compose.yml**: api + postgres, healthchecks, named volume.
- EF migrations: guarded startup `MigrateAsync()` **or** a separate init step — document
  which.
- Optional **`.gitlab-ci.yml`** stub (restore → build → test → docker build/push), since
  we deploy via GitLab CI + ArgoCD.
- README: local dev, Docker run, DB migrate, env setup, Swagger URL; must run with **no
  public internet at runtime**.

## Config (env-overridable)
`ConnectionStrings__Default`, `Jwt__Secret|Issuer|Audience|AccessMinutes|RefreshDays`,
`Cors__Origins`, `Telegram__BotToken`, `Modules__Enabled` (comma list). Ship `.env.example`.

## Acceptance criteria
- [ ] register → login → refresh → authenticated call works end-to-end.
- [ ] v1 modules (Habits, Tasks, Journal, Today) are fully functional with tests.
- [ ] Tier 2/3 modules exist as registered stubs returning `501 Not Implemented`, with
      their entities + migrations in place.
- [ ] User-isolation integration test passes.
- [ ] `docker compose up` → API + Postgres, `/health/ready` green, Swagger reachable.
- [ ] Adding a new module = drop a folder implementing `IModule`; no changes to unrelated code.

## How to proceed
1. Scaffold solution, SharedKernel, Infrastructure, the `IModule` registration pattern.
2. Build **Platform** (Auth/Settings/Notifications/Integrations), then the **v1 loop**
   with tests.
3. Generate entities + migrations for **all** Tier 2/3 modules, but leave their endpoints
   as `501` stubs.
4. Wire Docker, health, metrics, Swagger; write the README.
Ask one round of clarifying questions only for hard-to-reverse choices; otherwise build
and show the API running behind Swagger.

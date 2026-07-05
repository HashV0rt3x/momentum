# Momentum API — Documentation

Base URL: `http://localhost:8080/api/v1` (prod: `https://api.yourdomain.com/api/v1`)

- **Format:** JSON, `Content-Type: application/json`
- **Auth:** `Authorization: Bearer <accessToken>` on every protected route
- **Language:** `Accept-Language: uz | ru | en` (or `?locale=uz`) — affects locale chosen on first login
- **Dates:** ISO-8601 UTC (`2026-07-05T09:30:00.000Z`)
- **IDs:** opaque strings (GUIDs)

**Error shape** (all non-2xx):

```json
{ "status": 400, "message": "Human readable", "code": "VALIDATION_ERROR", "errors": { "field": ["msg"] } }
```

`errors` is present only for validation failures.

| Code | Status | Meaning |
| --- | --- | --- |
| `VALIDATION_ERROR` | 400 | Request body failed validation (see `errors`) |
| `UNAUTHORIZED` | 401 | Missing/invalid access token |
| `TELEGRAM_SIGNATURE_INVALID` | 401 | Telegram HMAC verification failed |
| `TELEGRAM_AUTH_EXPIRED` | 401 | `auth_date` older than 24h |
| `LOGIN_CODE_INVALID` | 401 | 6-digit code wrong, used, or expired |
| `REFRESH_TOKEN_INVALID` | 401 | Refresh token wrong, rotated, revoked, or expired |
| `FORBIDDEN` | 403 | Role/permission check failed |
| `NOT_FOUND` | 404 | Resource doesn't exist for the current user |
| `CONFLICT` | 409 | Business-rule conflict (e.g. duplicate category key) |
| — | 429 | Rate limit hit (10 req/min per IP on `/auth/*`) |
| `INTERNAL_ERROR` | 500 | Unexpected server error |

**Pagination** (`GET /tasks` when `?page=` given):

```json
{ "items": [], "total": 128, "page": 1, "pageSize": 50 }
```

`pageSize` max 200, default 50.

---

## 1. Authentication

Telegram-only, no passwords. Three ways in; all return the same auth response:

```json
{
  "user": { /* User */ },
  "accessToken": "eyJ…",
  "refreshToken": "…",
  "expiresIn": 900
}
```

Access token lives `Jwt__AccessMinutes` (default 15 min); refresh token lives
`Jwt__RefreshDays` (default 30 days), is single-use and **rotates** on every refresh.
First user ever becomes `owner`, everyone after `member`. On first login locale is
chosen: Telegram `language_code` → `?locale=`/`Accept-Language` → `uz`.

### POST /auth/telegram — Login Widget

No auth. Rate-limited.

```json
{
  "source": "widget",
  "data": {
    "id": 123456789,
    "first_name": "Diyor",
    "last_name": "Karimov",
    "username": "diyor",
    "photo_url": "https://t.me/i/userpic/320/x.jpg",
    "auth_date": 1719400000,
    "hash": "e3b0c442…"
  }
}
```

### POST /auth/telegram — Mini App

```json
{ "source": "webapp", "initData": "query_id=…&user=%7B…%7D&auth_date=…&hash=…" }
```

### POST /auth/telegram/code — bot 6-digit code

User sends `/start` to the bot → bot replies with a 6-digit code (valid 5 min,
single-use) → exchange it here. No auth. Rate-limited.

```json
{ "code": "123456" }
```

### POST /auth/refresh

No auth. Rotates the refresh token — the old one stops working immediately.

```json
{ "refreshToken": "…" }
```

→ `200 { "accessToken": "…", "refreshToken": "…", "expiresIn": 900 }`

### POST /auth/logout

Auth required. Revokes the given refresh token. Idempotent. → `204`

```json
{ "refreshToken": "…" }
```

---

## 2. Account

### GET /me → User

```json
{
  "id": "0197…", "telegramId": 123456789, "name": "Diyor Karimov",
  "username": "diyor", "avatarUrl": "https://…", "email": null,
  "role": "owner", "jobTitle": null, "locale": "uz",
  "timezone": "Asia/Tashkent", "createdAt": "2026-07-05T08:00:00Z"
}
```

`role`: `owner | admin | member | guest`.

### PATCH /me → User

Any subset; nulls/absent fields unchanged.

```json
{ "name": "Diyor K.", "jobTitle": "Founder", "timezone": "Asia/Tashkent", "locale": "ru" }
```

`timezone` must be a valid IANA id; `locale` one of `uz|ru|en`.

---

## 3. Settings

### GET /settings

Created lazily with defaults on first call.

```json
{
  "locale": "uz",
  "theme": "system",
  "weekStart": "mon",
  "timer": { "focus": 25, "shortBreak": 5, "longBreak": 15, "roundsBeforeLongBreak": 4 },
  "notifications": {
    "taskDue": true, "mentions": true, "weeklySummary": true,
    "focusReminders": false, "channel": "telegram"
  }
}
```

### PATCH /settings

Any subset (nested partials allowed) → returns the full settings object.
Constraints: `theme` `light|dark|system` · `weekStart` `mon|sun` · timer minutes
`focus` 1–240, `shortBreak` 1–60, `longBreak` 1–120, `roundsBeforeLongBreak` 1–12 ·
`channel` `telegram|none`. `locale` here also updates the user record.

```json
{ "locale": "en", "timer": { "focus": 50 } }
```

---

## 4. Categories

Stable machine keys (`work`, `deep_work`, …) — the API never translates names.
New users are seeded with: `work`, `deep_work`, `meetings`, `learning`, `personal`.

| Method | Path | Body | Response |
| --- | --- | --- | --- |
| GET | `/categories` | — | `Category[]` |
| POST | `/categories` | `{ "key": "reading", "color": "#37A8C9", "icon": "BookOpen" }` | `201 Category` |
| PATCH | `/categories/:id` | `{ "color": "#3FB07E", "icon": "GraduationCap" }` | `200 Category` |
| DELETE | `/categories/:id` | — | `204` |

`Category`: `{ "id": "…", "key": "deep_work", "color": "#3E63C8", "icon": "Brain" }`.
`key` is immutable and unique per user (`409 CONFLICT` on duplicate); format `^[a-z0-9_]{1,64}$`; `color` is `#RRGGBB`.

---

## 5. Projects

| Method | Path | Body | Response |
| --- | --- | --- | --- |
| GET | `/projects` | — | `Project[]` |
| POST | `/projects` | `{ "name": "Website Redesign", "color": "#37A8C9", "description": "…" }` | `201 Project` |
| PATCH | `/projects/:id` | any subset of `name, color, description, isArchived` | `200 Project` |
| DELETE | `/projects/:id` | — | `204` (tasks keep existing, their `projectId` becomes null) |

`Project`:

```json
{ "id": "…", "name": "Mobile App v2", "color": "#4F80E1", "description": "…",
  "isArchived": false, "memberIds": ["<ownerId>"], "createdAt": "…" }
```

---

## 6. Tasks

### GET /tasks

Filters (all optional): `status` (`todo|in_progress|done`), `priority`
(`low|medium|high|urgent`), `projectId`, `assigneeId`, `categoryKey`, `dueBefore`
(ISO), `q` (searches title+description, case-insensitive), `page`, `pageSize`.
Returns a plain array, or the paginated shape when `page` is given.

### GET /tasks/:id → Task

```json
{
  "id": "…", "title": "Design the weekly analytics dashboard", "description": "…",
  "status": "in_progress", "priority": "high", "projectId": "…", "categoryKey": "deep_work",
  "assigneeIds": ["…"], "tags": ["ui", "charts"],
  "subtasks": [{ "id": "…", "title": "Wireframe", "done": true }],
  "estimatedMinutes": 240, "trackedMinutes": 0,
  "dueDate": "…", "startDate": "…", "completedAt": null,
  "createdAt": "…", "updatedAt": "…"
}
```

`trackedMinutes` is always 0 until the time-tracking module lands.

### POST /tasks → 201 Task

Only `title` required. `status` defaults `todo`, `priority` defaults `medium`,
`assigneeIds` defaults to yourself.

```json
{
  "title": "Write onboarding emails", "description": "",
  "status": "todo", "priority": "medium", "categoryKey": "work",
  "projectId": "…", "tags": ["copy"], "estimatedMinutes": 90,
  "dueDate": "2026-07-06T00:00:00.000Z"
}
```

`projectId` must be one of your projects (else `404 NOT_FOUND`).

### PATCH /tasks/:id → 200 Task

Any subset; nulls unchanged. Setting `status: "done"` sets `completedAt`
server-side; moving away from done clears it.

### DELETE /tasks/:id → 204

### Subtasks

| Method | Path | Body | Response |
| --- | --- | --- | --- |
| POST | `/tasks/:id/subtasks` | `{ "title": "Handoff to devs" }` | `201 Subtask` |
| PATCH | `/tasks/:id/subtasks/:subId` | `{ "done": true }` and/or `{ "title": "…" }` | `200 Subtask` |
| DELETE | `/tasks/:id/subtasks/:subId` | — | `204` |

---

## 7. Focus Sessions (Pomodoro)

The client runs the timer (lengths come from `/settings` → `timer`) and records
each finished or aborted round here.

### GET /focus-sessions

Query: `?date=today` (resolved in the **user's timezone**) or `?from=&to=` (ISO
range; wins over `date`). No params = today. → `FocusSession[]`

```json
[{ "id": "…", "type": "focus", "taskId": "…", "plannedMinutes": 25,
   "completedMinutes": 25, "completed": true, "startedAt": "…T08:10:00Z" }]
```

### POST /focus-sessions → 201 FocusSession

```json
{ "type": "focus", "taskId": "…", "plannedMinutes": 25,
  "completedMinutes": 25, "completed": true, "startedAt": "…T08:10:00Z" }
```

`type`: `focus | short_break | long_break` · `plannedMinutes` 1–480 ·
`completedMinutes` 0–480 and ≤ `plannedMinutes` · `completed: false` = aborted
early · `taskId` optional, must be your task (else `404`).

---

## 8. Ops endpoints (outside /api/v1)

| Path | Purpose |
| --- | --- |
| `GET /health/live` | Liveness (no dependencies) |
| `GET /health/ready` | Readiness (checks Postgres) |
| `GET /metrics` | Prometheus metrics |
| `GET /swagger` | Swagger UI |

---

## 9. Not implemented yet (per spec)

`/team*`, `/time-entries*`, `/goals*`, `/analytics/*`, `/notifications*` —
planned next phases.

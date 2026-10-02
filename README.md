# RentApp

A mobile-first rent collection and tenant management app for PG owners, PG managers and small
rental-property operators.

> **Know who has paid rent, who hasn't, how much is outstanding, and collect it on time.**

RentApp is built to be a simple rent collection assistant, not a property ERP.

## Status

| Phase | Scope | State |
|---|---|---|
| 0 | Discovery | Done |
| 1 | Foundation: API, mobile shell, PostgreSQL, Redis, logging, errors, Swagger, migrations, health checks | Done |
| 2 | Auth and organizations: sign-up/sign-in, rotating refresh tokens, owner/staff roles, staff permissions, organization isolation | **Done** |
| 3 | Properties, rooms and beds: archive/restore, auto-created beds, capacity limits, derived occupancy and vacancy | **Done** |
| 4 | Tenants: tenant records, bed assignment, move-in, move-out, moving beds, history, search; occupancy from tenancies | **Done** |
| 5 | Rent engine: monthly dues, due dates, overdue tracking, waivers, background generation; app redesign with bottom tabs and a dashboard | **Done** |
| 6 | Payments and receipts: recording with oldest-first allocation, idempotent retries, owner void, gap-free receipt numbers, PDF receipts and sharing | **Done** |
| 7 | Dashboard: one cached request for collections, this month's progress, dues, occupancy, overdue and recent payments; redesigned Home with quick actions | **Done** |
| 8 | Reminders: a daily queue (upcoming, due today, 3 and 7+ days overdue), ready-to-send messages, WhatsApp/SMS/share/copy, history and mark as sent | **Done** |
| 9 | Polish | Next |
| 10 | Test and release | Planned |

The full plan is in [ROADMAP.md](ROADMAP.md).

## V1 features (planned)

- Properties, rooms and beds, with occupancy and vacancy derived from active tenancies
- Tenants, move-in and move-out
- Monthly rent charges with due dates, partial and multiple payments, outstanding and overdue tracking
- Payments (cash, UPI, bank transfer, card, other) with void/reversal and audit history; never hard-deleted
- Receipts with unique numbers, plus PDF download
- Rent reminders: generated text you can copy or share. There is no unofficial WhatsApp automation.
- A dashboard showing who has paid, who hasn't, and who is overdue

## Stack

| Layer | Technology |
|---|---|
| Mobile | React Native, Expo SDK 57, Expo Router, TypeScript (strict), TanStack Query |
| API | ASP.NET Core 10 (C#), REST under `/api/v1`, EF Core 10 |
| Database | PostgreSQL 17 (source of truth) |
| Cache | Redis 7 (cache and rate limiting only) |
| Local infrastructure | Docker Compose (PostgreSQL and Redis only) |
| Tests | xUnit with Testcontainers (backend); Jest with React Native Testing Library (mobile) |

The spec named .NET 8. .NET 10 (the current LTS) was chosen during Phase 0 sign-off.

## Requirements

- .NET SDK 10.0.1xx or newer (pinned via `global.json`, roll-forward allowed)
- Node.js 20 or newer (developed on 24) and npm
- Docker Desktop
- A phone with **Expo Go**, on the same Wi-Fi as your computer

## Quick start

```powershell
# 1. Configure (first time only)
Copy-Item .env.example .env          # then fill in POSTGRES_PASSWORD, the matching Password= and JWT_SECRET

# 2. Start PostgreSQL and Redis
docker compose up -d

# 3. Start the API (applies migrations automatically in Development)
dotnet tool restore
dotnet run --project src/RentApp.Api --launch-profile http
#   Swagger:   http://localhost:5080/swagger
#   Readiness: http://localhost:5080/health/ready

# 4. Start the mobile app (in a second terminal)
cd mobile
npm install
npx expo start                        # scan the QR code with Expo Go
```

The app opens on **Sign in**. Tap **Create an account** to register your PG; you become its owner. From **Home → Properties** add your PG, then its rooms (beds are created for you). From **Home → Tenants** add tenants and give them a bed. From **Home → Staff** you can add staff and choose what they can do. **Home → System status** shows whether the phone can reach the API, PostgreSQL and Redis.

For details, troubleshooting and phone networking, see [DEVELOPMENT.md](DEVELOPMENT.md).

## Environment variables

Every variable is documented in [.env.example](.env.example). The main ones are:

| Variable | Purpose |
|---|---|
| `DATABASE_CONNECTION_STRING` | PostgreSQL connection (Npgsql format) |
| `REDIS_CONNECTION_STRING` | Redis connection |
| `JWT_SECRET`, `JWT_ISSUER`, `JWT_AUDIENCE` | Access-token signing. The secret must be at least 32 bytes, or the API refuses to start. |
| `CORS_ALLOWED_ORIGINS` | Browser origins allowed by CORS. Leave empty, since the mobile app doesn't need CORS. |
| `POSTGRES_*`, `REDIS_PORT` | Docker Compose only |

`.env` is git-ignored and is read only in Development. Real environments inject these variables directly.

## Tests

```powershell
dotnet test                 # unit tests and integration tests (integration needs Docker running)
cd mobile; npm test         # mobile unit and component tests
cd mobile; npm run typecheck; npm run lint
```

## Documentation

| Document | Contents |
|---|---|
| [ARCHITECTURE.md](ARCHITECTURE.md) | System shape, layers, modules, request pipeline |
| [API.md](API.md) | API conventions, error format, endpoints |
| [DATABASE.md](DATABASE.md) | Database conventions and migrations |
| [DEVELOPMENT.md](DEVELOPMENT.md) | Local setup, phone networking, troubleshooting |
| [SECURITY.md](SECURITY.md) | Security controls and data handling |
| [ROADMAP.md](ROADMAP.md) | Phases, and what is out of scope for V1 |

# Architecture

## System shape

```text
React Native / Expo (Expo Go on a phone)
        |
        |  HTTPS (HTTP on the LAN in development only), JSON, /api/v1
        v
ASP.NET Core API  (modular monolith)
        |
   +----+----+
   |         |
PostgreSQL  Redis
(source of   (cache, rate limiting,
 truth)       short-lived coordination)
```

V1 is deliberately a **modular monolith**. It has no microservices, message brokers or Kubernetes. Each
of those would need a concrete requirement before being added.

## Backend layers

```text
src/
  RentApp.Domain/          Entities, enums, domain rules. No dependencies.
  RentApp.Application/     Use cases per module: services, DTOs, validation, interfaces. Depends on Domain.
  RentApp.Infrastructure/  EF Core (PostgreSQL), Redis, PDF, token services. Implements Application interfaces.
  RentApp.Api/             Thin controllers, middleware, authentication, OpenAPI. Composition root.
tests/
  RentApp.UnitTests/         Pure logic: rent calculations, error mapping, configuration.
  RentApp.IntegrationTests/  The real API against throwaway PostgreSQL and Redis containers (Testcontainers).
```

These rules apply across the layers:

- Controllers contain no business logic. They validate input, call an Application service, and return a DTO.
- EF Core entities are never returned from the API; responses always use DTOs.
- The Application layer has no knowledge of HTTP. It throws typed `AppException`s (`NotFoundException`,
  `ConflictException`, and so on), and the API layer maps them to status codes.
- Shared build settings live in `Directory.Build.props`, and package versions are managed centrally in
  `Directory.Packages.props`. Warnings are errors, and the recommended .NET analyzers are enabled.

### Logical modules

The modules are Identity, Organizations, Properties, Rooms, Beds, Tenants, Rent, Payments, Receipts,
Reminders, Notifications and Audit. Each one gets its own folder inside `Application` and `Infrastructure`
as it is built. Modules talk through Application services, never through each other's tables directly.

## Request pipeline (API)

These steps run in order:

1. **CorrelationIdMiddleware.** It accepts a well-formed `X-Correlation-ID` from the client, or generates
   one. The ID becomes `HttpContext.TraceIdentifier`, is echoed in the response header, and is added to
   the logging scope.
2. **RequestLoggingMiddleware.** It writes one structured line per request with method, path, status and
   duration. The query string is not logged.
3. **Exception handler** (`GlobalExceptionHandler`). It converts exceptions into the standard error body.
   Unexpected exceptions become a generic 500 response, and their details are logged server-side only.
4. **Status code pages.** These give framework-generated errors such as 404 and 405 the same error body.
5. HSTS and HTTPS redirection run in every environment except Development and Testing.
6. CORS: no browser origins are allowed unless they are configured explicitly.
7. OpenAPI document and Swagger UI, in Development only.
8. Health endpoints and controllers.

## Configuration

Configuration sources are applied in this order, with later sources taking precedence:

1. `appsettings.json`
2. `appsettings.{Environment}.json`
3. Environment variables
4. The flat variables documented in `.env.example`

`FlatEnvironmentVariables` maps the flat variables onto configuration keys. For example,
`DATABASE_CONNECTION_STRING` maps to `ConnectionStrings:Database`. In Development, the repo-root `.env`
file is loaded first, and it is the same file Docker Compose uses.

Required settings are validated at startup with `ValidateOnStart`, so a missing connection string stops
the app immediately with a clear message.

The environments are `Development`, `Testing` (used by the integration tests) and `Production`.

## Mobile

```text
mobile/src/
  app/          Expo Router routes only (thin files that render a screen)
  screens/      Screen components
  components/   Reusable UI (AppText, Button, Card, StatusPill, LoadingState, ErrorState)
  api/          The centralized HTTP client, error normalization, API base-URL resolution, QueryClient
  hooks/        TanStack Query hooks (server state)
  theme/        Design tokens, plus light and dark colors
```

The mobile app follows these rules:

- The app gets server state through TanStack Query. Global client state is avoided unless a real need appears.
- All HTTP calls go through `api/client.ts`. It handles timeouts, correlation IDs, and normalizing every
  failure to `ApiClientError`.
- `api/errors.ts` turns errors into human-readable text, such as "Unable to connect." The UI never shows
  raw status codes.
- Queries retry only transient failures. Mutations, including recording a payment, are never retried
  automatically.
- In development, the API address is derived from the Expo dev server host. Release builds must set
  `EXPO_PUBLIC_API_URL`.
- Every touch target is at least 48 pt, and color is never the only signal of a status.

## Authentication and organization isolation

```text
Mobile                         API                                  PostgreSQL
------                         ---                                  ----------
login -----------------------> AuthService ---- verify hash ------> users
      <--- access JWT (15 min) + refresh token (30 days, rotating) -- refresh_tokens (hash only)
request + Bearer JWT --------> JwtBearer -> HttpCurrentUser(org, role, perms)
                               fallback policy: authenticated
                               OwnerOnly / Permission:* policies
                               AppDbContext query filters --------> WHERE organization_id = @org
                               OrganizationIsolationInterceptor (blocks cross-organization writes)
```

- **`ICurrentUser`** (Application layer) is the only way services learn who is calling. In the API it reads
  validated JWT claims. Malformed claims are treated as anonymous.
- **Mobile session** (`src/auth/session.ts`):
  - The refresh token lives in SecureStore and the access token in memory.
  - The API client gets a token before each request, refreshes once on `401`, and retries once.
  - Refreshing is **single-flight**, because refresh tokens rotate and reuse is treated as theft.
  - At start-up, a saved session is resumed. If the server is unreachable, the app shows a retry screen
    instead of forcing a new sign-in.
- **Routes:** `src/app/(auth)` (sign-in, register, forgot password) and `src/app/(app)` (signed in) are
  switched with `Stack.Protected`. Staff screens are an owner-only protected group. These guards are for
  the UI only; the API enforces everything.

## Properties, rooms and beds

- **Data model.** Organization → Property → Room → Bed. Each level carries `OrganizationId`, and the
  database enforces it through composite foreign keys.
- **Occupancy.** `Occupancy.Derive(bedStatus, roomStatus, hasActiveTenancy)` in the Domain layer is the
  single rule. `OccupancyQueries` aggregates bed counts in SQL (grouped counts only), and nothing about
  occupancy is persisted.
- **Archiving.** Records are archived, never deleted. Archiving a room archives its beds. Archived room
  numbers and bed labels can be reused, enforced by partial unique indexes.

## Tenants and tenancies

- **Tenancies.** A tenant lives in a bed through a `RentAgreement`. Moving beds ends one agreement and
  starts another, so the full history of who lived where is kept.
- **Data minimization.** The `Tenant` record holds contact details only.
- **"Today".** `OrganizationClock` works out today's date in the organization's time zone (default
  `Asia/Kolkata`). That date decides whether a tenancy is upcoming or current, and is what move-out
  dates are checked against.
- **Phase 5.** The rent engine will read active agreements (rent, due day, dates) to generate monthly
  charges. Cancelled agreements are never charged.

## Rent engine

- **`RentSchedule`** (Domain layer) is a set of pure functions that decide which monthly charges should
  exist and when they're due. **`RentStatusRules`** derives each charge's status from its dates and amounts.
- **`RentChargeGenerator`** turns the schedule into rows. It is idempotent: a unique index stops a month
  being charged twice, and the advisory lock prevents deadlocks. It runs on move-in and move-out, on
  request (`POST /rent/generate`), and in **`RentGenerationWorker`**, a background job.
- **The background job** processes one organization per scope, acting as **`SystemCurrentUser`** for
  that organization. Organization filters and the cross-organization write guard therefore apply exactly
  as they do for a person, and audit entries record a null actor, meaning "system".
- **Business date.** "Today" comes from a keyed `TimeProvider` (`BusinessTime.Key`). Tests pin the
  business date without affecting token lifetimes or audit timestamps.

## Payments and receipts

- **`PaymentAllocator`** (Domain layer) is a pure function that splits an amount across dues, oldest
  first, never exceeding a balance.
- **`PaymentService`** records and voids payments using the consistency strategy in DATABASE.md. It
  makes sure newly due charges exist before allocating, so a tenant can always pay what the app shows.
- **Receipts are snapshots.** Names, address, room and period are copied when the payment is recorded, so
  editing a property or tenant later never changes a receipt that has already been issued.
- **PDFs** are drawn by **`ReceiptPdfRenderer`** (Infrastructure layer) with PDFsharp 6 (MIT). It uses the
  embedded Noto Sans fonts (SIL Open Font License), so the PDF has the ₹ sign and needs no fonts installed
  on the server. That matters for Linux containers. Amounts use Indian grouping and are also written in
  words.
- **Mobile.** Record payment opens prefilled: the due's balance or everything owed, UPI selected, and
  today's date. Quick-amount chips cover other common amounts. One idempotency key lives for the life of
  the form, so saving again after a timeout can't record twice. Success shows the receipt with a Share
  button. The PDF is downloaded with the session token (`expo-file-system`) and passed to the share sheet
  (`expo-sharing`), for WhatsApp, email or Files.

## Reminders

- **`ReminderRules`** (Domain layer) is a set of pure functions. They decide each due's stage, whether the
  queue should suggest it given earlier reminders, and the message text. Amounts use the shared
  **`IndianRupees`** formatter, the same one the receipt PDF uses.
- **`ReminderService`** builds the queue from outstanding dues due within the next 3 days or earlier, and
  records reminders. Audit entries hold only the type, channel and status. The message contains the
  tenant's name, so it is kept out of the audit log.
- **Mobile.** The Reminders screen (from More or a Home quick action, which shows the count) has two tabs:
  "To send", filtered by stage with one-tap WhatsApp, SMS, Share and Copy, and "History" with "Mark as
  sent". The compose screen lets the person edit the message first. Remind also appears on Home overdue
  cards, rent dues and tenants.
- **Sending stays manual.** WhatsApp uses `https://wa.me/<number>?text=...`. Indian 10-digit numbers get
  91 added. SMS uses an `sms:` link, copying uses `expo-clipboard`, and sharing uses React Native's
  `Share`. If the person backs out of the share sheet, nothing is recorded.

## Dashboard and caching

- **`DashboardService`** builds the home screen's figures in one request, reusing the rent and payment
  queries. **`IAppCache`** (Application layer) is implemented by **`RedisAppCache`** (Infrastructure layer).
- **Invalidation by version, not deletion.** Each organization has a data version in Redis
  (`rentapp:org:{id}:version`). **`OrganizationDataVersionInterceptor`** increments it after a write to
  business data commits; inside a transaction it waits for the commit. The version is part of the cache
  key, so old entries are never read again and expire after 60 seconds. Sign-ins, token refreshes, staff
  changes and audit entries don't change the version.
- **What a user may see is part of the key** (with the business date and property), so a staff member
  is never served figures cached for the owner.
- **Redis is optional at runtime.** Calls have a 1-second timeout. Failures are logged as warnings and
  skipped, and the dashboard is computed from PostgreSQL. If a version bump is lost while Redis is
  failing, cached figures can be at most 60 seconds old.
- **Mobile Home** uses the dashboard request alone. Its query key sits under `['rent']`, so every rent,
  tenant and payment change refreshes it, and property changes refresh it too. Quick actions are Record
  payment (with a "Who paid?" picker), Overdue, Add tenant and Add property. The Remind action arrives
  with reminders in Phase 8.

## Mobile design system

- **Brand.** A violet-to-magenta gradient with an orange accent (`theme/tokens.ts`). The logo
  (`components/Logo.tsx`, app icon and splash) is a home with a ₹ badge, and `APP_NAME` is defined in one
  place.
- **Navigation.** Bottom tabs (Home, Rent, Tenants, Properties, More) are hidden per permission, and
  hidden tabs also redirect if opened directly. Detail screens are stacked above the tabs.
- **Feel:**
  - Pressable elements scale and give a light haptic tap (`PressableScale`).
  - Lists fade and slide in using the native `Animated` API (`FadeIn`).
  - Skeleton loaders, friendly empty states, avatars with initials, and icons from Ionicons.

## Health checks

| Endpoint | Meaning |
|---|---|
| `GET /health/live` | The process is up. No dependency checks run. |
| `GET /health/ready` | PostgreSQL and Redis are reachable. Returns 503 when either is down. |

The API keeps running when Redis is down. Readiness reports it, and the Redis client reconnects automatically.

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

## Health checks

| Endpoint | Meaning |
|---|---|
| `GET /health/live` | The process is up. No dependency checks run. |
| `GET /health/ready` | PostgreSQL and Redis are reachable. Returns 503 when either is down. |

The API keeps running when Redis is down. Readiness reports it, and the Redis client reconnects automatically.

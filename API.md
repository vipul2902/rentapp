# API

- **Base path:** `/api/v1`
- **Format:** JSON (camelCase), with enums serialized as strings
- **Interactive documentation (Development only):** Swagger UI at `/swagger`, and the OpenAPI document at `/openapi/v1.json`

## Conventions

| Topic | Convention |
|---|---|
| Correlation | Send `X-Correlation-ID` (1–64 characters from `A-Z a-z 0-9 - _ .`), or the server generates one. It is always echoed in the response and appears as `traceId` in error bodies. |
| Errors | Every error uses the standard body below. Stack traces and internal details are never returned. |
| Validation | `400` with `code: "VALIDATION_FAILED"`, plus an `errors` map from field to messages |
| Money | Decimal amounts. They are never floating point at any layer. |
| Pagination, filtering, sorting, search | Added with the first list endpoints (Phase 3) and documented here then |

### Error body

```json
{
  "code": "RENT_CHARGE_NOT_FOUND",
  "message": "The requested rent charge was not found.",
  "traceId": "0f8fad5bd9cb469fa16570867728950e"
}
```

`message` is always safe to show to an end user. For validation failures, an `errors` object is added:

```json
{
  "code": "VALIDATION_FAILED",
  "message": "Some fields are missing or invalid. Please check and try again.",
  "traceId": "…",
  "errors": { "amount": ["Amount must be greater than zero."] }
}
```

### Status codes

| Status | `code` (generic) | When |
|---|---|---|
| 400 | `BAD_REQUEST`, `VALIDATION_FAILED`, or a module-specific code | Invalid input |
| 401 | `UNAUTHORIZED` | Missing or expired token |
| 403 | `FORBIDDEN` | Signed in, but not permitted |
| 404 | `NOT_FOUND`, or a module-specific code such as `RENT_CHARGE_NOT_FOUND` | Not found, or belongs to another organization |
| 405 | `METHOD_NOT_ALLOWED` | Wrong HTTP method |
| 409 | `CONFLICT`, or a module-specific code | Concurrent change or duplicate |
| 429 | `RATE_LIMITED` | Too many attempts |
| 500 | `INTERNAL_ERROR` | Unexpected server error. Details are logged with the `traceId`. |
| 503 | `SERVICE_UNAVAILABLE` | A dependency is unavailable |

Records that belong to another organization return **404**, not 403, so their existence is not revealed.

## Endpoints

### Available now (Phase 1)

| Method | Path | Description |
|---|---|---|
| GET | `/health/live` | Liveness: `{"status":"Healthy","totalDurationMs":0,"checks":[]}` |
| GET | `/health/ready` | Readiness of PostgreSQL and Redis. Returns `200` or `503` with a per-check status. |

### Planned

The planned endpoints follow spec §10, and each one is documented here as it ships:

```text
POST /api/v1/auth/register | login | refresh | logout          Phase 2
GET|POST /api/v1/properties, GET|PUT|DELETE /properties/{id}  Phase 3
GET|POST /api/v1/properties/{id}/rooms, /rooms/{id}/beds       Phase 3
GET|POST /api/v1/tenants, GET|PUT /tenants/{id}                Phase 4
GET /api/v1/rent/charges | /rent/overdue, POST /rent/generate  Phase 5
POST /api/v1/payments, GET /payments/{id}, POST /{id}/void     Phase 6
GET /api/v1/receipts/{id} | /receipts/{id}/pdf                 Phase 6
GET /api/v1/dashboard                                          Phase 7
GET|POST /api/v1/reminders                                     Phase 8
```

`POST /auth/register` is an addition to the spec's endpoint list. It is needed for success criterion 1,
"Sign up".

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

## Authentication

- Every endpoint requires `Authorization: Bearer <accessToken>` unless it is marked **anonymous** below.
  This is deny-by-default: a request to an unknown URL without a token gets `401`, not `404`.
- **Access tokens** are JWTs signed with HS256. They last 15 minutes and carry these claims: `sub` (user ID),
  `org` (organization ID), `role` (`Owner` or `Staff`), and one `perm` claim per granted staff permission.
- **Refresh tokens** are opaque random strings that last 30 days. They **rotate on every use**: each call to
  `/auth/refresh` returns a new refresh token, and the old one stops working. If an already-used refresh
  token is presented again, the API treats it as stolen and revokes the whole session, so clients must
  never refresh twice in parallel.
- **Role changes take effect within 15 minutes.** Permission changes and disabled accounts apply to existing
  access tokens when they expire. Disabling an account or resetting its password revokes all of its
  refresh tokens immediately.
- **Rate limiting.** `register`, `login` and `refresh` allow 10 requests per minute per client IP, and return
  `429 RATE_LIMITED` with a `Retry-After` header beyond that.

### Roles and permissions

| Role | Access |
|---|---|
| `Owner` | Everything in the organization, including staff management. Holds every permission implicitly. |
| `Staff` | Only the permissions the owner grants: `ViewProperties`, `ViewTenants`, `RecordPayments`, `GenerateReceipts`, `SendReminders` |

The server enforces permissions on every endpoint. The mobile app hides actions only as a convenience.

## Endpoints

### Health (anonymous)

| Method | Path | Description |
|---|---|---|
| GET | `/health/live` | Liveness |
| GET | `/health/ready` | Readiness of PostgreSQL and Redis. Returns `200` or `503` with a per-check status. |

### Auth

| Method | Path | Access | Success | Errors (`code`) |
|---|---|---|---|---|
| POST | `/api/v1/auth/register` | anonymous | `201` `AuthResponse` | `400 VALIDATION_FAILED`, `409 EMAIL_ALREADY_REGISTERED` |
| POST | `/api/v1/auth/login` | anonymous | `200` `AuthResponse` | `401 INVALID_CREDENTIALS`, `401 ACCOUNT_DISABLED` |
| POST | `/api/v1/auth/refresh` | anonymous | `200` `AuthResponse` (new refresh token) | `401 SESSION_EXPIRED`, `401 ACCOUNT_DISABLED` |
| POST | `/api/v1/auth/logout` | anonymous | `204` (always, even for unknown tokens) | none |
| GET | `/api/v1/auth/me` | signed in | `200` `UserProfile` | `401` |

`register` creates the organization and makes the caller its owner. This endpoint is an addition to the
spec's list, needed for "Sign up". A wrong password and an unknown email return the same error, so
responses don't reveal which accounts exist.

```jsonc
// POST /api/v1/auth/register
{ "organizationName": "Sunrise PG", "name": "Asha Rao", "email": "asha@example.com",
  "phone": "+91 98765 43210", "password": "at-least-8-chars" }

// AuthResponse
{ "accessToken": "eyJ...", "accessTokenExpiresAt": "2026-10-02T12:15:00+00:00",
  "refreshToken": "q3Jk...", "refreshTokenExpiresAt": "2026-11-01T12:00:00+00:00",
  "user": { "id": "...", "name": "Asha Rao", "email": "asha@example.com", "phone": "+91 98765 43210",
            "role": "Owner", "status": "Active",
            "permissions": ["ViewProperties","ViewTenants","RecordPayments","GenerateReceipts","SendReminders"],
            "organization": { "id": "...", "name": "Sunrise PG", "timeZone": "Asia/Kolkata" } } }
```

`login` takes `{ "email", "password" }`. `refresh` and `logout` take `{ "refreshToken" }`.

### Users and staff (owner only)

| Method | Path | Body | Success |
|---|---|---|---|
| GET | `/api/v1/users?page=1&pageSize=20&search=` | none | `200` `PagedResult<StaffMember>`, owner first, then by name |
| GET | `/api/v1/users/{id}` | none | `200` `StaffMember` |
| POST | `/api/v1/users` | `{ name, email, phone?, password, permissions: [] }` | `201` `StaffMember` |
| PUT | `/api/v1/users/{id}/permissions` | `{ permissions: [] }` | `200` `StaffMember` |
| POST | `/api/v1/users/{id}/disable` | none | `200`. Also ends all of that user's sessions. |
| POST | `/api/v1/users/{id}/enable` | none | `200` |
| POST | `/api/v1/users/{id}/reset-password` | `{ newPassword }` | `204`. Also ends all of that user's sessions. |

Error responses:

| Status | When |
|---|---|
| `403 FORBIDDEN` | The caller is a staff member |
| `404 USER_NOT_FOUND` | The user doesn't exist **or belongs to another organization** |
| `400 OWNER_CANNOT_BE_MODIFIED` | The target is the owner |
| `409 EMAIL_ALREADY_REGISTERED` | Emails are unique across the whole system, because login is by email alone |

### Paging

`page` starts at 1. `pageSize` is 1-100, with a default of 20. Responses use this shape:

```json
{ "items": [], "page": 1, "pageSize": 20, "totalCount": 0, "totalPages": 0 }
```

### Planned

The remaining endpoints follow spec section 10, and each one is documented here as it ships:

```text
GET|POST /api/v1/properties, GET|PUT|DELETE /properties/{id}  Phase 3
GET|POST /api/v1/properties/{id}/rooms, /rooms/{id}/beds       Phase 3
GET|POST /api/v1/tenants, GET|PUT /tenants/{id}                Phase 4
GET /api/v1/rent/charges | /rent/overdue, POST /rent/generate  Phase 5
POST /api/v1/payments, GET /payments/{id}, POST /{id}/void     Phase 6
GET /api/v1/receipts/{id} | /receipts/{id}/pdf                 Phase 6
GET /api/v1/dashboard                                          Phase 7
GET|POST /api/v1/reminders                                     Phase 8
```

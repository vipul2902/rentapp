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

### Properties, rooms and beds

**Access:** reading requires `ViewProperties` (owners always have it). Every change is **owner-only**.
Nothing is ever deleted: `DELETE` archives the record.

| Method | Path | Body | Success |
|---|---|---|---|
| GET | `/api/v1/properties?page&pageSize&search&includeArchived&sort=Name\|City\|Newest` | none | `200` `PagedResult<Property>` |
| POST | `/api/v1/properties` | `{ name, address, city, state?, postalCode?, contactPhone? }` | `201` `Property` |
| GET | `/api/v1/properties/{id}` | none | `200` `Property` |
| PUT | `/api/v1/properties/{id}` | same as POST | `200` `Property` |
| DELETE | `/api/v1/properties/{id}` | none | `204`, archives the property |
| POST | `/api/v1/properties/{id}/restore` | none | `200` `Property` |
| GET | `/api/v1/properties/{id}/rooms?includeArchived` | none | `200` `Room[]`, each with its beds. Not paged, because a property's rooms are bounded. |
| POST | `/api/v1/properties/{id}/rooms` | `{ roomNumber, roomType?, capacity (1-50), createBeds = true, defaultMonthlyRent? }` | `201` `Room`. With `createBeds`, beds A, B, C… are created up to the capacity. |
| GET | `/api/v1/rooms/{id}` | none | `200` `Room` |
| PUT | `/api/v1/rooms/{id}` | `{ roomNumber, roomType?, capacity, status: Active\|Unavailable }` | `200` `Room` |
| DELETE | `/api/v1/rooms/{id}` | none | `204`, archives the room **and its beds** |
| GET | `/api/v1/rooms/{id}/beds` | none | `200` `Bed[]` |
| POST | `/api/v1/rooms/{id}/beds` | `{ label?, defaultMonthlyRent? }` (the label defaults to the next free letter) | `201` `Bed` |
| GET | `/api/v1/beds/{id}` | none | `200` `Bed` |
| PUT | `/api/v1/beds/{id}` | `{ label, status: Available\|Reserved\|Unavailable, defaultMonthlyRent? }` | `200` `Bed` |
| DELETE | `/api/v1/beds/{id}` | none | `204`, archives the bed |

```jsonc
// Room
{ "id": "...", "propertyId": "...", "roomNumber": "201", "roomType": "AC", "capacity": 3, "status": "Active",
  "beds": [ { "id": "...", "roomId": "...", "label": "A", "status": "Available", "occupancy": "Vacant", "defaultMonthlyRent": 8500.00 },
            { "id": "...", "roomId": "...", "label": "B", "status": "Reserved",  "occupancy": "Reserved", "defaultMonthlyRent": 8500.00 } ],
  "occupancy": { "totalBeds": 2, "occupied": 0, "vacant": 1, "reserved": 1, "unavailable": 0 } }
```

- **`status`** is what the owner sets. **`occupancy`** is derived and read-only, with the values
  `Vacant`, `Occupied`, `Reserved` and `Unavailable`:
  - An active tenancy makes a bed `Occupied`. Tenancies arrive in Phase 4; until then no bed is occupied.
  - Otherwise, an `Unavailable` room makes every bed in it `Unavailable`.
- **`Property.occupancy`** and **`roomCount`** count only rooms and beds that aren't archived.
- **`defaultMonthlyRent`** is a suggestion used to pre-fill a tenancy. It must be a positive amount with
  at most 2 decimal places.

| Error `code` | Status | When |
|---|---|---|
| `PROPERTY_NOT_FOUND`, `ROOM_NOT_FOUND`, `BED_NOT_FOUND` | 404 | The record doesn't exist **or belongs to another organization** |
| `ROOM_NUMBER_TAKEN` | 409 | The room number is already used in this property (archived rooms don't count) |
| `BED_LABEL_TAKEN` | 409 | The label is already used in this room, compared without regard to case |
| `ROOM_FULL` | 409 | The room already has as many beds as its capacity |
| `CAPACITY_BELOW_BED_COUNT` | 400 | The new capacity is lower than the room's current number of beds |
| `PROPERTY_ARCHIVED`, `ROOM_ARCHIVED`, `BED_ARCHIVED` | 409 | The record is archived (restore the property first) |

### Tenants and tenancies

**Access:** reading requires `ViewTenants` (owners always have it). Every change is **owner-only**. A
*tenancy* is the spec's **RentAgreement**: one tenant in one bed, with rent, deposit, due day and dates.
Dates are calendar dates (`"2026-10-02"`) in the organization's time zone.

| Method | Path | Body | Success |
|---|---|---|---|
| GET | `/api/v1/tenants?page&pageSize&search&filter=All\|Current\|Former\|Unassigned&propertyId&roomId` | none | `200` `PagedResult<TenantSummary>` |
| POST | `/api/v1/tenants` | `{ fullName, phone, email?, emergencyContactName?, emergencyContactPhone?, permanentAddress?, moveIn? }` | `201` `TenantDetail` |
| GET | `/api/v1/tenants/{id}` | none | `200` `TenantDetail`, including `currentTenancy` and full `history` |
| PUT | `/api/v1/tenants/{id}` | the personal fields above | `200` `TenantDetail` |
| DELETE | `/api/v1/tenants/{id}` | none | `204`, archives the tenant. Allowed only when they have no bed. |
| POST | `/api/v1/tenants/{id}/move-in` | `{ bedId, startDate, monthlyRent?, securityDeposit = 0, rentDueDay = 5 }` | `200` `TenantDetail` |
| POST | `/api/v1/tenants/{id}/move-out` | `{ moveOutDate }` (the last day in the bed; not in the future) | `200` `TenantDetail` |
| POST | `/api/v1/tenants/{id}/move` | `{ bedId, moveDate, monthlyRent? }` (the first day in the new bed; not in the future) | `200` `TenantDetail` |
| PUT | `/api/v1/tenants/{id}/tenancy` | `{ monthlyRent, securityDeposit, rentDueDay }` | `200` `TenantDetail` |

**Search.** Matches name, email, or the **digits** of the phone number, so `9876543210` finds
"+91 98765 43210". It also matches the current room number exactly.

**Move-in rules:**
- `startDate` can be up to 10 years in the past, so tenants who already live there can be entered, and
  up to 1 year ahead.
- A future start date makes the tenancy **Upcoming**, and the bed shows **Reserved** until that date.
- `monthlyRent` defaults to the bed's `defaultMonthlyRent`. If the bed has none, it is required.
- Assigning a bed the owner had marked **Reserved** releases the reservation.

**Move-out rules:**
- For a current tenancy, move-out records the last day and frees the bed (`endReason: MovedOut`).
- An **upcoming** tenancy is **cancelled** instead (`endReason: Cancelled`), and nothing will ever be
  charged for it.

**Moving beds:**
- `move` ends the current tenancy the day before `moveDate` (`endReason: Transferred`) and starts a new one.
- The deposit and due day carry over. Rent defaults to the new bed's suggested rent, or else the current rent.

**Beds.** `Bed.tenant` (`{ tenantId, fullName, moveInDate }`) appears on room and bed responses **only
for callers with `ViewTenants`**. `occupancy` is always shown.

```jsonc
// Tenancy (currentTenancy / history items)
{ "id": "...", "propertyId": "...", "propertyName": "Sunrise PG", "roomId": "...", "roomNumber": "201",
  "bedId": "...", "bedLabel": "A", "monthlyRent": 8500.00, "securityDeposit": 10000.00, "rentDueDay": 5,
  "startDate": "2026-09-01", "endDate": null, "status": "Active", "state": "Current", "endReason": null }
```

| Error `code` | Status | When |
|---|---|---|
| `TENANT_NOT_FOUND`, `BED_NOT_FOUND` | 404 | The record doesn't exist **or belongs to another organization** |
| `BED_OCCUPIED` | 409 | The bed already has an active tenancy, including when a simultaneous request won |
| `TENANT_ALREADY_ASSIGNED` | 409 | The tenant already has a bed; use `move` |
| `BED_UNAVAILABLE`, `ROOM_UNAVAILABLE`, `PROPERTY_ARCHIVED` | 409 | The target isn't rentable |
| `NO_ACTIVE_TENANCY`, `TENANT_HAS_ACTIVE_TENANCY`, `TENANT_ARCHIVED` | 409 | The action doesn't fit the tenant's current state |
| `RENT_REQUIRED`, `SAME_BED`, `DATE_IN_FUTURE`, `DATE_BEFORE_MOVE_IN`, `MOVE_IN_OUT_OF_RANGE` | 400 | The input breaks a rule |

Since Phase 4, properties, rooms and beds that have tenants are protected. These requests return `409`:

| Request | Error `code` |
|---|---|
| Archive a property with tenants | `PROPERTY_HAS_TENANTS` |
| Archive a room with tenants, or mark it Unavailable | `ROOM_HAS_TENANTS` |
| Archive a bed with a tenant, or set it to Reserved or Unavailable | `BED_HAS_TENANT` |

### Rent dues

**Access:** reading requires `ViewTenants`. Generating and waiving are **owner-only**. Status values are
calculated when read, never stored: `Upcoming`, `DueToday`, `Overdue`, `PartiallyPaid`, `Paid`, `Waived`
and `Cancelled`. `daysOverdue` counts days since the due date while a balance remains.

| Method | Path | Body | Success |
|---|---|---|---|
| GET | `/api/v1/rent/charges?filter=All\|Outstanding\|Overdue\|DueToday\|Upcoming\|Paid&tenantId&propertyId&month&page&pageSize` | none | `200` `PagedResult<RentCharge>`. What's owed lists oldest first; `All` and `Paid` list newest first. |
| GET | `/api/v1/rent/charges/{id}` | none | `200` `{ charge, adjustments[] }` |
| GET | `/api/v1/rent/overdue?propertyId&page&pageSize` | none | `200` `PagedResult<RentCharge>`, most overdue first |
| GET | `/api/v1/rent/summary?propertyId` | none | `200` `{ today, outstanding, overdue, dueToday, dueThisWeek, billedThisMonth }`, each `{ amount, count }` |
| POST | `/api/v1/rent/generate` | none | `200` `{ created }`. Creates any missing charges and is safe to repeat. |
| POST | `/api/v1/rent/charges/{id}/adjustments` | `{ amount, reason }` | `200` `{ charge, adjustments[] }`. Waives part or all of the balance. |

Tenant responses also include `outstandingAmount` and `overdueAmount`, and the tenant list accepts
`filter=Overdue`.

**How charges are created.** These rules are tested in `RentScheduleTests`:
- There is one charge per tenant per calendar month, for the full monthly rent. Partial months aren't
  pro-rated; owners waive the difference with a reason.
- The **due date** is the tenancy's due day, moved to the month's last day in short months (31 becomes
  30 in September, and 28 or 29 in February). In the move-in month it is never earlier than the move-in date.
- A charge appears once its due date is within **7 days**. A background job runs every 6 hours to keep
  this current, and a move-in immediately creates every month owed for backdated tenants.
- When a tenant moves beds mid-month, the month is charged once, to the tenancy they started it in.
- Moving out cancels unpaid charges for months after the last day. A cancelled booking cancels all of its
  unpaid charges. Charges with any payment are never cancelled.

| Error `code` | Status | When |
|---|---|---|
| `RENT_CHARGE_NOT_FOUND` | 404 | The charge doesn't exist or belongs to another organization |
| `RENT_CHARGE_CANCELLED` | 409 | The charge is no longer owed |
| `ADJUSTMENT_EXCEEDS_BALANCE` | 400 | The waiver is larger than the remaining balance |

### Paging

`page` starts at 1. `pageSize` is 1-100, with a default of 20. Responses use this shape:

```json
{ "items": [], "page": 1, "pageSize": 20, "totalCount": 0, "totalPages": 0 }
```

### Planned

The remaining endpoints follow spec section 10, and each one is documented here as it ships:

```text
GET|POST /api/v1/tenants, GET|PUT /tenants/{id}                Phase 4
GET /api/v1/rent/charges | /rent/overdue, POST /rent/generate  Phase 5
POST /api/v1/payments, GET /payments/{id}, POST /{id}/void     Phase 6
GET /api/v1/receipts/{id} | /receipts/{id}/pdf                 Phase 6
GET /api/v1/dashboard                                          Phase 7
GET|POST /api/v1/reminders                                     Phase 8
```

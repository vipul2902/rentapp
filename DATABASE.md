# Database

PostgreSQL 17 is the only source of truth. Redis never holds financial state.

## Conventions

| Topic | Convention |
|---|---|
| Naming | snake_case tables and columns (`EFCore.NamingConventions`) |
| Keys | `uuid` primary keys, generated in the application as UUIDv7 so they sort by creation time |
| Money | `numeric(12,2)` by default (EF convention). `float` and `double` are never used. |
| Strings | `varchar(256)` by default. Longer columns opt in explicitly. |
| Timestamps | `timestamptz`, stored in UTC. `created_at` and `updated_at` are stamped automatically through `IAuditableEntity`, and `created_at` cannot be modified. |
| Calendar dates | `date` (C# `DateOnly`) for due dates and rent periods. "Today" is evaluated in the organization's time zone. |
| Tenancy | Every business table carries `organization_id`, which is indexed and enforced in queries. |
| Deletion | Financial records are never deleted; they are voided or reversed instead. Other records are archived (soft delete) where that makes sense. |
| Integrity | Foreign keys, unique constraints and check constraints in the database itself, not only in code |
| Transactions | Payment recording and allocation run in one transaction, with row locks on the affected charges. |

## Migrations

Migrations live in `src/RentApp.Infrastructure/Persistence/Migrations`. The EF CLI is pinned as a local
tool in `.config/dotnet-tools.json`.

```powershell
dotnet tool restore

# Create a migration after changing the model
dotnet ef migrations add <Name> --project src/RentApp.Infrastructure --startup-project src/RentApp.Infrastructure --output-dir Persistence/Migrations

# Apply migrations to the database in .env
dotnet ef database update --project src/RentApp.Infrastructure --startup-project src/RentApp.Infrastructure

# Produce an idempotent SQL script (for reviewed production deployments)
dotnet ef migrations script --idempotent --project src/RentApp.Infrastructure --startup-project src/RentApp.Infrastructure -o migrate.sql
```

The EF tooling reads `DATABASE_CONNECTION_STRING` from the environment or the repo `.env`
(`DesignTimeDbContextFactory`).

In Development and Testing, the API applies pending migrations at startup
(`Database:ApplyMigrationsOnStartup`). In Production that setting is **off**, and migrations are applied
as a reviewed deployment step.

## Organization isolation

The database layer enforces isolation in three ways:

1. **Global query filters.** Every entity that implements `IOrganizationScoped` is applied automatically by
   reflection, so a new entity can't miss it. Filters restrict queries to the caller's `organization_id`.
   With no authenticated caller, the filter matches **nothing**, so isolation fails closed.
2. **Save-time guard.** `OrganizationIsolationInterceptor` rejects any insert, update or delete of another
   organization's row while a request is authenticated.
3. **Indexes.** Every scoped table has an index on `organization_id`.

Only the sign-up, sign-in and refresh flows use `IgnoreQueryFilters()`, because they run before a caller
exists. Integration tests cover cross-organization reads and writes.

## Current schema

| Migration | Contents |
|---|---|
| `InitialBaseline` | An empty baseline that establishes the migration history |
| `AddOrganizationsUsersAndTokens` | The `organizations`, `users`, `refresh_tokens` and `audit_logs` tables |
| `AddPropertiesRoomsAndBeds` | The `properties`, `rooms` and `beds` tables |

| Table | Notes |
|---|---|
| `organizations` | `name`, `status` (Active/Suspended), `time_zone` (default `Asia/Kolkata`), `owner_user_id` (nullable FK to users; it is circular with `users.organization_id`, so it is set right after the owner row is inserted, in the same transaction) |
| `users` | `organization_id` (FK), `name`, `email` (as entered), `normalized_email` (**globally unique**, used for login), `phone`, `password_hash` (PBKDF2), `role` (Owner/Staff), `status` (Active/Disabled), `permissions` (int bit set), `last_login_at`. Check constraints cover role, status, the permission range, and "an owner stores no permissions". |
| `refresh_tokens` | `token_hash` (SHA-256 hex, unique; the token itself is never stored), `family_id` (one per login, for reuse detection), `expires_at`, `revoked_at` and `revoked_reason` (both null or both set, enforced by a check constraint), `replaced_by_token_id` |
| `audit_logs` | Append-only: `actor_user_id`, `action` (e.g. `user.permissions_changed`), `entity_type`, `entity_id`, `details` (jsonb, never containing secrets), `created_at`. Indexed by (organization, time) and (entity). |

| `properties` | `name`, `address`, `city`, `state`, `postal_code`, `contact_phone`, `status` (Active/Archived). Alternate key `(id, organization_id)`. |
| `rooms` | `property_id`, `room_number`, `room_type`, `capacity` (1-50, check constraint), `status` (Active/Unavailable/Archived). **Composite FK `(property_id, organization_id)` references `properties(id, organization_id)`**, so a room can never belong to another organization's property. Unique `(property_id, room_number)` **where status <> 'Archived'**. |
| `beds` | `room_id`, `label` (upper-case), `status` (Available/Reserved/Unavailable/Archived), `default_monthly_rent` numeric(12,2), which must be > 0 when set. Composite FK `(room_id, organization_id)` references `rooms`. Unique `(room_id, label)` where status <> 'Archived'. |

**Occupancy is never stored.** It is calculated when read, from bed status, room status and (from
Phase 4) active tenancies, so it cannot drift out of date.

**Capacity under concurrency.** Adding a bed or changing a room's capacity first takes a row lock on
the room (an `UPDATE` inside the transaction). Simultaneous requests therefore queue up, and a room can
never end up with more beds than its capacity. An integration test covers this.

Staff permission bit values are fixed and must never be renumbered: `ViewProperties`=1, `ViewTenants`=2,
`RecordPayments`=4, `GenerateReceipts`=8, `SendReminders`=16. A unit test enforces this.

The remaining domain tables, such as properties, rooms, beds, tenants and rent, arrive in their phases.

## Local database access

```powershell
docker compose exec postgres sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB"'
```

To reset the local database (**this destroys all local data**):

```powershell
docker compose down -v; docker compose up -d
```

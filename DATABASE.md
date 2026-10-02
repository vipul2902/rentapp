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

## Current schema

| Migration | Contents |
|---|---|
| `InitialBaseline` | An empty baseline that establishes the migration history (`__EFMigrationsHistory`) |

The domain tables arrive phase by phase, starting with organizations, users and refresh tokens in Phase 2.
The planned model, including PaymentAllocation, RentChargeAdjustment and ReceiptCounter, is described in
the Phase 0 report and will be documented here as each table ships.

## Local database access

```powershell
docker compose exec postgres sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB"'
```

To reset the local database (**this destroys all local data**):

```powershell
docker compose down -v; docker compose up -d
```

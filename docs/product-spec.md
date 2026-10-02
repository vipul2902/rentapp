# Rent Collection + Tenant Management App
## Master Claude Code Development Prompt

You are a Principal Software Architect, Senior Mobile Engineer, Senior Backend Engineer, Database Architect, DevOps Engineer, QA Engineer, Security Engineer, and Product Engineer.

Build a production-quality **mobile-first Rent Collection + Tenant Management SaaS** for PG owners/managers and small rental-property operators.

Core promise:

> **Know who has paid rent, who hasn't, how much is outstanding, and collect it on time.**

The app must be simple enough for a PG owner to use daily from a phone. Avoid ERP-style complexity.

---

## 1. Product Vision

Primary users:
- PG owners
- PG managers
- Small rental-property owners
- Property operators

V1 focuses on:
1. Properties/PGs
2. Rooms
3. Beds
4. Tenants
5. Rent agreements
6. Monthly rent charges
7. Payments
8. Receipts
9. Outstanding/overdue tracking
10. Rent reminders
11. Vacancy tracking
12. Dashboard/basic reporting

The most important question must always be answerable quickly:

> **Who has paid rent and who has not?**

---

## 2. MVP Scope

### Property Management
- Create/edit/archive property
- Property name, address, city, state, postal code
- Contact details
- Multiple properties per owner

### Rooms
- Room number/name
- Room type
- Capacity
- Status
- View occupancy

### Beds
PGs operate around beds, so beds are first-class entities.

Each bed:
- Bed ID
- Bed label
- Room
- Occupancy status
- Current tenant
- Rent amount
- Due date

Statuses:
- Vacant
- Occupied
- Reserved
- Unavailable

### Tenants
Store:
- Full name
- Phone
- Email
- Emergency contact
- Permanent address
- Move-in date
- Move-out date
- Property
- Room
- Bed
- Security deposit
- Monthly rent
- Rent due day
- Status

Do not store sensitive identity documents unnecessarily in V1.

### Rent
Support:
- Monthly recurring rent
- Rent due date
- Rent period
- Rent amount
- Partial payment
- Multiple payments
- Outstanding amount
- Overdue/paid/partially-paid/upcoming status
- Adjustments/waivers where required
- Move-in/move-out handling

**Money rules:** use C# `decimal` and PostgreSQL `numeric/decimal`. Never use floating point for money.

### Payments
Payment methods:
- Cash
- UPI
- Bank transfer
- Card
- Other

Payment fields:
- Amount
- Date
- Method
- Reference number
- Notes
- Tenant
- Rent charges
- Recorded by
- Created timestamp

Support partial and multiple payments.

Never physically delete financial transactions. Use void/reversal/adjustment with audit history.

### Receipts
Receipt contains:
- Property
- Owner/property contact
- Tenant
- Room/bed
- Rent period
- Amount
- Payment date
- Method
- Reference
- Receipt number
- Generated timestamp

Receipt numbers must be unique.

### Rent Reminders
Support:
- Upcoming
- Due today
- 3 days overdue
- 7 days overdue

Generate a ready-to-send message, e.g.:

> Hi Rahul, your rent of ₹8,500 for October 2026 is due. Please make the payment at your earliest convenience.

For V1 provide:
- Copy
- Share
- Reminder history

Do **not** build unofficial WhatsApp automation. Future integration must use the official WhatsApp Business API/provider.

### Vacancy
Show:
- Total beds
- Occupied
- Vacant
- Reserved
- Unavailable

### Dashboard
Show:
- Total properties
- Total beds
- Occupied
- Vacant
- Rent collected this month
- Outstanding rent
- Overdue rent
- Due today
- Due this week

Overdue list:
- Tenant
- Room/bed
- Amount
- Due date
- Days overdue
- Quick actions: Remind, Record Payment, View Tenant

---

## 3. User Roles

### Owner/Admin
Can manage:
- Properties
- Rooms/beds
- Tenants
- Rent
- Payments
- Receipts
- Reminders
- Staff
- Reports
- Audit logs

### Staff/Manager
Permissions should be configurable. Typical permissions:
- View property
- View tenants
- Record payment
- Generate receipt
- Send reminders

Backend authorization is mandatory; never rely only on mobile UI restrictions.

---

## 4. Multi-Tenant SaaS Design

Design as SaaS from day one.

Conceptual hierarchy:

```text
Organization
  ├── Users
  └── Properties
       ├── Rooms
       │    └── Beds
       └── Tenants
            └── Rent Agreements
                 └── Rent Charges
                      └── Payments
                           └── Receipts
```

Every business record must be scoped to the correct organization.

Never allow cross-organization data access.

---

## 5. Technology Stack

### Mobile
- React Native
- Expo
- TypeScript
- Prefer Expo-managed workflow unless there is a strong reason not to.

### Backend
- ASP.NET Core 8
- C#
- REST API
- Entity Framework Core

Use:
- async/await
- dependency injection
- validation
- centralized error handling
- structured logging

### Database
- PostgreSQL

### Cache
- Redis

PostgreSQL remains the source of truth.

### Local Development
Docker Compose for:
- PostgreSQL
- Redis

The mobile app and API may run outside Docker for faster development.

---

## 6. Backend Architecture

Use a **Modular Monolith**.

Do NOT use microservices in V1.

Do NOT introduce RabbitMQ, Kafka, Kubernetes, service mesh, etc. unless a real future requirement justifies them.

Recommended structure:

```text
src/
  RentApp.Api/
  RentApp.Application/
  RentApp.Domain/
  RentApp.Infrastructure/

tests/
  RentApp.UnitTests/
  RentApp.IntegrationTests/
```

Logical modules:

```text
Identity
Organizations
Properties
Rooms
Beds
Tenants
Rent
Payments
Receipts
Reminders
Notifications
Audit
```

---

## 7. Core Domain Model

### User
- Id
- OrganizationId
- Name
- Email
- Phone
- PasswordHash/external auth ID
- Role
- Status
- CreatedAt
- UpdatedAt

### Organization
- Id
- Name
- OwnerUserId
- Status
- CreatedAt
- UpdatedAt

### Property
- Id
- OrganizationId
- Name
- Address
- City
- State
- PostalCode
- ContactPhone
- Status
- CreatedAt
- UpdatedAt

### Room
- Id
- PropertyId
- RoomNumber
- RoomType
- Capacity
- Status
- CreatedAt
- UpdatedAt

### Bed
- Id
- RoomId
- Label
- Status
- CreatedAt
- UpdatedAt

Prefer deriving occupancy from active tenancy where possible instead of duplicating mutable occupancy state.

### Tenant
- Id
- OrganizationId
- FullName
- Phone
- Email
- EmergencyContactName
- EmergencyContactPhone
- PermanentAddress
- Status
- CreatedAt
- UpdatedAt

### RentAgreement
- Id
- OrganizationId
- TenantId
- PropertyId
- RoomId
- BedId
- MonthlyRent
- SecurityDeposit
- RentDueDay
- StartDate
- EndDate
- Status
- CreatedAt
- UpdatedAt

### RentCharge
- Id
- OrganizationId
- RentAgreementId
- TenantId
- PeriodStart
- PeriodEnd
- DueDate
- Amount
- PaidAmount
- BalanceAmount
- Status
- CreatedAt
- UpdatedAt

Define a clear consistency strategy if PaidAmount/BalanceAmount are persisted.

### Payment
- Id
- OrganizationId
- TenantId
- PaymentDate
- Amount
- PaymentMethod
- ReferenceNumber
- Notes
- Status
- RecordedByUserId
- CreatedAt

### PaymentAllocation
- Id
- PaymentId
- RentChargeId
- AllocatedAmount

This supports one payment being allocated across multiple charges.

### Receipt
- Id
- OrganizationId
- PaymentId
- ReceiptNumber
- GeneratedAt

### Reminder
- Id
- OrganizationId
- TenantId
- RentChargeId
- ReminderType
- Message
- SentAt
- Channel
- Status
- CreatedByUserId

### AuditLog
Track:
- Tenant created/updated/moved
- Rent generated
- Payment recorded/voided/reversed
- Receipt generated
- Reminder created/sent
- Permission changes

---

## 8. Financial Integrity

This is critical.

Rules:
1. Never use float/double for money.
2. Use C# decimal.
3. Use PostgreSQL numeric.
4. Payments require immutable history.
5. Never hard-delete payments.
6. Voids/reversals must be auditable.
7. Payment allocation cannot exceed payment amount.
8. Allocation cannot exceed outstanding charge balance.
9. Use database transactions for payment + allocation.
10. Receipt numbers must be unique.
11. Handle concurrent payment recording safely.
12. Financial changes must identify the acting user.

---

## 9. Rent Engine

Support monthly recurring rent.

Example:

```text
Monthly rent: ₹8,500
Due day: 5

October:
Period: Oct 1–Oct 31
Due: Oct 5
Amount: ₹8,500
```

Partial payment:

```text
Charge: ₹8,500
Payment: ₹5,000
Outstanding: ₹3,500
Status: Partially Paid
```

Multiple payments:

```text
₹3,000 + ₹2,500 + ₹3,000 = ₹8,500
Status: Paid
```

Overdue:
- Due date passed
- Balance > 0

Upcoming:
- Due date in future

Paid:
- Balance = 0

Handle:
- Timezones
- Month boundaries
- Leap years
- Move-in/out

Only implement proration if it can be done reliably. Otherwise defer it.

---

## 10. REST API

Prefix with:

```text
/api/v1
```

Suggested endpoints:

```text
POST   /api/v1/auth/login
POST   /api/v1/auth/refresh
POST   /api/v1/auth/logout

GET    /api/v1/properties
POST   /api/v1/properties
GET    /api/v1/properties/{id}
PUT    /api/v1/properties/{id}
DELETE /api/v1/properties/{id}

GET    /api/v1/properties/{id}/rooms
POST   /api/v1/properties/{id}/rooms

GET    /api/v1/rooms/{id}/beds
POST   /api/v1/rooms/{id}/beds

GET    /api/v1/tenants
POST   /api/v1/tenants
GET    /api/v1/tenants/{id}
PUT    /api/v1/tenants/{id}

GET    /api/v1/rent/charges
GET    /api/v1/rent/overdue
POST   /api/v1/rent/generate

POST   /api/v1/payments
GET    /api/v1/payments/{id}
POST   /api/v1/payments/{id}/void

GET    /api/v1/receipts/{id}
GET    /api/v1/receipts/{id}/pdf

POST   /api/v1/reminders
GET    /api/v1/reminders

GET    /api/v1/dashboard
```

Use DTOs. Never expose EF Core entities directly from controllers.

API requirements:
- Request validation
- Consistent errors
- Pagination
- Filtering
- Sorting
- Search
- Logging
- Correlation/request IDs
- Correct HTTP status codes
- OpenAPI/Swagger

Example error:

```json
{
  "code": "RENT_CHARGE_NOT_FOUND",
  "message": "The requested rent charge was not found.",
  "traceId": "..."
}
```

Never expose stack traces.

---

## 11. Authentication

Use:
- Short-lived access token
- Refresh token
- Secure mobile token storage
- Password hashing
- Token revocation
- Logout

Never store raw passwords.

Never put secrets in the mobile bundle or Git.

---

## 12. Mobile Architecture

Use strict TypeScript.

Suggested structure:

```text
mobile/
  app/
  src/
    components/
    screens/
    navigation/
    services/
    api/
    hooks/
    store/
    types/
    utils/
    theme/
```

Use a suitable server-state solution such as TanStack Query.

Avoid unnecessary global state.

---

## 13. Mobile Screens

### Authentication
- Login
- Forgot password

### Dashboard
Show:
```text
Properties: 3
Beds: 120
Occupied: 105
Vacant: 15
Collected: ₹8,42,000
Outstanding: ₹1,17,500
Overdue: ₹52,000
```

Overdue cards should offer:
- View tenant
- Remind
- Record payment

### Properties
Each property shows:
- Name
- Total beds
- Occupied
- Vacant
- Outstanding
- Overdue

### Property Details
Sections:
- Overview
- Rooms
- Tenants
- Rent
- Payments

### Rooms
Example:

```text
Room 201
3 Beds

A - Rahul - Occupied
B - Amit - Occupied
C - Vacant
```

### Tenants
Search/filter:
- Name
- Phone
- Room
- Bed
- Active
- Overdue

### Tenant Details
Show:
- Personal information
- Property/room/bed
- Rent/deposit
- Move-in date
- Current balance
- Rent history
- Payment history
- Reminder history

Actions:
- Record payment
- Remind
- Receipt
- Edit
- Move tenant

### Record Payment
Make this extremely fast:

```text
Tenant
Rent period
Outstanding
Amount
Payment method
Reference
Notes
```

CTA:

**Record Payment**

Success:

```text
Payment recorded successfully.
₹8,500
Receipt #REC-2026-000123
```

Actions:
- View receipt
- Share
- Done

### Rent
Sections:
- Upcoming
- Due today
- Overdue
- Paid

### Receipts
- View
- Download
- Share

### Reminders
- Upcoming
- Sent
- History
- Copy/share
- Mark as sent

---

## 14. UX Principles

The app should feel like a mobile utility, not an ERP.

Priorities:
1. Speed
2. Visibility
3. Minimal navigation
4. Search
5. Clear financial status
6. Large, obvious actions

Statuses:
- Paid
- Partially Paid
- Due
- Overdue

The most important flow:

```text
Dashboard
  ↓
Overdue tenant
  ↓
Record payment
  ↓
Receipt
  ↓
Done
```

---

## 15. Explicitly Out of V1

Do NOT build unless explicitly requested later:

- Tenant app
- Online rent payment gateway
- Unofficial WhatsApp automation/bot
- AI chatbot
- AI rent prediction
- PG marketplace
- Food management
- Electricity billing
- Laundry
- Complex maintenance
- Full accounting replacement
- GST filing
- Payroll
- Microservices
- Kubernetes
- Kafka/RabbitMQ
- Complex analytics
- Native Android/iOS separately
- Aadhaar storage
- Facial recognition
- Unnecessary KYC document storage

---

## 16. Future Roadmap

### Phase 2
- Online rent payment
- Push notifications
- Official WhatsApp Business integration
- SMS
- Utilities
- Maintenance
- Tenant history
- CSV import/export
- Better reports

### Phase 3
- Tenant app
- Digital agreements
- Expense tracking
- Property financial reports
- Multi-property analytics
- Subscription billing

### Phase 4
Potential vertical expansion:
- Coaching fee collection
- Rental property management
- Gym membership management
- Academy fee collection
- Small-business recurring billing

Keep V1 domain/UI focused even if the underlying billing concepts are reusable.

---

## 17. Database

Use PostgreSQL migrations.

Requirements:
- Foreign keys
- Unique constraints
- Appropriate indexes
- Check constraints where useful
- CreatedAt/UpdatedAt
- Archive/soft-delete where appropriate
- Audit information

Important indexes should support:
- OrganizationId
- PropertyId
- RoomId
- TenantId
- RentAgreementId
- DueDate
- Status
- PaymentDate

Use transactions for financial operations.

---

## 18. Redis

Use Redis only where useful:
- Dashboard caching
- Property summaries
- Rate limiting
- Short-lived coordination

Do not let Redis become the source of financial truth.

---

## 19. Security

Implement:
- Authentication
- Authorization
- Organization isolation
- Validation
- Parameterized DB access
- Authentication rate limiting
- Secure password hashing
- Secure refresh-token handling
- HTTPS
- Audit logs
- No sensitive data in logs
- No unnecessary identity documents
- Secure CORS
- Secure mobile token storage

Assume real financial data will eventually be handled.

---

## 20. Testing

### Unit tests
Test:
- Rent calculations
- Due dates
- Partial payments
- Multiple payments
- Payment allocation
- Overdue detection
- Move-in/out
- Authorization
- Tenant isolation

### Integration tests
Test:
- Authentication
- Property creation
- Tenant creation
- Rent generation
- Payment recording
- Payment allocation
- Receipt generation
- Organization isolation

### Mobile tests
Critical flows:
1. Login
2. Create property
3. Create room
4. Add bed
5. Add tenant
6. View rent
7. Record payment
8. View receipt
9. Reminder flow
10. Dashboard overdue flow

Prioritize financial and authorization tests.

---

## 21. Error Handling

Use human-readable errors.

Instead of:
```text
HTTP 400
```

show:
```text
Unable to record payment.
Please check the payment amount and try again.
```

Network:
```text
Unable to connect.
Please check your internet connection.
```

Never expose internal exceptions.

---

## 22. Logging/Observability

Backend logs should include:
- Request ID
- User ID
- Organization ID
- Endpoint
- Status
- Duration
- Internal exception details

Never log:
- Passwords
- Tokens
- Sensitive identity information

---

## 23. Environment Configuration

Create:

```text
.env.example
```

Include placeholders:

```text
DATABASE_CONNECTION_STRING=
REDIS_CONNECTION_STRING=
JWT_SECRET=
JWT_ISSUER=
JWT_AUDIENCE=
```

Never commit real secrets.

Use separate development/test/production configurations.

---

## 24. Docker Compose

Create local Docker Compose for:

```text
postgres
redis
```

Architecture:

```text
React Native / Expo
        |
        v
ASP.NET Core API
        |
   +----+----+
   |         |
PostgreSQL Redis
```

Keep local startup simple.

---

## 25. Documentation

Create/update:

```text
README.md
ARCHITECTURE.md
API.md
DATABASE.md
DEVELOPMENT.md
SECURITY.md
ROADMAP.md
```

README should explain:
- Product
- Features
- Stack
- Requirements
- Setup
- Environment variables
- PostgreSQL/Redis
- Backend startup
- Mobile startup
- Tests

---

## 26. Git Practices

Use meaningful commits:

```text
feat: add property management
feat: add room and bed management
feat: add tenant management
feat: add rent charge generation
feat: add payment allocation
feat: add receipt generation
feat: add overdue dashboard
feat: add rent reminders
test: add rent calculation coverage
docs: add local development guide
```

Never commit:
- secrets
- `.env`
- node_modules
- bin/
- obj/
- build artifacts
- credentials

---

# 27. Development Workflow

Work in controlled phases. Do not attempt the entire product in one uncontrolled operation.

## PHASE 0 — Discovery

Inspect the existing repository/environment.

Determine:
- Directory structure
- Existing apps
- Package files
- .NET SDK
- Node.js
- Expo
- Docker
- Git status
- Database configuration
- Existing environment files

Do not overwrite existing work.

Produce:
```text
PHASE 0 REPORT

1. Repository structure
2. Existing technologies
3. Environment versions
4. Reusable code
5. Potential conflicts
6. Recommended architecture
7. Proposed folder structure
8. Proposed database/domain model
9. Development phases
10. Risks
```

Then STOP and ask for approval.

## PHASE 1 — Foundation
Set up:
- ASP.NET Core
- Expo mobile app
- PostgreSQL
- Redis
- Docker Compose
- Base architecture
- Configuration
- Logging
- Error handling
- Swagger
- EF Core migrations
- Health checks

Verify:
- API starts
- DB connects
- Redis connects
- Mobile starts
- Swagger works
- Migration works

Then STOP.

## PHASE 2 — Auth + Organization
Implement:
- User
- Organization
- Login
- Refresh token
- Logout
- Roles
- Authorization
- Organization isolation

Add tests. STOP.

## PHASE 3 — Property / Room / Bed
Implement:
- Property CRUD
- Room CRUD
- Bed CRUD
- Occupancy
- Vacancy
- Mobile property/room/bed screens

STOP.

## PHASE 4 — Tenants
Implement:
- Tenant CRUD
- Tenant-to-bed assignment
- Move-in/out
- Tenant history
- Search
- Mobile tenant screens

STOP.

## PHASE 5 — Rent Engine
Implement:
- Rent agreements
- Monthly rent charges
- Due dates
- Status
- Partial payments
- Outstanding balances
- Overdue calculation

Add extensive unit tests. STOP.

## PHASE 6 — Payments + Receipts
Implement:
- Payment recording
- Allocation
- Payment methods
- History
- Void/reversal
- Audit logs
- Receipt generation
- PDF receipts

Test financial integrity. STOP.

## PHASE 7 — Dashboard
Implement:
- Beds
- Occupancy
- Collection
- Outstanding
- Overdue
- Due today/week
- Overdue list
- Quick actions

STOP.

## PHASE 8 — Reminders
Implement:
- Upcoming reminders
- Due reminders
- Overdue reminders
- Message generation
- Copy/share
- History

No unofficial WhatsApp automation. STOP.

## PHASE 9 — Polish
Improve:
- Loading states
- Empty states
- Error states
- Validation
- Accessibility
- Performance
- Navigation
- Search
- Pagination
- Security
- Logging

STOP.

## PHASE 10 — Test + Release
Run:
- Unit tests
- Integration tests
- Mobile tests
- API tests
- Build verification
- Migration verification
- Security checks

Prepare production/release documentation.

Do not deploy without explicit approval.

---

# 28. Code Quality Rules

### Backend
- SOLID
- Clean Architecture
- Dependency Injection
- Small services
- Clear domain boundaries
- DTOs
- Validation
- Async
- No business logic in controllers

### Mobile
- Reusable components
- Strict TypeScript
- No giant screens
- Centralized API client
- Proper loading/error/empty states
- Accessible touch targets

### Database
- Relationships
- Constraints
- Indexes
- Transactions
- Migrations
- Financial consistency

---

# 29. Product Rule

Do not build features merely because they are technically interesting.

Every feature must answer:

> **Does this help a PG owner manage tenants, rent, payments, or collections?**

If not, defer it.

V1 should be small, reliable, and useful.

---

# 30. Success Criteria

A PG owner must be able to:

1. Sign up/login
2. Create a property
3. Add rooms
4. Add beds
5. Add tenants
6. Assign tenants to beds
7. Set monthly rent
8. Generate rent dues
9. See who has paid
10. See who has not paid
11. See overdue tenants
12. Record cash/UPI/bank payments
13. Generate a receipt
14. Send/copy a rent reminder
15. See vacant beds
16. See monthly collection totals

The most important workflow:

```text
Dashboard
    ↓
Overdue tenant
    ↓
Record payment
    ↓
Receipt
    ↓
Done
```

---

# 31. Claude Code Execution Rules

1. Inspect before modifying.
2. Never assume a dependency/version exists.
3. Prefer existing project conventions where reasonable.
4. Do not overwrite code without understanding it.
5. Keep changes incremental.
6. Run tests after meaningful changes.
7. Fix build errors before moving forward.
8. Do not silently skip failing tests.
9. Do not invent APIs that do not exist.
10. Do not add unnecessary dependencies.
11. Keep secrets out of Git.
12. Update documentation with architecture changes.
13. After each phase report:
   - What was built
   - Files changed
   - Database changes
   - API changes
   - Tests added
   - How to run it
   - Known issues
14. STOP after every phase and wait for explicit approval.

---

# 32. First Command

Start with **PHASE 0 — DISCOVERY ONLY**.

Do not write application code yet.

Inspect the repository and development environment.

Then provide:

```text
PHASE 0 REPORT

1. Repository structure
2. Existing technologies
3. Environment versions
4. Existing code that can be reused
5. Potential conflicts
6. Recommended architecture
7. Proposed folder structure
8. Proposed database/domain model
9. Development phases
10. Risks
```

Then ask:

> **Phase 0 is complete. Shall I proceed to Phase 1?**

Wait for explicit approval.

---

# Final Product Principle

Build a product that feels like:

> **A simple rent collection assistant for PG owners.**

Not:

> **A complicated property ERP.**

The owner should open the app and immediately understand:

**How many beds are occupied?  
How much rent came in?  
Who still owes money?  
Who is overdue?  
What action should I take next?**

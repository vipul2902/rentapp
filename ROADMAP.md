# Roadmap

Every feature must answer one question: **does this help a PG owner manage tenants, rent, payments or
collections?** If it doesn't, it is deferred.

## V1 delivery phases

Each phase ends with a report and an explicit approval before the next one starts.

| Phase | Scope | State |
|---|---|---|
| 0 | Discovery and plan | Done |
| 1 | Foundation: API, mobile shell, PostgreSQL, Redis, Docker Compose, configuration, logging, errors, Swagger, migrations, health checks | Done |
| 2 | Auth and organizations: register, login, refresh, logout, roles and permissions, organization isolation | Next |
| 3 | Properties, rooms and beds: CRUD, occupancy, vacancy, mobile screens | Planned |
| 4 | Tenants: CRUD, bed assignment, move-in and move-out, history, search, mobile screens | Planned |
| 5 | Rent engine: agreements, monthly charges, due dates, statuses, balances, overdue | Planned |
| 6 | Payments and receipts: recording, allocation, void/reversal, audit, receipt numbers, PDFs | Planned |
| 7 | Dashboard: occupancy, collected, outstanding, overdue, due today and this week, quick actions | Planned |
| 8 | Reminders: upcoming, due and overdue messages, copy and share, history | Planned |
| 9 | Polish: loading, empty and error states, accessibility, performance, pagination, security | Planned |
| 10 | Test and release: full test pass, build and migration verification, release documentation. **Nothing is deployed without explicit approval.** | Planned |

## Decisions deferred to their phase

- **Proration (Phase 5).** V1 charges full months unless proration can be made reliable. Owners can
  apply manual adjustments.
- **Forgot password (Phase 2).** Self-service email reset needs an email provider, which is not chosen yet.
  Until then, owners reset staff passwords, and in development the email is written to the log.
- **PDF library (Phase 6).** An MIT-licensed library (PDFsharp/MigraDoc) is preferred, to avoid
  revenue-based license terms.

## After V1

**Phase 2 (product):** online rent payment, push notifications, official WhatsApp Business API
integration, SMS, utilities, maintenance, tenant history, CSV import and export, better reports.

**Phase 3 (product):** a tenant app, digital agreements, expense tracking, property financial reports,
multi-property analytics, subscription billing.

**Phase 4 (product):** possible vertical expansion into coaching or academy fee collection, gyms, and
small-business recurring billing. The V1 domain and UI stay focused on PGs even though the billing
concepts could be reused.

## Explicitly out of V1

- A tenant app
- An online payment gateway
- Unofficial WhatsApp automation or bots
- AI chatbot or rent prediction
- A marketplace
- Food management, electricity billing or laundry
- Complex maintenance
- Full accounting, GST filing or payroll
- Microservices, Kubernetes, Kafka or RabbitMQ
- Complex analytics
- Separate native Android and iOS apps
- Storing Aadhaar numbers, facial recognition, or unnecessary KYC documents

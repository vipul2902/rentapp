# Security

This app will handle real financial data and tenant personal data. It is built as if it already does.

## Controls

| Control | Status |
|---|---|
| Secrets stay out of Git: `.env` is git-ignored, and only `.env.example` with placeholders is committed | Phase 1 |
| Secrets never go in the mobile bundle: only `EXPO_PUBLIC_API_URL`, which is public, is compiled in | Phase 1 |
| No stack traces or internal details in API responses; one standard error body | Phase 1 |
| Logs exclude query strings, passwords and tokens; EF sensitive-data logging is never enabled | Phase 1 |
| Correlation IDs on every request and response, for tracing without logging personal data | Phase 1 |
| Database and Redis ports bound to `127.0.0.1` in local Docker Compose | Phase 1 |
| Strict CORS: no browser origins unless configured explicitly | Phase 1 |
| HTTPS and HSTS outside Development and Testing | Phase 1 |
| Parameterized database access only (EF Core; no string-built SQL) | Phase 1 onward |
| Password hashing: PBKDF2-HMAC-SHA512 through ASP.NET Core `PasswordHasher`, with transparent re-hashing when parameters improve | Done (Phase 2) |
| 15-minute JWT access tokens, plus 30-day refresh tokens that are stored **hashed**, **rotated** on every use, and revoked as a whole session if a rotated token is replayed; logout revokes the session | Done (Phase 2) |
| Rate limiting on register, login and refresh (10 per minute per IP, configurable). In-memory; must move to Redis before running more than one instance. | Done (Phase 2) |
| On mobile, only the refresh token is persisted, in Keychain/Keystore (`expo-secure-store`, this device only); the access token is kept in memory | Done (Phase 2) |
| Deny-by-default authorization (fallback policy), owner-only and per-permission policies, plus service-level owner checks | Done (Phase 2) |
| Organization isolation: automatic global filters that fail closed, a save-time cross-organization guard, 404 for other organizations' records, and integration tests | Done (Phase 2) |
| Tenant data minimization: contact details only, no identity documents. Tenant names appear on bed data only for users with `ViewTenants`. Tenant names and phone numbers are not logged. | Done (Phase 4) |
| Database-level isolation for child records: composite foreign keys `(parent_id, organization_id)`, so a room or bed cannot reference another organization's property or room | Done (Phase 3) |
| Audit log recording the acting user: registration and staff changes (Phase 2); waivers (Phase 5); payments and voids (Phase 6) | Done |
| Financial and audit history is permanent: database triggers reject edits and deletes of audit entries, waivers, allocations and receipts, and deletes of payments and charges. Payments only change by being voided once. | Done (Phase 6) |
| Idempotent payment recording (`Idempotency-Key`), so network retries cannot double-charge the books | Done (Phase 6) |
| Receipt PDFs are sent with `Cache-Control: private, no-store` and are only available to users with receipt permission | Done (Phase 6) |
| In-app account deletion. Staff accounts are anonymized; the owner's deletion closes the organization and anonymizes every account. All sessions end and a password is required. | Done (Phase 9) |
| Security headers on every response (nosniff, frame deny, no referrer, strict CSP) and `no-store` caching by default | Done (Phase 9) |
| 1 MB request body limit; no `Server` header | Done (Phase 9) |
| Slow requests (over 1 s) logged as warnings, still without query strings or personal data | Done (Phase 9) |
| Dependency audit: `dotnet list package --vulnerable` reports none. See the npm note below. | Done (Phase 9) |

## Known limitations (accepted for V1)

- **Revocation delay.** Revoking a permission or disabling an account takes up to 15 minutes to reach an
  access token that is already issued. Refresh tokens are revoked immediately.
- **No account lockout.** Brute-force protection is per-IP rate limiting only.
- **npm audit (Phase 9).** 18 findings (5 high), all in packages pulled in by Expo SDK 57. Most are
  build-time tooling (`@expo/cli`, `node-forge`, `xcode`, `uuid`); `decode-uri-component` (via expo-router)
  is a denial-of-service on malformed URLs inside the app. Fixing them needs a new Expo SDK, so re-check
  before release. `npm audit fix` did not reduce them and was reverted.
- **Closed organizations' data is retained.** Financial records are permanent by design. How long to keep
  them, and when to purge tenant personal data after closure, needs a retention policy and legal review
  (DPDP Act) before launch.
- **One account per email.** A person cannot belong to two organizations with the same email.
- **No owner self-service password reset.** This needs an email provider; see ROADMAP.md.
- **Permanence is enforced by triggers, not by database roles.** The app's database user owns the tables,
  so it could drop a trigger. In production, run migrations with a separate owner role and give the app a
  role with no DDL rights. This is on the Phase 10 checklist.

## Data handling

- **Minimal personal data.** Only the tenant fields in the spec are stored. Aadhaar numbers and other
  identity documents, KYC scans and biometrics are **not** stored in V1.
- **Personal data never goes in logs.** That covers phone numbers, addresses, names in search terms,
  and tokens.
- **Financial records are immutable.** Payments are voided or reversed with a reason and the acting user;
  they are never deleted.
- **Legal review is needed before launch.** India's Digital Personal Data Protection Act, 2023 is likely
  to apply. This is a general-knowledge note, not legal advice, and consent, retention and data-subject
  rights need review before production.

## Production checklist (to be completed in Phase 10)

- [ ] Set `AllowedHosts` to the real host names (it is currently `*`)
- [ ] Configure forwarded headers if running behind a reverse proxy or load balancer
- [ ] Store secrets in a managed secret store, never in files on the server
- [ ] Use TLS for PostgreSQL and Redis connections, and require a Redis password or ACL
- [ ] Apply migrations as a reviewed step, never on startup
- [ ] Use a separate database owner role for migrations and a least-privilege app role, with no DDL and no ability to drop triggers
- [ ] Turn off Swagger (already Development-only)
- [ ] Run `npm audit` and `dotnet list package --vulnerable`, then triage the findings

## Reporting a vulnerability

Report suspected vulnerabilities privately to the project owner. Do not open a public issue.

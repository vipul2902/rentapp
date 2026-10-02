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
| Audit log recording the acting user: registration and staff changes (Phase 2); financial changes (Phase 6) | Partly done |

## Known limitations (accepted for V1)

- **Revocation delay.** Revoking a permission or disabling an account takes up to 15 minutes to reach an
  access token that is already issued. Refresh tokens are revoked immediately.
- **No account lockout.** Brute-force protection is per-IP rate limiting only.
- **One account per email.** A person cannot belong to two organizations with the same email.
- **No owner self-service password reset.** This needs an email provider; see ROADMAP.md.
- **Audit logs are append-only by convention, not by database permissions.** Revoking UPDATE/DELETE from
  the app's database role is planned for Phase 6.

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
- [ ] Turn off Swagger (already Development-only)
- [ ] Run `npm audit` and `dotnet list package --vulnerable`, then triage the findings

## Reporting a vulnerability

Report suspected vulnerabilities privately to the project owner. Do not open a public issue.

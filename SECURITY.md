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
| Password hashing (PBKDF2 through ASP.NET Core `PasswordHasher`) | Phase 2 |
| Short-lived JWT access tokens, rotating refresh tokens stored **hashed**, revocation, logout | Phase 2 |
| Rate limiting on authentication endpoints | Phase 2 |
| Mobile tokens kept in the device's secure storage (`expo-secure-store`), never in AsyncStorage | Phase 2 |
| Authorization enforced on the backend for every endpoint, never only by hiding UI | Phase 2 |
| Organization isolation: every query is scoped by `organization_id`, with tests that try cross-organization access | Phase 2 |
| Audit log of financial and permission changes, recording the acting user | Phase 6 |

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

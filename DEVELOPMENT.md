# Local development

## One-time setup

1. Install the .NET SDK 10, Node.js 20 or newer, and Docker Desktop. Install **Expo Go** on your phone.
2. From the repo root:

   ```powershell
   Copy-Item .env.example .env
   ```

3. Edit `.env`:
   - Set `POSTGRES_PASSWORD`, and put the same value after `Password=` in `DATABASE_CONNECTION_STRING`.
   - Set `JWT_SECRET` to a random value. You can generate one with:

     ```powershell
     [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
     ```

4. Install the EF tool and mobile dependencies:

   ```powershell
   dotnet tool restore
   cd mobile; npm install
   ```

## Daily workflow

```powershell
docker compose up -d                                          # PostgreSQL and Redis
dotnet run --project src/RentApp.Api --launch-profile http     # API on http://0.0.0.0:5080
cd mobile; npx expo start                                      # Metro on :8081; scan the QR code with Expo Go
```

| URL | What it is |
|---|---|
| http://localhost:5080/swagger | Swagger UI |
| http://localhost:5080/health/ready | Readiness of PostgreSQL and Redis |
| http://localhost:8081 | Metro (Expo dev server) |

Stop the containers with `docker compose stop`. Your data stays in the `postgres-data` volume.

## Running on your phone (Expo Go)

The phone and the computer must be on the **same Wi-Fi network**.

- **API address.** You don't need to configure anything. In development, the app calls the API on the
  same computer that Expo Go loaded the app from, on port 5080. To point it somewhere else, create
  `mobile/.env.local` with `EXPO_PUBLIC_API_URL=http://<ip>:5080`.
- **Why the API binds to `0.0.0.0`.** The phone can't reach `localhost` on your computer, so the `http`
  launch profile listens on all interfaces. This is for development only.
- **Windows Firewall.** The first time the API and Node start, Windows asks whether to allow access, and
  you should allow both. If the phone still can't connect, check that inbound rules exist for
  "RentApp.Api" and "Node.js JavaScript Runtime" on your active network profile. Public Wi-Fi uses the
  **Public** profile.
- **The phone and computer are on different networks, or client isolation is on.** Use
  `npx expo start --tunnel`. The tunnel only carries Metro, though, so the API still needs a reachable
  address set in `EXPO_PUBLIC_API_URL`.
- **Plain HTTP.** This is accepted on the LAN in development only. Every non-local environment enforces
  HTTPS and HSTS.

The first screen, **System status**, shows the resolved API address and whether the API, PostgreSQL and
Redis are reachable. Pull down to refresh it.

## Tests

```powershell
dotnet test                                   # all backend tests (Docker must be running for integration tests)
dotnet test tests/RentApp.UnitTests           # unit tests only, no Docker needed
cd mobile; npm test; npm run typecheck; npm run lint
```

The integration tests start their own disposable PostgreSQL and Redis containers through Testcontainers.
They never touch your dev database.

## Low-memory tips (8 GB machines)

- Use a physical phone with Expo Go instead of an Android emulator.
- Close Swagger and browser tabs you aren't using, and stop the containers when you aren't working on
  the backend.

## Troubleshooting

| Symptom | Fix |
|---|---|
| API fails at startup with `ConnectionStrings:Database ... is required` | `.env` is missing or `DATABASE_CONNECTION_STRING` is empty |
| `/health/ready` shows postgres as Unhealthy | Check that `docker compose ps` shows the containers as healthy, and that the password matches in both places in `.env` |
| The phone shows "Unable to connect." | Check the same Wi-Fi, the firewall rules above, and that the API is running. Open `http://<pc-ip>:5080/health/ready` in the phone's browser. |
| `dotnet ef` says the build failed | Run `dotnet build` to see the errors. Stop the running API first, because it locks the build output. |
| Port 5432 or 6379 is already in use | Change `POSTGRES_PORT` or `REDIS_PORT` in `.env`, and update the connection strings to match. |

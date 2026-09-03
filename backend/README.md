# Depicta — Backend (.NET 8)

The API + background worker that ties the whole workflow together: scan → analyze (Vision AI)
→ review → apply → revert. Single-site (v1). SQLite for zero-setup local dev; PostgreSQL for
production (switch in `appsettings.json`).

## What's implemented (service layer)

| Piece | File |
|-------|------|
| Site connect/get/disconnect (encrypts credentials) | `Services/SiteService.cs` |
| Scan → upsert MediaItems → wake worker | `Services/ScanService.cs` |
| Review list, **approve→write**, reject, regenerate, revert | `Services/MediaService.cs` |
| Background analysis (only caller of Vision) | `Workers/AltAnalysisWorker.cs` |
| WordPress plugin client (Basic auth + **HMAC-signed**, HTTPS) | `Infrastructure/WordPress/WordPressRestClient.cs` |
| Credential encryption at rest (Data Protection, persisted keys) | `Security/DataProtectionCredentialProtector.cs` |
| **JWT admin auth** (login, PBKDF2 password, token service) | `Controllers/AuthController.cs`, `Security/Auth/*` |
| Claude Vision provider + alt-text validator | `Vision/*` |
| Enforced state machine | `Domain/StateMachine/MediaItemStateMachine.cs` |

## Security (Phase 1)

Every endpoint except `POST /api/auth/login` requires a valid **admin JWT** (enforced by a
fallback authorization policy — no controller can forget to opt in). Additional layers:

- **Passwords** are stored as PBKDF2-HMAC-SHA256 (210k iterations, per-hash salt), verified in
  constant time. The admin password is never in the codebase.
- **Rate limiting** — a per-IP global limiter (120 req/min) plus a strict login limiter
  (5 attempts/min) to throttle brute force.
- **Signed WordPress calls** — the backend never sends the plugin secret on the wire. Each
  request carries an HMAC-SHA256 signature over method + route + timestamp + one-time nonce +
  body hash, so a captured request can't be replayed (the plugin rejects reused nonces and
  stale timestamps within a 5-minute window).
- **Data Protection keys** are persisted to `DataProtection:KeyPath` so credential ciphertext
  stays decryptable across restarts.
- **Security headers** (`nosniff`, `DENY` framing, locked-down CSP for this JSON-only API) and
  **HTTPS redirection + HSTS** outside Development.
- **CORS** allows any `localhost` origin in dev and an explicit `Cors:AllowedOrigins`
  allow-list in production.

### Log in

```
POST /api/auth/login   { "username": "admin", "password": "..." }  ->  { token, expiresAt }
```

Send the token as `Authorization: Bearer <token>` on every other call. In Swagger, click
**Authorize** and paste the token.

**Dev credentials** (from `appsettings.Development.json`): `admin` / `depicta-dev-2026`.
Change them before doing anything real — generate a new hash and set it via user-secrets:

```bash
# print a PBKDF2 hash for a password (one-liner, no extra tooling)
dotnet run --project backend/Depicta.Api -- hash "your-strong-password"
dotnet user-secrets set "Auth:Admin:PasswordHash" "v1.210000....."
dotnet user-secrets set "Auth:SigningKey" "$(openssl rand -base64 48)"
```

## Run

```bash
cd backend/Depicta.Api
dotnet restore
dotnet run            # http://localhost:5080  · Swagger UI at /swagger
```

SQLite (`depicta.db`) is created automatically on first run. To use the Vision AI, set your
Anthropic key (don't commit it):

```bash
dotnet user-secrets init
dotnet user-secrets set "Vision:Claude:ApiKey" "sk-ant-..."
```

Without a key the app still runs; scanned items just stay `unprocessed` (the worker will log
the auth error and mark them `failed`, which you can retry once a key is set).

## Connect a WordPress site

Install the companion plugin (`../wordpress-plugin/depicta`), copy its **shared secret**
(Settings → Depicta) and create a WordPress **Application Password**. Then, in Swagger, call:

```
PUT /api/site
{
  "url": "https://your-site.com",
  "wpUsername": "your-wp-user",
  "appPassword": "xxxx xxxx xxxx xxxx",
  "pluginSecret": "the-secret-from-the-plugin"
}
```

Now `POST /api/scans` will pull images missing alt text, the worker generates proposals, and
the frontend (or Swagger) drives review/approve.

## Wire the frontend to this backend

```bash
cd ../../frontend
echo "VITE_API_BASE=http://localhost:5080" > .env.local
npm run dev
```

The dashboard now uses the real API instead of the mock — no code change (see
`src/api/httpClient.ts`).

## Build verification note

This repo's pure, correctness-critical logic — the MediaItem **state machine** (approval can
never be skipped) and the **alt-text sanitizer** (≤125 chars, no "image of" filler) — is
covered by a runnable check that passes 22/22. The EF Core / ASP.NET layer compiles against
standard .NET 8 APIs; run `dotnet build` in your environment to restore packages and produce
the binary (the authoring sandbox had no NuGet access).

## Database & migrations (Phase 2)

Local dev stays on **SQLite** and creates its schema automatically (`EnsureCreated()`) — no
migrations, no setup. **PostgreSQL** (production) uses **EF Core migrations**, applied
automatically on startup (`Database.Migrate()`).

Generate the migrations once (needs the EF tools and the Npgsql design-time factory that ships
in `Infrastructure/DepictaDbContextDesignFactory.cs`):

```bash
dotnet tool install --global dotnet-ef      # one-time
cd backend/Depicta.Api
dotnet ef migrations add InitialCreate       # writes Migrations/ (PostgreSQL)
```

Then point the app at Postgres and it applies them on boot:

```bash
set Database__Provider=postgres
set ConnectionStrings__Depicta=Host=localhost;Database=depicta;Username=postgres;Password=...
dotnet run
```

To apply migrations manually instead: `set DEPICTA_DESIGN_CONNECTION=...` then
`dotnet ef database update`.

## Tuning (Phase 2)

Vision + worker behaviour is config-driven (`appsettings.json`):

| Setting | Default | What it does |
|---------|---------|--------------|
| `Vision:Claude:MaxImageEdge` | 1568 | Long edge (px) sent to the model; larger images are downscaled first (smaller payload, fewer tokens). |
| `Vision:Claude:ImageQuality` | 82 | JPEG quality used when an image is re-encoded. |
| `Worker:MaxAttempts` | 3 | Tries per image before it's marked failed (transient errors only). |
| `Worker:RetryBaseMs` | 800 | Base for exponential backoff between retries (800 → 1600 → 3200 ms + jitter). |

Transient failures (network/timeout, HTTP 429/5xx) are retried with backoff and honour a
server `Retry-After`; permanent ones (bad image, invalid key, model not found) fail fast. A
failure on one image never blocks the rest of the batch.

## Tests & CI (Phase 3)

Tests live in `Depicta.Tests` (xUnit) and are wired into the `Depicta.sln` solution.

```bash
cd backend
dotnet test                     # runs unit + integration tests
```

- **Unit** — the state machine (approval can never be skipped; `Applied` only from `Approved`),
  the alt-text sanitizer, the PBKDF2 password hasher, and the HMAC `RequestSigner` (pinned to a
  known signature vector so the C# side can't drift from the PHP plugin).
- **Integration** — boots the real API in-memory (`WebApplicationFactory`) against a throwaway
  SQLite db and checks the auth boundary: `/api/media` is 401 without a token, login issues a
  token, bad credentials are rejected, and a valid token unlocks the endpoint.

**CI**: `.github/workflows/ci.yml` runs `dotnet test` (backend) and `npm run build` (frontend)
on every push and pull request.

## Production notes

- Provide secrets via environment variables (never appsettings): `Auth__SigningKey` (≥32 bytes),
  `Auth__Admin__PasswordHash`, `Vision__Claude__ApiKey`. The app refuses to start in Production
  without a real signing key.
- Set `Cors__AllowedOrigins__0=https://app.yourdomain.com` (the dev "any localhost" rule is off
  outside Development).
- Set `Database:Provider=postgres` and a real `ConnectionStrings:Depicta`; migrations apply on
  startup.
- Point `DataProtection:KeyPath` at durable, protected storage (or a KMS/key vault provider) so
  stored credentials remain decryptable across restarts and instances.

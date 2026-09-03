# Depicta

AI-generated alt text for WordPress — built human-in-the-loop and security-first.
Depicta finds images missing alt text, drafts a WCAG-compliant description with a Vision
model, and writes it back **only after the site owner approves it**, with every change
reversible.

## Status

Building in phases. **Phase 1 & 2 are in this repo.**

- [x] **Phase 1 — Architecture & data flow** → [`docs/01-architecture.md`](docs/01-architecture.md)
- [x] **Phase 2 — Data model & REST API spec** → [`docs/02-data-model-and-api.md`](docs/02-data-model-and-api.md)
- [x] **Phase 3 — React + Tailwind review UI** (full-site dashboard, side-by-side + bulk approve, working against a mock client) → [`frontend/`](frontend/README.md)
- [x] **Phase 4 — Vision system prompt** (≤125 chars, WCAG, no "image of" filler) → [`docs/04-vision-prompt.md`](docs/04-vision-prompt.md) + `backend/.../Vision/`
- [x] **Phase 5 — WordPress plugin PHP** (secure REST routes + revert-safe writes) → [`wordpress-plugin/depicta/`](wordpress-plugin/depicta/)
- [x] **Backend service layer & end-to-end wiring** — scan/analyze/apply/revert logic, background worker, WP client, credential encryption, live-API frontend toggle → [`backend/README.md`](backend/README.md)

## Decisions (v1)

- **Backend:** .NET 8 + EF Core (PostgreSQL by default)
- **Frontend:** React + Tailwind
- **WordPress:** lightweight companion plugin, two REST routes only
- **Vision:** provider-abstracted (`IVisionProvider`), Claude Vision as the default
- **Scope:** single site (multi-tenant is a later migration, not a rewrite)

## Layout

```
depicta/
├── docs/
│   ├── 01-architecture.md          # Phase 1: components, data flow, security
│   └── 02-data-model-and-api.md    # Phase 2: schema, state machine, REST contracts
├── backend/
│   └── Depicta.Api/                 # .NET 8 Web API
│       ├── Domain/Enums             # MediaItemState, ScanRunStatus
│       ├── Domain/Entities          # Site, MediaItem, AltProposal, AltRevision, ScanRun
│       ├── Domain/StateMachine      # enforced MediaItem transitions
│       ├── Infrastructure           # DepictaDbContext (EF config)
│       ├── Abstractions             # IVisionProvider, IWordPressClient
│       ├── Vision                   # ClaudeVisionProvider + AltTextSanitizer (Phase 4)
│       ├── Contracts                # request/response DTOs
│       └── Controllers              # REST contracts
├── frontend/                        # React + TS + Tailwind (Vite) — Phase 3
└── wordpress-plugin/depicta/        # PHP companion plugin — Phase 5
    ├── depicta.php
    └── includes/                    # REST controller + admin settings
```

## Run the backend (once Phase 3 fills in the service layer)

```bash
cd backend/Depicta.Api
dotnet restore
# set ConnectionStrings:Depicta (PostgreSQL) in appsettings.Development.json or env
dotnet ef migrations add InitialCreate
dotnet ef database update
dotnet run
```

The core security rule: the backend can never modify a site on its own — it can only call
the plugin's `set-alt` route, and only for an item the owner has explicitly approved.

## Deployment (Docker)

The whole stack — PostgreSQL, the .NET API, and the nginx-served React app — runs with one
command. You need Docker, and the initial EF migration generated once (PostgreSQL applies
migrations automatically on startup).

**1. Generate the migration** (one-time — see [`backend/README.md`](backend/README.md) → *Database & migrations*):

```bash
cd backend/Depicta.Api
dotnet ef migrations add InitialCreate
cd ../..
```

**2. Create your secrets file** (never committed — `.env` is gitignored):

```bash
cp .env.example .env
# fill in the values:
#   AUTH_SIGNING_KEY   ->  openssl rand -base64 48
#   ADMIN_PASSWORD_HASH -> dotnet run --project backend/Depicta.Api -- hash "your-password"
#   VISION_API_KEY, POSTGRES_PASSWORD
```

**3. Bring it up:**

```bash
docker compose up --build
```

App → `http://localhost:8081` · API + Swagger → `http://localhost:8080`

### Hosting

Every piece is a standard container, so it runs on any container host: the API + PostgreSQL on a
container platform with a free tier, and the static frontend on any static host — both give you an
`https` subdomain without buying a domain. The exact provider and steps are finalized at deploy
time (free-tier terms change), but nothing in the app is provider-specific.

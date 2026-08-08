# Deployment Guide — AI Brand Identity Studio

Phase 1 covers **local Docker Compose** and an **Azure** outline (App Service + SQL + Redis + Blob).

---

## 1. Prerequisites

| Tool | Local | Azure |
| --- | --- | --- |
| Docker Desktop | Yes | Optional (build) |
| .NET 10 SDK | For non-Docker API | Build pipeline |
| Node.js 20+ | For non-Docker web | Build pipeline |
| Azure CLI | Optional | Recommended |

Copy `.env.example` → `.env` and set secrets before starting.

---

## 2. Local — Docker Compose

### 2.1 Expected compose layout

File: `docker/docker-compose.yml` (services):

| Service | Image / build | Ports | Role |
| --- | --- | --- | --- |
| `sql` | `mcr.microsoft.com/mssql/server:2022-latest` | `1433` | SQL Server |
| `redis` | `redis:7-alpine` | `6379` | Cache / job status |
| `api` | build `backend` Dockerfile | `5080` | ASP.NET Core API + SignalR |
| `web` | build `frontend` Dockerfile | `3000` | Next.js 16 |
| `storage` | volume or minio (optional) | — | Local asset files |

### 2.2 Typical commands

```bash
# From repository root
cp .env.example .env

docker compose -f docker/docker-compose.yml up --build
```

- Web: http://localhost:3000  
- API / Swagger: http://localhost:5080/swagger  
- Health: http://localhost:5080/health  

```bash
# Tear down (keep volumes)
docker compose -f docker/docker-compose.yml down

# Tear down + wipe DB volume
docker compose -f docker/docker-compose.yml down -v
```

### 2.3 Environment wiring (compose)

Pass through from `.env`:

- `ConnectionStrings__DefaultConnection` → `sql` host  
- `ConnectionStrings__Redis` → `redis:6379`  
- `Jwt__Key`, `Jwt__Issuer`, `Jwt__Audience`  
- `Storage__Provider=Local`, `Storage__LocalPath=/app/storage`  
- `AI__*` keys (empty → Mock)  
- `Razorpay__*` (optional)  
- `NEXT_PUBLIC_API_URL=http://localhost:5080`

### 2.4 Database init

On API start (Development):

1. Wait for SQL healthy  
2. `Database.MigrateAsync()` applies EF migrations (startup seed calls this; do not use `EnsureCreated`)  
3. Seed roles + plans  

> Local SQLite: if you previously used `EnsureCreated`, delete `aibrandstudio.db` once so migrations can create a versioned schema.  

### 2.5 Run without Docker (dev)

```bash
# Terminal 1 — infra only
docker compose -f docker/docker-compose.yml up sql redis

# Terminal 2 — API
cd backend
dotnet run --project src/AiBrandStudio.Api

# Terminal 3 — Web
cd frontend
npm install
npm run dev
```

---

## 3. Azure outline

### 3.1 Resource map

| Resource | SKU guidance (MVP) | Purpose |
| --- | --- | --- |
| Resource Group | e.g. `rg-aibrand-dev` | Grouping |
| Azure App Service (API) | Linux B1/B2 or P0v3 | Host `AiBrandStudio.Api` |
| Azure App Service / Static Web / Container | Web on App Service or SWA + API separate | Next.js |
| Azure SQL Database | Serverless or S0 | Primary DB |
| Azure Cache for Redis | Basic C0 | Cache / SignalR scale-out backplane |
| Azure Blob Storage | Standard LRS | Logos, exports |
| Key Vault | Standard | JWT key, AI keys, Razorpay secrets |
| Application Insights | — | Traces / requests |

Optional later: Azure Container Apps or AKS using manifests under `k8s/`.

### 3.2 API App Service configuration

Application settings (examples):

```
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__DefaultConnection=<ADO.NET from Azure SQL>
ConnectionStrings__Redis=<Redis connection>
Jwt__Issuer=AiBrandStudio
Jwt__Audience=AiBrandStudio
Jwt__Key=<Key Vault reference>
Storage__Provider=AzureBlob
Storage__AzureBlobConnection=<Key Vault reference>
Storage__AzureBlobContainer=assets
AI__OpenAI__ApiKey=<Key Vault reference>
AI__Gemini__ApiKey=
AI__Ideogram__ApiKey=
AI__DefaultTextProvider=OpenAI
AI__DefaultImageProvider=Ideogram
Razorpay__KeyId=
Razorpay__KeySecret=
Razorpay__WebhookSecret=
Credits__SignupBonus=50
Credits__BrandGenerate=5
Credits__LogoGenerate=8
Credits__PaletteGenerate=2
Cors__Origins=https://<web-host>
```

### 3.3 Web (Next.js)

- Set `NEXT_PUBLIC_API_URL` to the public API HTTPS URL at **build time**.  
- Enable WebSockets on App Service if hosting SignalR on same API host (required).  
- For sticky sessions: prefer Redis SignalR backplane so multiple API instances work without affinity.

### 3.4 SQL

1. Create Azure SQL logical server + database `AiBrandStudio`.  
2. Allow App Service outbound IPs / use VNet integration.  
3. Run EF migrations from CI or a one-off Job:  
   `dotnet ef database update --connection "$CONN"`  
4. Disable public access if using private endpoint (recommended for prod).

### 3.5 Redis

- Enable SSL.  
- Use as: plan cache, rate-limit counters, SignalR backplane (`AddStackExchangeRedis`).

### 3.6 Blob Storage

- Container `assets` (private).  
- API issues short-lived SAS URLs for downloads.  
- Lifecycle rule: delete incomplete multipart / old temp exports after N days.

### 3.7 Networking & TLS

- HTTPS only; HTTP → HTTPS redirect.  
- Custom domains on web + api.  
- WAF (Application Gateway / Front Door) optional for Phase 1.5.

### 3.8 Deployment flow (manual sketch)

```text
GitHub Actions build
  → restore/test
  → publish API zip / container
  → build Next.js
  → azure/webapps-deploy (API)
  → deploy web artifact
  → (gated) ef migrations
```

Details: see `cicd.md`.

---

## 4. SignalR on Azure

| Concern | Approach |
| --- | --- |
| WebSockets | On in App Service config |
| Scale-out | Redis backplane |
| CORS | Exact frontend origin |
| Sticky sessions | Not required with Redis backplane |

Hub path remains `/hubs/generation`.

---

## 5. Operational checklist

- [ ] Secrets only in Key Vault / App Settings — not in git  
- [ ] SQL backups / PITR enabled  
- [ ] Blob soft-delete enabled  
- [ ] Health check alerts on `/health/ready`  
- [ ] Razorpay webhook URL: `https://<api>/api/payments/razorpay/webhook`  
- [ ] Admin user created out-of-band; MFA via IdP later  

---

## 6. Rollback

1. Redeploy previous App Service deployment slot (use staging slots).  
2. DB migrations must be backward-compatible; avoid destructive column drops in the same release as API that still needs them.  
3. Keep prior container image tags immutable.

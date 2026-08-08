# CI/CD Notes — AI Brand Identity Studio

GitHub Actions guidance for Phase 1: build, test, and deploy notes for the monorepo (`backend`, `frontend`, `docker`, `docs`).

---

## 1. Goals

| Goal | Approach |
| --- | --- |
| Every PR is buildable | Restore, compile, lint, unit/integration tests |
| Deterministic CI | Mock AI providers; no live Razorpay/OpenAI calls |
| Safe deploys | Build artifacts → deploy to Azure slots / environments with secrets from GitHub Environments |
| Fast feedback | Path filters so docs-only PRs skip heavy jobs when desired |

---

## 2. Suggested workflow layout

```text
.github/workflows/
  ci.yml              # PR + push: build & test
  e2e.yml             # PR labeled / main: Playwright smoke
  deploy-api.yml      # main / tags → Azure App Service API
  deploy-web.yml      # main / tags → Web host
```

Phase 1 may start with a single `ci.yml` and manual Azure deploy until pipelines harden.

---

## 3. CI pipeline (`ci.yml`) — recommended jobs

### 3.1 Backend

```yaml
# Pseudocode structure — adapt to repo paths
jobs:
  backend:
    runs-on: ubuntu-latest
    services:
      sqlserver:
        # or use Testcontainers inside tests
      redis:
        image: redis:7
        ports: ["6379:6379"]
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "10.0.x"
      - name: Restore
        run: dotnet restore backend/AiBrandStudio.sln
      - name: Build
        run: dotnet build backend/AiBrandStudio.sln -c Release --no-restore
      - name: Test
        run: dotnet test backend/AiBrandStudio.sln -c Release --no-build --logger trx
        env:
          ConnectionStrings__DefaultConnection: ${{ secrets.CI_SQL_CONNECTION }}
          ConnectionStrings__Redis: localhost:6379
          Jwt__Key: CI_ONLY_TEST_KEY_AT_LEAST_32_CHARS_LONG!!
          AI__DefaultTextProvider: Mock
          AI__DefaultImageProvider: Mock
          Storage__Provider: Local
          Storage__LocalPath: ${{ runner.temp }}/storage
```

**Notes:**

- Prefer **Testcontainers** for SQL so `services:` stay optional.  
- Upload TRX / coverage as artifacts.  
- Fail job on any non-zero test exit code.

### 3.2 Frontend

```yaml
  frontend:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with:
          node-version: "20"
          cache: npm
          cache-dependency-path: frontend/package-lock.json
      - working-directory: frontend
        run: npm ci
      - run: npm run lint
        working-directory: frontend
      - run: npm run test --if-present
        working-directory: frontend
      - run: npm run build
        working-directory: frontend
        env:
          NEXT_PUBLIC_API_URL: http://localhost:5080
```

### 3.3 Docker (optional job)

Build compose images to ensure Dockerfiles stay healthy:

```bash
docker compose -f docker/docker-compose.yml build
```

Skip `up` on every PR if runners lack resources; run on `main` nightly instead.

---

## 4. E2E notes

| Item | Recommendation |
| --- | --- |
| Tool | Playwright |
| Trigger | `main` pushes + `workflow_dispatch` + PRs with label `e2e` |
| Stack | compose up API+web+sql+redis **or** Playwright webServer hooks |
| AI | Mock providers only |
| Artifacts | Trace ZIP + screenshots on failure |
| Scope on PR | Smoke: register + wizard happy path |
| Scope nightly | Full suite in `testing-strategy.md` |

Set secrets for seeded admin only in the e2e environment; never use production keys.

---

## 5. Deploy — API

### 5.1 Build artifact

```bash
dotnet publish backend/src/AiBrandStudio.Api/AiBrandStudio.Api.csproj \
  -c Release \
  -o ./publish/api
```

Or build/push a container image to Azure Container Registry and deploy that image to App Service / Container Apps.

### 5.2 GitHub Action sketch

```yaml
# After CI success on main
- uses: azure/login@v2
  with:
    client-id: ${{ secrets.AZURE_CLIENT_ID }}
    tenant-id: ${{ secrets.AZURE_TENANT_ID }}
    subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}
- uses: azure/webapps-deploy@v3
  with:
    app-name: app-aibrand-api
    package: ./publish/api
```

Prefer **OIDC federated credentials** over long-lived Azure passwords.

### 5.3 Migrations

- Run EF migrations as a **gated** step against the target slot connection string.  
- Or use a dedicated migration job before switching traffic.  
- Avoid auto-migrate on Production startup unless explicitly approved.

```bash
dotnet ef database update \
  --project backend/src/AiBrandStudio.Infrastructure \
  --startup-project backend/src/AiBrandStudio.Api \
  --connection "$AZURE_SQL_CONNECTION"
```

---

## 6. Deploy — Web (Next.js)

```bash
cd frontend
npm ci
npm run build
```

Deploy `.next` standalone output (if `output: 'standalone'`) or use Azure Static Web Apps / App Service Node host.

**Important:** `NEXT_PUBLIC_API_URL` is baked in at build time — set it per environment (dev/staging/prod) in the workflow `env:`.

---

## 7. Secrets & variables

| Name | Scope | Purpose |
| --- | --- | --- |
| `Jwt__Key` | Azure App Settings | Not required in GitHub if only in Azure |
| `CI_SQL_CONNECTION` | GitHub env `ci` | Integration tests |
| `AZURE_CLIENT_ID` / `TENANT` / `SUBSCRIPTION` | GitHub env `prod` | OIDC deploy |
| `RAZORPAY_*` | Azure only | Never in frontend |
| `AI__*__ApiKey` | Azure Key Vault refs | Never log |

Enable secret scanning / push protection on the repository.

---

## 8. Branch policy

| Rule | Setting |
| --- | --- |
| Default branch | `main` |
| PR required | Yes |
| Required checks | `backend`, `frontend` |
| Dismiss stale reviews | Yes |
| Restrict force push | Yes on `main` |

---

## 9. Caching

- `actions/setup-dotnet` + NuGet cache  
- `actions/setup-node` npm cache  
- Docker layer cache (if building images on CI)

---

## 10. Versioning & releases

- Tag releases `v0.1.0`, `v0.2.0` for Phase 1 milestones.  
- Changelog: keep user-facing notes in GitHub Releases.  
- Image tags: `aibrand-api:<git-sha>` immutable.

---

## 11. Failure playbook

| Failure | Action |
| --- | --- |
| Flaky integration (SQL not ready) | Add retry/wait-for-healthy; use Testcontainers |
| Frontend build fails on env | Ensure `NEXT_PUBLIC_*` set in job |
| Deploy succeeded, app unhealthy | Check `/health/ready`; slot swap back |
| Migration failed | Do not swap slots; fix forward migration |

---

## 12. Docs-only changes

Optional path filter:

```yaml
on:
  pull_request:
    paths-ignore:
      - "docs/**"
      - "**/*.md"
```

Or a lightweight `docs` job that only checks links. Prefer not skipping backend tests when `backend/**` changes.

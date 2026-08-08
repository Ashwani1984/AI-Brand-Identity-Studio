# Staging provision — Azure + GitHub secrets

## Goal

Create `rg-aibrand-stg` (SQL, Blob, App Insights, API + Web App Service; Redis optional) and set GitHub Actions secrets for `deploy-staging.yml`.

## Prerequisites

1. **Azure subscription** with rights to create resource groups (Owner/Contributor).  
2. Azure CLI (`az`) — `winget install Microsoft.AzureCLI`  
3. GitHub CLI (`gh`) authenticated to `Ashwani1984/AI-Brand-Identity-Studio`  
4. Permission to create app registrations / service principals in the Entra tenant  

## One-shot provision

```powershell
az login
az account set --subscription "c2bc0d95-b639-4aa5-a67c-5e67a7f0e6bf"
.\scripts\provision-staging.ps1 -Location centralindia -ResourceGroup rg-aibrand-stg -AppServiceSku B1
```

Notes:

- Use **subscription ID** (not tenant ID). Tenant `40eca41b-...` is the directory; subscription is `c2bc0d95-...`.
- Default region is `centralindia` (some regions restrict SQL / App Service quota on new subs).
- Redis is **off by default** (classic Cache for Redis is retired for new creates; Managed Redis is optional via `-DeployRedis`).
- App Service defaults to **B1** (Basic). F1 free hits daily quota with two apps and blocks deploys.

The script will:

1. Create resource group + deploy `infra/main.bicep`  
2. Create a Contributor SP scoped to that RG (`--sdk-auth`)  
3. Set GitHub secrets (repo + `staging` environment):

| Secret | Purpose |
| --- | --- |
| `AZURE_CREDENTIALS` | SP JSON for `azure/login` |
| `AZURE_API_APP_NAME` | API App Service name |
| `AZURE_WEB_APP_NAME` | Web App Service name |
| `STAGING_API_URL` | `https://<api>.azurewebsites.net` (smoke) |
| `NEXT_PUBLIC_API_URL` | Same URL for Next build |
| `STAGING_SQL_ADMIN_PASSWORD` | Ops recovery (not used by workflow yet) |

4. Set repo variable `ENABLE_STAGING_DEPLOY=true`  
5. Create GitHub Environment `staging`

## After provision

```powershell
gh workflow run deploy-staging.yml --repo Ashwani1984/AI-Brand-Identity-Studio
gh run watch
```

Optional: set `NEXT_PUBLIC_SENTRY_DSN` / `SENTRY_AUTH_TOKEN` in repo secrets when Sentry is ready.

## Related

- `infra/main.bicep`  
- `scripts/provision-staging.ps1`  
- `.github/workflows/deploy-staging.yml`  
- [deployment.md](deployment.md)

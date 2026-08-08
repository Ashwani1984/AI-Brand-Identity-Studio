# Provision Azure staging + wire GitHub Actions secrets/vars.
# Prerequisites: az login, gh auth login, Contributor on subscription.
param(
  [string]$SubscriptionId = "",
  [string]$Location = "centralindia",
  [string]$ResourceGroup = "rg-aibrand-stg",
  [string]$GithubRepo = "Ashwani1984/AI-Brand-Identity-Studio",
  [string]$EnvironmentName = "staging",
  [string]$AppServiceSku = "B1",
  [bool]$DeployRedis = $false
)

$ErrorActionPreference = "Stop"

function Require-Cmd($name) {
  if (-not (Get-Command $name -ErrorAction SilentlyContinue)) {
    throw "$name is required on PATH"
  }
}

Require-Cmd az
Require-Cmd gh

Write-Host "==> Azure account"
az account show -o table
if ($SubscriptionId) {
  az account set --subscription $SubscriptionId
}
$acct = az account show | ConvertFrom-Json
$subId = $acct.id
$tenantId = $acct.tenantId
Write-Host "Subscription: $($acct.name) ($subId)"

Write-Host "==> Resource group $ResourceGroup ($Location)"
az group create --name $ResourceGroup --location $Location -o none

# Generate secrets (not printed in full)
$sqlPassword = -join ((48..57 + 65..90 + 97..122 | Get-Random -Count 24 | ForEach-Object { [char]$_ })) + "Aa1!"
$jwtKey = -join ((48..57 + 65..90 + 97..122 | Get-Random -Count 48 | ForEach-Object { [char]$_ }))

$paramsFile = Join-Path $env:TEMP "aibrand-staging.params.json"
@{
  '$schema'        = 'https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#'
  contentVersion   = '1.0.0.0'
  parameters       = @{
    environmentName  = @{ value = $EnvironmentName }
    location         = @{ value = $Location }
    sqlAdminPassword = @{ value = $sqlPassword }
    jwtKey           = @{ value = $jwtKey }
    appServiceSku    = @{ value = $AppServiceSku }
    deployRedis      = @{ value = $DeployRedis }
  }
} | ConvertTo-Json -Depth 6 | Set-Content -Path $paramsFile -Encoding ascii

Write-Host "==> Deploy Bicep (this can take 10-20 minutes for Redis/SQL)"
$deployName = "aibrand-staging-$(Get-Date -Format 'yyyyMMddHHmmss')"
az deployment group create `
  --name $deployName `
  --resource-group $ResourceGroup `
  --template-file "$PSScriptRoot\..\infra\main.bicep" `
  --parameters "@$paramsFile"
if ($LASTEXITCODE -ne 0) { throw "Bicep deployment failed (exit $LASTEXITCODE)" }

$outs = az deployment group show -g $ResourceGroup -n $deployName --query properties.outputs -o json | ConvertFrom-Json
if (-not $outs.apiAppName.value) { throw "Deployment outputs missing apiAppName" }
$apiAppName = $outs.apiAppName.value
$webAppName = $outs.webAppName.value
$apiUrl = $outs.apiUrl.value
$webUrl = $outs.webUrl.value

Write-Host "API: $apiUrl"
Write-Host "WEB: $webUrl"
Write-Host "API app: $apiAppName"
Write-Host "WEB app: $webAppName"

Write-Host "==> Service principal for GitHub Actions"
$spName = "sp-aibrand-gha-$EnvironmentName"
# Recreate if exists
$existing = az ad sp list --display-name $spName --query "[0].appId" -o tsv 2>$null
if ($existing) {
  Write-Host "Removing existing SP $spName ($existing)"
  az ad sp delete --id $existing 2>$null
}

$spJson = az ad sp create-for-rbac `
  --name $spName `
  --role contributor `
  --scopes "/subscriptions/$subId/resourceGroups/$ResourceGroup" `
  --sdk-auth `
  -o json

Write-Host "==> GitHub environment + secrets ($GithubRepo)"
gh api -X PUT "repos/$GithubRepo/environments/staging" | Out-Null

function Set-Secret([string]$name, [string]$value) {
  $value | gh secret set $name --repo $GithubRepo --env staging
  # Also set at repo level for workflows that do not use environment (compat)
  $value | gh secret set $name --repo $GithubRepo
}

Set-Secret "AZURE_CREDENTIALS" $spJson
Set-Secret "AZURE_API_APP_NAME" $apiAppName
Set-Secret "AZURE_WEB_APP_NAME" $webAppName
Set-Secret "STAGING_API_URL" $apiUrl
Set-Secret "NEXT_PUBLIC_API_URL" $apiUrl

# Optional Sentry: set NEXT_PUBLIC_SENTRY_DSN / SENTRY_AUTH_TOKEN later in repo settings.

Write-Host "==> Enable automatic staging deploy on push"
gh variable set ENABLE_STAGING_DEPLOY --repo $GithubRepo --body "true"

# Store SQL password as secret for ops (not used by workflow yet)
Set-Secret "STAGING_SQL_ADMIN_PASSWORD" $sqlPassword

Remove-Item $paramsFile -Force -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "STAGING_PROVISION_PASS"
Write-Host "  Resource group: $ResourceGroup"
Write-Host "  API URL:        $apiUrl"
Write-Host "  Web URL:        $webUrl"
Write-Host "  Next:           gh workflow run deploy-staging.yml --repo $GithubRepo"
Write-Host "  Note:           First web deploy may need Next standalone/output tuning on App Service."

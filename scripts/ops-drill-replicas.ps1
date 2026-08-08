# Ops drill: 2 API replicas + Redis backplane generation notify
$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
Set-Location $Root

Write-Host "==> Starting sql + redis + api-a + api-b"
docker compose -f docker/docker-compose.replicas.yml up -d --build

Write-Host "==> Waiting for health"
$ready = $false
for ($i = 1; $i -le 90; $i++) {
  try {
    Invoke-WebRequest -Uri "http://127.0.0.1:5081/health" -UseBasicParsing | Out-Null
    Invoke-WebRequest -Uri "http://127.0.0.1:5082/health" -UseBasicParsing | Out-Null
    $ready = $true
    break
  } catch {
    Start-Sleep -Seconds 2
  }
}
if (-not $ready) {
  docker compose -f docker/docker-compose.replicas.yml ps
  throw "Replicas did not become healthy in time"
}

Write-Host "==> Cross-replica SignalR notify drill"
$env:BASE_A = "http://127.0.0.1:5081"
$env:BASE_B = "http://127.0.0.1:5082"
node "$Root\scripts\ops-drill-replicas.mjs"

Write-Host "==> Drill complete"

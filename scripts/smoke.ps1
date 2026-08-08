# End-to-end API smoke test (PowerShell)
# Requires API running at http://localhost:5080 (override with $env:BASE)

$ErrorActionPreference = "Stop"
$base = if ($env:BASE) { $env:BASE } else { "http://localhost:5080" }
$email = "smoke_$(Get-Random)@example.com"

Write-Host "Health check..."
Invoke-RestMethod -Uri "$base/health" | Out-Null

Write-Host "Register $email"
$reg = Invoke-RestMethod -Uri "$base/api/auth/register" -Method POST -ContentType "application/json" -Body (@{
  email = $email; password = "SmokeTest!123"; fullName = "Smoke Tester"
} | ConvertTo-Json)
$headers = @{ Authorization = "Bearer $($reg.accessToken)" }

$brand = Invoke-RestMethod -Uri "$base/api/brands/wizard" -Method POST -Headers $headers -ContentType "application/json" -Body (@{
  businessName = "NovaCore Labs"; industry = "SaaS"; style = "Minimal"
} | ConvertTo-Json)

Invoke-RestMethod -Uri "$base/api/brands/$($brand.id)/generate" -Method POST -Headers $headers | Out-Null

function Wait-Job([object]$job) {
  $id = $job.id
  for ($i = 0; $i -lt 60; $i++) {
    $status = Invoke-RestMethod -Uri "$base/api/jobs/$id" -Headers $headers
    if ($status.status -eq "Completed") { return $status }
    if ($status.status -eq "Failed") { throw "Job failed: $($status.error)" }
    Start-Sleep -Milliseconds 500
  }
  throw "Job timed out: $id"
}

$logoJob = Invoke-RestMethod -Uri "$base/api/logos/generate" -Method POST -Headers $headers -ContentType "application/json" -Body (@{
  brandId = $brand.id; style = "Minimal"
} | ConvertTo-Json)
Wait-Job $logoJob | Out-Null

Invoke-RestMethod -Uri "$base/api/palettes/generate" -Method POST -Headers $headers -ContentType "application/json" -Body (@{
  brandId = $brand.id
} | ConvertTo-Json) | Out-Null

$zipJob = Invoke-RestMethod -Uri "$base/api/exports/zip" -Method POST -Headers $headers -ContentType "application/json" -Body (@{
  projectId = $brand.projectId
} | ConvertTo-Json)
$zip = Wait-Job $zipJob
Write-Host "SMOKE_PASS job=$($zip.id) status=$($zip.status)"

#!/usr/bin/env bash
# API smoke test — requires API at $BASE (default http://localhost:5080)
set -euo pipefail

BASE="${BASE:-http://localhost:5080}"
EMAIL="smoke_$(date +%s)_$RANDOM@example.com"

echo "Health check..."
curl -fsS "$BASE/health" >/dev/null

echo "Register $EMAIL"
REG=$(curl -fsS -X POST "$BASE/api/auth/register" \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"$EMAIL\",\"password\":\"SmokeTest!123\",\"fullName\":\"Smoke Tester\"}")
TOKEN=$(echo "$REG" | python3 -c "import sys,json; print(json.load(sys.stdin)['accessToken'])")
AUTH="Authorization: Bearer $TOKEN"

echo "Wizard brand"
BRAND=$(curl -fsS -X POST "$BASE/api/brands/wizard" -H "$AUTH" -H "Content-Type: application/json" \
  -d '{"businessName":"NovaCore Labs","industry":"SaaS","style":"Minimal"}')
BRAND_ID=$(echo "$BRAND" | python3 -c "import sys,json; print(json.load(sys.stdin)['id'])")
PROJECT_ID=$(echo "$BRAND" | python3 -c "import sys,json; print(json.load(sys.stdin)['projectId'])")

echo "Generate brand"
curl -fsS -X POST "$BASE/api/brands/$BRAND_ID/generate" -H "$AUTH" >/dev/null

wait_job() {
  local job_json="$1"
  local job_id
  job_id=$(echo "$job_json" | python3 -c "import sys,json; print(json.load(sys.stdin)['id'])")
  for _ in $(seq 1 60); do
    STATUS_JSON=$(curl -fsS "$BASE/api/jobs/$job_id" -H "$AUTH")
    STATUS=$(echo "$STATUS_JSON" | python3 -c "import sys,json; print(json.load(sys.stdin)['status'])")
    if [ "$STATUS" = "Completed" ]; then
      echo "$STATUS_JSON"
      return 0
    fi
    if [ "$STATUS" = "Failed" ]; then
      echo "$STATUS_JSON" >&2
      return 1
    fi
    sleep 0.5
  done
  echo "Job timed out: $job_id" >&2
  return 1
}

echo "Logo job"
LOGO_JOB=$(curl -fsS -X POST "$BASE/api/logos/generate" -H "$AUTH" -H "Content-Type: application/json" \
  -d "{\"brandId\":\"$BRAND_ID\",\"style\":\"Minimal\"}")
wait_job "$LOGO_JOB" >/dev/null

echo "Palette"
curl -fsS -X POST "$BASE/api/palettes/generate" -H "$AUTH" -H "Content-Type: application/json" \
  -d "{\"brandId\":\"$BRAND_ID\"}" >/dev/null

echo "Export ZIP job"
ZIP_JOB=$(curl -fsS -X POST "$BASE/api/exports/zip" -H "$AUTH" -H "Content-Type: application/json" \
  -d "{\"projectId\":\"$PROJECT_ID\"}")
ZIP_DONE=$(wait_job "$ZIP_JOB")
echo "SMOKE_PASS export=$(echo "$ZIP_DONE" | python3 -c "import sys,json; r=json.load(sys.stdin); print((r.get('resultJson') or '')[:80])")"

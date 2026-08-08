#!/usr/bin/env bash
# Ops drill: 2 API replicas + Redis backplane generation notify
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

echo "==> Starting sql + redis + api-a + api-b"
docker compose -f docker/docker-compose.replicas.yml up -d --build

echo "==> Waiting for health"
for i in $(seq 1 90); do
  if curl -fsS http://127.0.0.1:5081/health >/dev/null 2>&1 && curl -fsS http://127.0.0.1:5082/health >/dev/null 2>&1; then
    break
  fi
  sleep 2
  if [ "$i" -eq 90 ]; then
    echo "Replicas did not become healthy in time" >&2
    docker compose -f docker/docker-compose.replicas.yml ps >&2
    exit 1
  fi
done

echo "==> Cross-replica SignalR notify drill"
BASE_A=http://127.0.0.1:5081 BASE_B=http://127.0.0.1:5082 node scripts/ops-drill-replicas.mjs

echo "==> Drill complete"

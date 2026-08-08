# Ops drill — 2 API replicas + Redis SignalR backplane

**Exit gate (Phase 2):** Load test / ops drill — 2 API replicas + Redis backplane pass smoke generation notify.

## What it proves

1. Two API processes share SQL + Redis.  
2. A logo job is **enqueued and processed on replica B**.  
3. The client’s SignalR connection is on **replica A**.  
4. `generationProgress` (`started` / `completed`) arrives on A via the **Redis backplane**.

Jobs use an in-process channel worker, so enqueue must hit the same instance that should run the job (here: B). Notifications are fan-out over Redis — that is the multi-instance contract.

## Prerequisites

- Docker Desktop  
- Node 20+ (uses `frontend`’s `@microsoft/signalr`)  
- Ports **5081**, **5082**, **1433**, **6379** free  

## Run

```powershell
# from repo root
.\scripts\ops-drill-replicas.ps1
```

```bash
chmod +x scripts/ops-drill-replicas.sh
./scripts/ops-drill-replicas.sh
```

Or manually:

```bash
docker compose -f docker/docker-compose.replicas.yml up -d --build
BASE_A=http://127.0.0.1:5081 BASE_B=http://127.0.0.1:5082 node scripts/ops-drill-replicas.mjs
```

## Pass criteria

Script prints:

```text
OPS_DRILL_PASS replicas=2 redis_backplane=ok generation_notify=ok
```

Fail if no `generationProgress` notify is received on A after the B-side job completes.

## Staging / Azure variant

1. Scale App Service (or container app) to **2 instances**.  
2. Confirm `ConnectionStrings__Redis` is set (SignalR `AddStackExchangeRedis`).  
3. Point `BASE_A` / `BASE_B` at two instance-specific URLs if available, or run the Node drill twice against the public hostname while watching instance logs for `generationProgress` publish + client receipt.  
4. Prefer slot-specific smoke after scale-out before promote.

## Teardown

```bash
docker compose -f docker/docker-compose.replicas.yml down
```

## Related

- [ops-runbook.md](ops-runbook.md)  
- [deployment.md](deployment.md) § SignalR / Redis  
- `docker/docker-compose.replicas.yml`  
- `scripts/ops-drill-replicas.mjs`

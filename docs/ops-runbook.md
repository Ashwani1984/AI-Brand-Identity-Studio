# Operations runbook — AI Brand Identity Studio

## On-call quick links

| Signal | Check |
| --- | --- |
| API down | `GET /health` and `GET /health/ready` |
| Trace correlation | ProblemDetails `traceId` + App Insights / Sentry release |
| Payments | Razorpay dashboard + `POST /api/payments/razorpay/webhook` logs |
| Credits | `CreditLedgers` rows; admin grant path |
| Blob outage | `Storage:Provider` + Azure Storage metrics |

---

## Payment webhook

1. Confirm `Razorpay:WebhookSecret` is set in App Settings.  
2. Replay event from Razorpay dashboard if signature failed (400).  
3. Idempotency: paid payments skip double credit (`Status=Paid` / ledger reason `Payment:{id}`).  
4. If order missing: customer must recreate checkout; do not manually invent order ids.

## Credit ledger

1. Query user balance vs sum of ledger deltas.  
2. Failed AI jobs should refund (`Refund:*` reason).  
3. Admin goodwill: `POST /api/admin/users/{id}/credits` (when enabled) — record reason.

## Blob / storage outage

1. Confirm `Storage:Provider` (`Local` vs `AzureBlob`).  
2. Azure: check container `assets`, connection string, firewall.  
3. API still serves metadata; binary reads fail → generation jobs fail and refund.  
4. Failover: temporarily set `Storage:Provider=Local` only for emergency single-instance recovery (not multi-instance safe).

## Generation jobs

1. `GET /api/jobs/{id}` for status.  
2. Worker recovers `Running` → `Queued` on process start.  
3. Stuck Queued: check API logs / Redis SignalR (notifications only; jobs are DB-backed).

## Deploy rollback

1. Azure App Service → Deployment Center / previous slot swap.  
2. Prefer staging smoke (`scripts/smoke.sh`) before prod promote.  
3. Keep App Insights + Sentry release tags aligned with Git SHA / release tag.

## Multi-instance SignalR drill

See [ops-drill-replicas.md](ops-drill-replicas.md) (`scripts/ops-drill-replicas.ps1`).

## Accessibility baseline

See [qa-a11y.md](qa-a11y.md) (Lighthouse ≥ 90 on `/dashboard` + `/wizard`).

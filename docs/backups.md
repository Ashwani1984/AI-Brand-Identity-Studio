# Backups & recovery — AI Brand Identity Studio

## Targets

| Asset | RPO | RTO | Mechanism |
| --- | --- | --- | --- |
| Azure SQL | ≤ 1 hour | ≤ 4 hours | Point-in-time restore (Basic+) / LTR weekly |
| Blob (`assets`) | ≤ 24 hours | ≤ 8 hours | Soft delete + versioning; GRS optional |
| App config / secrets | n/a | ≤ 1 hour | Key Vault / App Settings export in IaC |
| Redis | best-effort | ≤ 1 hour | Cache rebuild; SignalR reconnect |

## Automated backups (Azure)

1. **SQL** — enable Long-term retention (weekly, 4 weeks) on `AiBrandStudio` database.  
2. **Blob** — enable soft delete (14 days) and versioning on the storage account created by `infra/main.bicep`.  
3. **Verify monthly** — restore SQL to a throwaway DB and download one known asset blob.

## Restore drill checklist

- [ ] Create restore target resource group `aibrand-restore-YYYYMM`  
- [ ] PITR SQL to restore server; update connection string on a staging slot  
- [ ] Restore / copy blob container to staging account  
- [ ] Run `BASE=<staging-api> ./scripts/smoke.sh`  
- [ ] Record time-to-recover in ops log  

## Local / Docker

```bash
# SQL volume wipe is destructive
docker compose -f docker/docker-compose.yml down -v

# SQLite local file
cp backend/aibrandstudio.db backend/aibrandstudio.db.bak
```

## Ownership

Platform on-call owns backup verification; Product owns customer communication for incidents exceeding RTO.

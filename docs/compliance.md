# Compliance controls — Phase 5

Operational checklist for data subject rights, retention, and DPA readiness.

## Account data export

Authenticated users can download a JSON package of their account data:

```http
GET /api/account/export
Authorization: Bearer <access_token>
```

Includes: profile, organizations/memberships, projects, brands, assets metadata, credit ledger (last 500), payments, subscription. File/blob binaries are referenced by metadata only; request asset downloads separately if needed.

Audit action: `account.data_exported`.

## Delete request path

```http
POST /api/account/delete-request
{ "confirmation": "DELETE" }
```

Effects:

1. Sets `Users.DeletionRequestedAtUtc`
2. Marks `IsActive = false` (login blocked)
3. Revokes refresh tokens
4. Writes audit `account.deletion_requested`

Hard erase of rows and blobs is an ops job after the retention window (`Users.DataRetentionDays`, default **365** on register; minimum configurable **30**). Until then, support can cancel a request by clearing `DeletionRequestedAtUtc` and reactivating the user in DB.

## Consent flags

```http
PUT /api/account/consent
{
  "consentMarketing": true,
  "acceptTerms": true,
  "dataRetentionDays": 365
}
```

| Field | Meaning |
| --- | --- |
| `ConsentMarketing` | Opt-in for product/marketing email |
| `TermsAcceptedAtUtc` | Timestamp of terms acceptance (set on register / when `acceptTerms`) |
| `DataRetentionDays` | Preferred retention (30–3650) before erasure after delete request |

## DPA checklist (processor / vendor)

Use this when signing a customer DPA or reviewing subprocessors:

- [ ] **Roles** — Document controller (customer) vs processor (AI Brand Identity Studio) boundaries
- [ ] **Subprocessors** — List Azure (SQL, Blob, Redis, App Insights), Razorpay, AI providers (OpenAI/Gemini/Ideogram when enabled), Sentry
- [ ] **Security** — TLS in transit; secrets in Key Vault / env; JWT auth; webhook HMAC; least-privilege admin role
- [ ] **Breach notice** — Commit to notify controller within contractual window (recommend ≤72h)
- [ ] **Data location** — Document Azure region for SQL/Blob (see `infra/main.bicep`)
- [ ] **Subject rights** — Export + delete-request APIs above; support SLA for hard erase
- [ ] **Retention** — Default 365 days post-delete-request unless shorter DPA term
- [ ] **Audit** — Immutable `AuditLogs` for admin credit grants, plan/payment changes, consent, deletion
- [ ] **AI training** — Confirm providers configured with no-training / enterprise terms where required
- [ ] **Return/delete** — On contract end: export available; purge per retention + ticket

## Related

- [ops-runbook.md](ops-runbook.md) — on-call
- [backups.md](backups.md) — RPO/RTO
- [admin-manual.md](admin-manual.md) — credit grants and audit

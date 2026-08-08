# Testing Strategy — AI Brand Identity Studio

Phase 1 MVP testing approach across unit, integration, end-to-end, and security.

---

## 1. Goals

| Goal | Measure |
| --- | --- |
| Prevent auth/credit regressions | Unit + integration coverage on ledger & JWT |
| Keep API contracts stable | Integration tests against TestServer / WebApplicationFactory |
| Protect critical UX paths | Playwright e2e: register → wizard → generate (mock) → export |
| Reduce security debt early | Authz, secret masking, webhook signature tests |

AI provider calls are **mocked by default** in CI (`AI__DefaultTextProvider=Mock`, `AI__DefaultImageProvider=Mock`) so pipelines stay deterministic and free.

---

## 2. Test pyramid

```text
        /\
       /e2e\          few — critical journeys
      /------\
     / integ  \       API + DB (+ Redis) contracts
    /----------\
   /    unit    \     domain, validators, credit math
  /--------------\
```

| Layer | Location | Tools |
| --- | --- | --- |
| Unit | `backend/tests/AiBrandStudio.Tests` | xUnit, FluentAssertions, Moq/NSubstitute |
| Integration | same or `*.IntegrationTests` | WebApplicationFactory, Testcontainers (SQL/Redis) optional |
| Frontend unit | `frontend` | Vitest / Jest + Testing Library (as adopted) |
| E2E | `frontend/e2e` or repo `/e2e` | Playwright |
| Security | tests + optional CI scanners | Authz tests, `dotnet` analyzers, secret scan |

---

## 3. Unit testing

### 3.1 Focus areas

| Area | Examples |
| --- | --- |
| Domain | Credit balance never negative; soft-delete flags |
| Application handlers | Command validation; debit-then-generate order; refund on failure |
| Validators | Register password rules; wizard required fields |
| Mapping | Palette → CSS / Tailwind export strings |
| Payment abstraction | Razorpay order DTO mapping without live HTTP |

### 3.2 Conventions

- Name: `Method_Scenario_Expected`  
- One assert theme per test  
- No real network; fake `ITextGenerator` / `IImageGenerator` / `IPaymentGateway`  
- Pure functions for export packaging helpers tested with in-memory streams

### 3.3 Sample cases

- `DebitCredits_WhenBalanceInsufficient_ThrowsInsufficientCredits`  
- `Signup_CreatesLedgerWithConfiguredBonus`  
- `PaletteExport_Css_ContainsRootVariables`  
- `MaskApiKey_ShowsOnlyLastFour`

---

## 4. Integration testing

### 4.1 Scope

Spin API with:

- SQL Server (LocalDB, Docker, or Testcontainers)  
- Redis (container or in-memory substitute if interface allows)  
- Local storage provider  

Use `WebApplicationFactory<Program>` to exercise real middleware (JWT, problem details).

### 4.2 Priority suites

| Suite | Assertions |
| --- | --- |
| Auth | Register → login → refresh → logout; revoked refresh rejected |
| Projects | CRUD ownership; other user gets 404/403 |
| Wizard | POST wizard creates brand; generate returns job id |
| Credits | Generate reduces balance; concurrent debit safe |
| Assets / Export | ZIP endpoint creates `Exports` row Ready with readable archive |
| Admin | User forbidden on `/api/admin/users`; admin OK; keys masked |
| Payments | Order endpoint 503 when keys missing; 200 shape when faked gateway |

### 4.3 SignalR

Integration test: start hub connection with test JWT, `SubscribeJob`, complete job in handler, assert `GenerationCompleted` received (or use channel timeout).

### 4.4 Data isolation

- Unique DB name / Respawn / transaction rollback per test class  
- Seed roles/plans once per fixture  

---

## 5. End-to-end (e2e)

### 5.1 Environment

- `docker compose` stack **or** API + web + SQL + Redis  
- Mock AI providers  
- Seeded Free plan  

### 5.2 Critical journeys

| ID | Journey |
| --- | --- |
| E2E-01 | Register → land on dashboard empty state |
| E2E-02 | Create project → complete wizard → see narrative result |
| E2E-03 | Generate logos (mock images) → favorite one |
| E2E-04 | Generate palette → export CSS download |
| E2E-05 | Asset library lists items → export ZIP downloads |
| E2E-06 | Login as admin → users list → grant credits → audit log entry visible |

### 5.3 Practices

- Use `data-testid` sparingly on primary CTAs  
- Prefer role/name selectors for a11y alignment  
- Record traces on failure in CI artifacts  
- Keep suite under ~10 minutes on CI runners  

---

## 6. Security testing

### 6.1 Automated in CI

| Check | How |
| --- | --- |
| Authn | Unauthenticated POST `/api/projects` → 401 |
| Authz | User token on `/api/admin/*` → 403 |
| IDOR | User A cannot GET User B’s `projectId` |
| Secrets | Admin API keys response never contains full key material |
| JWT | Tampered / expired token → 401 |
| Webhook | Invalid Razorpay signature → 400/401 when secret configured |
| Headers | Production security headers smoke (HTTPS redirect in Azure) |

### 6.2 Manual / periodic

- OWASP ZAP baseline against staging  
- Dependency audit: `dotnet list package --vulnerable`, `npm audit`  
- Review CORS allowlist  
- Confirm `.env` / keys not in git history  

### 6.3 Threat notes (Phase 1)

| Threat | Mitigation |
| --- | --- |
| Credit fraud via replay | Idempotent job ids; server-side debit |
| Prompt injection to admin tools | Admin UI separate; no executing model output as code |
| ZIP path traversal | Sanitize entry names when packaging |
| Webhook spoofing | HMAC verification |

---

## 7. Performance & resilience (lightweight)

Phase 1 is not a full load-test program. Smoke + ops drills:

- Health endpoints under compose  
- Concurrent 10 mock generations — no credit double-spend  
- Redis down → API ready probe fails; graceful error on cache miss paths  
- **2-replica SignalR notify drill** — [ops-drill-replicas.md](ops-drill-replicas.md)  

Accessibility gate (manual / helper): [qa-a11y.md](qa-a11y.md) — Lighthouse Accessibility ≥ 90 on `/dashboard` and `/wizard`. 

---

## 8. CI gating policy

| Gate | Required |
| --- | --- |
| Unit + integration (backend) | Yes — block merge |
| Frontend lint/typecheck | Yes |
| E2E smoke (subset E2E-01, E2E-02) | Yes on `main` / PR |
| Full e2e | Nightly or manual |
| Security scanner | Warn on PR; fail on high severity when tuned |

See `cicd.md` for workflow notes.

---

## 9. Definition of done (feature)

A Phase 1 feature is done when:

1. Unit tests cover domain/application rules touched  
2. Integration test covers the happy path + one authz/credit failure  
3. UI path covered by e2e **or** justified as admin-only with integration coverage  
4. Docs (`api.md` / manuals) updated if surface changed  

---

## 10. Test data

| Persona | Use |
| --- | --- |
| `user@test.local` | Standard User role |
| `admin@test.local` | Admin role |
| Fixed passwords | From CI secrets / user-secrets — not committed |

Prefer factory helpers (`TestUserFactory`) over brittle SQL inserts.

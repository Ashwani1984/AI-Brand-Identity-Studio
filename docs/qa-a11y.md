# Accessibility QA — Lighthouse baseline (manual)

**Exit gate (Phase 3):** Lighthouse a11y on dashboard + wizard passes agreed baseline (manual QA).

Aligned with SRS **NFR-A11Y-01..04** (WCAG 2.2 AA target for login, wizard, dashboard).

## Agreed baseline

| Page | Path | Lighthouse **Accessibility** | Notes |
| --- | --- | --- | --- |
| Dashboard | `/dashboard` | **≥ 90** | After sign-in |
| Wizard | `/wizard` | **≥ 90** | After sign-in; first step visible |
| Login (reference) | `/login` | **≥ 90** | Public; optional CI helper |

**Fail the gate if:**

- Accessibility category **&lt; 90**, or  
- Any **serious/critical** contrast or name/role/value audit remaining without an accepted exception logged below.

Best Practices / SEO / Performance are **out of scope** for this gate (track separately).

## Manual procedure (Chrome)

1. Start API + web (`docker compose` or local `dotnet` + `npm run dev`).  
2. Register or sign in; confirm cookie `abs_access` is set.  
3. Open **Dashboard** → DevTools → **Lighthouse** → Categories: **Accessibility** only → Desktop or Mobile (run **both** once per release).  
4. Record score in the results table below.  
5. Repeat on **Wizard**.  
6. Fix blockers (missing labels, contrast, focus order) or file exceptions with owner + ticket.

### Helper script (authenticated Chrome)

With frontend + API up:

```bash
cd frontend
npm run qa:a11y
```

This launches Chromium via Playwright, signs in, opens dashboard + wizard, and runs Lighthouse accessibility audits against the agreed thresholds (`scripts/lighthouse-a11y.mjs`). Treat a local pass as evidence for the manual gate; still spot-check in Chrome once per milestone.

## Results log

| Date | Env | Dashboard | Wizard | Tester | Notes |
| --- | --- | --- | --- | --- | --- |
| _yyyy-mm-dd_ | local / staging | _score_ | _score_ | _name_ | |

## Accepted exceptions

| Audit id | Page | Reason | Owner | Review by |
| --- | --- | --- | --- | --- |
| — | — | — | — | — |

## Related

- [SRS.md](SRS.md) §4.3 Accessibility  
- [testing-strategy.md](testing-strategy.md) §5.3  
- `frontend` script `qa:a11y` → `../scripts/lighthouse-a11y.mjs`

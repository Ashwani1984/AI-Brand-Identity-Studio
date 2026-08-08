# Software Requirement Specification (SRS)

**Product:** AI Brand Identity Studio  
**Phase:** 1 — MVP  
**Version:** 1.0  
**Status:** Approved for implementation  

---

## 1. Purpose

AI Brand Identity Studio is an enterprise SaaS platform that helps founders, marketers, and agencies generate a complete brand identity—name positioning, logo concepts, color palettes, typography pairings, and downloadable assets—without requiring design expertise.

Phase 1 delivers a production-shaped MVP: authenticated workspaces, a guided brand wizard, AI-backed generators (with mock fallback), an asset library, ZIP export, a Razorpay payment skeleton, and basic admin controls.

### 1.1 Goals

| Goal | Success measure |
| --- | --- |
| Time-to-first-brand | User can register → create project → finish wizard → see generated assets in under 10 minutes |
| Credit-safe AI usage | Every generation debits credits; insufficient balance blocks the call with a clear error |
| Provider resilience | When OpenAI / Gemini / Ideogram keys are missing, mock providers still return usable demo assets |
| Export readiness | Selected logos, palettes, and brand JSON pack into a single ZIP |
| Admin visibility | Admins can list users, adjust credits, view masked API key status, and read audit logs |

### 1.2 Out of scope (Phase 1)

- Live Stripe payments (Razorpay skeleton + `IPaymentGateway` only)
- Collaborative real-time editing of brand canvases
- White-label / multi-tenant custom domains
- Native mobile apps
- Full brand guideline PDF designer
- Marketplace / community sharing

---

## 2. Actors

| Actor | Description | Primary capabilities |
| --- | --- | --- |
| **Guest** | Unauthenticated visitor | View marketing/landing pages; register; login |
| **User** | Authenticated customer (default role) | Manage projects, run wizard & generators, manage assets, export ZIP, view subscription/plans |
| **Admin** | Internal operator (`Admin` role) | User management, credit adjustments, API key status (masked), audit logs, plan inspection |
| **System** | Background / integration processes | Token refresh, credit ledger writes, generation job progress via SignalR, Razorpay webhook handler (stub) |
| **AI Provider** | External services (OpenAI, Gemini, Ideogram, Mock) | Text/image generation invoked through application abstractions |

---

## 3. Functional requirements

### 3.1 Authentication (Auth)

| ID | Requirement | Priority |
| --- | --- | --- |
| FR-AUTH-01 | Guest can register with email, display name, and password (min length & complexity policy) | Must |
| FR-AUTH-02 | On successful registration, system creates `User`, assigns `User` role, creates credit ledger with signup bonus (`Credits__SignupBonus`) | Must |
| FR-AUTH-03 | User can login with email/password and receive JWT access token + refresh token (HTTP-only cookie or body per API contract) | Must |
| FR-AUTH-04 | Client can refresh access token using a valid refresh token | Must |
| FR-AUTH-05 | Logout invalidates the current refresh token (and optionally all sessions) | Must |
| FR-AUTH-06 | Passwords stored as salted hashes (ASP.NET Identity / PBKDF2 or Argon2 via Identity) | Must |
| FR-AUTH-07 | OAuth hooks for Google / Microsoft / GitHub are configured but may be disabled when client IDs are empty | Should |
| FR-AUTH-08 | Failed login attempts are rate-limited and audited | Should |

### 3.2 Dashboard

| ID | Requirement | Priority |
| --- | --- | --- |
| FR-DASH-01 | Authenticated user sees dashboard with recent projects, credit balance, and quick actions (New project, Continue wizard) | Must |
| FR-DASH-02 | Dashboard shows subscription plan name and usage summary (credits used / remaining for period when applicable) | Should |
| FR-DASH-03 | Empty state guides first-time users into Brand Wizard | Must |

### 3.3 Projects

| ID | Requirement | Priority |
| --- | --- | --- |
| FR-PROJ-01 | User can create a project with name and optional description | Must |
| FR-PROJ-02 | User can list, view, update, and soft-delete own projects | Must |
| FR-PROJ-03 | Project owns zero or more Brands; deleting a project archives related brands/assets | Must |
| FR-PROJ-04 | Users cannot access another user’s projects (ownership enforcement) | Must |

### 3.4 Brand Wizard

| ID | Requirement | Priority |
| --- | --- | --- |
| FR-WIZ-01 | Multi-step wizard captures: brand/business name, industry, audience, tone, competitors (optional), preferred styles, color preferences, logo style keywords | Must |
| FR-WIZ-02 | Wizard validates required fields before allowing “Generate brand” | Must |
| FR-WIZ-03 | Submitting wizard creates/updates a `Brand` record and enqueues/invokes brand generation | Must |
| FR-WIZ-04 | Progress of long-running generation is pushed via SignalR hub `/hubs/generation` | Must |
| FR-WIZ-05 | User can resume incomplete wizard drafts within a project | Should |

### 3.5 Logo generator

| ID | Requirement | Priority |
| --- | --- | --- |
| FR-LOGO-01 | User can generate N logo concepts for a brand (default N=4) using image provider (Ideogram / mock) | Must |
| FR-LOGO-02 | Generation costs `Credits__LogoGenerate` credits; insufficient credits → `402`/`400` with message | Must |
| FR-LOGO-03 | Each logo stores prompt used, provider name, storage URI, and thumbnail URI | Must |
| FR-LOGO-04 | User can mark a logo as primary / favorite | Should |

### 3.6 Palette generator

| ID | Requirement | Priority |
| --- | --- | --- |
| FR-PAL-01 | User can generate color palettes (primary, secondary, accent, neutrals) for a brand | Must |
| FR-PAL-02 | Generation costs `Credits__PaletteGenerate` credits | Must |
| FR-PAL-03 | Palettes can be exported as CSS variables, JSON, and Tailwind theme snippet | Must |
| FR-PAL-04 | Contrast notes for body text on background are suggested (WCAG awareness) | Should |

### 3.7 Typography generator

| ID | Requirement | Priority |
| --- | --- | --- |
| FR-TYPE-01 | User can request typography suggestions (display + body + mono optional) based on brand tone | Must |
| FR-TYPE-02 | Suggestions include Google Fonts (or equivalent) family names, weights, and usage guidance | Must |
| FR-TYPE-03 | Typography result is stored against the brand and included in export metadata | Must |

### 3.8 Brand (concept) generation

| ID | Requirement | Priority |
| --- | --- | --- |
| FR-BRAND-01 | System generates brand narrative: tagline options, positioning statement, voice guidelines, do/don’t list | Must |
| FR-BRAND-02 | Uses configured text provider (`AI__DefaultTextProvider`: OpenAI / Gemini / Mock) | Must |
| FR-BRAND-03 | Costs `Credits__BrandGenerate` credits; writes `Prompt` + `CreditLedger` entries | Must |

### 3.9 Asset Library

| ID | Requirement | Priority |
| --- | --- | --- |
| FR-ASSET-01 | All generated logos, palette files, typography JSON, and brand docs appear in Asset Library filtered by project/brand | Must |
| FR-ASSET-02 | User can rename, favorite, and soft-delete assets | Must |
| FR-ASSET-03 | Assets resolve via signed/local URLs from Local filesystem or Azure Blob | Must |

### 3.10 Export ZIP

| ID | Requirement | Priority |
| --- | --- | --- |
| FR-EXP-01 | User can select assets (or “export brand package”) and download a ZIP | Must |
| FR-EXP-02 | ZIP includes: logos (PNG/SVG if available), `palette.json` + `palette.css`, `typography.json`, `brand.json`, README | Must |
| FR-EXP-03 | Export job is recorded in `Exports` with status and storage URI | Must |

### 3.11 Subscriptions & Razorpay skeleton

| ID | Requirement | Priority |
| --- | --- | --- |
| FR-SUB-01 | System exposes subscription plans (Free / Pro / Agency) with credit allowances | Must |
| FR-SUB-02 | `IPaymentGateway` abstracts payments; Razorpay implements create-order | Must |
| FR-SUB-03 | `POST /api/payments/razorpay/order` creates order for plan upgrade (returns order id + key id) | Must |
| FR-SUB-04 | Webhook endpoint verifies signature when secret configured; otherwise logs stub acceptance in Development | Should |
| FR-SUB-05 | Successful payment creates `Payments` row and updates `Subscriptions` | Must (when webhook live) |

### 3.12 Admin

| ID | Requirement | Priority |
| --- | --- | --- |
| FR-ADM-01 | Admin can list/search users, view roles, credit balance, and subscription | Must |
| FR-ADM-02 | Admin can grant/revoke credits with reason (ledger entry + audit log) | Must |
| FR-ADM-03 | Admin can view AI provider configuration status with **masked** API keys | Must |
| FR-ADM-04 | Admin can browse audit logs (auth, credit, admin actions, exports) | Must |
| FR-ADM-05 | Non-admin callers receive `403` on `/api/admin/*` | Must |

---

## 4. Non-functional requirements

### 4.1 Security

| ID | Requirement |
| --- | --- |
| NFR-SEC-01 | All mutating APIs require JWT Bearer auth except register/login |
| NFR-SEC-02 | HTTPS in non-local environments; HSTS on Azure App Service |
| NFR-SEC-03 | Secrets via environment / Key Vault — never commit real keys |
| NFR-SEC-04 | Refresh tokens rotated; revoked tokens rejected |
| NFR-SEC-05 | Razorpay webhook signature verification when secret present |
| NFR-SEC-06 | Admin API key views always mask secrets (show last 4 only) |
| NFR-SEC-07 | Input validation on all DTOs; SQL via parameterized EF Core |
| NFR-SEC-08 | CORS limited to configured frontend origins |

### 4.2 Scalability & performance

| ID | Requirement |
| --- | --- |
| NFR-PERF-01 | API p95 (non-AI) &lt; 300 ms under MVP load |
| NFR-PERF-02 | Redis caches plans, session metadata, and generation job status |
| NFR-PERF-03 | Long AI calls are async with SignalR progress; HTTP may return `202` + job id |
| NFR-PERF-04 | Blob/local storage for binary assets; DB stores metadata only |
| NFR-PERF-05 | Horizontal scale of API (stateless) behind Azure App Service / container |

### 4.3 Accessibility (a11y)

| ID | Requirement |
| --- | --- |
| NFR-A11Y-01 | WCAG 2.2 AA target for core flows (login, wizard, dashboard) |
| NFR-A11Y-02 | Keyboard navigable wizard steps; visible focus rings |
| NFR-A11Y-03 | Form errors announced to assistive tech |
| NFR-A11Y-04 | Color contrast for text/UI chrome meets AA; palette suggestions note contrast |

### 4.4 Reliability & observability

| ID | Requirement |
| --- | --- |
| NFR-REL-01 | Structured logging (Serilog or built-in) with correlation id |
| NFR-REL-02 | Generation failures do not debit credits (or auto-refund ledger) |
| NFR-REL-03 | Health endpoints for API, SQL, Redis |
| NFR-REL-04 | Audit trail for privileged and credit-affecting actions |

### 4.5 Compliance / data

| ID | Requirement |
| --- | --- |
| NFR-DATA-01 | Soft-delete preferred over hard-delete for users/projects/assets |
| NFR-DATA-02 | User can request account data export (Phase 1: via support/admin; Phase 2: self-serve) |

---

## 5. Technology stack

| Layer | Technology | Notes |
| --- | --- | --- |
| Frontend | **Next.js 16**, React 19, TypeScript, Tailwind CSS, shadcn/ui | App Router; calls REST + SignalR |
| Backend | **ASP.NET Core / `net10.0`**, Clean Architecture | Api → Application → Domain; Infrastructure for EF, Redis, providers |
| Patterns | CQRS + MediatR | Commands/queries per feature |
| Database | **SQL Server** 2022 + EF Core | Primary system of record |
| Cache | **Redis** | Job status, plans cache, rate limits |
| Auth | **JWT** access + refresh tokens | OAuth hooks optional |
| Storage | Azure Blob / local filesystem | Configurable `Storage__Provider` |
| Payments | **Razorpay** via `IPaymentGateway` | Stripe later |
| AI text | **OpenAI**, **Gemini**, **Mock** | `AI__DefaultTextProvider` |
| AI image | **Ideogram**, **Mock** | `AI__DefaultImageProvider` |
| Realtime | **SignalR** hub `/hubs/generation` | Progress events |
| Containers | Docker Compose | Local full stack |
| Cloud | Azure App Service + SQL + Redis + Blob | See `deployment.md` |

### 5.1 Clean Architecture boundaries

```
AiBrandStudio.Api            → HTTP, SignalR, auth middleware, DI composition
AiBrandStudio.Application    → Commands/queries, validators, interfaces
AiBrandStudio.Domain         → Entities, enums, domain rules
AiBrandStudio.Infrastructure → EF Core, Redis, Razorpay, AI SDKs, storage
```

Domain and Application must not reference Infrastructure. AI and payment providers are injected behind interfaces (`ITextGenerator`, `IImageGenerator`, `IPaymentGateway`, `IBlobStorage`).

---

## 6. User journeys (summary)

1. **First brand:** Register → Dashboard empty state → Create project → Wizard steps → Generate brand/logo/palette/type → Review → Save to library → Export ZIP.  
2. **Iterate:** Open project → Regenerate logo with new keywords → Favorites → Export.  
3. **Upgrade:** View plans → Create Razorpay order → Complete checkout (when live) → Credits/plan updated.  
4. **Admin:** Login as Admin → Users → Adjust credits → Inspect logs / masked keys.

---

## 7. Assumptions & dependencies

- OpenAI / Gemini / Ideogram accounts and keys provided by operator for non-mock generation.  
- Razorpay merchant account for India/supported regions; Phase 1 may run order-create only in sandbox.  
- Signup bonus and per-action credit costs configured via environment.  
- Frontend `NEXT_PUBLIC_API_URL` points at API base (default `http://localhost:5080`).

---

## 8. Traceability

| Area | Spec | API | UI | Tests |
| --- | --- | --- | --- | --- |
| Auth | §3.1 | `api.md` Auth | Login wireframe | Unit + e2e login |
| Wizard / generate | §3.4–3.7 | brands/logos/palettes/typography | Wizard wireframes | Integration + SignalR |
| Assets / export | §3.9–3.10 | assets, exports | Assets wireframe | Integration ZIP |
| Billing skeleton | §3.11 | subscriptions, payments | Dashboard plans | Contract tests |
| Admin | §3.12 | admin/* | Admin wireframe | Authz tests |

---

## 9. Glossary

| Term | Definition |
| --- | --- |
| **Credit** | Consumable unit deducted for AI generation |
| **Brand** | Identity package within a project (narrative + visual system) |
| **Asset** | Stored file or structured output linked to a brand |
| **Mock provider** | Deterministic local AI substitute when keys are absent |
| **Generation job** | Async unit of work tracked and broadcast over SignalR |

# AI Brand Identity Studio — Enterprise Specification
## Aligned to Enterprise Prompt Formula

**Product:** AI Brand Identity Studio  
**Version:** 1.1  
**Status:** Phase 1 complete (stabilize core); Phases 2–5 tracked in [roadmap.md](roadmap.md)

---

## 1. Role
Principal Software Architect, AI Engineer, UI/UX Designer, Cloud Solution Architect, DevOps Engineer, and Enterprise Consultant delivering a cloud-native brand identity SaaS.

## 2. Objective
Design and develop a **production-ready, enterprise-grade** application for AI-powered Brand Identity Management that reduces branding cycles from **weeks to minutes**.

## 3. Business Context
| Problem | Impact | Solution |
| --- | --- | --- |
| Branding needs designers, agencies, expensive tools | High cost, slow time-to-market | Generative AI brand studio |
| Inconsistent brand assets across channels | Weak recognition | Unified brand kit + guidelines + exports |
| Non-designers cannot ship identity systems | Bottleneck | Guided wizard + one-click generators |

**Goal:** Complete brand identity (name, story, logo, palette, type, mockups, website, video prompts, marketing, exports) in minutes.

## 4. Requirements (Quality bar)
The solution must be: Production Ready · Enterprise Grade · Cloud Native · Secure · Scalable · Responsive · Modular · Maintainable · AI Powered.

## 5. Technology Stack
| Layer | Choice |
| --- | --- |
| Frontend | React 19, Next.js 16, TypeScript, Tailwind CSS |
| Backend | ASP.NET Core (net10.0), Clean Architecture, CQRS, MediatR |
| Database | SQL Server (prod) · SQLite (local) · EF Core |
| Cache | Redis (optional memory fallback) |
| Storage | Azure Blob (local FS fallback) |
| Auth | JWT + Refresh Tokens · OAuth hooks (Google/Microsoft/GitHub) |
| Payments | Razorpay (`IPaymentGateway` for Stripe) |
| Cloud | Azure App Service / AKS · Docker · GitHub Actions |
| AI | OpenAI, Gemini, Ideogram (+ Mock fallback) |

## 6. Modules (behavior-defined)

### Authentication
Register/login with JWT+refresh; `/me` profile; rate-limited auth; OAuth endpoints return configured/not-configured status; RBAC (`User`/`Admin`).

### Dashboard
Credits, project counts, analytics snapshot, quick launch to all generators, empty-state CTA to wizard.

### Projects
CRUD brand projects owned by user; open detail to regenerate brand/logo/palette/export.

### Brand Generator
Multi-step wizard (business, audience, style, mission); AI generates name, tagline, story, voice; credits debit; version snapshot.

### Logo Generator
Styles: Minimal, Luxury, Modern, Vintage, Mascot, Tech, Geometric, Gradient, Flat, Monogram.  
Outputs: SVG content, PNG/SVG asset URL, vector prompt.  
Supports regenerate, history list, copy prompt, download SVG/PNG.

### Prompt Generator
Multi-tool packs (Ideogram/MJ/Flux/DALL·E/SD/Runway/ChatGPT); copy per tool; 1 credit; upsell.

### Color / Typography
Palette HEX + CSS/JSON/Tailwind export; font recommendations with Google Fonts URL.

### Mockup Generator
Business card, cup, packaging, apparel, billboard, etc.; image asset + prompt; credits.

### Landing / Website Generator
Responsive HTML homepage (hero, about, services, contact); stored as asset.

### Video Generator
Provider-specific cinematic prompts + storyboard + VO + music (Runway/Pika/Gemini/Luma/Hailuo).

### Marketing / Social
Ad copy packs, launch plan, 90-day roadmap; 14–30 day multi-platform calendar.

### Brand Guidelines
Client-ready MD + HTML brand book; commercial use section; 3 credits.

### Asset Library & Downloads
List assets; Standard ZIP free; Pro ZIP (guidelines/landing/mockups) for 5 credits.

### Subscription & Payments
Plans (Free/Builder/Scale); Razorpay order by plan + webhook verification; upgrade CTAs across app.

### Admin Panel & User Management
List users, credits, generation logs (Admin role).

### Analytics
Projects, brands, logos, assets, credits, ledger activity.

## 7. User Flow
```
Register → Login → Dashboard → Create Project / Brand Wizard
→ Generate Brand → Generate Logo → Generate Mockups
→ Generate Website → Guidelines → Export ZIP → Upgrade/Pay
```
Guided UI: `/workflow`

## 8. Architecture
```
Frontend (Next.js) → REST/JWT → Api Controllers
→ Application (MediatR CQRS) → Domain Entities
→ Infrastructure (EF, Redis, AI, Storage, Payments)
→ SQL Server / SQLite · Azure Blob · AI Providers
```
See [architecture.md](architecture.md).

## 9. Database
Tables: Users, Roles, UserRoles, RefreshTokens, OAuthAccounts, Projects, Brands, BrandVersions, Logos, Palettes, Fonts, Prompts, Assets, Mockups, Videos, Exports, CreditLedgers, SubscriptionPlans, Subscriptions, Payments, Notifications, AuditLogs, AiProviderConfigs, Templates.  
DDL: `database/01_schema.sql`

## 10. API (selected)
| Area | Endpoints |
| --- | --- |
| Auth | `POST /api/auth/register\|login\|refresh\|logout` · `GET /api/auth/me` · `GET /api/auth/oauth/status` |
| Projects | CRUD `/api/projects` |
| Brands | `/api/brands/wizard` · `/{id}/generate` |
| Logo | `POST /api/logos/generate` · `GET /api/logos?brandId=` |
| Generators | prompts, palettes, typography, mockups, landing, videos, marketing, social, guidelines |
| Exports | `/api/downloads` · `/api/downloads/zip` |
| Billing | `/api/subscriptions/plans` · `/api/payments/razorpay/order` |
| Admin | `/api/admin/users` · `/api/admin/logs` |
| Analytics | `/api/analytics` |
| Realtime | SignalR `/hubs/generation` |

Full list: [api.md](api.md)

## 11. Security (OWASP-aligned)
- JWT bearer + refresh rotation
- RBAC on admin routes
- Password hashing (BCrypt)
- Rate limiting on auth
- Parameterized EF queries (SQLi mitigation)
- CORS allowlist
- Secrets via env / Key Vault placeholders
- Audit-friendly credit ledger
- XSS: React escaping; SVG sanitized use-cases (trusted generation)
- CSRF: token APIs (Bearer) — SPA pattern

## 12. UI/UX Design System
- Teal `#0F766E` · Ink `#0B1220` · Porcelain `#F7F8FA`
- Plus Jakarta Sans + JetBrains Mono
- Light/dark theme, glass sidebar, mobile drawer
- Reusable: `GeneratorStudio`, `BrandProjectPicker`, `UpgradeBanner`, `AppShell`
- WCAG: contrast targets, labels, keyboard-focusable controls

## 13. Testing
Unit (AI providers), API smoke (`scripts/smoke.ps1`), CI build FE+BE.  
See [testing-strategy.md](testing-strategy.md).

## 14. Deployment
Docker Compose (web/api/sql/redis), Azure App Service guide, K8s base manifests, GitHub Actions CI.  
See [deployment.md](deployment.md), [cicd.md](cicd.md).

## 15. Output Delivered
| Artifact | Location |
| --- | --- |
| Source (FE) | `frontend/` |
| Source (BE) | `backend/` |
| SQL | `database/` |
| Docs | `docs/` |
| Docker/K8s/CI | `docker/`, `k8s/`, `.github/` |

## 16. Constraints (enforced)
- No deprecated stacks (Next 16 / ASP.NET current)
- SOLID + Clean Architecture + CQRS
- Responsive + reusable components
- Avoid duplicate generator UI (shared studio)
- Performance: TanStack Query caching, async AI I/O
- Accessibility + OWASP practices above

---

## Compliance Matrix

| Enterprise criterion | Status |
| --- | --- |
| Production-ready structure | Met (runnable FE/BE, Docker, CI) |
| Clean Architecture / CQRS / MediatR | Met |
| Next.js + TS + Tailwind | Met |
| SQL Server schema + Azure path | Met (SQLite local / SQL Server prod) |
| JWT + OAuth hooks | Met (OAuth status API; full IdP wiring Phase 2) |
| Auth → Export user flow | Met (`/workflow`) |
| Logo SVG/PNG/prompt/styles/regenerate | Met |
| Admin + Analytics + Payments | Met (payments skeleton) |
| Live multi-provider video render | Partial (prompts/storyboard) |
| Full OAuth login buttons live | Partial (hooks + status) |
| Stripe live + webhooks reconciled | Partial (abstraction ready) |

**Verdict:** Phase 1 stabilize-core exit is met (migrations, debit-before-work credits, protected storage, Razorpay verify, global exceptions). Remaining depth is Phases 2–5 per [roadmap.md](roadmap.md) (queues, Blob, frontend structure, ops, tenancy).

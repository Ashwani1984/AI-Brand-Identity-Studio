# AI Brand Identity Studio

Enterprise SaaS for AI-powered Brand Identity Management — reduce branding from **weeks to minutes**.

## Enterprise alignment
See **[docs/ENTERPRISE-SPEC.md](docs/ENTERPRISE-SPEC.md)** for the full Role → Objective → Modules → Security → Deployment formula and compliance matrix.

## Stack
| Layer | Technology |
| --- | --- |
| Frontend | Next.js 16, React 19, TypeScript, Tailwind CSS |
| Backend | ASP.NET Core (net10.0), Clean Architecture, CQRS, MediatR |
| Database | SQL Server (prod) · SQLite (local) · EF Core |
| Cloud | Azure · Docker · Kubernetes · GitHub Actions |
| Auth | JWT + Refresh · OAuth hooks (Google / Microsoft / GitHub) |
| Payments | Razorpay (`IPaymentGateway`) |
| AI | OpenAI · Gemini · Ideogram · Mock fallback |

## Guided user flow
`Register → Login → Dashboard → Brand Wizard → Logo → Mockups → Website → Export`  
UI: http://localhost:3000/workflow

## Quick start
```bash
cp .env.example .env
# API
cd backend/src/AiBrandStudio.Api && dotnet run
# Web
cd frontend && npm install && npm run dev
# Or
docker compose -f docker/docker-compose.yml up --build
```
- Web: http://localhost:3000  
- Swagger: http://localhost:5080/swagger  
- Admin: `admin@aibrand.studio` / `Admin@12345`

## Repository
```
AI-Brand-Identity-Studio/
├── docs/                 # Enterprise spec, SRS, API, architecture, manuals
├── database/             # SQL Server DDL + seed
├── backend/              # Clean Architecture solution
├── frontend/             # Next.js app
├── docker/ · k8s/ · .github/workflows/
└── scripts/smoke.ps1
```

## Constraints followed
SOLID · Clean Architecture · Responsive UI · Reusable components · OWASP-minded auth · No deprecated stacks · Modular generators

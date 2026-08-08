# Module coverage matrix

| Module | UI route | API | Status |
| --- | --- | --- | --- |
| Dashboard | `/dashboard` | `/api/projects`, `/api/analytics` | Done + quick launch |
| Brand Projects | `/projects` | `/api/projects` | Done |
| Brand Generator | `/wizard` | `/api/brands/wizard`, `/generate` | Done + step validation |
| Logo Generator | `/logo` | `/api/logos/generate`, `GET /api/logos` | Done — SVG/PNG export, styles, regenerate, history, edit notes |
| User Flow | `/workflow` | — | Done — enterprise Register→Export guide |
| Auth OAuth status | — | `GET /api/auth/oauth/status` | Done — IdP readiness hooks |
| Prompt Generator | `/prompts` | `/api/prompts/generate` | Done |
| Color Palette Generator | `/palette` | `/api/palettes/*` | Done |
| Typography Generator | `/typography` | `/api/typography/suggest` | Done |
| Mockup Generator | `/mockups` | `/api/mockups/generate` | Done |
| Landing Page Generator | `/landing` | `/api/landing/generate` | Done |
| Video Generator | `/video` | `/api/videos/generate` | Done (prompt/storyboard) |
| Marketing Generator | `/marketing` | `/api/marketing/generate` | Done |
| Social Media Calendar | `/social` | `/api/social/calendar` | Done |
| Brand Guidelines | `/guidelines` | `/api/guidelines/generate` | Done |
| Asset Library | `/assets` | `/api/assets` | Done |
| Downloads | `/downloads` | `/api/downloads` | Done |
| Subscription | `/billing` | `/api/subscriptions/plans` | Done |
| Payments | `/payments` | `/api/payments/razorpay/order` | Done (skeleton) |
| User Management | `/users` | `/api/admin/users` | Done |
| Admin Panel | `/admin` | `/api/admin/users`, `/logs` | Done |
| Analytics | `/analytics` | `/api/analytics` | Done |

## Revenue modules (upgraded)
- **Prompt Generator** — multi-tool packs (Ideogram/MJ/Flux/DALL·E/SD/Runway/ChatGPT), 1 credit, copy CTAs, upsell
- **Brand Guidelines** — client brand book MD+HTML, 3 credits, sell tip ₹4,999–₹14,999
- **Downloads** — Standard free ZIP vs Pro ZIP (5 credits) with guidelines/landing/mockups
- **Subscription** — feature lists + popular badge + retainer tip


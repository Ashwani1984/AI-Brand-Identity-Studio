# REST API Documentation — AI Brand Identity Studio

**Base URL (local):** `http://localhost:5080`  
**API prefix:** `/api`  
**Auth:** Bearer JWT (`Authorization: Bearer <access_token>`)  
**Content-Type:** `application/json`  
**Realtime:** SignalR hub `/hubs/generation`

OpenAPI/Swagger: `http://localhost:5080/swagger`

---

## Conventions

| Item | Rule |
| --- | --- |
| IDs | UUID strings |
| Errors | Problem Details (`application/problem+json`) where possible |
| Timestamps | ISO-8601 UTC |
| Pagination | `?page=1&pageSize=20` → `{ items, page, pageSize, totalCount }` |
| Credits | Insufficient credits → `402 Payment Required` or `400` with `code: INSUFFICIENT_CREDITS` |
| Async gen | May return `202 Accepted` + `{ jobId }` while progress streams on SignalR |

### Common error shape

```json
{
  "type": "https://httpstatuses.com/400",
  "title": "Bad Request",
  "status": 400,
  "detail": "Insufficient credits",
  "code": "INSUFFICIENT_CREDITS"
}
```

---

## 1. Auth

### `POST /api/auth/register`

Create account; awards signup bonus credits.

**Body**

```json
{
  "email": "ada@example.com",
  "password": "Str0ng!Pass",
  "displayName": "Ada Lovelace"
}
```

**Responses:** `201` user + tokens; `400` validation; `409` email taken.

---

### `POST /api/auth/login`

**Body**

```json
{
  "email": "ada@example.com",
  "password": "Str0ng!Pass"
}
```

**Response `200`**

```json
{
  "accessToken": "<jwt>",
  "expiresIn": 1800,
  "refreshToken": "<opaque>",
  "user": {
    "id": "...",
    "email": "ada@example.com",
    "displayName": "Ada Lovelace",
    "roles": ["User"],
    "creditBalance": 50
  }
}
```

---

### `POST /api/auth/refresh`

**Body**

```json
{ "refreshToken": "<opaque>" }
```

**Response `200`:** new access (+ rotated refresh).  
**Response `401`:** invalid/revoked.

---

### `POST /api/auth/logout`

**Auth:** Bearer required  

**Body**

```json
{ "refreshToken": "<opaque>", "allSessions": false }
```

**Response `204`**

---

## 2. Projects

### `GET /api/projects`

List current user’s projects.

**Query:** `page`, `pageSize`, `q` (name search)

**Response `200`:** paged project summaries (`id`, `name`, `description`, `status`, `updatedAtUtc`, `brandCount`).

---

### `POST /api/projects`

**Body**

```json
{ "name": "Northwind Rebrand", "description": "Q3 identity refresh" }
```

**Response `201`:** project resource.

---

### `GET /api/projects/{projectId}`

**Response `200`:** project detail + nested brand summaries.  
**Response `404`:** missing or not owned.

---

### `PUT /api/projects/{projectId}`

**Body:** `{ "name", "description", "status" }`  
**Response `200`**

---

### `DELETE /api/projects/{projectId}`

Soft-delete. **Response `204`**

---

## 3. Brands & Wizard

### `POST /api/brands/wizard`

Create or update brand from wizard payload; may start brand narrative generation.

**Body**

```json
{
  "projectId": "...",
  "brandId": null,
  "name": "Lumen Forge",
  "industry": "SaaS / Developer Tools",
  "audience": "Engineering managers at Series A startups",
  "tone": "Confident, precise, warm",
  "competitors": ["Linear", "Notion"],
  "styleKeywords": ["geometric", "minimal", "teal accents"],
  "colorPreferences": { "liked": ["#0F766E"], "avoid": ["#7C3AED"] },
  "generateNow": true
}
```

**Response `202`**

```json
{
  "brandId": "...",
  "jobId": "...",
  "status": "Queued"
}
```

When `generateNow: false`, returns `201` draft brand without debiting credits.

---

### `POST /api/brands/{brandId}/generate`

Re-run brand narrative generation (debits `Credits__BrandGenerate`).

**Response `202`:** `{ jobId }`

---

### `GET /api/brands/{brandId}`

Full brand: narrative, primary logo/palette/typography ids, status.

---

### `PATCH /api/brands/{brandId}`

Update fields / set primaries:

```json
{
  "primaryLogoId": "...",
  "primaryPaletteId": "...",
  "primaryTypographyId": "...",
  "status": "Final"
}
```

---

## 4. Logos

### `POST /api/logos/generate`

**Body**

```json
{
  "brandId": "...",
  "count": 4,
  "styleKeywords": ["wordmark", "geometric"],
  "negativePrompt": "clipart, 3d gloss"
}
```

**Credits:** `Credits__LogoGenerate` (per request or per image—document in config; default per request).  
**Response `202`:** `{ jobId }`  
**SignalR:** progress + final logo ids.

---

### `GET /api/logos?brandId={id}`

List logos for brand.

---

### `POST /api/logos/{logoId}/favorite`

Toggle favorite. **Response `200`:** `{ isFavorite }`

---

## 5. Palettes

### `POST /api/palettes/generate`

**Body**

```json
{
  "brandId": "...",
  "mood": "calm professional",
  "seedHex": "#0F766E"
}
```

**Response `202`:** `{ jobId }`

---

### `GET /api/palettes/{paletteId}`

Returns colors + contrast notes.

---

### `GET /api/palettes/{paletteId}/export`

**Query:** `format=json|css|tailwind`  

**Response `200`:** file download or JSON body depending on `Accept` / format.

Example CSS:

```css
:root {
  --color-primary: #0f766e;
  --color-secondary: #134e4a;
  --color-accent: #2dd4bf;
  --color-neutral-900: #0b1220;
  --color-neutral-50: #f4f7f7;
}
```

---

## 6. Typography

### `POST /api/typography/suggest`

**Body**

```json
{
  "brandId": "...",
  "tone": "Confident, precise, warm",
  "preferVariableFonts": true
}
```

**Response `202`:** `{ jobId }` or **`200`** synchronous suggestion when mock/fast path:

```json
{
  "id": "...",
  "displayFont": "Fraunces",
  "bodyFont": "Source Sans 3",
  "monoFont": "IBM Plex Mono",
  "guidance": {
    "displayWeights": [600, 700],
    "bodyWeights": [400, 600],
    "notes": "Use Fraunces for headlines; Source Sans 3 for UI and body."
  }
}
```

---

## 7. Assets

### `GET /api/assets`

**Query:** `projectId`, `brandId`, `assetType`, `page`, `pageSize`, `favoriteOnly`

**Response `200`:** paged asset cards (`id`, `name`, `assetType`, `storageUri`, `thumbnailUri`, `isFavorite`, `createdAtUtc`).

---

### `GET /api/assets/{assetId}`

Metadata + resolvable URL.

---

### `PATCH /api/assets/{assetId}`

```json
{ "name": "Primary mark — dark", "isFavorite": true }
```

---

### `DELETE /api/assets/{assetId}`

Soft-delete. **`204`**

---

## 8. Exports

### `POST /api/exports/zip`

**Body**

```json
{
  "projectId": "...",
  "brandId": "...",
  "assetIds": ["...", "..."],
  "includeBrandJson": true,
  "includePaletteCss": true
}
```

**Response `202`**

```json
{ "exportId": "...", "jobId": "...", "status": "Pending" }
```

---

### `GET /api/exports/{exportId}`

```json
{
  "id": "...",
  "status": "Ready",
  "downloadUrl": "https://.../exports/....zip",
  "completedAtUtc": "2026-08-08T10:00:00Z"
}
```

---

## 9. Subscriptions

### `GET /api/subscriptions/plans`

Public or authenticated. Returns active plans:

```json
{
  "items": [
    {
      "code": "FREE",
      "name": "Free",
      "monthlyCredits": 50,
      "priceInr": 0,
      "features": ["Wizard", "Mock AI", "ZIP export"]
    },
    {
      "code": "PRO",
      "name": "Pro",
      "monthlyCredits": 500,
      "priceInr": 1499,
      "features": ["Priority generation", "Higher resolution logos"]
    }
  ]
}
```

---

### `GET /api/subscriptions/me`

Current user’s subscription + period window. **Auth required.**

---

## 10. Payments (Razorpay skeleton)

### `POST /api/payments/razorpay/order`

Create Razorpay order for plan upgrade.

**Body**

```json
{
  "planCode": "PRO",
  "currency": "INR"
}
```

**Response `200`**

```json
{
  "orderId": "order_xxx",
  "amount": 149900,
  "currency": "INR",
  "razorpayKeyId": "rzp_test_xxx",
  "paymentId": "<internal-payment-uuid>"
}
```

**Notes:** Amount in paise. Requires Razorpay keys; otherwise `503` with `PAYMENTS_NOT_CONFIGURED`.

---

### `POST /api/payments/razorpay/webhook`

Razorpay webhook (no user JWT). Verifies `X-Razorpay-Signature` when `Razorpay__WebhookSecret` set.

**Response `200`:** `{ "received": true }`

---

## 11. Admin

All routes require role `Admin`.

### `GET /api/admin/users`

**Query:** `q`, `page`, `pageSize`  

Returns users with roles, credit balance, plan code, `isActive`.

---

### `GET /api/admin/users/{userId}`

Detail + recent credit ledger entries.

---

### `POST /api/admin/users/{userId}/credits`

```json
{
  "delta": 100,
  "reason": "Support goodwill credit"
}
```

Writes `CreditLedgers` + `AuditLogs`. **`200`:** `{ creditBalance }`

---

### `GET /api/admin/api-keys`

Returns provider configuration status with **masked** secrets:

```json
{
  "items": [
    { "provider": "OpenAI", "isConfigured": true, "lastFour": "a1b2" },
    { "provider": "Gemini", "isConfigured": false, "lastFour": null },
    { "provider": "Ideogram", "isConfigured": false, "lastFour": null },
    { "provider": "Razorpay", "isConfigured": true, "lastFour": "9xyz" }
  ]
}
```

---

### `GET /api/admin/audit-logs`

**Query:** `from`, `to`, `actorUserId`, `action`, `page`, `pageSize`

---

## 12. SignalR — `/hubs/generation`

**Transport:** WebSockets (Long Polling fallback)  
**Auth:** JWT via `access_token` query or Negotiate header (same policy as API).

### Client → Server

| Method | Args | Description |
| --- | --- | --- |
| `SubscribeJob` | `jobId: string` | Join group for job updates |
| `UnsubscribeJob` | `jobId: string` | Leave group |

### Server → Client

| Event | Payload |
| --- | --- |
| `GenerationProgress` | `{ jobId, status, progressPercent, message }` |
| `GenerationCompleted` | `{ jobId, status, result }` |
| `GenerationFailed` | `{ jobId, error }` |

`result` shape depends on `JobType` (brand narrative, logo ids, palette id, typography id, export download url).

**Example (JS client)**

```ts
import * as signalR from "@microsoft/signalr";

const connection = new signalR.HubConnectionBuilder()
  .withUrl(`${API_URL}/hubs/generation`, {
    accessTokenFactory: () => accessToken,
  })
  .withAutomaticReconnect()
  .build();

connection.on("GenerationProgress", (p) => console.log(p));
await connection.start();
await connection.invoke("SubscribeJob", jobId);
```

---

## 13. Health

### `GET /health`

Liveness.

### `GET /health/ready`

Checks SQL + Redis. **`200`/`503`**.

---

## 14. Versioning

Phase 1 uses unversioned `/api` routes. Breaking changes in Phase 2 introduce `/api/v2` while keeping v1 until deprecation window closes.

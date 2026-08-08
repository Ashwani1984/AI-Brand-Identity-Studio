# Admin Manual — AI Brand Identity Studio (MVP)

Guide for operators with the **Admin** role: manage users, credits, masked API key status, and audit logs.

**Access:** Sign in with an admin account → **Admin** appears in the main navigation. Non-admins receive **403** on `/api/admin/*`.

---

## 1. Admin responsibilities (Phase 1)

| Area | You can | You cannot (Phase 1) |
| --- | --- | --- |
| Users | Search, view roles/plan/credits, toggle guidance on activity | Full impersonation / edit passwords via UI (use Identity tooling if needed) |
| Credits | Grant or revoke with reason | Silently change balance without ledger |
| API keys | See **configured vs missing** + last four chars | View or copy full secrets from the UI |
| Logs | Browse audit trail | Delete audit history from UI |
| Plans | Inspect plan catalog indirectly via user subscription | Live price editing (use DB/seed/config) |

---

## 2. Opening Admin

1. Sign in with a user that has the `Admin` role.  
2. Click **Admin** in the shell nav.  
3. Use sub-nav: **Users** · **API keys** · **Audit logs**.

Seed an admin via secure ops process (not a committed default password). Rotate credentials after first login.

---

## 3. Users

### 3.1 List & search

- Filter by email or display name.  
- Columns: email, roles, credit balance, plan, active flag, created date.

### 3.2 User detail

Open a row to see:

- Profile fields  
- Current subscription period (if any)  
- Recent **credit ledger** entries  

### 3.3 Typical support actions

| Scenario | Action |
| --- | --- |
| User spent credits on failed generation (confirmed bug) | Grant credits with reason `Support refund — job <id>` |
| Abuse / spam registrations | Mark inactive (when UI/API supports) and note in audit |
| “I never got signup bonus” | Verify ledger; grant `SignupBonus correction` if missing |

---

## 4. Credits

### 4.1 Adjust credits

1. Open **Admin** → **Grant / revoke credits**.  
2. Select user, enter **amount** (positive to grant, negative to revoke).  
3. Enter a clear **reason** (required).  
4. Confirm **Apply**.

API: `POST /api/admin/credits/grant` `{ userId, amount, reason }`  
Org pool: `POST /api/admin/credits/grant-org` `{ organizationId, amount, reason }`

Effects:

- `Users.CreditBalance` (or org pool) updated  
- Append-only `CreditLedgers` row  
- `AuditLogs` entry (`admin.credit_grant` / `admin.credit_revoke`) with actor = you  

Monthly finance CSV: `GET /api/admin/reports/monthly?year=2026&month=8`  
Immutable audit list: `GET /api/admin/audit` 

### 4.2 Rules of thumb

- Never grant large amounts without ticket/reference id in the reason.  
- Prefer small goodwill grants over disabling the credit system.  
- Revokes should not drive balance below zero — API should reject; if allowed, document why.

### 4.3 Default cost reference

| Action | Default debit |
| --- | --- |
| Signup bonus | +50 (configurable) |
| Brand generate | −5 |
| Logo generate | −8 |
| Palette generate | −2 |

---

## 5. API keys (masked)

Admin → **API keys** shows provider status for:

- OpenAI  
- Gemini  
- Ideogram  
- Razorpay  

| Field | Meaning |
| --- | --- |
| Configured | Key present in environment / Key Vault |
| Missing | App will use **Mock** for that AI capability (or payments disabled) |
| Last four | Mask only — e.g. `••••a1b2` |

### 5.1 Changing keys

Keys are **not** edited in the Admin UI in Phase 1. Update:

- Local: `.env`  
- Azure: App Settings / Key Vault references  

Then restart the API. Confirm status flips to Configured and last four updates.

### 5.2 Security rules

- Never paste full keys into chat, tickets, or audit reason fields.  
- Never screenshot Admin API keys page into public channels.  
- If a key leaks, rotate at the provider, update Key Vault, restart, and audit the incident.

---

## 6. Audit logs

Admin → **Audit logs**

| Column | Example |
| --- | --- |
| Time (UTC) | 2026-08-08T10:15:00Z |
| Actor | ops@company.com |
| Action | CreditsGranted / UserLoginFailed / ExportCreated |
| Entity | User / Export / … |
| Detail | Reason snippet / ids (no secrets) |

### Filters

- Date range  
- Actor  
- Action name  

### What should always be logged

- Admin credit adjustments  
- Admin viewing sensitive configuration (optional Phase 1.1)  
- Auth failures (rate-limit relevant)  
- Export completion  
- Subscription payment state changes (when Razorpay live)

---

## 7. Payments & plans (ops view)

- Users on **Free** rely on signup bonus + grants.  
- **Razorpay** order creation requires configured keys; otherwise users see an error and you will see `Razorpay` as Missing/Configured on the API keys page.  
- Webhook URL for ops setup: `https://<api-host>/api/payments/razorpay/webhook`  
- After a successful webhook (when live), confirm `Payments` status Paid and `Subscriptions` updated.

---

## 8. Generation & mock mode

If AI keys are missing:

- Product still works with **Mock** providers (deterministic placeholders).  
- Useful for demos and CI; tell stakeholders logos/text are not production AI.  

When enabling real providers, watch:

- Credit burn rate  
- Provider error rates in logs / Application Insights  
- User complaints about empty images (quota, billing, content policy)

---

## 9. Incident checklist

1. Check `/health/ready` (SQL + Redis).  
2. Check Application Insights / API logs for correlation ids.  
3. Verify API key status page.  
4. Confirm Redis and Blob connectivity.  
5. If credits look wrong, inspect `CreditLedgers` for the user before granting more.  
6. Record actions in the ticket; rely on audit log for system of record.

---

## 10. Role management notes

Phase 1 admin assignment is typically via:

- Seed script / SQL insert into `UserRoles`, or  
- Internal tooling  

Do not grant Admin to shared mailboxes. Prefer named operators.

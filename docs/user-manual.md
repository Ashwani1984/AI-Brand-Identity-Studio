# User Manual — AI Brand Identity Studio (MVP)

This guide walks through the Phase 1 customer experience: register, create a brand, generate assets, and export a ZIP package.

**App URL (local):** http://localhost:3000  
**Credits:** Each AI action spends credits from your balance (shown in the top bar). Signup starts with a bonus (default **50**).

---

## 1. Create an account

1. Open the app and choose **Register**.  
2. Enter **email**, **display name**, and a strong **password**.  
3. Submit **Create account**.  
4. You land on the **Dashboard** with your credit balance visible.

Already registered? Use **Sign in** with email and password.

> OAuth buttons (Google / Microsoft / GitHub) may appear when the operator has configured them; otherwise use email registration.

---

## 2. Understand the dashboard

From the dashboard you can:

- See **credit balance** and current **plan** (e.g. Free).  
- Open **recent projects**.  
- Start **+ New project**.

If you have no projects yet, use the empty-state CTA to create your first one.

---

## 3. Create a project

1. Click **+ New project**.  
2. Enter a **name** (e.g. “Northwind Rebrand”) and optional description.  
3. Save. You enter the project workspace.

A **project** holds one or more **brands** and their assets.

---

## 4. Create a brand with the Wizard

1. Inside a project, choose **New brand** / **Start wizard**.  
2. Complete the steps:

| Step | What to enter |
| --- | --- |
| Basics | Brand name, industry, audience |
| Voice | Tone keywords, optional competitors |
| Visuals | Style keywords, liked/avoid colors, logo leanings |
| Review | Confirm answers; choose what to generate |

3. On Review, note the **credit cost** before confirming.  
4. Click **Generate**.

While generation runs, a **progress** indicator updates live (status text such as “Composing voice guidelines…”). Keep the tab open until completion.

### Tips

- Be specific about audience and tone — better inputs produce clearer narratives.  
- Avoid purple-only or trendy clichés if you want a distinctive palette; name colors you already like.  
- You can save a **draft** without generating if you need to pause (when the UI offers “Save draft”).

---

## 5. Review generation results

Open the brand’s result tabs:

### Narrative

- Tagline options, positioning, and voice do/don’t.  
- Use **Regenerate narrative** only if needed (spends brand-generation credits again).

### Logos

- Browse concepts in the grid.  
- Mark a **favorite** and set **primary** logo for exports.  
- **Regenerate** with refined style keywords if none fit.

### Palette

- Inspect swatches and hex values.  
- Download **CSS** or **JSON** export for handoff to engineers.  
- Prefer combinations that keep body text readable on your backgrounds.

### Typography

- Review display + body (and optional mono) pairings.  
- Apply the set to the brand so it is included in packages.

---

## 6. Asset Library

1. Go to **Assets** in the main nav (or project → Assets).  
2. Filter by project, brand, or type.  
3. Rename or favorite items as needed.  
4. Select one or more assets for bulk ZIP export.

Soft-deleted items disappear from the default list; contact support/admin if you need recovery in Phase 1.

---

## 7. Export a ZIP

1. From the brand **Export** tab **or** Asset Library selection, choose **Download ZIP** / **Export package**.  
2. Confirm included items (logos, palette, typography, brand JSON).  
3. Wait for the job to finish (progress may appear).  
4. Download when status is **Ready**.

### Typical ZIP contents

| Path / file | Contents |
| --- | --- |
| `logos/` | PNG (and SVG when available) |
| `palette.json` / `palette.css` | Color system |
| `typography.json` | Font pairings + guidance |
| `brand.json` | Narrative and metadata |
| `README.txt` | Short usage notes |

---

## 8. Subscriptions & payments

1. Open **Billing** / **Plans**.  
2. Compare Free / Pro / Agency credit allowances.  
3. To upgrade, start checkout (**Razorpay**).  

Phase 1 may run in **sandbox** or show “payments not configured” if the operator has not set Razorpay keys. Credits can still work via signup bonus and admin grants.

---

## 9. Credits at a glance (defaults)

| Action | Typical cost |
| --- | --- |
| Brand narrative generate | 5 |
| Logo generate | 8 |
| Palette generate | 2 |

Exact values are controlled by the server configuration. If you see **Insufficient credits**, wait for plan renewal, upgrade, or ask an admin for a grant.

---

## 10. Troubleshooting

| Issue | What to try |
| --- | --- |
| Stuck on “Generating…” | Refresh; reopen brand; check credit balance; retry once |
| Blank logos | Mock/provider issue — retry; notify admin if persistent |
| Cannot download ZIP | Ensure export status is Ready; disable blockers; retry |
| Logged out unexpectedly | Sign in again; refresh token expired after idle period |
| OAuth button fails | Use email/password; OAuth may be disabled |

---

## 11. Privacy & your data

- Projects and brands are **private to your account**.  
- Soft deletion keeps recovery possible for operators; hard purge is not self-serve in Phase 1.  
- Do not upload sensitive personal data about third parties into prompts unless you have rights to process it.

For account deletion requests in Phase 1, contact your administrator.

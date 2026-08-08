# UI Wireframes — AI Brand Identity Studio

Text wireframes for Phase 1 MVP screens, plus a detailed Figma AI prompt for a premium teal/ink SaaS look.

**Visual direction (summary):** Deep ink navy backgrounds with teal accents, crisp sans UI type, generous whitespace in content panels, no purple glow clichés. Brand wordmark is hero-level on marketing; in-app, product name stays visible in the shell without competing with task content.

---

## 1. Login / Register

```
┌──────────────────────────────────────────────────────────────┐
│  [full-bleed atmospheric bg: soft teal grain over ink]       │
│                                                              │
│         AI Brand Identity Studio                             │
│         Sign in to continue                                  │
│                                                              │
│         ┌────────────────────────────┐                       │
│         │ Email                      │                       │
│         │ Password                   │                       │
│         │ [ Sign in ]                │                       │
│         │ Forgot password · Register │                       │
│         └────────────────────────────┘                       │
│                                                              │
│         Optional: Continue with Google / Microsoft / GitHub  │
└──────────────────────────────────────────────────────────────┘
```

**Register:** Same shell; fields Email, Display name, Password, Confirm; CTA “Create account”; link back to Sign in.

**Notes:** Single composition; no cards-of-cards; form is the only interactive container. Keyboard-first; errors under fields.

---

## 2. Dashboard

```
┌─ Shell ──────────────────────────────────────────────────────┐
│ Logo wordmark          Credits: 42    Plan: Free   [Avatar]  │
│ Nav: Dashboard | Projects | Assets | Billing | (Admin?)      │
├──────────────────────────────────────────────────────────────┤
│                                                              │
│  Good afternoon, Ada                                         │
│  Pick up a project or start a new brand.                     │
│                                                              │
│  [ + New project ]                                           │
│                                                              │
│  Recent projects                                             │
│  ┌─────────────────┐  ┌─────────────────┐  ┌──────────────┐  │
│  │ Northwind       │  │ Lumen Forge     │  │ Empty state  │  │
│  │ Updated 2h ago  │  │ Wizard 80%      │  │ tip card? NO │  │
│  │ 2 brands        │  │ Continue →      │  │ — plain list │  │
│  └─────────────────┘  └─────────────────┘  └──────────────┘  │
│                                                              │
│  (No stat strip / no promo pills in first viewport)          │
└──────────────────────────────────────────────────────────────┘
```

**Empty state:** One headline (“Create your first brand project”), one sentence, one CTA—no feature grid.

---

## 3. Brand Wizard (steps)

### Step 1 — Basics

```
Project: Northwind Rebrand          Step 1 of 5 · Basics
─────────────────────────────────────────────────────────
Brand / product name     [________________]
Industry                 [________________]
Primary audience         [____________________________]

            [ Back ]                    [ Continue → ]
```

### Step 2 — Voice

```
Step 2 of 5 · Voice & tone
Tone keywords            [ confident ] [ warm ] [ precise ]
Competitors (optional)   [ Linear, Notion ]
Do / Don't notes         [ multiline ]

            [ Back ]                    [ Continue → ]
```

### Step 3 — Visual prefs

```
Step 3 of 5 · Visual direction
Style keywords           [ geometric, minimal ]
Liked colors             [ #0F766E ] [ + ]
Avoid colors             [ #7C3AED ]
Logo leanings            ( ) Wordmark  ( ) Symbol  ( ) Combo

            [ Back ]                    [ Continue → ]
```

### Step 4 — Review

```
Step 4 of 5 · Review
Summary of all answers (read-only list)
Credits required: Brand 5 · optional Logo 8 · Palette 2

[ ] Generate brand narrative now
[ ] Also generate logos & palette

            [ Back ]              [ Generate → ]
```

### Step 5 — Generating (results bridge)

```
Step 5 · Generating
Progress ████████░░ 80%   “Composing voice guidelines…”
(Live via SignalR — no fake spinners without status text)
```

---

## 4. Generation results

```
┌─ Brand: Lumen Forge ─────────────────────────────────────────┐
│ Tabs: Narrative | Logos | Palette | Typography | Export      │
├──────────────────────────────────────────────────────────────┤
│ Narrative                                                    │
│   Tagline options (3)                                        │
│   Positioning paragraph                                      │
│   Voice do / don’t                                           │
│   [ Regenerate narrative ]  (shows credit cost)              │
│                                                              │
│ Logos                                                        │
│   [img] [img] [img] [img]     ← flat grid, no floating badges│
│   Select primary · Favorite · Regenerate                     │
│                                                              │
│ Palette                                                      │
│   Swatches full-bleed strip (not inset card collage)         │
│   Hex labels · [ Export CSS ] [ Export JSON ]                │
│                                                              │
│ Typography                                                   │
│   Display: Fraunces  |  Body: Source Sans 3                  │
│   Specimen sentence                                          │
│   [ Apply to brand ]                                         │
└──────────────────────────────────────────────────────────────┘
```

---

## 5. Asset Library

```
┌─ Assets ─────────────────────────────────────────────────────┐
│ Filters: Project ▾  Brand ▾  Type ▾  ★ Favorites only        │
│ Search [____________________]                                │
│                                                              │
│ Name                 Type        Project       Updated       │
│ Primary mark         Logo        Northwind     today         │
│ Teal system          Palette     Northwind     today         │
│ Voice doc            BrandDoc    Lumen         yesterday     │
│                                                              │
│ Selection: 2  [ Download ZIP ]  [ Delete ]                   │
└──────────────────────────────────────────────────────────────┘
```

List/table preferred over heavy card masonry for Phase 1 density.

---

## 6. Admin

```
┌─ Admin ──────────────────────────────────────────────────────┐
│ Nav: Users | Credits tools | API keys | Audit logs           │
├──────────────────────────────────────────────────────────────┤
│ Users                                                        │
│ Search [________]                                            │
│ Email            Roles     Credits   Plan    Active          │
│ ada@…            User      42        Free    ✓               │
│ ops@…            Admin     999       —       ✓               │
│ → drawer: Adjust credits (±) + reason                        │
│                                                              │
│ API keys                                                     │
│ OpenAI     Configured   ••••a1b2                             │
│ Gemini     Missing      —                                    │
│ Ideogram   Missing      —                                    │
│ Razorpay   Configured   ••••9xyz                             │
│ (Never show full secrets)                                    │
│                                                              │
│ Audit logs                                                   │
│ Time | Actor | Action | Entity | Detail                      │
└──────────────────────────────────────────────────────────────┘
```

---

## 7. Figma AI prompt (premium teal / ink SaaS)

Copy-paste into Figma AI / design assistant:

```
Design a premium B2B SaaS product UI for “AI Brand Identity Studio” — a brand identity generator.

Visual system:
- Color: ink navy base (#0B1220), elevated surfaces (#111827 / #0F172A), primary accent teal (#0F766E) and bright teal highlight (#2DD4BF), text #E5E7EB / muted #94A3B8. Avoid purple, neon glow, and cream/terracotta editorial looks.
- Typography: distinctive pairing — display serif or characterful grotesque for the product wordmark and rare marketing headlines; clean modern grotesk for UI (not Inter/Roboto/Arial). Clear hierarchy, generous line-height in forms.
- Atmosphere: subtle teal grain/gradient mesh in the login full-bleed background only; in-app shell uses soft radial wash behind content, not flat gray.
- Layout: one composition per view. Dashboard first viewport = wordmark in shell + one greeting + one CTA + recent projects. No stat strips, promo pills, floating badges, or card clutter in the hero/dashboard head.
- Components: minimal chrome; cards only when they wrap a primary interaction (project open). Tables for assets/admin. Wizard as a focused single-column flow with a slim step indicator.
- Motion notes (for prototype): fade/slide between wizard steps; progress bar easing on generation; subtle hover lift on project rows only.

Screens to produce (desktop 1440 and mobile 390):
1) Login full-bleed
2) Dashboard
3) Brand wizard steps 1–4 + generating state
4) Generation results (narrative + logo grid + palette strip + typography specimen)
5) Asset library table
6) Admin users + masked API keys

Tone: confident, precise, calm — like a high-end design tool, not a crypto dashboard. High contrast AA for text. Include light empty states with a single CTA. Export a consistent component set: buttons (primary teal / ghost), inputs, tabs, table, toast, modal for credit confirm.
```

---

## 8. Motion checklist (implementation)

| Motion | Where |
| --- | --- |
| Step cross-fade | Wizard Continue/Back |
| Progress bar + status text | Generation waiting |
| Soft list enter | Dashboard recent projects |

Keep motion purposeful; respect `prefers-reduced-motion`.

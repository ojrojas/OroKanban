# Component Patterns

Reusable structural patterns, generalized beyond dashboards. Each pattern states its
elevation tier explicitly (see `tokens.md` → Elevation System) — follow that, not
intuition, since "looks white so it must have a shadow" is the most common way to drift
from the reference.

## Sidebar / vertical nav

- The sidebar itself is **not** a separately elevated card. It's a plain column sitting
  directly on the Tier 0 background (or Tier 0-colored panel with no shadow) — width
  `250px` if fixed.
- Nav items:
  - **Default (inactive)**: Tier N — fully transparent, icon (secondary color) + label
    (`#777777`, Medium). No fill, no shadow.
  - **Hover**: subtle flat background tint (`#F0F0EF`), 200ms transition. Still no shadow.
  - **Active**: promotes to **Tier 2** — its own white pill/card, radius `14px`, shadow
    `0 8px 24px rgba(0,0,0,.04)`, icon `#111111`, label bold. This is the ONLY nav item
    that gets a shadow.
- Bottom of sidebar (optional): 2–3 floating circular action buttons — **Tier 2**, white
  circle (`50%` radius), same soft shadow as the active nav item.

## Top bar

Horizontal row: search — primary CTA — icon buttons — avatar. (Adapt left-side content —
page context, logo, breadcrumbs — depending on the product.)

- **Search bar**: **Tier 1** — near-white fill (`#FFFFFF`–`#FDFDFD`), optional thin
  `#ECECEC` border, radius `18px` or full pill. No box-shadow. Search icon + muted
  placeholder text (e.g. "Search anything...").
- **Primary CTA** ("Create", "New", "Save", etc.): black fill, white bold text, full pill
  radius. Flat — no shadow needed, it's already the highest-contrast element on screen.
  Only one of these per screen.
- **Notification / message / secondary icon buttons**: **Tier 1** — circular, white/
  near-white fill, no shadow, subtle hover background tint. Do not add shadow here even
  though they're circular like the Tier 2 floating buttons — context (top bar utility
  icon vs. sidebar primary action) determines the tier, matching the reference.
- **Avatar**: circular, no shadow, no border needed.

## KPI / stat card

**Tier 2.** Large horizontal card containing 2+ stats side by side, separated by a thin
vertical divider (`#ECECEC`).

Per stat:
- Small label (Medium weight, secondary color)
- Huge number (Bold, 36–44px)
- Delta badge (Tier 1: light tinted pill, no shadow) — arrow + percentage
- Muted caption below the badge (e.g. "vs last month")

## Data / list card (e.g. product list, comments, activity, settings rows)

**Tier 2** for the card itself; rows inside are flat.

- White rounded card (`24px` radius) with shadow, title top (Bold), optional Tier 1
  dropdown filter pill top-right.
- Vertical list of rows, each row:
  - Leading thumbnail (rounded square, `12–16px` radius) or avatar (`50%`) — flat, no
    shadow, just a light gray or image fill
  - Title (Bold/Medium) + subtitle (secondary color) stacked
  - Right-aligned value (price, time, etc.)
  - Optional status badge (Tier 1)
  - Hairline `#ECECEC` divider between rows, none after the last row
- Optional footer button: full-width, **Tier 1** (white/outline, not black — the primary
  CTA lives in the top bar, not repeated here).

## Horizontal avatar/people row

- Row of circular avatars (`52px`), flat (no shadow), name label centered below in small
  Medium text.
- Trailing element: circular outline button (Tier 1, subtle border, no shadow) with an
  arrow icon, e.g. "View all".

## Analytics / chart card

**Tier 2.** Large white card, title top-left (Bold), Tier 1 period-filter dropdown
top-right.

- Chart area: minimal bar chart, bars muted gray by default, one bar highlighted in the
  accent color to draw the eye.
- Floating tooltip above the highlighted bar: small white card, its own Tier 2 shadow
  (this is a nested elevated surface — acceptable since it's genuinely floating above the
  chart, not just another flat label).
- Optional: a large, very light-gray oversized number as a background-style accent
  (low-contrast, doesn't compete with primary headings).

## Badges

**Tier 1 — never a shadow.** Pill-shaped (`999px` radius), small (12–13px Medium text),
padded ~`4px 8px`–`6px 12px`. Light tinted background matching the semantic color, full-
saturation color for the text (e.g. light green bg + green text for positive/active,
light red bg + red text for negative, light gray bg + gray text for neutral/offline).

## Buttons

- **Primary** (one per screen): black fill, white bold text, full pill radius. Flat — no
  shadow.
- **Secondary**: Tier 1 — white/transparent fill, `#ECECEC` border or none, `#111111` or
  `#777777` text, same pill radius, no shadow.
- **Icon-only, top-bar/utility context**: Tier 1, no shadow.
- **Icon-only, floating/primary-action context** (e.g. sidebar bottom buttons): Tier 2,
  with shadow.

## Cards (general, Tier 2)

- `24px` radius (20–28px acceptable range), white fill, resting shadow
  `0 8px 24px rgba(0,0,0,.04)`. Borderless — the shadow alone separates it from the
  background, don't add a border on top of the shadow.
- On hover (if interactive): elevate to `0 12px 32px rgba(0,0,0,.06)`, `200ms ease-in-out`,
  optionally slight `translateY(-2px)`.
- Generous internal padding (24–32px) — cards must "breathe," never crowd content
  edge-to-edge.

## Applying this beyond dashboards

- **Forms / settings screens**: form container can be a single Tier 2 card; individual
  inputs are Tier 1 (white/near-white, thin border, no shadow); the submit button is the
  one primary CTA (flat black pill).
- **Landing / marketing sections**: feature cards use Tier 2; small tags/labels use
  Tier 1; nav bar links behave like the sidebar's inactive/active/hover states (flat,
  tint-on-hover, elevated-pill on active, e.g. for tabs).
- **Login / auth**: the auth card itself is Tier 2 (elevated, centered), inputs inside
  are Tier 1, the submit button is the flat black primary CTA.

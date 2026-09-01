# Design Tokens

Default token set for the minimal aesthetic. Swap values for a different mood, but keep
the same *categories* — especially the elevation tiers below — so the system stays
coherent and faithful to the reference.

## Elevation System (read this first)

Verified pixel-by-pixel against the reference screenshot. There are exactly **three**
surface types. Getting this distinction right matters more than any single color value.

### Tier 0 — Background (flat, no shadow, no border needed)
The page/app background itself. Everything sits on top of this.
- Color: `#F7F7F6`
- Never gets a shadow — it IS the surface everything else is elevated from (or not).

### Tier 1 — Flat surface (white/near-white, NO shadow)
Distinguished from the background purely by fill color (and sometimes a hairline
border) — never by shadow. In the reference, these sample almost pure white
(`#FDFDFD`–`#FFFFFF`), just barely lifted off the `#F7F7F6` background by contrast alone.

Applies to:
- Search bar
- Filter/dropdown pills (e.g. "Last 7 days")
- Badges (status pills, delta pills)
- Notification/message icon buttons in the top bar
- Input fields generally

CSS: `background: #FFFFFF; border: 1px solid #ECECEC;` (border optional/very subtle —
omit it entirely for badges and dropdown pills, keep it for search/inputs). **No
box-shadow.**

### Tier 2 — Elevated surface (white, WITH soft shadow)
Reserved for a short, specific list of elements that need to visually "float" above
the background. This is the only tier that gets `box-shadow`.

Applies to:
- Content cards (KPI card, chart card, list cards like "Popular products"/"Comments")
- The ACTIVE nav item, rendered as its own white pill (not just a color change)
- Floating circular action buttons (e.g. bottom-of-sidebar utility buttons)

CSS:
```css
background: #FFFFFF;
border-radius: 24px; /* or 14px for nav-item-sized pills, 50% for circular buttons */
box-shadow: 0 8px 24px rgba(0,0,0,.04);
```
On hover/interactive state: `box-shadow: 0 12px 32px rgba(0,0,0,.06);` over `200ms
ease-in-out`, optionally `translateY(-2px)`.

### Tier N — Nav item default state (no fill, no shadow, no border)
Inactive sidebar/nav items are **fully transparent** — just icon + label in secondary
text color directly on the Tier 0 background. On hover, apply a very subtle flat gray
background tint (`#F0F0EF` or similar, barely darker than the page background) — still
**no shadow**. Only the active state promotes to Tier 2 (elevated white pill).

### Common mistake to avoid
Do not give every white-ish element a `box-shadow`. If in doubt, ask: "does this need to
look like it's floating above the page, or just visually distinct?" Floating → Tier 2.
Distinct-but-flat → Tier 1. This single check preserves fidelity to the reference far more
than any color tweak.

---

## Color Palette

| Role | Hex | Usage |
|---|---|---|
| Background | `#F7F7F6` | Page/app background (Tier 0) |
| Card / elevated surface | `#FFFFFF` | Tier 2 surfaces |
| Flat surface | `#FFFFFF`–`#FDFDFD` | Tier 1 surfaces (search, pills, badges) |
| Border | `#ECECEC` | Hairline borders on Tier 1 inputs, internal dividers |
| Primary text | `#111111` | Headings, high-contrast titles, primary buttons |
| Secondary text | `#777777` | Body copy, subtitles, inactive nav labels |
| Muted text | `#A9A9A9` | Timestamps, placeholders, least-important labels |
| Green | `#63D471` | Positive deltas, "Active" status badges (text on light green tint bg) |
| Red | `#F26B6B` | Negative deltas, warnings (text on light red tint bg) |
| Black button | `#111111` | Single primary CTA per screen |

Rules:
- Minimal palette — resist adding new hues. Green/red are reserved for semantic state
  (up/down, active/offline), not decoration.
- Only **one primary CTA** (black pill button) per screen/section.
- No gradients, no neumorphism, no heavy color blocking.
- Badges use a *light tint* of the semantic color as background with the full-saturation
  color as text (e.g. `background:#E8F9EC; color:#2FA84A;` for green), not solid fills.

## Typography

- Font family: **Inter**
- Headings: Bold, high contrast (`#111111`)
- Body: Regular
- Small labels (badges, nav labels): Medium
- Secondary/supporting text: `#777777`, Regular

Suggested scale (adapt to context):
| Use | Size | Weight |
|---|---|---|
| Page title | 28–32px | Bold |
| Section title (card headers) | 16–18px | Bold |
| Large stat/number | 36–44px | Bold |
| Body text | 14px | Regular |
| Small label / badge text | 12–13px | Medium |
| Micro / timestamp | 12px | Regular, muted color |

## Spacing

- **8px base grid.** All padding, gaps, and margins should be multiples of 8.
- Page/outer padding: `32px`
- Gap between major layout regions: `32px`
- Gap between stacked widgets in a column: `24px`
- Card internal padding: `24–32px`
- Nav item padding: `~11px 12px`

## Border Radius

| Element | Radius |
|---|---|
| Content cards (Tier 2) | `24px` (range 20–28px) |
| Nav item / active pill | `14px` |
| Buttons | `999px` (full pill) |
| Inputs / search bars | `18px` (or full pill, both appear in reference) |
| Avatars / circular buttons | `50%` |
| Small thumbnails (product images, row icons) | `12–16px` |

## Motion

- Duration: `200ms`, Easing: `ease-in-out`
- Use for: hover elevation (Tier 2 shadow increase + optional slight lift), background
  color transitions on nav hover, button state changes.
- No flashy effects — no bounces, no large scale transforms, no spinning.

## Icons

- Library: **Lucide Icons**, stroke width `1.75`, outline only, never filled.
- One icon per nav item / list row — no icon clutter.

## Borders

- `#ECECEC` at `1px` for internal dividers within a card (e.g. between KPI stats, between
  list rows) and optionally around Tier 1 inputs.
- Do not use borders as a substitute for the Tier 2 shadow on content cards — cards should
  be borderless and rely on shadow alone to separate from the background.

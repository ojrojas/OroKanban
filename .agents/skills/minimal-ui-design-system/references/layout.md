# Layout & Grid

These grid/spacing rules are the default for a nav+content style screen (dashboard,
admin panel, internal tool). For other screen types, keep the 8px spacing system and
Tier 2 card treatment, but adapt the structure itself — see "Other layout shapes" below.

## Desktop structure (default: 1440px design width)

```
┌────────────────────────────────────────────────────────────┐
│ Sidebar │ Top Bar                                          │
│         │                                                  │
│ 250px   │ Main Content (flexible)     Right Panel (340px)  │
│         │                                                  │
└────────────────────────────────────────────────────────────┘
```

- **Sidebar**: fixed width `250px`.
- **Main content**: flexible width, holds page title/subtitle, KPI section,
  primary content cards (lists, charts).
- **Right panel**: fixed width `340px`, stacked widgets with `24px` gap
  between them.
- **Gap** between the three regions: `32px`.
- **Outer padding**: `32px` around the whole layout.

## Page header (within main content)

- Page title (Bold, large, e.g. "Dashboard") + subtitle directly under it
  (secondary color, e.g. "Overview").
- Keep title/subtitle stacked tightly (4–8px gap), then `24–32px` before the
  next section.

## Responsive notes (when asked to adapt beyond desktop)

- Collapse right panel below the main content on narrower viewports (tablet).
- Collapse sidebar to icon-only rail or an overlay drawer on mobile; keep the
  same active-item white-card treatment.
- Maintain the 8px spacing grid at every breakpoint — reduce multiples (e.g.
  32px page padding → 16px on mobile) rather than breaking the grid.

## Other layout shapes (non-dashboard screens)

- **Single centered column** (forms, login, settings detail): max-width ~480–640px,
  centered on the Tier 0 background, `32px` page padding, content in one or more Tier 2
  cards stacked with `24px` gap.
- **Marketing/landing**: full-width sections on the Tier 0 (or alternating Tier 0/white)
  background, generous vertical rhythm (multiples of `32px`+ between sections), feature
  content in Tier 2 cards within a max-width container (~1200px).
- **Two-column detail view** (e.g. item + related info, no sidebar): flexible main column
  + fixed-width side column (e.g. `340px`), same `32px` gap as the dashboard's right
  panel.

Whatever the shape, keep: 8px spacing grid, Tier 0 background, Tier 2 cards for content
groupings, Tier 1 for inputs/badges/pills, one primary CTA.

## General UX principles to preserve at any breakpoint

- Strong visual hierarchy — one clear focal point per section.
- Generous whitespace; never let cards touch or crowd.
- Group related information naturally (don't scatter related stats/actions).
- Only one primary CTA visible at a time.
- Calm, premium feel over density — if in doubt, remove an element rather
  than add one.

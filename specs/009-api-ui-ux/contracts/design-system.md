# Contract: Design System

**Skill**: `.agents/skills/minimal-ui-design-system` (`references/tokens.md`, `components.md`, `layout.md`) — gobierna todo UI.

## Tokens (`shared/tokens/tokens.scss`)

```scss
:root {
  --bg: #F7F7F6; // Tier 0
  --card-bg: #FFFFFF;
  --flat-bg: #FFFFFF; // Tier 1 (alternativa #FDFDFD)
  --border: #ECECEC;
  --text-primary: #111111;
  --text-secondary: #777777;
  --text-muted: #A9A9A9;
  --green-bg: #E8F9EC; --green-text: #2FA84A; // badge tint
  --red-bg: #FCE8E8; --red-text: #C0392B;
  --black: #111111;
  --radius-card: 24px;
  --radius-pill: 14px;
  --radius-button: 999px;
  --radius-input: 18px;
  --shadow-card: 0 8px 24px rgba(0,0,0,.04);
  --shadow-hover: 0 12px 32px rgba(0,0,0,.06);
  --font: 'Inter', sans-serif;
  --grid: 8px;
}
```

## Elevación (regla más importante)

| Tier | Fondo | Shadow | Aplica |
|------|-------|--------|--------|
| 0 Background | `var(--bg)` | none | página |
| 1 Flat | `var(--flat-bg)` + opcional `border var(--border)` | **none** | `search-bar`, `filter-pill`, `badge`, `input`, icon buttons topBar, dropdown |
| 2 Elevated | `var(--card-bg)` | `var(--shadow-card)` | `kpi-card`, `list-card`, `chart-card`, `active nav pill`, `modal`, FABs |
| N Nav inactivo | transparent | none | nav item default `color var(--text-secondary)` |

Hover Tier 2: `shadow-hover` + `translateY(-2px)` `200ms ease-in-out`. Hover Tier 1/N: `bg #F0F0EF` sin shadow.

Fallo común: dar sombra a todo lo blanco — solo Tier 2 lleva `box-shadow`.

## Componentes (shared/ui, cada uno documenta su tier)

- `sidebar-nav` (contenedor 250px Tier 0, item N→active Tier 2 pill 14px)
- `top-bar` (search Tier 1 `18px` + CTA black flat `999px` + icon buttons Tier 1 + avatar)
- `kpi-card` (Tier 2 `24px`, divider #ECECEC, delta badge Tier 1)
- `list-card` (Tier 2 `24px`, header Bold, rows flat, hairline #ECECEC, badge Tier 1, footer button Tier 1)
- `chart-card` (Tier 2, filter pill Tier 1, tooltip Tier 2 anidado)
- `badge` (Tier 1 `999px`, `6px 12px`, `12px Medium`, tint bg + solid text)
- `button` (primary `bg var(--black) color #FFF 999px` flat; secondary `bg #FFF border #ECECEC 999px` Tier 1)
- `input` (Tier 1 `18px`, `border #ECECEC`, no shadow)
- `search-bar` (Tier 1 pill `18px`, icon + muted placeholder)
- `filter-pill` (Tier 1 `999px`, sin shadow)
- `pagination` (Tier 1 buttons)
- `avatar-row` (flat 52px, no shadow)
- `timeline` (card Tier 2, items flat)
- `modal` (Tier 2 `24px` + shadow)

## Layout (`references/layout.md`)

- Outer `32px`, gap regiones `32px`, gap widgets `24px`, card padding `24-32px`, nav item `11px 12px` — todo múltiplo `8px`.
- Grid: `sidebar 250px` + `content flex-1` con `max-width` y `8px` grid. Mobile: sidebar colapsado hamburger, contenido single column.

## Auditoría

Lint `no-hardcoded-color/shadow/spacing` — solo vars. Review checklist: cada nuevo componente declara su Tier.


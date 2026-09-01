---
name: minimal-ui-design-system
description: >
  Design-system reference for a premium, Apple-inspired minimal UI aesthetic — for ANY
  interface, not just dashboards: admin panels, marketing sites, forms, settings, mobile
  screens, e-commerce, login, profile pages, landing pages, or any UI/UX task. Gives design
  tokens (colors, spacing, radius, type) and a precise ELEVATION SYSTEM (flat vs.
  shadow-elevated surfaces) reverse-engineered from a reference screenshot, plus component
  patterns (nav, top bar, KPI cards, lists, widgets, buttons, badges). Use whenever asked
  to design, build, mock up, or code ANY interface, even without saying "Apple style" or
  naming this skill. Defaults to a Linear/Vercel/Stripe-inspired look; every token is
  adaptable for a different mood while keeping the same elevation logic. Consult before
  writing UI HTML/CSS/React so shadows and spacing stay precise instead of guessed.
---

# Minimal UI Design System

A reference design system for a premium, minimal interface aesthetic — usable for ANY
kind of UI, not only admin dashboards. Use it as the starting point for any interface
design or build task, then adapt tokens as needed.

## How to use this skill

1. **Read `references/tokens.md`** — colors, typography, spacing, radius, and especially
   the **Elevation System** section. This is the most important part to get right: which
   surfaces get a shadow and which don't.
2. **Read `references/components.md`** — concrete patterns (nav items, top bar, cards,
   buttons, badges, lists). Each pattern states explicitly whether it's a flat surface or
   an elevated surface — follow that exactly.
3. **Read `references/layout.md`** — grid/spacing rules for structuring a screen (not
   dashboard-specific — applies to any screen with a nav + content area, or adapt further
   for single-column layouts like forms/landing pages).
4. Build the requested UI using these tokens and patterns, adapting only what the user
   explicitly asks to change (mood, density, colors, layout shape).

## Default aesthetic (one-line summary)

Extremely clean, high-whitespace, soft off-white background, large rounded corners, thin
hairline borders, and **shadows used sparingly and deliberately** — most surfaces are flat
(distinguished by fill color/border alone), only specific elevated elements (cards, active
nav state, floating action buttons) get a soft diffuse shadow. Inter typeface, muted
minimal palette, one accent color per semantic state (green = positive, red = negative,
black = primary action). Inspired by Linear, Vercel, Apple, Framer, Arc Browser, Stripe.

## The most important rule: not everything white has a shadow

This is the detail most designs get wrong. See `references/tokens.md` → Elevation System
for the full breakdown, but the short version:

- **Flat white/near-white** (no shadow): search bars, filter/dropdown pills, badges,
  inactive nav items, hairline-bordered inputs. Distinguished from the background by fill
  color and/or a 1px border only.
- **Elevated white** (soft shadow): content cards (KPI cards, list cards, chart cards),
  the ACTIVE nav item (rendered as its own floating pill), floating circular action
  buttons. These are the only things that should carry `box-shadow` in this system.

Applying a shadow to every white element is the single most common way to break fidelity
with the reference aesthetic — resist it.

## Adapting the system

This is a default, not a hard requirement:

- **Different mood** (dark mode, playful, brutalist, dense) → keep the elevation *logic*
  (flat vs. elevated tiers) but change shadow color/opacity, radius, and palette.
- **Non-dashboard UI** (landing page, login form, settings, e-commerce, mobile) → the same
  tokens, elevation rules, and card/badge/button patterns apply; use `layout.md`'s spacing
  system even without a sidebar/multi-column structure — a single centered column with the
  same 8px grid and card treatment works identically.
- **Different brand colors** → replace accent/black-button colors; keep the flat-vs-elevated
  surface logic unless told otherwise.

Always state which defaults you kept vs. changed so the user can course-correct.

## Quick reference (most-used values)

| Token | Value |
|---|---|
| Background | `#F7F7F6` |
| Card (elevated) | `#FFFFFF` + shadow |
| Flat surface (search, pills, badges) | `#FFFFFF`–`#FDFDFD`, no shadow |
| Border | `#ECECEC` |
| Primary text | `#111111` |
| Secondary text | `#777777` |
| Muted text | `#A9A9A9` |
| Green (positive) | `#63D471` |
| Red (negative) | `#F26B6B` |
| Card radius | `24px` |
| Nav item / pill radius | `14px` |
| Button radius | `999px` (pill) |
| Input radius | `18px` |
| Card shadow (resting) | `0 8px 24px rgba(0,0,0,.04)` |
| Card shadow (hover) | `0 12px 32px rgba(0,0,0,.06)` |
| Font | Inter (Bold headings / Regular body / Medium small labels) |
| Transition | `200ms ease-in-out` |
| Spacing base | `8px` grid |

See `references/tokens.md` → Elevation System before generating any code with shadows.

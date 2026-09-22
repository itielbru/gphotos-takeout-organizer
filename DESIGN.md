---
name: Google Photos Takeout Organizer — Landing
description: A matte black album page with one mounted print and the quartz date burn-in returned to its corner.
colors:
  ink: "#0b0b0d"
  ink-2: "#141416"
  ink-3: "#1e1e22"
  paper: "#f6f3ee"
  paper-2: "#cfc9bf"
  paper-3: "#8d877d"
  burn: "#ff8a1e"
  burn-lift: "#ff9d3f"
  burn-glow: "rgba(255, 138, 30, .55)"
typography:
  display:
    fontFamily: "Bricolage Grotesque, Rubik, system-ui, sans-serif"
    fontSize: "clamp(2.2rem, 5.2vw, 4.6rem)"
    fontWeight: 700
    lineHeight: 0.98
    letterSpacing: "-0.025em"
  headline:
    fontFamily: "Bricolage Grotesque, Rubik, system-ui, sans-serif"
    fontSize: "clamp(1.7rem, 3.2vw, 2.6rem)"
    fontWeight: 700
    lineHeight: 1.05
    letterSpacing: "-0.02em"
  title:
    fontFamily: "Bricolage Grotesque, Rubik, system-ui, sans-serif"
    fontSize: "1.25rem"
    fontWeight: 600
    lineHeight: 1.3
    letterSpacing: "-0.01em"
  body:
    fontFamily: "Hanken Grotesk, Rubik, system-ui, sans-serif"
    fontSize: "clamp(16px, 1.05vw + 12px, 19px)"
    fontWeight: 400
    lineHeight: 1.55
    letterSpacing: "normal"
  fine:
    fontFamily: "Hanken Grotesk, Rubik, system-ui, sans-serif"
    fontSize: "0.92rem"
    fontWeight: 400
    lineHeight: 1.55
    letterSpacing: "normal"
  label:
    fontFamily: "Hanken Grotesk, Rubik, system-ui, sans-serif"
    fontSize: "0.8rem"
    fontWeight: 400
    lineHeight: 1.4
    letterSpacing: "0.02em"
  mono:
    fontFamily: "ui-monospace, Cascadia Mono, Consolas, monospace"
    fontSize: "0.92rem"
    fontWeight: 400
    lineHeight: 1.6
    letterSpacing: "normal"
rounded:
  xs: "2px"
  sm: "3px"
  md: "6px"
  pill: "999px"
spacing:
  sm: "12px"
  md: "18px"
  lg: "22px"
  xl: "40px"
  xxl: "64px"
  gutter: "clamp(16px, 4vw, 48px)"
  section: "clamp(48px, 9vh, 112px)"
components:
  button-primary:
    backgroundColor: "{colors.burn}"
    textColor: "{colors.ink}"
    rounded: "{rounded.md}"
    padding: "15px 22px"
  button-primary-hover:
    backgroundColor: "{colors.burn-lift}"
    textColor: "{colors.ink}"
  button-ghost:
    backgroundColor: "transparent"
    textColor: "{colors.paper}"
    rounded: "{rounded.md}"
    padding: "14px 18px"
  lang-pill:
    backgroundColor: "transparent"
    textColor: "{colors.paper-2}"
    rounded: "{rounded.pill}"
    padding: "6px 12px"
  print:
    backgroundColor: "{colors.paper}"
    rounded: "{rounded.xs}"
    padding: "4.2%"
  photo-corner:
    backgroundColor: "#2c2c31"
    size: "38px"
  burn-digits:
    textColor: "{colors.burn}"
    height: "clamp(18px, 3.6vw, 34px)"
  code-inline:
    backgroundColor: "{colors.ink-2}"
    textColor: "{colors.paper}"
    typography: "{typography.mono}"
    rounded: "{rounded.sm}"
    padding: "0.1em 0.4em"
  code-block:
    backgroundColor: "{colors.ink-2}"
    textColor: "{colors.paper}"
    typography: "{typography.mono}"
    rounded: "{rounded.md}"
    padding: "18px 20px"
---

# Design System: Google Photos Takeout Organizer — Landing

**Scope.** This system covers the GitHub Pages landing page only: `index.html` (English, LTR),
`he/index.html` (Hebrew, RTL), and the single shared stylesheet `docs/assets/landing.css` plus
`docs/assets/landing.js`. The rest of the repository is a C#/.NET Windows application (WinUI 3 +
CLI) with its own dark teal product palette; it is **not** part of this visual world and nothing
here governs it. Recorded from the built code after the fact.

## Overview

**Creative North Star: "The Mounted Print"**

The page is a matte black album page seen straight on, with exactly one 4×6 lab print mounted at
its corners, tilted a degree and a half off square. The product's whole claim — Takeout strips the
date off your photos and this puts it back — is staged as a physical event: the orange quartz
burn-in in the print's lower right reads today's date for a second and a half, flickers, and
resolves to 22 July 2023. That flip is the only authored moment on the page and everything else
is built to leave room for it.

The surface is deliberately poor in objects. There are no cards, no panels, no tinted containers
and no boxes: sections are separated by 1px hairlines the color of the page's own chrome, and
content sits directly on the black. Depth exists in one place — under the print — because a
physical print is the only thing here that is supposed to be physical. A fractal-noise grain
layer sits over the whole page at 11% alpha in screen blend, which reads as album-paper matte
rather than texture-for-its-own-sake.

Density is editorial rather than marketing: a 62ch measure on every prose block, a 1120px
container, tabular figures wherever a number is evidence, and long technical strings (EXIF tag
names, CLI flags, timestamps) shown raw instead of paraphrased. The world refuses the centered-
hero-plus-feature-grid tool page outright — the hero is asymmetric two-column, the feature list
is a numbered roll log, and the comparison is a real table. The page carries the product's own
honesty commitment visually: the unsigned-installer caveat sits in the hero's fine print, one
line under the download button, not buried at the bottom.

**Key Characteristics:**

- Matte black album page, glossy print white, one orange accent — nothing else
- Rules, never cards; a single object casts a single shadow
- Two-face type: Bricolage Grotesque display over Hanken Grotesk body; Rubik for Hebrew
- Tabular numerals wherever a number is evidence
- One authored motion beat (the burn-in flip); everything else is a 0.2–0.8s ease-out
- Bilingual by construction: one stylesheet, logical properties, three deliberate physical exceptions

## Colors

Three materials and one light source: the black of an album page, the white of a lab print, and
the orange of a quartz camera's date burn-in.

### Primary

- **Quartz Burn Orange** (`{colors.burn}`): The date burn-in's own color and the page's only
  bright element. It fills exactly one surface — the download button — and otherwise appears as
  light or as ink: the seven-segment digits and their glow, the roll log's step numbers, the
  flag token inside the CLI snippet, link hover, text selection, and the focus ring. It is never
  a background for text blocks, never a border, and never a second button.
- **Burn Lift** (`{colors.burn-lift}`): The download button's hover state only, one step brighter
  and slightly desaturated. It exists nowhere else on the page.
- **Burn Glow** (`{colors.burn-glow}`): The translucent form of the accent, used only as the
  drop-shadow bloom around the seven-segment digits so they read as emitted light rather than
  printed ink.

### Neutral

- **Album Black** (`{colors.ink}`): The page itself — `html`, `body`, the scrollbar track, and
  the browser theme color. Also the text color that sits *on* the orange button.
- **Raised Page** (`{colors.ink-2}`): The only tinted fill in the system, reserved for code —
  inline `code` and `pre` blocks. It is not a card background and must not become one.
- **Chrome Line** (`{colors.ink-3}`): Every rule, divider, table border, ghost-button border,
  language-pill border and the scrollbar thumb. This is the color that does the work cards would
  otherwise do.
- **Print White** (`{colors.paper}`): The lab print's glossy border, and the page's primary text
  color — headlines, body, emphasized table cells, values in the metadata strip.
- **Print White, Second Voice** (`{colors.paper-2}`): Supporting prose — the lede, section subs,
  paragraph text inside the log and the two-up, header nav at rest, bolded terms in the honest
  close. The workhorse for "read this, but read the headline first."
- **Print White, Third Voice** (`{colors.paper-3}`): Micro-labels, captions, table column heads,
  footnotes, the footer, and default link underlines. The quietest legible step.

### Named Rules

**The Single Burn Rule.** The accent fills exactly one surface per page — the download button.
Everywhere else it is light, ink or a 1px mark. A second orange-filled surface means one of them
is wrong.

**The Paper Ladder Rule.** Text importance descends `paper` → `paper-2` → `paper-3` and page
chrome ascends `ink` → `ink-2` → `ink-3`. There is no fourth step and no mid-gray. If a value
needs to sit between two rungs, the hierarchy is wrong, not the ladder.

**The No Tint Rule.** `ink-2` is code's background and nothing else. Sections, lists, tables and
callouts sit directly on `ink`. A tinted container is how this world turns into a tool page.

## Typography

**Display Font:** Bricolage Grotesque (600/700, optical sizing 12–96)
**Body Font:** Hanken Grotesk (400/500/600)
**Hebrew Font:** Rubik (400/500/600/700) — replaces *both* faces under `html[lang="he"]`
**Mono Font:** `ui-monospace`, Cascadia Mono, Consolas

**Character:** Bricolage's variable-width grotesque is set tight and heavy for headlines — negative
tracking, sub-1.0 leading — so titles read as stamped rather than typeset. Hanken Grotesk under it
is neutral and slightly warm, doing the long technical prose without competing. Hebrew collapses
the pairing into a single Rubik voice, which is the honest move: no Hebrew face in this project's
budget pairs with Bricolage, and a mismatched substitute would be worse than one good face used at
two weights.

### Hierarchy

- **Display** (700, `clamp(2.2rem, 5.2vw, 4.6rem)`, 0.98 line-height, -0.025em): The single `h1`.
  Balanced wrapping, set to break across two or three lines by design.
- **Headline** (700, `clamp(1.7rem, 3.2vw, 2.6rem)`, 1.05, -0.02em): Section openers. Always
  followed by a `sub` paragraph at the 62ch measure.
- **Title** (600, 1.25rem, -0.01em): Roll-log step names and the two-up column heads.
- **Body** (400, `clamp(16px, 1.05vw + 12px, 19px)`, 1.55): All prose, capped at a 62ch measure
  with `text-wrap: pretty`. The fluid floor of 16px is a hard minimum.
- **Fine** (400, 0.92rem): Header and footer navigation, the hero's caveat line, ghost-button
  label, table body, and table footnotes. The "read it if you care" tier.
- **Label** (400, 0.8rem, +0.02em, sentence case): Metadata-strip keys, table column heads, the
  burn-in caption. Tracking opens slightly at this size; case never changes.
- **Mono** (400, 0.92rem/0.92em): CLI snippets, file names, EXIF tag names, folder paths. Always
  forced `direction: ltr`, in both languages.

### Named Rules

**The Two-Face Rule.** Bricolage for display, headline and title; Hanken for everything else;
monospace only for literal machine strings. Hebrew swaps both faces to Rubik at once and keeps the
same weights. A third family is a defect.

**The Lowercase Label Rule.** Micro-labels are sentence case at 0.8rem with +0.02em tracking. This
world never uses uppercase-with-wide-tracking for small text — no eyebrows, no kickers, no
all-caps section tags.

**The Tabular Rule.** Any number that is evidence gets `font-variant-numeric: tabular-nums` — the
metadata strip, the roll log's step numbers, the burn-in caption. Figures in running prose do not.

## Layout

A single 1120px content column centered in a fluid gutter of `clamp(16px, 4vw, 48px)`; the header,
hero and footer widen to `1120px + 2 × gutter` so their edges align with the sections' inner text
rather than with the sections' padding. Vertical rhythm is one value: sections pad
`clamp(48px, 9vh, 112px)` top and bottom, which collapses sensibly on short viewports because it
is keyed to `vh`.

The hero is deliberately asymmetric — `minmax(0, 1.05fr) minmax(0, 1fr)`, text slightly wider than
the print, gap `clamp(28px, 5vw, 72px)`, vertically centered. Below it the metadata strip is a
four-column grid banded by hairlines above and below; the roll log is a three-column grid
(`2.2rem` step number / `22rem` title / remainder for prose) with a hairline under each row; the
two-up and the honest close are two-column grids with a 64px column gap. Every grid column is
declared `minmax(0, …)` and every child carries `min-width: 0`, which is what keeps the long CLI
string and the wide table from blowing out the page.

Prose is capped at a 62ch measure everywhere it appears — lede, section subs, the hero's fine
print. The comparison table is the one element allowed to exceed the viewport: it keeps a 720px
minimum width inside a horizontally scrollable wrapper rather than wrapping into illegibility.

**Responsive steps** are three, and each one removes rather than rearranges:

- **≤900px** — hero collapses to one column and the print jumps *above* the headline (`order: -1`);
  the metadata strip drops to two columns; two-up and close go single-column; the roll log drops to
  two columns with prose reflowing under the title.
- **≤640px** — secondary header nav links are hidden; the language switch and GitHub link survive.
- **≤480px** — header type steps down to 0.84rem, buttons go full-width, the metadata strip
  becomes a single column.

**Bidirectionality.** One stylesheet serves both languages. Layout is written in logical terms
(`text-align: start`, symmetric auto margins, grid), so RTL falls out of `dir="rtl"` without a
mirrored stylesheet. Three things are pinned physically on purpose: the burn-in stays bottom-right
(`html[lang="he"] .burn { right: 4.5%; left: auto }`), code blocks stay `direction: ltr` with
left alignment, and every inline Latin or numeric run in the Hebrew page is wrapped in `dir="ltr"`.

### Named Rules

**The Rule, Not The Card Rule.** Structure is expressed with 1px `ink-3` hairlines and grid gaps.
No section, list item, table or column gets a background, a border box, or a radius. If a group
needs separating, it gets a line.

**The Physical Print Rule.** The print and its burn-in are a photograph, not an interface. They do
not mirror under RTL, they do not reflow, and their geometry is identical in both languages.
Everything else mirrors.

**The 62ch Rule.** Every prose block carries `max-width: var(--measure)` (62ch). A paragraph that
runs the full 1120px container is a defect.

## Elevation & Depth

This system is flat by construction with exactly one exception. Sections, lists, tables and
navigation have no shadow, no border box and no tonal card — separation comes from hairlines and
from the paper/ink text ladder. Depth is spent entirely on the mounted print, which stacks three
shadows at once (a long ambient drop, a tighter contact shadow, and a 1px inset that reads as the
print's cut edge) so it sits convincingly above the album page. The orange button carries a small
dark drop shadow that deepens on hover — not to look raised, but so a bright orange block does not
appear pasted onto black. The seven-segment digits use a double `drop-shadow` bloom instead of a
box shadow, which is the difference between printed ink and emitted light.

A full-page grain overlay (`feTurbulence`, 11% alpha, `mix-blend-mode: screen`, fixed, 160px tile)
sits above the background and below content at `z-index: 0`, with `pointer-events: none`. It is
the page's material, not a decoration, and it must stay under ~6% perceived lift.

### Shadow Vocabulary

- **Print rest** (`0 30px 60px -20px rgba(0,0,0,.85), 0 12px 24px -12px rgba(0,0,0,.6), inset 0 0 0 1px rgba(0,0,0,.06)`):
  The mounted print at rest. The only three-layer shadow in the system.
- **Corner lift** (`0 2px 4px rgba(0,0,0,.6)`): Under each photo corner, so the corners read as
  separate paper on top of the print.
- **Button rest** (`0 8px 20px -10px rgba(0,0,0,.9)`) / **Button hover**
  (`0 14px 26px -10px rgba(0,0,0,.9)`): Seats the accent button against the black.
- **Burn bloom** (`drop-shadow(0 0 6px var(--burn-glow)) drop-shadow(0 0 1px rgba(255,138,30,.9))`):
  The quartz digits' emission. Never applied to text.

### Named Rules

**The One Object Rule.** The print is the only element in this world that casts a real shadow. A
second shadowed object means the page has grown a card.

## Shapes

Corners are near-square throughout: the print sits at 2px (a lab print's cut edge, not a rounded
card), inline code at 3px, buttons and code blocks at 6px, the focus ring at 2px. The only fully
round shape on the page is the language pill in the header (999px), and its roundness is the
signal that it is a toggle rather than a link.

The page's recurring geometry is the rectangle and the line: a 1px `ink-3` hairline above and below
the metadata strip, under every roll-log row, under every table row, above the closing section, and
under each item in the caveat list. Against that grid of straight lines, the print's **-1.4° tilt**
is the single deliberate break in alignment — the reason the page reads as an album rather than a
layout — and it eases to -0.4° on hover, as if the print were being straightened by a hand. The
four photo corners are clip-path triangles with a diagonal highlight seam, overhanging the print's
edge by 8px so they read as mounted on top of it.

## Components

### Buttons

- **Shape:** Slightly softened rectangle (6px radius); never pill, never square.
- **Primary:** Burn orange fill with album-black label (`{components.button-primary}`), display
  face at 700 weight, 1rem, -0.01em, with a 20px inline stroke-SVG arrow at a 10px gap. There is
  exactly one per page — the installer download.
- **Hover / Focus:** Background lifts to `{colors.burn-lift}`, `translateY(-1px)`, shadow deepens,
  all over 0.35s on the page's ease-out curve; `:active` returns to `translateY(0)`. Focus is the
  global 2px burn ring at 3px offset.
- **Ghost:** Transparent with a 1px `ink-3` border and `paper` label at 0.92rem/600 and slightly
  tighter padding (`14px 18px`), so it reads as secondary by weight and size rather than by a
  different color. On hover only its border brightens to `paper-3`; the label does not change.
- **Full-width below 480px:** both variants stretch and center their content.

### Navigation

- **Header:** A flex row — wordmark left at 600 weight, links right at 22px gaps, all 0.92rem.
  Links rest at `paper-2` and go to `paper` on hover, with no underline and no active state (this
  is a single-page site; anchors are not "pages"). Links wrap is prevented by `white-space: nowrap`.
- **Language pill:** The one round element — 1px `ink-3` border, 999px radius, `6px 12px` — and the
  one nav item that survives every breakpoint, alongside GitHub.
- **Footer:** Same flex logic at `paper-3`, links at `paper-2` going to `paper` with an underline
  appearing on hover — the inverse of the header, because here the underline is the affordance.
- **Global links in prose:** `paper` text with a `paper-3` underline at 1px and 0.18em offset;
  both flip to burn on hover.

### Metadata Strip (signature)

A four-column band, hairline above and below, that shows one photo's metadata resolving from
Takeout JSON to written EXIF. Each cell is a 0.8rem `paper-3` label over a 1rem `paper` value at
600 weight, all in tabular figures. It is the page's proof-as-data counterpart to the print's
proof-as-image, and it carries `role="group"` with a label. Values are `dir="ltr"` in both
languages. Collapses 4 → 2 → 1 column.

### Roll Log (signature)

The "what it does" list, built as a numbered film-roll log rather than a feature grid: a hairline
above the list and under each row, a burn-orange tabular step number in a 2.2rem column, a 1.25rem
display-face title in a 22rem column, and `paper-2` prose filling the rest. Baseline-aligned. No
icons, no cards, no bullets. On mobile the prose drops under the title and the number column stays.

### Mounted Print (signature)

The hero's single object: a `paper` frame with 4.2% padding (the glossy border of a lab print) and
a 2px radius, rotated -1.4°, carrying the three-layer print shadow, four clip-path photo corners
overhanging by 8px, and a black-backed image face locked to an 880:610 aspect ratio. A 115° linear
gradient at ≤14% white sits over the face as gloss sheen. The face holds the real product GIF — per
the product's own principle, the real app doing the real job. Hovering the wrapper eases the tilt
to -0.4° over 0.8s. Under a 1400px perspective, but not itself 3D-transformed.

### Date Burn-In (signature)

Seven-segment digits drawn as SVG at run time from a 10×18 cell grid, skewed -6°, filled burn
orange with off-segments at 8% alpha so the unlit segments stay faintly visible like a real LCD.
Positioned at 4.5% / 5% from the image's bottom-right corner in both languages, sized
`clamp(18px, 3.6vw, 34px)` tall, with the double drop-shadow bloom. On load it renders *today's*
date, waits 1500ms, dips to 0.15 opacity for 140ms, and re-renders the photo's real date while the
caption beneath swaps via a class. It carries an `aria-label` with the date written out, and the
caption is an `aria-live="polite"` region so the flip is announced rather than seen only.
Under `prefers-reduced-motion: reduce` it renders the real date immediately and the caption starts
in its resolved state — no flash, no flip.

### Code

- **Inline** (`{components.code-inline}`): `ink-2` fill, 3px radius, `paper` text at 0.92em mono.
  Used for file names, folder paths and flags inside prose.
- **Block** (`{components.code-block}`): `ink-2` fill with a 1px `ink-3` border and 6px radius,
  horizontally scrollable, `direction: ltr` always. Two semantic spans only: `.c` comments in
  `paper-3`, `.o` flags in burn. Syntax highlighting beyond those two roles is not part of the
  system.

### Comparison Table

Full-width, hairline-ruled (top plus one under every row), 0.92rem, 720px minimum width inside a
scrollable wrapper. Column heads are 0.8rem `paper-3`; row heads are `paper-2` at 500 weight and
never wrap; the "this tool" column is `paper` at 600 weight — the table's only emphasis, doing the
work a highlighted column would otherwise do with a fill. Cells are top-aligned and `text-align:
start` so the table mirrors correctly.

### Motion

One easing curve (`cubic-bezier(.16, 1, .3, 1)`) and four durations: 0.2s for color, 0.35s for
button transform and shadow, 0.8s for the print straightening, and the burn-in's authored
0.12s/0.14s flicker after a 1500ms hold. `prefers-reduced-motion: reduce` is honored in three
places — smooth scrolling is disabled, print and button transitions are removed, and the burn-in
skips straight to the real date.

## Do's and Don'ts

### Do:

- **Do** keep burn orange to one filled surface per page (the download button) and use it elsewhere
  only as light, a 1px mark, or a single glyph — The Single Burn Rule.
- **Do** separate content with 1px `ink-3` hairlines and grid gaps. New sections inherit the log /
  strip / table pattern, not a card pattern.
- **Do** cap every prose block at the 62ch measure and let headlines wrap with `text-wrap: balance`.
- **Do** use tabular figures for any number that is evidence — timestamps, coordinates, step
  numbers, offsets.
- **Do** wrap Latin and numeric runs in `dir="ltr"` inside the Hebrew page, and keep code blocks
  `direction: ltr` in both languages.
- **Do** honor `prefers-reduced-motion` for anything that moves: the reduced path must show the
  *resolved* state, never a blank or half-played one.
- **Do** give every motion the shared ease-out curve and keep new transitions inside 0.2–0.8s.
- **Do** show the real product — the real wizard GIF, real EXIF tag names, real CLI flags. No stock
  imagery, no mock UI, no invented numbers.

### Don't:

- **Don't** add a card, panel or tinted container. `ink-2` is code's background only.
- **Don't** give a second element a shadow. The print is the only object in this world.
- **Don't** introduce uppercase-with-tracking micro-type — no eyebrows, no kickers, no all-caps
  section tags. Labels are sentence case at 0.8rem.
- **Don't** add a third type family. Hebrew collapses to Rubik for both roles; monospace is for
  literal machine strings only.
- **Don't** mirror the print or its burn-in under RTL. A date burn-in is always bottom-right on a
  physical print.
- **Don't** round corners past 6px. The 999px pill belongs to the language switch alone.
- **Don't** let the page grain exceed its current weight (11% alpha, screen blend) or apply it to
  individual elements; it is one fixed layer over the whole page.
- **Don't** patch layout with inline `style` attributes or one-off literal colors; extend the
  spacing and color tokens instead.
- **Don't** mark up emphasis that renders identically to its surroundings. If a word needs
  emphasis, give it a visible treatment; if it doesn't, don't wrap it.

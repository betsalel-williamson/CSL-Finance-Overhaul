# Design System: Finance Overhaul (Cities: Skylines)

The look of Finance Overhaul, a Cities: Skylines I mod that adds a stock market, city bonds and
local IPOs to the game's Economy panel. This file is for Google Stitch
(`stitch upload design DESIGN.md`) and for anyone generating design options. The sources of truth
are `mod/Kit/FoTheme.cs` (every colour the mod draws), the Figma library
([CSL-Mod-Financial-Markets-Mod](https://www.figma.com/design/ZJ0oXFLXCOFOXQsLxE86Vh/CSL-Mod-Financial-Markets-Mod))
and `docs/developer/ui-design-tokens.md`. When they change, update this file too.

## 1. Visual Theme & Atmosphere

Dark, dense, in-game finance terminal. The mod's surfaces sit inside the game's own Economy panel
and dialogs (Colossal UI), so they must look like the game: near-black blue-grey panels, flat fills,
small rounded corners, white text with a soft black drop shadow, and one cyan accent. Numbers carry
the meaning (money green for gains, orange for losses, pale blue for ₡ values), so colour is spent
on data, not decoration. Desktop only, viewed at 1920 × 1080 and up; a list row is about 1,164 px
wide. No gradients, no glass, no imagery beyond the game's round coin icons.

## 2. Color Palette & Roles

### Primary Foundation

- **Night Header (`#090E11`)**: window title bars, section headings, tooltips and popovers (at 92%
  opacity).
- **Economy Panel (`#2A323C`)**: the game's Economy panel body behind the list.
- **Section Fill (`#1E242C`)**: a stock's card in the Investments list; hover `#28303A`.
- **Panel Alt (`#181D23`)**: inset areas and secondary strips.
- **Dialog Grey (`#32373A`)**: the game's Buy / Sell dialog body.
- **Chart Black (`#1E1F21`)**: behind the game's share-value graph.

### Accent & Interactive

- **Monitor Cyan (`#65D6F8`)**: the one accent: open orders, the chosen sub tab's underline, hover
  outline of a row, links.
- **Offer Gold (`#F0AA24`)**: IPO offers and unlock progress bars; text on it `#3A2404`.
- **Coin Gold (`#F6B83C`)**: the round stock coin icons (rim `#E8981C`).
- **Game Button**: the game's blue-grey button sprite with white text; disabled text `#5B616A`.

### Typography & Text Hierarchy

- **Game White (`#FEFEFE`)**: primary text, symbols, prices.
- **Label Grey (`#D2D5DA`)**: field labels and stat values.
- **Muted Grey (`#9B9A9F`)**: company names, holdings line, sort label, stat keys.
- **Dialog Body (`#A0A0A0`)**: muted text inside the game's dialogs.
- **Divider (`#FFFFFF` at 10%)**: 1 px rules between a stock and its orders, under stat rows.

### Functional States

- **Gain Green (`#97EE00`)**: the game's colour for rising money; change pills with dark text.
- **Loss Orange (`#FE9659`)**: the game's colour for falling money.
- **Value Blue (`#B9DDFE`)**: ₡ amounts in the game's panels.
- **Ticker colours** (LED boards only): gain `#4EF465`, loss `#E9424F`, symbol `#EFB058`, flat
  `#9FB0C0`.

## 3. Typography Rules

- **Open Sans** (the game's UI face; substitute Inter Semi Bold if unavailable). Bold symbols at
  `15px`, names and secondary lines at `11px` regular, prices at `15px` bold, pills `10px` bold,
  buttons `12px` semi bold, sub tabs `12px` semi bold.
- All text has a 1 px black drop shadow at 86% in game. No italics, no all-caps headings; numbers
  use thousands separators (`₡34,500`) and the game's ₡ sign.

## 4. Component Stylings

- **Buttons**: game button sprite, `3px` radius, `26px` tall in rows (`76px` wide for Buy / Sell),
  `18px` for small ones (Cancel). For actions only, never for a choice.
- **Tabs**: the game's tab strip, joined edge to edge. Primary: lit cyan tab with dark text.
  Secondary (sub tabs, under another strip): dimmed tabs, grey text, the chosen one plain with white
  text and a 2 px cyan line under it.
- **Change pill**: `16px` tall fully rounded pill in Gain Green or Loss Orange with dark text
  (`+4.88%`), click toggles % and ₡.
- **Stock card (Investments list)**: Section Fill, no border, `48px` tall (`64px` when held: a third
  line "500 shares · avg ₡70"); coin (`36px`) at left, symbol and name, a 30-day sparkline with a
  dashed baseline, price over its change pill (and the gain for a holding), Buy / Sell on the right.
  Hover: 2 px cyan outline; click opens the Buy / Sell dialog. Open orders are the lower part of the
  same card: same fill, a 1 px divider, one cyan line ("Open order · BUY · 500 shares at or below
  ₡830 · 88 days left") and Cancel.
- **Stat grid**: rows of muted key at left, value right-aligned, a 1 px divider under each.
- **Badges**: small pill, Night Header fill, cyan text (`LOCAL`).

## 5. Layout Principles

- **Spacing**: `4px` base; `8px` padding in cards, `4px` between cards, `16px` between groups.
- **List**: one column of cards inside the Economy panel's Investments tab (about 1,180 px wide, 560
  px tall, scrolls). Top bar: sub tabs at left (All · Owned · Orders · Local · Locked, each with its
  count), a Group button, then "Sort: Change %" and a ▼ arrow at right.
- **Density**: the game shows about 11 cards at once; prefer adding a line over growing a card.

## 6. Design System Notes for Stitch Generation

- **Atmosphere keywords**: dark city-builder game UI, flat in-game finance terminal, dense stock
  list, Night Header `#090E11`, Section Fill `#1E242C`, Monitor Cyan `#65D6F8`, Gain Green
  `#97EE00`, Loss Orange `#FE9659`, Game White `#FEFEFE`.
- **Component prompts**:
  - A dense dark stock row 1,164 px wide: gold coin icon, bold ticker and company name, a thin green
    30-day sparkline, bold price over a green rounded change pill, Buy and Sell buttons at the right
    edge.
  - Under a stock row, an attached cyan "Open order" line with a small Cancel button, same fill.
  - A row of quiet sub tabs with counts, the chosen one underlined in cyan.

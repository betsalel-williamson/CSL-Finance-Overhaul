# UI design tokens

Semantic design tokens for Finance Overhaul live in the Figma library
([CSL-Mod-Financial-Markets-Mod](https://www.figma.com/design/ZJ0oXFLXCOFOXQsLxE86Vh/CSL-Mod-Financial-Markets-Mod)).
Pages: **Foundations** (variables + specimen), **Components** (Options / Investments / PinCard
library), **UI Map** (screen pins). Tokens name colors, spacing, and radius for design and for any
future custom Colossal panels.

## What tokens are for

| Layer       | Use                                                             |
| ----------- | --------------------------------------------------------------- |
| Figma       | Variables bound to library components and the UI Map            |
| Docs / pins | Stable names in [UI catalog](../features/ui-catalog.md)         |
| Cities code | Options today use `UIHelper` — **no runtime token package yet** |

Trackpad Camera Control keeps Colossal-native Options without a token file. This mod adds a Figma
token layer first so treasury UI can stay consistent before custom panels ship.

## Collections (Figma)

- **Primitives** — raw palette and scale (hidden from most pickers).
- **Semantic** — aliases used by components (`color/bg/panel`, `color/text/primary`, …).

WEB code syntax on variables uses `var(--…)` names below so Code Connect / handoff stay stable even
though Colossal UI does not consume CSS.

## Source of truth

Colors were **sampled from vanilla Financial Districts captures** (Economy → Investments, Invest
dialog, hover tooltip, Options) — not invented. Game chrome uses **Open Sans** (Colossal's UI face);
annotation copy on the UI Map uses Inter so it never reads as game UI.

The three colours the Economy panel uses for money are not samples: they are the values **read from
the running game** (`EconomyPanel.m_positiveNumberColor` and its neighbours) and held in
`csl/green/400` (151, 238, 0, gain), `csl/orange/500` (254, 150, 89, loss) and `csl/blue/300` (185,
221, 254, money values). The code's copies are in `mod/Kit/FoTheme.cs`, and game UI scenario 53
fails if the game's values differ. Rule: anything the mod adds inside a game panel follows the game,
and Figma is updated to match; the mod's own windows follow Figma.

## Collections

- **FO/Primitives** (hidden): `csl/*` game colors (sampled, or read from the running game), `mod/*`
  colors the mod's own code declares (not taken from the game), `annotation/*` Figma-only colors,
  `space/*`, `radius/*`.
- **FO/Semantic**: everything components bind to. WEB code syntax `var(--fo-…)`.

## Semantic inventory (v2)

| Group                | Tokens                                                                                                                                                                                                                                                                                                                                                                                                                             |
| -------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Surfaces             | `color/bg/header`, `panel`, `dialog`, `panel-alt`, `card`, `card-top`, `inset`, `field`, `track`, `gap`, `tab`, `tab-active`, `button`, `button-primary`                                                                                                                                                                                                                                                                           |
| Text                 | `color/text/primary`, `label`, `muted`, `value` (blue ₡ marks), `accent`, `on-accent`, `icon`                                                                                                                                                                                                                                                                                                                                      |
| Strokes              | `color/stroke/default`, `strong`                                                                                                                                                                                                                                                                                                                                                                                                   |
| Market               | `color/market/gain`, `loss`, `price`, `total`, `bar`, `color/icon/sector`                                                                                                                                                                                                                                                                                                                                                          |
| Ticker               | `color/ticker/board` #1E303E, `board-lit` #38444B, `symbol` #EFB058, `gain` #4EF465, `loss` #E9424F, `unknown` (neutral, for `--`), `flat` (neutral, for `0.00`; aliases the same grey), `led-off` #293A46 (unlit LED dot); sampled from the Stock Exchange facade boards. From code: `frame` #0E1418 (board frame material), `led-off-bright` #1A2630, `led-off-brightest` #10181E (unlit dot at the two brighter Options levels) |
| Scroll               | `color/scroll/track` (game sprite `ScrollbarTrack`), `color/scroll/thumb` (`ScrollbarThumb`); primitives `csl/scroll/track`, `csl/scroll/thumb`                                                                                                                                                                                                                                                                                    |
| Chart                | `color/chart/bg`, `grid`, `line` (the game's own graph); the mod's chart overlay draws its grid in `grid` too; `hover-line` (white, alpha 150), `hover-dot` (white), `tip` (#090E11, alpha 235), `label` #9EA3AA                                                                                                                                                                                                                   |
| Mod windows          | `color/text/white` #FFFFFF (field text; the title bar caption and a selected tab use `color/text/primary`), `color/text/shadow` (black, alpha 220: label drop shadow), `color/icon/arrow` #E6ECF0 (sort arrow), `color/monitor/popover` (#090E11, alpha 235), `divider` (white, alpha 26), `slider-track` (white, alpha 64), `chart-back` (black at 35%), `badge-bg` #463418                                                       |
| Game-panel additions | City bonds: `color/bonds/panel` (#181E24, alpha 245), `row` #242C34, `title` and row labels #97EE00 (the game's Loans headings), `text`, `button-text` and `term-off` #FEFEFE (the game's panel text), `button-text-disabled` #5B616A (the game's "Take" button); the panel and row are the mod's own. Order ticket: `color/order/tab-text` #FEFEFE selected or not (the game's tab and button text), `card-fallback` #20262C      |
| Annotation           | `color/bg/page`, `color/bg/pin`, `color/doc/text`, `muted`, `stroke`, `color/pin/marker`, `color/status/shipped`, `vanilla-host`, `planned`                                                                                                                                                                                                                                                                                        |
| Spacing              | `space/xs` 4 · `sm` 8 · `md` 12 · `lg` 16 · `xl` 24                                                                                                                                                                                                                                                                                                                                                                                |
| Radius               | `radius/sm` 4 · `md` 6                                                                                                                                                                                                                                                                                                                                                                                                             |

## Text styles

| Style                  | Font                  | Use                                                                                           |
| ---------------------- | --------------------- | --------------------------------------------------------------------------------------------- |
| `FO/Game/Header`       | Open Sans SemiBold 18 | Modal title bar                                                                               |
| `FO/Game/Title`        | Open Sans SemiBold 16 | Row name                                                                                      |
| `FO/Game/Tab`          | Open Sans SemiBold 13 | Economy tabs                                                                                  |
| `FO/Game/Body`         | Open Sans SemiBold 13 | Label: value lines                                                                            |
| `FO/Game/Section`      | Open Sans SemiBold 11 | Column headers                                                                                |
| `FO/Game/Button`       | Open Sans SemiBold 13 | Buy / Sell                                                                                    |
| `FO/Game/Copy`         | Open Sans Regular 12  | Flavor text                                                                                   |
| `FO/Ticker/Board`      | Doto Bold 26          | Facade LED ticker (dot-matrix stand-in for the in-game `LedFont`, 5×7 with one descender row) |
| `FO/Doc/*`, `FO/PinId` | Inter                 | UI Map annotation only                                                                        |

The UI Map (band 08) draws two real board rows at each LED brightness level (Classic, Bright,
Brightest), in the mod's own LED font; Foundations keeps the base colours only. The board between
dots is one colour at every level (`color/ticker/board` #1E303E, `LedPalette.Board`). What changes
is the lit dot, its glow into the gap beside it, and the unlit dot. Each dot is a 4 × 4 texel cell
(`TexelsPerDot`): a 3 × 3 block in the dot colour and a 1-texel gap on its right and bottom. Beside
a lit dot the gap is the board blended toward the dot colour by 0, 0.3 or 0.55 (`LedPalette.Gap`);
beside an unlit dot it is always the board. Lit colours and glows above Classic are computed by
`LedPalette`, so they are raw fills on the specimen, not variables.

## Type scale in code

The mod sets `textScale` on the game's UI font (Open Sans Semibold); pixel size is `textScale` × 16
(the n/16 values in code, such as 0.875 and 0.8125, rely on that base). The Foundations page shows
each step at its size.

| `textScale` | px   | Used for                                                                                      |
| ----------- | ---- | --------------------------------------------------------------------------------------------- |
| 1.8         | 28.8 | Stock Monitor detail symbol                                                                   |
| 1.25        | 20   | Stock Monitor detail price                                                                    |
| 1.125       | 18   | City bonds section title                                                                      |
| 1.1         | 17.6 | Welcome back title                                                                            |
| 1.0         | 16   | Stock Monitor row symbol                                                                      |
| 0.9         | 14.4 | Row price, popover title, detail action buttons; open-order card text (game row text × 0.9)   |
| 0.875       | 14   | Welcome back lead and main button, order ticket estimate, Bonds you owe title                 |
| 0.85        | 13.6 | Stock Monitor empty state                                                                     |
| 0.8125      | 13   | Welcome back question and secondary button                                                    |
| 0.8         | 12.8 | Change pill, detail name and change, popover labels, bond buttons and rows                    |
| 0.75        | 12   | Tabs, range chips, sort, stat values, order price hint, bond copy, Welcome back cards         |
| 0.7         | 11.2 | Row company name, hover readouts, stat keys, share chart axis, order line detail, bond hints  |
| 0.6875      | 11   | Welcome back footer                                                                           |
| 0.68        | 10.9 | Stock Monitor detail yesterday line                                                           |
| 0.65        | 10.4 | Stock Monitor footer, bond row next-payment line                                              |
| 0.6         | 9.6  | Order badge, order line type and distance, detail chart axis and notes; floor for fitted text |
| 0.55        | 8.8  | Smallest an order line may shrink to                                                          |

Text treatments (`mod/Kit/FoText.cs`): every `FoText.Label` has a drop shadow (`color/text/shadow`,
offset 1, -1); `FoText.Heavy` draws each glyph twice, 0.6 px apart, as a bold stand-in for small
text on pills. Text the mod adds beside a game label copies that label's font, scale and colour.

Window title bar (`mod/Kit/FoWindow.cs`): 40 px high; caption centred in white in the font of the
game's info view panel captions, read from the game at run time (`FO/Game/Header` is the Figma
stand-in); close button 32 × 32 flush right, 2 from the top; gear button 24 × 24 left of it, 7 from
the top.

## Game sprites

Base graphics the mod takes from the game's default UI atlas, by exact sprite name. The atlas art is
not in the repo, so the Foundations page shows each as a labelled placeholder tile, not the real
graphic.

| Sprite                                                                       | State                                                                       | Used by                                                                                                                     |
| ---------------------------------------------------------------------------- | --------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------- |
| `GenericPanelWhite`                                                          | Only state; tinted                                                          | Stock Monitor window and popover, chart hover readouts, Welcome back frame, header and cards, pill and badge, City bonds    |
| `GenericPanel`                                                               | Fallback only                                                               | Open-order card when the game's row panel cannot be copied                                                                  |
| `EmptySprite`                                                                | Flat fill; tinted                                                           | 1 px dividers, order side bar, owned marker, chart back, hover line and dot, slider track, field selection and caret        |
| `ButtonMenu`, `ButtonMenuHovered`, `ButtonMenuPressed`, `ButtonMenuDisabled` | Normal, hovered, pressed (also the selected tab or chip), disabled          | `FoButton` (tabs, range chips, sort, actions), Welcome back buttons, order ticket tabs, bond buttons, Cancel order fallback |
| `ButtonMenuFocused`                                                          | Selected bond term                                                          | City bonds term selector                                                                                                    |
| `buttonclose`, `buttonclosehover`, `buttonclosepressed`                      | Normal, hovered, pressed                                                    | Close button of the Stock Monitor and the Welcome back dialog                                                               |
| `RoundBackBig`, `RoundBackBigHovered`, `RoundBackBigPressed`                 | Normal and focused, hovered, pressed (also while the Stock Monitor is open) | Gear button (24 px), Stock Monitor toolbar button (46 px)                                                                   |
| `Options`                                                                    | Foreground                                                                  | Gear glyph on the gear button                                                                                               |
| `check-unchecked`, `check-checked`                                           | Box; tick shown over the box when on                                        | `FoCheckbox` (16 px)                                                                                                        |
| `TextFieldPanel`, `TextFieldPanelHovered`                                    | Normal and focused, hovered                                                 | Share amount, order price and bond amount fields                                                                            |
| `ScrollbarTrack`, `ScrollbarThumb`                                           | Track (12 wide), thumb (8 wide)                                             | Bonds you owe list                                                                                                          |

The Cancel order button and the open-order card name no sprite: they copy the game Sell button's
sprites and the game row panel's background at run time.

## Mod-drawn graphics

Textures the mod paints itself, drawn on the Foundations page with their tokens:

| Graphic          | Code                                    | What it is                                                                                                  |
| ---------------- | --------------------------------------- | ----------------------------------------------------------------------------------------------------------- |
| Sort arrow       | `FoButton.ArrowTexture`                 | 10 × 6 px triangle, down or up, `color/icon/arrow`                                                          |
| Row sparkline    | `FoSparkline.Draw`                      | Line in the change colour with a dashed baseline at the first price (same colour, alpha 200); 72 × 28 at 2× |
| Price chart      | `StockMonitorDetail`, `ShareChart`      | Line, dashed baseline, area from alpha 80 to 0, grid lines, hover line, dot and readout                     |
| Toolbar icon     | `StockMonitorToolbarButton.IconTexture` | 52 × 52 sparkline of a fixed series in `color/ticker/gain`, shown at 26 × 26                                |
| Scroll edge fade | `CityBondsSection.FadeTexture`          | 1 × 32 px, the section colour from solid to clear                                                           |
| LED board        | `TickerOverlay`                         | Dot-matrix rows in the mod's `LedFont`, board and frame colours from `color/ticker/*`                       |

The change pill, order badge and caret are not textures: they are `GenericPanelWhite` and
`EmptySprite` tinted by the mod.

## Values read from the game

Read from the running game with the dev `dump CONTROL` scenario command (it logs each control's
tint, sprites, text colour and scale). The mod's additions inside the game's panels use these.

| Where                 | The game's value                                                                                        | Used for                                                     |
| --------------------- | ------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------ |
| Invest dialog, shares | `LabelShareSliderNumber`: sprite `GenericPanelLight` tinted #343434, text #B9DDFE, scale 0.875          | `FoTheme.Field` and `GameFieldSprite`: every typed field     |
| Invest dialog, chart  | `UIGraph`: curve #FFEB04 (3 wide), axes #7495A5, help axes #343E47; grid lines sample as about #75797C  | `FoTheme.ChartGrid` only; the line is not copied (see below) |
| Economy tabs          | Sprites `GenericTab`, `GenericTabFocused`, `GenericTabHovered`; text #FEFEFE, scale 1, selected #101010 | `FoTheme.GameText`: order ticket tabs (scale 1), bond terms  |
| Invest dialog buttons | Sprite `ButtonMenu`; text #FEFEFE, hovered #0784FF, disabled #2E2E2E                                    | `FoTheme.GameText`                                           |
| Loans tab, offers     | Panel `UnlockingPanel2` tinted #4FDB00; bank name #97EE00 at 1.375; row labels #97EE00; values #B9DDFE  | `FoTheme.GameHeading`: City bonds title and row labels       |
| Loans tab, "Take"     | Sprite `ButtonMenu`; text #FEFEFE, disabled #5B616A                                                     | `FoTheme.GameText`, `GameDisabledText`: bond buttons         |

The game's Economy tabs measure 16 px (scale 1); the Figma style `FO/Game/Tab` is still 13 and has
not been changed, because the UI Map mockups that use it would need checking one by one.

Figma variables follow these values: `color/bg/field` (primitive `csl/field/tint`),
`color/bonds/text`, `button-text`, `term-off` and `color/order/tab-text`, `tab-text-off`
(`csl/text/panel`), `color/bonds/button-text-disabled` (`csl/text/button-disabled`),
`color/bonds/title` (`csl/loans/heading`). The Foundations page has a Stock Monitor section listing
every colour that window draws with; the LED brightness examples are on the UI Map (band 08), drawn
from the mod's own font and palette.

Decided where the game has no equivalent:

- Share charts (Invest dialog and Stock Monitor) draw their line in the change colour: green for a
  gain, red for a loss, grey when flat. The game's yellow line is not used; only the grid matches
  the game's chart.
- The City bonds panel (#181E24, alpha 245) and its rows (#242C34) are the mod's own: the Loans tab
  has no dark panel to copy.
- `color/ticker/board-lit` #38444B is not used by code (the lit gap is computed by
  `LedPalette.Gap`); it stays in Figma for components that bind it.

## Components (Components page)

| Tier       | Component                                     | Variants / properties                                                                                         |
| ---------- | --------------------------------------------- | ------------------------------------------------------------------------------------------------------------- |
| Annotation | `FO/AnnotationPin`                            | Number=1…20                                                                                                   |
| Atom       | `FO/EconomyTab`                               | State=Default/Active · Label                                                                                  |
| Atom       | `FO/Button`                                   | Kind=Row/Primary × State=Default/Disabled · Label                                                             |
| Atom       | `FO/KeyValue`                                 | Tone=Default/Value/Gain/Loss/Price · Label, Value                                                             |
| Atom       | `FO/SectionLabel`                             | Label                                                                                                         |
| Atom       | `FO/SectorIcon`                               | Glyph                                                                                                         |
| Atom       | `FO/ModalHeader`                              | Title                                                                                                         |
| Molecule   | `FO/InvestmentRow`                            | Kind=Purchasable/Portfolio × Trend=Gain/Loss · Name; Portfolio adds Avg cost / share                          |
| Molecule   | `FO/TotalBar`                                 | Amount                                                                                                        |
| Molecule   | `FO/ShareChart`                               | —                                                                                                             |
| Molecule   | `FO/ShareAmount`                              | State=Default/Editing · Shares                                                                                |
| Molecule   | `FO/OrderTab`                                 | State=Default/Active · Label                                                                                  |
| Molecule   | `FO/OrderPriceField`                          | Label, Value, Hint                                                                                            |
| Molecule   | `FO/OpenOrderRow`                             | Portfolio order card: Name, Side, Order, Expiry, Cancel                                                       |
| Molecule   | `FO/OptionsCheckbox`, `FO/OptionsBuiltFooter` | Label                                                                                                         |
| Atom       | `FO/SortDirection`                            | Direction=Down/Up · Stock Monitor sort direction button                                                       |
| Molecule   | `FO/ScrollHint`                               | Kind=EdgeFade (the rule)/ShadowLip/FadeChevron/MorePill · list mid-scroll                                     |
| Molecule   | `FO/MonitorOrderLine`                         | Side=Buy/Sell/Paused × Density=Regular/Compact · Type, Detail, Distance (Stock Monitor → Orders tab, planned) |
| Atom       | `FO/Scrollbar`                                | State=Overflow/Fits (Fits is invisible); see the scrolling rule below                                         |
| Molecule   | `FO/BondRow`                                  | Title, Next · Repay button (City bonds, Economy → Loans)                                                      |
| Organism   | `FO/OptionsPanel`                             | Title                                                                                                         |
| Organism   | `FO/EconomyInvestmentsPanel`                  | Composed from the above                                                                                       |
| Organism   | `FO/InvestDialog`                             | Composed from the above                                                                                       |
| Organism   | `FO/InvestmentTooltip`                        | Description                                                                                                   |
| Facade     | `FO/TickerEntry`                              | Tone=Gain/Loss/Flat/Unknown · Symbol, Change                                                                  |
| Facade     | `FO/TickerBoard`                              | LED screen: three rows sorted by symbol, clipped                                                              |

## Rule: scrolling lists

Every list that can hold more rows than it shows (`UIScrollablePanel`) gets:

1. **A vertical scrollbar** (`FO/Scrollbar`) at its right edge: 12 wide with a 4 gap, the game's
   `ScrollbarTrack` / `ScrollbarThumb` sprites (`color/scroll/track`, `thumb`), shown only while the
   content overflows (`autoHide`). Rows narrow by the bar's width so nothing sits under it; the
   mouse wheel and dragging the thumb both scroll.
2. **Edge fades** (`FO/ScrollHint` Kind=EdgeFade; the compared alternatives ShadowLip, FadeChevron
   and MorePill sit beside it on the Components page): a 22-unit feather from the panel colour to
   clear over the top and/or bottom of the list, shown only on an edge with more rows past it and
   easing in and out over the last 22 units of scrolling (`ScrollFade`, 1-unit dead zone at the
   ends; proved never to flicker or show when everything fits, `ScrollFadeTests`).

Text that follows a list (footers) sits below the list's box, never over its last row. First applied
to City bonds → Bonds you owe (`CityBondsSection.Scrollbar`, `UpdateFades`; game UI scenario 44
checks both fades at the top and bottom).

## Relationship to Trackpad

Mirror **catalog + hosts + UI parity docs** from
[CSL-TrackpadCameraControl](https://github.com/betsalel-williamson/CSL-TrackpadCameraControl)
(`FeelCatalog` + Options host). Do not mirror Trackpad’s Debug panel chrome unless a Finance
Overhaul in-game panel is specified later. Live reload remains assembly identity +
`install-mod-local.sh` ([Live reload](./live-reload.md)), not UI hot-swap.

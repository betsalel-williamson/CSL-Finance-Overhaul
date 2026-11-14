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

## Collections

- **FO/Primitives** (hidden): `csl/*` sampled game colors, `annotation/*` Figma-only colors,
  `space/*`, `radius/*`.
- **FO/Semantic**: everything components bind to. WEB code syntax `var(--fo-…)`.

## Semantic inventory (v2)

| Group      | Tokens                                                                                                                                                                                                                                                                    |
| ---------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Surfaces   | `color/bg/header`, `panel`, `dialog`, `panel-alt`, `card`, `card-top`, `inset`, `field`, `track`, `gap`, `tab`, `tab-active`, `button`, `button-primary`                                                                                                                  |
| Text       | `color/text/primary`, `label`, `muted`, `value` (blue ₡ marks), `accent`, `on-accent`, `icon`                                                                                                                                                                             |
| Strokes    | `color/stroke/default`, `strong`                                                                                                                                                                                                                                          |
| Market     | `color/market/gain`, `loss`, `price`, `total`, `bar`, `color/icon/sector`                                                                                                                                                                                                 |
| Ticker     | `color/ticker/board` #1E303E, `board-lit` #38444B, `symbol` #EFB058, `gain` #4EF465, `loss` #E9424F, `unknown` (neutral, for `--`), `flat` (neutral, for `0.00`; aliases the same grey), `led-off` #293A46 (unlit LED dot); sampled from the Stock Exchange facade boards |
| Chart      | `color/chart/bg`, `grid`, `line`                                                                                                                                                                                                                                          |
| Annotation | `color/bg/page`, `color/bg/pin`, `color/doc/text`, `muted`, `stroke`, `color/pin/marker`, `color/status/shipped`, `vanilla-host`, `planned`                                                                                                                               |
| Spacing    | `space/xs` 4 · `sm` 8 · `md` 12 · `lg` 16 · `xl` 24                                                                                                                                                                                                                       |
| Radius     | `radius/sm` 4 · `md` 6                                                                                                                                                                                                                                                    |

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

## Components (Components page)

| Tier       | Component                                     | Variants / properties                               |
| ---------- | --------------------------------------------- | --------------------------------------------------- |
| Annotation | `FO/AnnotationPin`                            | Number=1…12                                         |
| Atom       | `FO/EconomyTab`                               | State=Default/Active · Label                        |
| Atom       | `FO/Button`                                   | Kind=Row/Primary × State=Default/Disabled · Label   |
| Atom       | `FO/KeyValue`                                 | Tone=Default/Value/Gain/Loss/Price · Label, Value   |
| Atom       | `FO/SectionLabel`                             | Label                                               |
| Atom       | `FO/SectorIcon`                               | Glyph                                               |
| Atom       | `FO/ModalHeader`                              | Title                                               |
| Molecule   | `FO/InvestmentRow`                            | Kind=Purchasable/Portfolio × Trend=Gain/Loss · Name |
| Molecule   | `FO/TotalBar`                                 | Amount                                              |
| Molecule   | `FO/ShareChart`                               | —                                                   |
| Molecule   | `FO/ShareAmount`                              | State=Default/Editing · Shares                      |
| Molecule   | `FO/OptionsCheckbox`, `FO/OptionsBuiltFooter` | Label                                               |
| Organism   | `FO/OptionsPanel`                             | Title                                               |
| Organism   | `FO/EconomyInvestmentsPanel`                  | Composed from the above                             |
| Organism   | `FO/InvestDialog`                             | Composed from the above                             |
| Organism   | `FO/InvestmentTooltip`                        | Description                                         |
| Facade     | `FO/TickerEntry`                              | Tone=Gain/Loss/Flat/Unknown · Symbol, Change        |
| Facade     | `FO/TickerBoard`                              | LED screen: three rows sorted by symbol, clipped    |

## Relationship to Trackpad

Mirror **catalog + hosts + UI parity docs** from
[CSL-TrackpadCameraControl](https://github.com/betsalel-williamson/CSL-TrackpadCameraControl)
(`FeelCatalog` + Options host). Do not mirror Trackpad’s Debug panel chrome unless a Finance
Overhaul in-game panel is specified later. Live reload remains assembly identity +
`install-mod-local.sh` ([Live reload](./live-reload.md)), not UI hot-swap.

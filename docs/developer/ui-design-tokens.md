# UI design tokens

Semantic design tokens for Finance Overhaul live in the Figma library
([CSL-Mod-Financial-Markets-Mod](https://www.figma.com/design/ZJ0oXFLXCOFOXQsLxE86Vh/CSL-Mod-Financial-Markets-Mod)).
Pages: **Foundations** (variables + specimen), **Components** (Options / Investments / PinCard
library), **UI Map** (screen pins). Tokens name colors, spacing, and radius for design and for any
future custom Colossal panels.

## What tokens are for

| Layer        | Use                                                              |
| ------------ | ---------------------------------------------------------------- |
| Figma        | Variables bound to library components and the UI Map             |
| Docs / pins  | Stable names in [UI catalog](../features/ui-catalog.md)          |
| Cities code  | Options today use `UIHelper` — **no runtime token package yet**  |

Trackpad Camera Control keeps Colossal-native Options without a token file. This mod adds a Figma
token layer first so treasury UI can stay consistent before custom panels ship.

## Collections (Figma)

- **Primitives** — raw palette and scale (hidden from most pickers).
- **Semantic** — aliases used by components (`color/bg/panel`, `color/text/primary`, …).

WEB code syntax on variables uses `var(--…)` names below so Code Connect / handoff stay stable even
though Colossal UI does not consume CSS.

## Semantic inventory (v1)

### Color

| Token                         | Role                                      |
| ----------------------------- | ----------------------------------------- |
| `color/bg/page`               | Canvas / page background                  |
| `color/bg/panel`              | Modal and Options content panel           |
| `color/bg/panel-alt`          | Nested lists, sidebar                     |
| `color/bg/pin`                | Design pin card                           |
| `color/text/primary`          | Primary labels                            |
| `color/text/muted`            | Footers, meta, secondary copy             |
| `color/text/accent`           | Active tab / emphasis                     |
| `color/stroke/default`        | Hairline borders                          |
| `color/status/shipped`        | Pin / chip — mod-owned                    |
| `color/status/vanilla-host`   | Pin / chip — FD host                      |
| `color/status/planned`        | Pin / chip — not MVP                      |
| `color/market/gain`           | Positive change / gain                    |
| `color/market/loss`           | Negative change / loss                    |
| `color/pin/marker`            | Annotation pin fill (red numbered dot)    |

### Spacing

| Token            | Use                         |
| ---------------- | --------------------------- |
| `space/xs` (4)   | Tight chip gaps             |
| `space/sm` (8)   | Row internal gaps           |
| `space/md` (12)  | Panel and pin padding       |
| `space/lg` (16)  | Panel content padding       |
| `space/xl` (24)  | Section gaps on UI Map      |

### Radius

| Token         | Use                |
| ------------- | ------------------ |
| `radius/sm`   | Chips, checkboxes  |
| `radius/md`   | Panels, pin cards  |

## Text styles (v1)

| Style              | Use                              |
| ------------------ | -------------------------------- |
| `FO/Title`         | Options group / panel titles     |
| `FO/Body`          | Control labels                   |
| `FO/Meta`          | Built footers, pin docs/code     |
| `FO/PinId`         | Pin id emphasis                  |

## Figma components (v1)

| Component              | Variants / notes                                      |
| ---------------------- | ----------------------------------------------------- |
| `FO/OptionsCheckbox`   | Probe-bias style Options row                          |
| `FO/OptionsBuiltFooter`| Dev Built UTC + local                                 |
| `FO/OptionsPanel`      | Group title + checkbox + footers                      |
| `FO/EconomyTab`        | Default / Active (Taxes…Investments)                  |
| `FO/InvestmentRow`     | Purchasable \| Portfolio                              |
| `FO/MarketButton`      | Buy \| Sell (vanilla host)                            |
| `FO/AnnotationPin`     | Red numbered dot (frame-local integer)                |
| `FO/PinLegendRow`      | Legend row: number · title + description              |

## Relationship to Trackpad

Mirror **catalog + hosts + UI parity docs** from
[CSL-TrackpadCameraControl](https://github.com/betsalel-williamson/CSL-TrackpadCameraControl)
(`FeelCatalog` + Options host). Do not mirror Trackpad’s Debug panel chrome unless a Finance
Overhaul in-game panel is specified later. Live reload remains assembly identity +
`install-mod-local.sh` ([Live reload](./live-reload.md)), not UI hot-swap.

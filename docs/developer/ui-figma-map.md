# UI Figma map

Schematic UI map for Options and the vanilla Investments host live in Figma. Use that file when
designing treasury surfaces; keep [Market analogs](../features/market-analogs.md) as product intent
until a later slice owns custom trading chrome.

Component inventory: [UI catalog](../features/ui-catalog.md). Variables and text styles:
[UI design tokens](./ui-design-tokens.md). Look/interaction contract:
[UI parity](../glossary/ui-parity.md).

Agent editing heuristics: `.agents/skills/figma-fo-ui/SKILL.md` and `.cursor/rules/figma-fo-ui.mdc`.

## File

[CSL-Mod-Financial-Markets-Mod](https://www.figma.com/design/ZJ0oXFLXCOFOXQsLxE86Vh/CSL-Mod-Financial-Markets-Mod)
— page **UI Map**.

## What a pin is

A **pin** is a red circular marker (`FO/AnnotationPin`) with a **number unique inside its band**.
Pins are **attached to the screenshot and to the new UI** for that band (parented on those
surfaces). The number ties to a **legend** entry with title + description (docs / code / status) —
the legend is copy only, not the pin. Thin connectors pin→legend are optional when they stay
readable. Pins must not overlap each other or cover important UI text. Different bands may reuse the
same numbers (Options `1` and Investments `1` are independent).

## Status legend

| Status         | Meaning                                                     |
| -------------- | ----------------------------------------------------------- |
| `shipped`      | Mod-owned Options control                                   |
| `vanilla-host` | Financial Districts Economy Investments (or related) chrome |
| `planned`      | Documented in market analogs; not built                     |

Market math stays Cities-free under `src/FinanceOverhaul.Market/`. Harmony and Options stay in
`mod/Host/`.

## Pin inventory

Numbers below are **per-frame**. Catalog ids stay stable for docs↔code.

### Options frame (`shipped`)

Pin 6 is on the new-UI panel only; the in-game screenshot predates the order flags (capture
pending).

| #   | Catalog id            | Title                                                                          | Docs                                                                        | Code evidence                                                                                                |
| --- | --------------------- | ------------------------------------------------------------------------------ | --------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------ |
| 1   | `opt.title`           | Options / Content Manager title                                                | [Live reload](./live-reload.md)                                             | [`mod/Host/Mod.cs`](../../mod/Host/Mod.cs)                                                                   |
| 2   | `opt.toggles`         | Feature toggles (live ticker, experimental Index)                              | [City Index stock](../client/city-index-fund.md)                            | [`mod/Host/Mod.cs`](../../mod/Host/Mod.cs), [`ModSettings`](../../src/FinanceOverhaul.Market/ModSettings.cs) |
| 3   | `opt.built-utc`       | Dev Built (UTC) footer                                                         | [Live reload](./live-reload.md)                                             | [`mod/Host/Mod.cs`](../../mod/Host/Mod.cs)                                                                   |
| 4   | `opt.built-local`     | Dev Built (local) footer                                                       | [Live reload](./live-reload.md)                                             | [`mod/Host/Mod.cs`](../../mod/Host/Mod.cs)                                                                   |
| 5   | `opt.copy-build-info` | Copy build info button                                                         | [Live reload](./live-reload.md)                                             | [`mod/Host/Mod.cs`](../../mod/Host/Mod.cs)                                                                   |
| 6   | `opt.order-flags`     | Order type flags (limit buy, stop-loss, take-profit, trailing stop; temporary) | [Automated orders](../features/automated-orders.md#options-flags-temporary) | [`mod/Host/Mod.cs`](../../mod/Host/Mod.cs), [`ModSettings`](../../src/FinanceOverhaul.Market/ModSettings.cs) |

### Investments frame (`vanilla-host`)

| #   | Catalog id                     | Title                        | Docs                                                        | Code evidence / host                     |
| --- | ------------------------------ | ---------------------------- | ----------------------------------------------------------- | ---------------------------------------- |
| 1   | `host.economy-investments-tab` | Economy → Investments tab    | [Install and first run](../client/install-and-first-run.md) | Vanilla FD Economy panel                 |
| 2   | `host.purchasable-list`        | Purchasable Investments      | [Install and first run](../client/install-and-first-run.md) | `BuySellInvestmentPanel`                 |
| 3   | `host.row-value`               | Per-row Value (₡)            | [Install and first run](../client/install-and-first-run.md) | Vanilla mark                             |
| 4   | `host.row-change-rate`         | Per-row Change rate (%)      | [Install and first run](../client/install-and-first-run.md) | Vanilla display                          |
| 5   | `host.buy`                     | Buy action                   | [Install and first run](../client/install-and-first-run.md) | Untouched by MVP                         |
| 6   | `host.sell`                    | Sell action                  | [Install and first run](../client/install-and-first-run.md) | Untouched by MVP                         |
| 7   | `host.portfolio-list`          | Your Portfolio               | [Install and first run](../client/install-and-first-run.md) | Vanilla FD                               |
| 8   | `host.portfolio-current-value` | Portfolio Current value      | [Install and first run](../client/install-and-first-run.md) | Vanilla mark                             |
| 9   | `host.portfolio-total`         | Portfolio Total value        | [Install and first run](../client/install-and-first-run.md) | Aggregate marks                          |
| 10  | `host.price-tick`              | Price simulation (no window) | [ADR 0001](../features/adr/0001-reflective-stock-hooks.md)  | `InvestmentAI` / `EconomyManager`        |
| 11  | `host.se-world-info`           | Stock Exchange world info    | [Stock Exchange](../glossary/stock-exchange.md)             | `StockExchangeWorldInfoPanel` (TBD shot) |

The zone-income block under Investments columns is not pinned.

### Hover tooltip frame (`vanilla-host`)

Row hover — Share value chart + flavor text.

| #   | Catalog id               | Title              | Docs                                                        | Code evidence / host |
| --- | ------------------------ | ------------------ | ----------------------------------------------------------- | -------------------- |
| 1   | `host.hover-share-chart` | Share value chart  | [Install and first run](../client/install-and-first-run.md) | `InvestmentTooltip`  |
| 2   | `host.hover-description` | Flavor description | [Install and first run](../client/install-and-first-run.md) | Vanilla tooltip copy |

### Invest / Buy dialog frame (`vanilla-host` + typed field)

Click Buy / open invest dialog. Pin 6 is the mod's typed field; pin 9 marks its editing state
(`FO/ShareAmount` `State=Editing`) drawn below the dialog.

| #   | Catalog id                  | Title                 | Docs                                                        | Code evidence / host                                    |
| --- | --------------------------- | --------------------- | ----------------------------------------------------------- | ------------------------------------------------------- |
| 1   | `host.invest-dialog-title`  | Invest in: {name}     | [Install and first run](../client/install-and-first-run.md) | Buy/sell dialog chrome                                  |
| 2   | `host.invest-share-chart`   | Share value chart     | [Install and first run](../client/install-and-first-run.md) | Same chart family as hover                              |
| 3   | `host.invest-description`   | Flavor description    | [Install and first run](../client/install-and-first-run.md) | Vanilla copy                                            |
| 4   | `host.invest-current-value` | Current value / share | [Install and first run](../client/install-and-first-run.md) | Mark for pricing                                        |
| 5   | `host.invest-share-amount`  | Share amount slider   | [Install and first run](../client/install-and-first-run.md) | Quantity                                                |
| 6   | `host.invest-share-field`   | Share amount field    | [Typed share amount](../features/share-amount-input.md)     | Typed entry: `ShareAmountField` (shipped, mod-enhanced) |
| 7   | `host.invest-total-price`   | Price (total)         | [Install and first run](../client/install-and-first-run.md) | quantity × mark                                         |
| 8   | `host.invest-buy-confirm`   | Buy confirm           | [Install and first run](../client/install-and-first-run.md) | Untouched by MVP                                        |
| 9   | `host.invest-share-field`   | Editing state         | [Typed share amount](../features/share-amount-input.md)     | `ShareAmountEditor`: caret, live slider, Esc reverts    |

### Index stock frame (`experimental`)

Built from library instances; in-game capture pending. Toggle off by default
([Save compatibility](../features/save-compatibility.md)).

| #   | Catalog id         | Title                | Code evidence                                |
| --- | ------------------ | -------------------- | -------------------------------------------- |
| 1   | `idx.listing`      | Index listing        | `mod/Host/IndexListing.cs`                   |
| 2   | `idx.mark`         | Index mark (Value)   | `CityIndexBasket.TryComputeMark`             |
| 3   | `idx.change`       | Change rate          | Vanilla `CalculateChangeRate`                |
| 4   | `idx.buy`          | Buy                  | Vanilla `BuyShares`                          |
| 5   | `idx.icon`         | Icon fallback        | Vanilla `MoneyThumb` (planned custom sprite) |
| 6   | `idx.holding`      | Index holding        | Vanilla portfolio row                        |
| 7   | `idx.dialog-title` | Invest in: Index     | `Patcher.IndexLocalizedNamePrefix`           |
| 8   | `idx.chart`        | Basket history chart | Vanilla `ShareChart`                         |
| 9   | `idx.description`  | Index description    | `Patcher.IndexLocalizedDescriptionPrefix`    |

### Stock Exchange facade frame (`shipped`)

Live LED ticker (`plan.live-ticker`). Screenshot: the vanilla facade at night; new UI: three
`FO/TickerBoard` instances (LED dot-matrix, entries sorted by symbol, TVLC at 0.00 as captured in
game).

| #   | Catalog id         | Title            | Code evidence                                        |
| --- | ------------------ | ---------------- | ---------------------------------------------------- |
| 1   | `plan.live-ticker` | Ticker board     | `TickerBoards` / `TickerOverlay`, `VanillaBoards`    |
| 2   | `ticker.symbol`    | Symbol           | `TickerSymbols.For`                                  |
| 3   | `ticker.gain`      | Gain             | `TickerFeed` (`CalculateChangeRate(id, data, 5)`)    |
| 4   | `ticker.loss`      | Loss             | `LedText.RateColour`                                 |
| 5   | `ticker.unknown`   | Unknown value    | `TickerLine.FormatRate` (`--`, never `NaN`)          |
| 6   | `ticker.flat`      | Flat (no change) | `LedText.RateColour` (neutral when it prints `0.00`) |
| 7   | `ticker.scroll`    | Scroll and clip  | `LedMarquee` (ring buffer, whole-dot steps)          |

### Automated orders frame (limit buy and all sell types `shipped`; recurring `planned`)

Band 09: the limit buy ticket in the Invest dialog, a Sell ticket for the planned sell types, and
Your Portfolio with an open order card right under its holding
([Automated orders](../features/automated-orders.md)).

| #   | Catalog id          | Title            | Code evidence / status                                  |
| --- | ------------------- | ---------------- | ------------------------------------------------------- |
| 1   | `orders.type`       | Order type       | Shipped: Buy now / Limit (`OrderTicket`); sells planned |
| 2   | `orders.price`      | Trigger price    | Shipped (`OrderTicket`, `FO/OrderPriceField`)           |
| 3   | `orders.estimate`   | Estimate         | Shipped: own row, red when unaffordable                 |
| 4   | `orders.place`      | Place order      | Shipped: `OrderTicket` → `OrderDesk.PlaceLimitBuy`      |
| 5   | `orders.sell-types` | Sell order types | Stop-loss, take-profit, trailing stop shipped           |
| 6   | `orders.open-list`  | Open orders      | Shipped: cards in Your Portfolio (`PortfolioOrders`)    |
| 7   | `orders.cancel`     | Cancel           | Shipped: `PortfolioOrders` → `OrderDesk.Cancel`         |
| 8   | `orders.recurring`  | Recurring buy    | Planned                                                 |
| 9   | `orders.notice`     | Fill notice      | Log only today (`OrderDesk`)                            |

### Stock Monitor frame (list `shipped`; rest `planned`)

Band 10: the floating Stock Monitor — in place over a real screenshot (50%) and at 1:1: list window,
detail view, settings popover, Options flag ([Stock Monitor](../features/stock-monitor.md)).

| #   | Catalog id          | Title          | Code evidence / status                             |
| --- | ------------------- | -------------- | -------------------------------------------------- |
| 1   | `monitor.window`    | Window         | Shipped (`StockMonitorHost`); Esc to close planned |
| 2   | `monitor.settings`  | Settings (⚙)   | Gear cycles opacity today; popover planned         |
| 3   | `monitor.filter`    | Filter + sort  | Shipped                                            |
| 4   | `monitor.row`       | Row            | Shipped; click → detail planned                    |
| 5   | `monitor.sparkline` | Sparkline      | Shipped (`Sparkline`, 30 game days)                |
| 6   | `monitor.change`    | Change pill    | Shipped (black text); % ↔ ₡ toggle planned         |
| 7   | `monitor.hover`     | Hover          | Planned                                            |
| 8   | `monitor.detail`    | Detail         | Planned (7D / 30D)                                 |
| 9   | `monitor.stats`     | Stats          | Planned                                            |
| 10  | `monitor.trade`     | Buy / Sell     | Planned                                            |
| 11  | `monitor.open`      | Toolbar button | Shipped (top-left mod row, right-drag to move)     |
| 12  | `monitor.flag`      | Options flag   | Shipped: Stock Monitor window (testing)            |

### Planned frame (`planned`)

| #   | Catalog id         | Title            | Docs                                                                                             |
| --- | ------------------ | ---------------- | ------------------------------------------------------------------------------------------------ |
| 1   | `plan.city-index`  | City index fund  | [Market analogs](../features/market-analogs.md), [Index fund](../glossary/index-fund.md)         |
| 2   | `plan.sector-etf`  | Sector ETFs      | [Market analogs](../features/market-analogs.md), [Sector ETF](../glossary/sector-etf.md)         |
| 3   | `plan.bonds`       | Municipal bonds  | [Market analogs](../features/market-analogs.md), [Municipal bond](../glossary/municipal-bond.md) |
| 4   | `plan.commodities` | Commodities      | [Market analogs](../features/market-analogs.md), [Commodity](../glossary/commodity.md)           |
| 5   | `plan.orders`      | Automated orders | [Market analogs](../features/market-analogs.md)                                                  |
| 6   | `plan.ipo`         | Listings / IPOs  | [Market analogs](../features/market-analogs.md), [IPO](../glossary/ipo.md)                       |

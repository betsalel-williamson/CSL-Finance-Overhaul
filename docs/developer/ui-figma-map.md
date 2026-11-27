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

Every calculation stays Cities-free under `src/FinanceOverhaul.Core/`. Hooks, Options and drawing
are in `mod/` ([Repository layout](./repository-layout.md)).

## Pin inventory

Numbers below are **per-frame**. Catalog ids stay stable for docs↔code.

### Options frame (`shipped`)

Pin 6 is on the new-UI panel only; the in-game screenshot predates the order flags (capture
pending).

| #   | Catalog id            | Title                                                                          | Docs                                                                        | Code evidence                                                                                             |
| --- | --------------------- | ------------------------------------------------------------------------------ | --------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------- |
| 1   | `opt.title`           | Options / Content Manager title                                                | [Live reload](./live-reload.md)                                             | [`mod/Mod.cs`](../../mod/Mod.cs)                                                                          |
| 2   | `opt.toggles`         | Feature toggles (live ticker, experimental Index)                              | [City Index stock](../client/city-index-fund.md)                            | [`mod/Mod.cs`](../../mod/Mod.cs), [`ModSettings`](../../src/FinanceOverhaul.Core/Settings/ModSettings.cs) |
| 3   | `opt.built-utc`       | Dev Built (UTC) footer                                                         | [Live reload](./live-reload.md)                                             | [`mod/Mod.cs`](../../mod/Mod.cs)                                                                          |
| 4   | `opt.built-local`     | Dev Built (local) footer                                                       | [Live reload](./live-reload.md)                                             | [`mod/Mod.cs`](../../mod/Mod.cs)                                                                          |
| 5   | `opt.copy-build-info` | Copy build info button                                                         | [Live reload](./live-reload.md)                                             | [`mod/Mod.cs`](../../mod/Mod.cs)                                                                          |
| 6   | `opt.order-flags`     | Order type flags (limit buy, stop-loss, take-profit, trailing stop; temporary) | [Automated orders](../features/automated-orders.md#options-flags-temporary) | [`mod/Mod.cs`](../../mod/Mod.cs), [`ModSettings`](../../src/FinanceOverhaul.Core/Settings/ModSettings.cs) |

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
| 1   | `idx.listing`      | Index listing        | `mod/Engines/IndexListing.cs`                |
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
| 2   | `ticker.symbol`    | Symbol           | `SymbolBook` via `StockSymbols.For` (prefab-keyed)   |
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

### Stock Monitor frame (`shipped`; Esc to close planned)

Band 10: the floating Stock Monitor — in place over a real screenshot (50%) and at 1:1: list window,
detail view, settings popover, Options flag ([Stock Monitor](../features/stock-monitor.md)). Below
the 1:1 row, drawn before the build and now shipped: one frame per filter tab (All, Portfolio,
Orders and the two empty states); a sorting table (which sort each tab offers) with the Orders tab's
three sort states; and the Orders tab with compact rows. Orders are grouped by stock with a line per
order (the chosen layout); the two layouts not chosen are kept dimmed. Title bars use the in-game
geometry (40 px bar, 24 px gear beside a 32 px close button flush right).

| #   | Catalog id               | Title                  | Code evidence / status                                                           |
| --- | ------------------------ | ---------------------- | -------------------------------------------------------------------------------- |
| 1   | `monitor.window`         | Window                 | Shipped; solid on hover (City Vitals); Esc planned                               |
| 2   | `monitor.settings`       | Settings (⚙)           | Shipped: popover (`StockMonitorPopover`)                                         |
| 3   | `monitor.filter`         | Filter + sort          | Shipped                                                                          |
| 4   | `monitor.row`            | Row                    | Shipped; click → the game's Invest dialog (2026-11-26)                           |
| 5   | `monitor.sparkline`      | Sparkline              | Shipped (`Sparkline`, 30 game days)                                              |
| 6   | `monitor.change`         | Change pill            | Shipped (black text; click flips ₡ / %)                                          |
| 7   | `monitor.hover`          | Hover                  | Shipped (`StockMonitorHover`)                                                    |
| 8   | `monitor.detail`         | Detail                 | Retired 2026-11-26: moved into the Invest dialog (`OrderTicket.Info`)            |
| 9   | `monitor.stats`          | Stats                  | In the Invest dialog (`FO/StatGrid`, `StockDetail`)                              |
| 10  | `monitor.trade`          | Buy / Sell             | The row click opens the game's Invest dialog                                     |
| 11  | `monitor.open`           | Toolbar button         | Shipped (top-left mod row, right-drag to move)                                   |
| 12  | `monitor.flag`           | Options flag           | Shipped: Stock Monitor window (testing)                                          |
| 13  | `monitor.tabs`           | Tabs + order count     | Shipped (`MonitorOrders.TabLabel`)                                               |
| 14  | `monitor.orders-badge`   | Orders badge           | Shipped (`StockMonitor.OrdersBadge`); All and Portfolio rows                     |
| 15  | `monitor.order-line`     | Order line             | Shipped (`MonitorOrders.Line`, `FO/MonitorOrderLine`)                            |
| 16  | `monitor.order-distance` | Distance to trigger    | Shipped: "₡6 away · 9.1%" (`MonitorOrders.DistanceText`)                         |
| 17  | `monitor.order-paused`   | Paused order           | Shipped: muted, "· paused in Options", no distance                               |
| 18  | `monitor.empty`          | Empty state            | Shipped (`StockMonitor.EmptyText`)                                               |
| 19  | `monitor.order-compact`  | Compact rows           | Shipped: one line per order (`MonitorOrders.CompactText`)                        |
| 20  | `monitor.orders-sort`    | Sort on the Orders tab | Shipped: Nearest (default) → Expires → Symbol; own memory (`monitor.ordersSort`) |

### City bonds frame (`shipped`, behind the Options flag)

Band 11: a "City bonds" section on Economy → Loans — placement on a real screenshot (the help strip
plus the gap above the three loan cards, about 1230 × 312 UI units) and the section at 1:1, a
refused state, and the Options flag ([Bonds](../features/bonds.md)). Component `FO/BondRow` is one
owed bond.

| #   | Catalog id                    | Title                  | Code evidence / status                         |
| --- | ----------------------------- | ---------------------- | ---------------------------------------------- |
| 1   | `bonds.section`               | City bonds section     | Shipped (`CityBondsSection`)                   |
| 2   | `bonds.term`                  | Term                   | Shipped (`BondPricing.BaseRate`, `Periods`)    |
| 3   | `bonds.amount`, `bonds.quote` | Amount + live quote    | Shipped (`BondText`, `BondMath.Schedule`)      |
| 4   | `bonds.sell`                  | Sell bond              | Shipped (`BondDesk.Issue`)                     |
| 5   | `bonds.owed`                  | Bonds you owe          | Shipped (`FO/BondRow`, `BondDesk.Redeem`)      |
| 6   | `bonds.budget`                | Payments in the budget | Shipped engine (`BondDesk`, loan-payment line) |
| 7   | `opt.city-bonds`              | Options flag           | Shipped: City bonds (testing), off by default  |

### Roadmap frame (band 04, status board)

Band 04 shows every original idea as a card grouped by area (Funds, Bonds, Commodities, Listings,
Automation, World & facade), coloured shipped / partly / prioritised / not started, matching
[Roadmap status](../features/roadmap.md). Pins mark the priority order set on 2026-11-15; the legend
also gives context for the unranked ideas.

| #   | Priority                                 | Cards                                                      |
| --- | ---------------------------------------- | ---------------------------------------------------------- |
| 1   | Fill notifications                       | Fill notifications                                         |
| 2   | Local company listings and local IPOs    | Local IPOs, Company listings & IPOs                        |
| 3   | Dividend shares                          | Dividend shares                                            |
| 4   | Recurring buys                           | Recurring buys (DCA)                                       |
| 5   | Commodities: resources, utilities, cargo | Resource commodities, Utility commodities, Cargo / freight |
| 6   | External world and other exchanges       | External world, Other exchanges                            |
| 7   | Buying external bonds                    | Buy / hold bonds                                           |

### Notices frame (band 13, `shipped`)

Order and bond notices in the game's Chirper ([Notices in Chirper](../features/notices.md));
component `FO/ChirpNotice`.

| #   | Catalog id              | Title         | Code evidence / status                         |
| --- | ----------------------- | ------------- | ---------------------------------------------- |
| 1   | `notices.fill`          | Fill (buy)    | Shipped (`NoticeText.Filled`, `OrderDesk`)     |
| 2   | `notices.fill`          | Fill (sell)   | Shipped                                        |
| 3   | `notices.expired`       | Expired       | Shipped                                        |
| 4   | `notices.cancelled`     | Cancelled     | Shipped                                        |
| 5   | `notices.batch`         | Daily summary | Shipped (`NoticeBatch`, more than three a day) |
| 6   | `notices.bond-maturity` | Bond repaid   | Shipped (`BondDesk`)                           |
| 7   | `notices.bond-coupon`   | Bond coupon   | Shipped, off by default                        |
| 8   | `opt.notices`           | Options flags | Shipped: notices on, every bond coupon off     |

### Welcome back frame (band 14, `shipped`)

Shown once when a city loads with the mod after days played without it
([Save compatibility](../features/save-compatibility.md#welcome-back-dialog)).

| #   | Catalog id         | Title               | Status                                                 |
| --- | ------------------ | ------------------- | ------------------------------------------------------ |
| 1   | `welcome.dialog`   | When it shows       | Shipped: × does the default, Start fresh (scenario 50) |
| 2   | `welcome.catch-up` | Pay what was missed | Shipped (scenario 49)                                  |
| 3   | `welcome.skip`     | Start fresh         | Shipped, the default (scenarios 48, 50)                |
| 4   | `welcome.index`    | Index shares back   | Shipped (Index copy; also a Brokerage chirp)           |

### Investments list and tab groups (band 20, `planned` behind a dev flag)

Band 20 holds captures of the one list (dev flag "Investments list and research",
[design](../features/investments-and-research.md)), the four "Row layouts" options (not picked), and
"Two columns": the list on the left and the overview on the right with its sub tabs (Market built;
Portfolio, Heatmap, City, Feed designed in Stitch from `DESIGN.md`, not built). Components on the
Components page:

| Component              | States                                                                                                         | Code                                                                                                                 |
| ---------------------- | -------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------- |
| `FO/InvestmentListRow` | Stock, Held, Local, Locked, Order, Heading; Default and Hover (outlined)                                       | `InvestmentsList` (rules: `InvestmentList`)                                                                          |
| `FO/ToggleTabs`        | Dialog side (Buy, Sell, Sell disabled), order types, Monitor filters, chart range, Investments tabs, bond term | Kit `FoToggleTabs`: `OrderTicket`, `StockMonitorWindow`, `StockMonitorDetail`, `InvestmentsList`, `CityBondsSection` |

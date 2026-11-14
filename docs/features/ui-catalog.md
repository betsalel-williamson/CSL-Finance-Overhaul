# UI catalog

Ordered inventory of player-facing UI for Finance Overhaul — the Trackpad-style **feel catalog**
analogue. Figma components and [design tokens](../developer/ui-design-tokens.md) implement this
table; [UI Figma map](../developer/ui-figma-map.md) pins map screens to this inventory.

## Hosts

| Host                   | Toolkit                     | Role                                      |
| ---------------------- | --------------------------- | ----------------------------------------- |
| Options                | Cities `UIHelper`           | Mod settings (shipped today)              |
| Economy Investments    | Vanilla Financial Districts | Trading board — host / observation only   |
| Custom treasury panels | Colossal UI (future)        | Index / bonds / orders when those ship    |
| Figma design library   | Components + variables      | Specimens and pins — not loaded by Cities |

## Options sections (shipped)

Section order: **Finance Overhaul** (single group today).

| Field id               | Label                                                           | Control  | Host    | Status                 |
| ---------------------- | --------------------------------------------------------------- | -------- | ------- | ---------------------- |
| `live-ticker`          | Live Stock Exchange ticker (visual only)                        | Checkbox | Options | shipped                |
| `index-stock`          | Experimental: Index stock (adds a mod-only investment to saves) | Checkbox | Options | experimental           |
| `orders-limit-buy`     | Orders: Limit buys                                              | Checkbox | Options | temporary flag         |
| `orders-stop-loss`     | Orders: Stop-loss sells                                         | Checkbox | Options | temporary flag         |
| `orders-take-profit`   | Orders: Take-profit sells                                       | Checkbox | Options | temporary flag         |
| `orders-trailing-stop` | Orders: Trailing stop sells                                     | Checkbox | Options | temporary flag         |
| `share-charts`         | Share value charts in the Stock Monitor style (testing)         | Checkbox | Options | temporary flag         |
| `stock-monitor`        | Stock Monitor window (testing)                                  | Checkbox | Options | temporary flag         |
| `built-utc`            | Built (UTC): …                                                  | Footer   | Options | shipped (`--dev` only) |
| `built-local`          | Built (local): …                                                | Footer   | Options | shipped (`--dev` only) |

The four order flags are on by default; see
[Automated orders](./automated-orders.md#options-flags-temporary).

Title chrome (`Finance Overhaul {version}`) is Content Manager / Options identity — see
[Live reload](../developer/live-reload.md).

## Vanilla Investments regions (host)

| Region id                 | Label                   | Notes                                       |
| ------------------------- | ----------------------- | ------------------------------------------- |
| `economy-investments-tab` | Investments             | Economy modal tab                           |
| `purchasable-list`        | Purchasable Investments | Left column                                 |
| `row-value`               | Value                   | Vanilla mark                                |
| `row-change-rate`         | Change rate             | Vanilla display                             |
| `buy` / `sell`            | Buy / Sell              | Vanilla trade — untouched by MVP            |
| `portfolio-list`          | Your Portfolio          | Right column                                |
| `portfolio-current-value` | Current value           | Vanilla mark                                |
| `portfolio-total`         | Total value             | Aggregate                                   |
| `portfolio-avg-cost`      | Avg cost / share        | Mod row: average cost basis (cost-basis.md) |
| `portfolio-open-order`    | Open order card         | Mod entry under its holding, with Cancel    |

### Hover tooltip (host)

Appears when hovering a purchasable (or portfolio) row — vanilla `InvestmentTooltip`-class chrome.

| Region id           | Label             | Notes                     |
| ------------------- | ----------------- | ------------------------- |
| `hover-share-chart` | Share value chart | History sparkline / graph |
| `hover-description` | Flavor text       | Sector blurb              |

### Invest / Buy dialog (host)

Opens after clicking Buy (or equivalent) on a row — buy amount + confirm.

| Region id              | Label                 | Notes                                                                            |
| ---------------------- | --------------------- | -------------------------------------------------------------------------------- |
| `invest-dialog-title`  | Invest in: {name}     | Dialog chrome                                                                    |
| `invest-share-chart`   | Share value chart     | Same family as hover chart                                                       |
| `invest-description`   | Flavor text           | Sector blurb                                                                     |
| `invest-current-value` | Current value / share | Mark used for order pricing                                                      |
| `invest-share-amount`  | Share amount slider   | Quantity                                                                         |
| `invest-share-field`   | Share amount field    | Typed entry (mod) + steppers — see [Typed share amount](./share-amount-input.md) |
| `invest-total-price`   | Price (total)         | quantity × mark                                                                  |
| `invest-buy-confirm`   | Buy                   | Confirms purchase — untouched by MVP                                             |

Zone-income UI under the market columns is not part of this catalog.

## Planned treasury (documented only)

City index, sector ETFs, bonds, commodities, automated orders, and IPOs stay in
[Market analogs](./market-analogs.md) until the stock hook is solid. Figma shows them as ghost
frames with `planned` pins — not shippable chrome.

## Parity

[UI parity](../glossary/ui-parity.md) is look and interaction parity with this catalog, not source
parity with Figma or with Trackpad Camera Control’s Debug host.

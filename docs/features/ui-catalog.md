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

Two sections. **Preferences** (how a shipped feature looks or behaves) show in every build under
**Finance Overhaul**. **Feature flags** (whether a feature exists at all) show only in `--dev`
builds under **Feature flags (dev builds only)**; release builds hide them and use the release value
(`ModSettings.PinReleaseFlags`, unit test `ReleaseFlagsTests`).

### Preferences (every build)

| Field id            | Label                                                    | Control  | Default                       |
| ------------------- | -------------------------------------------------------- | -------- | ----------------------------- |
| `live-ticker`       | Live Stock Exchange ticker (visual only)                 | Checkbox | on                            |
| `ticker-shows`      | Stock Exchange ticker shows                              | Dropdown | Change %                      |
| `ticker-info-views` | Show the LED ticker in info views (traffic, population…) | Checkbox | on                            |
| `ticker-brightness` | LED ticker brightness                                    | Dropdown | Bright                        |
| `orders-lifetime`   | Orders stay open for (new orders)                        | Dropdown | 90 days                       |
| `notices`           | Order and bond notices in Chirper                        | Checkbox | on                            |
| `notices-coupons`   | Also chirp every bond coupon                             | Checkbox | off (shown when bonds are on) |
| `built-utc`         | Built (UTC): …                                           | Footer   | `--dev` only                  |
| `built-local`       | Built (local): …                                         | Footer   | `--dev` only                  |

### Feature flags (dev builds only)

| Field id               | Label                                                      | Release value |
| ---------------------- | ---------------------------------------------------------- | ------------- |
| `orders-limit-buy`     | Orders: Limit buys                                         | on            |
| `orders-stop-loss`     | Orders: Stop-loss sells                                    | on            |
| `orders-take-profit`   | Orders: Take-profit sells                                  | on            |
| `orders-trailing-stop` | Orders: Trailing stop sells                                | on            |
| `stock-monitor`        | Stock Monitor window                                       | on            |
| `share-charts`         | Share value charts in the Stock Monitor style              | on            |
| `index-stock`          | Index stock (adds an investment to saves)                  | on            |
| `city-bonds`           | City bonds (sell bonds to raise money; adds mod save data) | on            |
| `dev-test-stock`       | Dev: Test Stock (TST) for game UI tests                    | off           |

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

Local IPOs plan one window and one preference
([The IPO moment](./local-ipos.md#the-ipo-moment-planned)): an IPO dialog in the mod's own windows,
and "Show a dialog when a local company goes public" (checkbox, on by default) under Preferences,
shown while Local IPOs is on. Neither is shipped.

## Parity

[UI parity](../glossary/ui-parity.md) is look and interaction parity with this catalog, not source
parity with Figma or with Trackpad Camera Control’s Debug host.

# Market analogs

Real-world instruments mapped onto Financial Districts so the Stock Exchange can become city
treasury management instead of day-trading thirteen volatile names. The sections below are the
planned product surface after the stock-hook MVP — bonds and commodities are first-class future
features alongside funds, automation, and IPOs.

## City index funds

A single weighted fund over all listed city sectors. Price is the weighted average of existing DLC
sector prices. Population, land value, and overall production move the index; a single local
disaster does not wipe the position.

**First shippable slice:** a tradable stock named **Index** (equal-weight basket of the other
sector names) — see [City index fund](./city-index-fund.md).

## Sector ETFs

Grouped baskets such as Industrial & Cargo (oil, ore, forestry, farming, freight) or Civic &
Utilities (healthcare, power, water, transit). City infrastructure investments move basket returns.

## High-dividend funds

Mature service sectors with low price growth and periodic cash dividends into the city budget
(weekly or monthly in-game cadence).

## Municipal and infrastructure bonds

[Municipal bonds](../glossary/municipal-bond.md) — fixed-income treasury tools for late-game surplus
and big builds:

- **Buy / hold bonds** — lock surplus cash for a fixed in-game term (for example 1–2 years) for
  guaranteed, low-risk interest paid into the city budget.
- **Issue bonds** — raise instant cash for a highway, metro, or harbor project, then repay principal
  plus interest on a schedule so debt service shows up as a budget line.
- **Infrastructure series** — optional bond flavors tied to a project type (transit, utilities,
  education) so yields and default risk track how well that service performs.

Bonds stay out of the day-trading panel: they are treasury instruments, not volatile sector stocks.

## Commodities

[Commodities](../glossary/commodity.md) — real-economy materials that sit beside equities on the
exchange:

- **Resource commodities** — oil, ore, forestry, and farm goods priced from Industry production,
  storage, and freight throughput (not only the matching sector equity).
- **Utility commodities** — power and water as tradable forward-style contracts when supply is tight
  or abundant, so grid and reservoir investments move marks without micromanaging thirteen stocks.
- **Cargo / freight basket** — a light commodity index over outbound goods so harbor and rail
  expansions show up in treasury returns.

Commodity marks feed future sector ETFs and black-swan shocks; they do not replace vanilla resource
mechanics — they mirror them for the treasury UI.

## Company listings and IPOs

Additional issuer names (including project / studio company Easter eggs) that enter the exchange
through IPO events with roadshow unlock conditions, float size, and opening volatility — still tied
to simulation signals, not RNG-only casinos.

## Automated order types

Limit, stop-loss, trailing stop, dollar-cost averaging, and dividend reinvestment so mayors set
rules once and keep building. Notifications fire when an order fills.

## Performance note

Index and ETF marks reuse vanilla sector prices. Weighted averages are cheap; hooks stay out of
per-frame camera paths.

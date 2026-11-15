# Roadmap status

Where the original ideas stand ([Market analogs](./market-analogs.md),
[World dynamics](./world-dynamics.md), the Figma "Planned treasury" band) and what could come next.
Status as of 2026-11-15. Figma: UI Map band 04 "Roadmap — status" shows the same cards.

## Original ideas: 7 of 24 shipped, 1 partly, 16 not started

| #   | Idea                                                       | Group      | Status                                                                                |
| --- | ---------------------------------------------------------- | ---------- | ------------------------------------------------------------------------------------- |
| 1   | City index fund                                            | Funds      | **Shipped** (experimental flag): Index stock, geometric (Jevons) average, rebased     |
| 2   | Sector ETFs (e.g. Industrial & Cargo, Civic & Utilities)   | Funds      | Not started                                                                           |
| 3   | High-dividend funds (periodic cash into the budget)        | Funds      | Not started (treasury piece 3, "dividend investments")                                |
| 4   | Issue city bonds (raise money)                             | Bonds      | **Shipped** (flag): math, pricing, daily engine, Economy → Loans section              |
| 5   | Buy / hold bonds (lend surplus for interest)               | Bonds      | Not started: **next treasury piece**; the schedule maths already works for both sides |
| 6   | Infrastructure bond series (transit, utilities, education) | Bonds      | Not started                                                                           |
| 7   | Resource commodities (oil, ore, forestry, farm)            | Commodity  | Not started                                                                           |
| 8   | Utility commodities (power, water)                         | Commodity  | Not started                                                                           |
| 9   | Cargo / freight basket                                     | Commodity  | Not started                                                                           |
| 10  | Company listings and IPOs                                  | Listings   | Not started                                                                           |
| 11  | Local IPOs (companies grown in the city list)              | Listings   | Not started                                                                           |
| 12  | Dividend shares (and dividend reinvestment orders)         | Listings   | Not started                                                                           |
| 13  | Other exchanges (neighbour / regional boards)              | Listings   | Not started                                                                           |
| 14  | Interface for many companies (search, group, watchlist)    | Listings   | Partly: Stock Monitor filters, sort and direction; Investments panel unchanged        |
| 15  | Limit buy orders                                           | Automation | **Shipped**                                                                           |
| 16  | Stop-loss sells                                            | Automation | **Shipped**                                                                           |
| 17  | Take-profit sells                                          | Automation | **Shipped**                                                                           |
| 18  | Trailing stop sells                                        | Automation | **Shipped**                                                                           |
| 19  | Dollar-cost averaging (recurring monthly buys)             | Automation | Not started                                                                           |
| 20  | Fill notifications                                         | Automation | Not started (fills are only logged; Chirper planned)                                  |
| 21  | Ordinary market cycle (booms, corrections)                 | World      | Not started                                                                           |
| 22  | Black swan events                                          | World      | Not started                                                                           |
| 23  | External world pressure (demand spikes, rate shocks)       | World      | Not started                                                                           |
| 24  | Live Stock Exchange ticker (stretch goal)                  | Facade     | **Shipped**: LED boards, % or price and ₡, brightness, info views, full at start      |

Shipped: 1, 4, 15–18, 24. Partly: 14. Everything else is not started.

## Shipped beyond the original list

Typed share amount, average cost per share, open orders in Your Portfolio with an order lifetime
option, the Stock Monitor window (list, detail, hover, settings, sort and direction), share value
charts in the Monitor style, unique ticker symbols, and the game UI test runner that freezes the
player's own orders.

## Open loose ends

- Save compatibility matrix: Index cases 1, 3, 4, 5 and city bonds not yet run in game.
- Bonds: a payment that takes cash below zero is not yet tested in game.
- Game UI scenario 14 assumes the city holds Oil Industry.
- Esc to close the Stock Monitor; exact alignment of our chart grid with the vanilla grid.
- Parked by choice: price history longer than 30 days (6M–10Y).

## Next-step candidates

1. **Buy bonds** — the natural next treasury piece: lend surplus cash for a fixed term at a quoted
   rate, coupons into the budget, sell early at a price (needs price/yield maths with proofs).
2. **Recurring buys (DCA)** — a monthly automated order on top of the shipped order engine; small.
3. **Fill notifications** — Chirper (or a toast) when an order fills or expires; small.
4. **Dividend investments** — periodic cash payouts; unlocks dividend reinvestment orders.
5. **Sector ETFs** — baskets reusing the Index maths (weighted geometric averages).
6. **Save matrix** — run the remaining save-compatibility cases (Index, bonds) before more state
   lands in saves.

# Roadmap status

Where the original ideas stand ([Market analogs](./market-analogs.md),
[World dynamics](./world-dynamics.md), the Figma "Planned treasury" band) and what could come next.
Status as of 2026-11-15. Figma: UI Map band 04 "Roadmap — status" shows the same cards.

## Original ideas: 8 of 24 shipped, 1 partly, 15 not started

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
| 20  | Fill notifications                                         | Automation | **Shipped**: [Notices in Chirper](./notices.md), orders and bonds                     |
| 21  | Ordinary market cycle (booms, corrections)                 | World      | Not started                                                                           |
| 22  | Black swan events                                          | World      | Not started                                                                           |
| 23  | External world pressure (demand spikes, rate shocks)       | World      | Not started                                                                           |
| 24  | Live Stock Exchange ticker (stretch goal)                  | Facade     | **Shipped**: LED boards, % or price and ₡, brightness, info views, full at start      |

Shipped: 1, 4, 15–18, 20, 24. Partly: 14. Everything else is not started.

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

## Priorities (set 2026-11-15)

Worked one at a time in this order (single-piece flow: maths with proofs → engine → Figma → UI →
game UI test → docs), each behind its own Options flag until proven.

| Priority | Work                                                              | Ideas covered | Size | Notes                                                                                                                                                                                                           |
| -------- | ----------------------------------------------------------------- | ------------- | ---- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1 ✓      | Fill notifications                                                | 20            | S    | **Shipped 2026-11-15** ([Notices](./notices.md)). Chirper message (or a toast) when an order fills, expires or is skipped, and when a bond coupon or repayment goes out.                                        |
| 2        | Local company listings and local IPOs ([design](./local-ipos.md)) | 10, 11        | L    | Companies the city grew (large office, industry or commercial employers) list on the exchange through an IPO event. Needs new mod-owned listings in the save and, past a dozen names, the many-company UI (14). |
| 3        | Dividend shares                                                   | 12            | M    | Listings that pay a periodic cash dividend into the budget; unlocks dividend reinvestment as an order type. Overlaps high-dividend funds (3).                                                                   |
| 4        | Recurring buys                                                    | 19            | S    | A monthly automated buy (dollar-cost averaging) on the shipped order engine.                                                                                                                                    |
| 5        | Commodities market: resources, utilities, cargo/freight           | 7, 8, 9       | L    | Marks priced from Industry production, storage, power and water supply, and outbound freight; mirrors vanilla mechanics for the treasury UI.                                                                    |
| 6        | External world and other exchanges                                | 21, 13        | L    | Light outside pressure (demand spikes, rate shocks) on existing prices, and neighbour or regional boards beside the local exchange.                                                                             |
| 7        | Buying external bonds                                             | 5             | M    | Lend surplus cash to outside issuers for a fixed term at a quoted rate; coupons into the budget; sell early at a price. The bond schedule maths already covers the holder's side.                               |

## Unranked ideas: context for choosing

Each with what the player would see, what drives it, dependencies, size and save impact.

### Sector ETFs (idea 2)

- **Player sees:** a few basket listings, e.g. "Industrial & Cargo" (oil, ore, forestry, farming,
  freight) and "Civic & Utilities" (healthcare, deathcare, transit), tradable like a stock, smoother
  than any single name.
- **Driven by:** the member stocks' prices, combined with the same geometric (Jevons) chain the
  Index uses, so the maths and its proofs carry over.
- **Depends on:** nothing new; benefits from commodities (5) if baskets should include them.
- **Size / save:** M. Adds listings to the save like the Index (same experimental-flag pattern and
  save matrix).
- **Why unranked might be fine:** the Index already gives a diversified option; ETFs matter more
  once local listings (2) add many names.

### High-dividend funds (idea 3)

- **Player sees:** a fund that pays cash into the budget every month or week, with slow price
  growth.
- **Driven by:** mature service sectors; payout from a yield on the holding's value.
- **Depends on:** dividend shares (3) build the payout engine; a dividend fund is then a basket of
  dividend payers (shares the ETF work above).
- **Size / save:** S once 3 and ETFs exist; otherwise M.
- **Suggestion:** fold into priority 3 as a follow-up rather than a separate epic.

### Infrastructure bond series (idea 6)

- **Player sees:** when issuing a city bond, an optional "series" (transit, utilities, education)
  whose rate moves with how well that service performs (coverage, budget, upkeep).
- **Driven by:** service coverage and budget in that category; a better-run service borrows cheaper.
- **Depends on:** issued city bonds (shipped); pricing gains a service-quality premium with its own
  proofs (bounded, monotone).
- **Size / save:** S–M. One more field per bond in the existing bond book (versioned codec).

### Ordinary market cycle (idea 19)

- **Player sees:** slow booms and mild corrections across the board, visible in the Stock Monitor
  and LED; the Index smoother than single names, bonds steady.
- **Driven by:** city-wide production, cargo throughput and service coverage, as a gentle modifier
  on vanilla price movement (never a casino).
- **Depends on:** a safe hook into the vanilla price step (the share-price hooks exist); pairs
  naturally with external world (6).
- **Size / save:** M; no save data (derived each day).
- **Risk:** changes vanilla balance, so it needs its own flag and a strength setting.

### Black swan events (idea 20)

- **Player sees:** rare shocks (cascading industry shutdowns, disasters, liquidity freezes) that
  drop correlated stocks and commodities together, announced through Chirper (uses priority 1).
- **Driven by:** real city events (disasters, large closures), not pure chance.
- **Depends on:** fill notifications (1) for the announcement; commodities (5) for correlation;
  stop-loss orders (shipped) as the intended hedge.
- **Size / save:** M; a small event log in mod data so a shock isn't replayed on load.
- **Risk:** frustrating if too frequent; needs an Options off switch and a frequency setting.

### Interface for many companies (idea 14, partly)

- **Player sees:** search, sector grouping and a watchlist in the Investments panel and the Stock
  Monitor once listings grow beyond the vanilla thirteen.
- **Driven by:** UI only.
- **Depends on:** becomes necessary with local listings (2) and other exchanges (6).
- **Size / save:** M; settings only (no save data).
- **Suggestion:** build the first slice as part of priority 2.
- **Capacity (measured 2026-11-15, game UI scenario 51):** the game holds **254 investments** (255
  slots; slot 0 is an empty placeholder that is never saved). The `MAX_INVESTMENT_COUNT = 32`
  constant limits nothing. At 33 and at 254 every stock gets a slot and shows in Economy →
  Investments and the Stock Monitor; a 255th and 256th get no slot and are simply not listed (no
  error, nothing overwritten). The daily investment update costs about 0.005 ms at any size; adding
  many at once costs a one-off spike (43 ms for 239), and 254 run like 15 once settled.

### Save compatibility matrix (loose end) — likely next after notices (user, 2026-11-15)

**Why it matters:** the contract in [Save compatibility](./save-compatibility.md) promises that a
city saved with the mod still opens without it (mod disabled, uninstalled or an older version),
losing only mod features, never the city. Every epic that stores something adds a way to break that
promise, and priority 2 (local listings) adds the riskiest kind: new companies in the save.

**What the mod writes into a city save today:**

| Data                                  | Where                                                   | Risk without the mod                                                         |
| ------------------------------------- | ------------------------------------------------------- | ---------------------------------------------------------------------------- |
| Index stock (flag)                    | A vanilla investment slot pointing at a mod-only prefab | Highest: vanilla must skip an unknown prefab (case 2 passed; 1, 3–5 not run) |
| Test stock TST (dev, test saves only) | Same as the Index                                       | Same; dev builds only                                                        |
| Open orders                           | Mod data `FinanceOverhaul.Orders` (vanilla ignores it)  | Low: orders vanish; holdings stay                                            |
| City bonds (flag)                     | Mod data `FinanceOverhaul.Bonds`                        | Low for loading; gameplay: the debt vanishes, the cash already raised stays  |
| Index rebase marker                   | Mod data `FinanceOverhaul.IndexRebase`                  | None                                                                         |
| Settings, Stock Monitor, notices      | The player's settings file, not the save                | None                                                                         |

**The checks** (on copies of a test city, never the player's own save):

1. Saved with the mod, Index off → vanilla: loads, investments identical.
2. Index on, no shares → vanilla: loads, Index absent (**passed 2026-11-13**).
3. Index on with shares → vanilla: loads, other holdings intact.
4. The save from 3 → mod again: Index relisted, no duplicate slots.
5. Index on → mod with Index off: same as 3.
6. Bonds owed → vanilla: loads; bonds gone; cash kept; nothing in the Loans tab breaks.
7. The save from 6 played a few days, then → mod again: bonds come back. **Decision needed:** the
   bond book catches up on missed days, so coupons missed while the mod was off would be paid in one
   lump the first day back. Options: pay the catch-up (honest, could surprise), or skip coupons for
   days the mod was absent (forgiving, needs a "last seen" day in the save).
8. Open orders → vanilla → mod again: orders resume; ones past their lifetime expire (and are
   chirped) on the first day.
9. Saved by today's mod → loaded by an older build: unknown codec versions or lines are skipped
   (covered by codec unit tests; confirm once in game).

**How:** mostly manual game runs (switching the mod off needs a restart), with notes, log excerpts
and screenshots recorded in the Results table of [Save compatibility](./save-compatibility.md); the
game UI runner can still set up the city state before saving and verify after loading.

**Size:** S–M (an afternoon of game restarts). The bond catch-up becomes a player choice in a simple
**Welcome back** dialog (Figma band 14), and the situations and an Alloy model with its results are
in [Save compatibility](./save-compatibility.md): P3 shows today's Index loss, fixed by keeping a
copy of the holding in mod data. Verified in game: the game keeps mod data when saving without the
mod. Built 2026-11-15: the "last seen" bookmark, the Welcome back dialog and the Index copy. All
matrix cases and an Index copy restore passed in game the same day.

**Suggestion:** do it before priority 2, which adds new listings to saves.

## Earlier next-step candidates (superseded by the priorities above)

1. **Buy bonds** — the natural next treasury piece: lend surplus cash for a fixed term at a quoted
   rate, coupons into the budget, sell early at a price (needs price/yield maths with proofs).
2. **Recurring buys (DCA)** — a monthly automated order on top of the shipped order engine; small.
3. **Fill notifications** — Chirper (or a toast) when an order fills or expires; small.
4. **Dividend investments** — periodic cash payouts; unlocks dividend reinvestment orders.
5. **Sector ETFs** — baskets reusing the Index maths (weighted geometric averages).
6. **Save matrix** — run the remaining save-compatibility cases (Index, bonds) before more state
   lands in saves.

# Brokerage mechanics map

Real brokerage surfaces (Charles Schwab research pages: U.S. Markets, International, Research Tools,
a money market fund quote, growth-of-₡10,000, and returns tables) translated into simple
city-treasury mechanics. Each row names the smallest game version, where it would live, and whether
it writes save data ([Save compatibility](./save-compatibility.md)).

Order is the proposed build order: UI-only proofs first, then opt-in instruments.

| #   | Real surface (Schwab)                         | Game mechanic                                                                           | Host UI                              | Save data | Catalog id           |
| --- | --------------------------------------------- | --------------------------------------------------------------------------------------- | ------------------------------------ | --------- | -------------------- |
| 1   | Order ticket quantity box                     | Type an exact share count (shipped as the first native-UI edit)                         | Invest dialog (vanilla)              | None      | `plan.typed-shares`  |
| 2   | Quote header (price, change, 7-day yield)     | Richer row readout: mark, day change, and holding P/L % on each Investments row         | Investments rows (vanilla, extended) | None      | `plan.quote-row`     |
| 3   | Growth of $10,000 chart vs benchmarks         | Dialog chart overlays a benchmark line (city index or loan rate) next to the stock      | Invest dialog chart                  | None      | `plan.benchmark`     |
| 4   | Annualized returns table (1/3/5/10 yr)        | Returns in game years from each investment's value history                              | Hover tooltip / dialog               | None\*    | `plan.returns`       |
| 5   | Market overview ticker (DJIA, NASDAQ, …)      | Strip under Investments: city index, best/worst sector, treasury cash yield             | Investments footer                   | None      | `plan.ticker`        |
| 6   | Sector & Industry view                        | Sector heat grid: change rate per Financial Districts sector                            | New Investments sub-view             | None      | `plan.sector-heat`   |
| 7   | Index / total-market fund                     | **Index** fund, equal weight of sector stocks (experimental toggle today)               | Investments list                     | Yes       | `plan.city-index`    |
| 8   | Money market fund (stable ₡1.00, 7-day yield) | City Treasury Cash: park idle cash, earns a yield tied to loan rates; never loses value | Investments list                     | Yes       | `plan.treasury-cash` |
| 9   | Sector ETFs                                   | Small baskets (Services, Industry, Energy) built like Index                             | Investments list                     | Yes       | `plan.sector-etf`    |
| 10  | Bonds, CDs & fixed income                     | Municipal bonds: fixed term and coupon, paid monthly, principal back at maturity        | New treasury view                    | Yes       | `plan.bonds`         |
| 11  | Watchlist                                     | Star stocks to pin them to the top of Purchasable                                       | Investments rows                     | Optional  | `plan.watchlist`     |
| 12  | Calendar                                      | Upcoming coupons, maturities, and loan payments                                         | New treasury view                    | None      | `plan.calendar`      |
| 13  | Orders (limit / stop)                         | Buy below / sell above a price, checked on the daily investment tick                    | Invest dialog                        | Yes       | `plan.orders`        |
| 14  | International markets / IPOs                  | Neighbor-city index or new listings when buildings unlock                               | Investments list                     | Yes       | `plan.ipo`           |

\*Returns can be computed from the vanilla value history the game already keeps
(`Investment.m_values`), so no extra save data is needed for short windows.

## Rules of thumb

- **Reuse vanilla chrome first.** Rows, dialog, chart, and tooltip already exist; the
  [UI catalog](./ui-catalog.md) and Figma library model them.
- **UI-only before instruments.** Items 1–6 prove we can extend native UI without touching saves.
- **Instruments keep their own state.** Items 7–14 should store holdings in mod data and only
  project them into vanilla lists at runtime, so a vanilla load degrades cleanly.
- **One real mechanic per instrument.** Equal weight (index), stable value plus yield (money
  market), fixed coupon (bonds). No hidden formulas the player can't read off the UI.

## Stretch goals

- **Live exchange ticker** (`plan.live-ticker`). The Stock Exchange building's facade boards scroll
  abbreviations with a change in brackets (for example `FC (+0.54)`, `Oil (-6.04)`). They are
  **static art baked into the building texture** (no code feeds them; values never change; see
  [Known issues](./known-issues.md)). To show live numbers, including **Index** and never `NaN`, the
  mod must render its own text on the facade (for example a text mesh or a generated texture placed
  over the boards on each Stock Exchange instance). Host-only and save-neutral.

  **Step 1 (symbols + live values):** `TickerSymbols.For` maps each investment's display name to a
  symbol (the artwork's own symbols where an investment matches, e.g. TJL, TVLC, DS, HS, FaI, FoI,
  OrI, Oil, CAi, CCr, CC; new ones `GeI`, `GGI`, `IDX`; a fallback rule for unknown names).
  `TickerFeed` builds the line once per in-game day from vanilla `GetLocalizedName` and
  `CalculateChangeRate(id, ref data, 5)`, the exact calls behind the Economy panel's Change rate,
  for unlocked investments, formatted by `TickerLine` as `SYM ( +2.67 )` and never `NaN` (`( -- )`).
  Logged as `ticker: …` and kept in `TickerFeed.CurrentLine`.

**Step 2a (shipped, calibrating):** `TickerBoards` / `TickerOverlay` draw one live board (three
rich-text rows from `TickerFeed.CurrentRows`, ticker token colors) on the lot-front facade of every
Stock Exchange via `Building.CalculatePosition` (local +Z = sidewalk side), refreshed every second.
OS serif font (Goudy Old Style → Hoefler Text → Baskerville → Georgia → Times). Options toggle
**Live Stock Exchange ticker** (default on, save-neutral). Known limit: Unity's font material draws
on top of other geometry. Next: calibrate placement from screenshots, depth-tested material,
scrolling, then hide or retexture the baked boards.

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

  **Step 1 (symbols + live values):** `SymbolBook` gives every stock prefab its own symbol, keyed by
  **prefab name** (the same in every language and game version; display names are not — the game's
  `Chirp Coin` prefab now shows as "ChirperCrypto"). The game's 13 prefabs and the mod's `Index` /
  test stock have fixed symbols, using the facade artwork's where it has one: CHC (ChirperCrypto),
  CAi, CCr, DS, HS, GGI, FaI, FoI, GeI, Oil, OrI, TJL, TVLC, IDX, TST. Any other prefab (a game
  update, another mod) gets a candidate from its display name, extended with more letters, then a
  digit, until it is unique; fixed symbols are reserved even when unlisted. Symbols are display only
  (orders and saves key on prefab names). The dev `log-symbols` step lists every prefab with its
  symbol, and game UI scenario `28-symbols-unique` fails on any clash. `TickerFeed` builds the line
  once per in-game day from vanilla `GetLocalizedName` and `CalculateChangeRate(id, ref data, 5)`,
  the exact calls behind the Economy panel's Change rate, for unlocked investments, formatted by
  `TickerLine` as `SYM ( +2.67 )` and never `NaN` (`( -- )`). Logged as `ticker: …` and kept in
  `TickerFeed.CurrentLine`.

**Step 2a (shipped):** `TickerBoards` / `TickerOverlay` draw three live boards exactly over the
vanilla boards, read from the Stock Exchange's three `AnimUV` sub-meshes (centre x −7.7…7.7, y
15.2…18.6; sides x ±5.5…20.8, y 10.8…14.1; face z 23.6). Entries are sorted alphabetically by symbol
and split over three rows. Options toggle **Live Stock Exchange ticker** (default on, save-neutral).

**Step 3 (shipped):** the baked boards are hidden by requiring `Building.Flags.Deleted` on the three
`AnimUV` sub-meshes (`MeshInfo.m_flagsRequired`, honoured by `BuildingAI.RenderMeshes`): runtime
only, restored on toggle-off, unload, disable and hot reload. The vanilla board meshes (frame, 0.3 m
bezel, recess) are redrawn with a plain lit near-black material, and unlit screens
(`Unlit/Transparent` with a 1×1 `ticker/board` texture, geometry queue) replace them and stay dark
in daylight like the self-lit vanilla boards.

**Step 4 (shipped): LED scroller.** Each screen shows three LED dot-matrix rows (`LedFont`, a
proportional 5×7 font with one descender row; about 173 dots across at 0.083 m pitch). Every row is
a traditional ring-buffer marquee (`LedMarquee`, Market, unit tested):

- the window offset only moves forward, in whole dots (14 dots/s), and is never reset;
- the next entry, with the newest values, is written into the ring just past the right edge;
- colours follow real tickers on the printed value: up green, down red, unchanged `0.00` and unknown
  `--` neutral grey (`LedText.RateColour`);
- columns inside the window are never redrawn, so values on screen never change and daily updates
  never make the scroll jump; a changed list of investments simply scrolls in.

Each row is one repeat-wrapped texture shared by every Stock Exchange (4×4 texels per dot with a
dark gap, mipmapped), drawn on a depth-tested `Unlit/Transparent` quad (queue 2001, just after the
panel, which sits 2 cm behind). Text therefore clips exactly at the screen edge and hides behind
buildings in front of the facade. Only freshly written columns are repainted (one entry every few
seconds per row); textures and materials are destroyed on hot reload.

**Performance:** visibility (LOD) every 0.25 s; LED rows draw and scroll only within 600 m of the
camera. When they come back into range the rows are refilled from today's values while still hidden.
Stock Exchange scan every 5 s (at once when a building with boards is removed); boards follow their
building every frame, so a moved or turned Stock Exchange keeps them on its facade. Verified
2026-11-13 with the dev in-game capture (`FinanceOverhaul.capture` trigger, `--dev` builds).

**Step 4b (shipped): what each entry shows.** Options → **Stock Exchange ticker shows**
(`TickerDisplay`, settings key `tickerShows`, visual only):

- **As the change pills show it (default, 2026-11-27):** `DS +3.22%` while the pills show %,
  `DS ₡951 +₡30` once a click on any change pill flips them to points (the Investments list's pills,
  the Stock Monitor's while the list follows it; `ModSettings.TickerShowsNow`). Settings written by
  older builds (`changePercent`, their default) read as this, which looks the same until a pill is
  flipped; a fixed % is now `changePercentOnly`.
- **Change %:** `DS +3.22%`, the game's change rate with a % sign, the same text as the Stock
  Monitor's % pill.
- **Price and ₡ change:** `DS ₡951 +₡30`, like an exchange tape. The ₡ change is the same rate in ₡
  (`price × r / (1 + r)`), the Monitor's ₡ pill.

Signs are + and − (no arrows), colour green, red or grey by the one rule (`LedText.RateColour`), and
the glyphs `% , ₡ −` were added to `LedFont`. The boards read the style before drawing their first
screen (level load, reload), and a switch redraws every row's whole screen in the new style at once,
so a screen never mixes styles (`ARestartedScreenIsAllInOneStyle`; scenario 40
`expect-ticker-style`). Proved over 5,000 seeded quotes (`TickerDisplayTests`): every character has
a glyph, the text equals the Monitor's pill text, the printed sign agrees with the colour in both
modes, and moves within ±50% fit the marquee slot (wilder ones are clipped). Game UI scenario 40
switches modes and logs the entries. The daily log line keeps the older `SYM ( +2.67 )` form.

**Step 4c (shipped): brightness.** Options → **LED ticker brightness** (`LedBrightness`, key
`tickerBrightness`, visual only): **Classic** (the Figma tokens), **Bright** (default: every lit
colour scaled to full strength, darker unlit dots, lit dots glow 30% into the 1-texel gap around
them) and **Brightest (daylight)** (also lifted 30% toward white, near-black unlit dots, 55% glow).
The gap matters most: it is about 40% of each dot, and the game blends it with the dot at a
distance. Proved in `LedPaletteTests`: a brighter level never lowers a lit dot's perceived
brightness, keeps its hue family, grows lit-to-unlit contrast, and the glowing gap stays dimmer than
its dot. A change repaints the boards on the next scroll step; game UI scenario 42 captures each
level.

**Step 4d (shipped): info views.** Options → **Show the LED ticker in info views (traffic,
population…)** (key `tickerInInfoViews`, default on: the boards stay). Off: while an info view is
open the live boards hide and the vanilla board sub-meshes are restored, so the facade gets the
game's own flat info-view look like the original; they come back when the view closes
(`ModSettings.TickerShownNow`). The swap runs inside the game's `InfoManager.EventInfoModeChanged`,
raised when the view actually switches and before that frame is drawn, so the vanilla boards never
flash their baked scroll on the way back; scenario 43 checks the swap at that moment
(`info-view-swap`). Game UI scenario 43 opens Traffic and Population with the option on and off; the
player's info view, live-ticker toggle and checkbox are put back afterwards.

**Step 4e (shipped): full boards at start.** On level load or a mod reload the boards no longer
scroll "Waiting for market data" until the next game day: `TickerFeed.Prime` builds today's entries
from the same read-only calls as soon as the boards appear (main thread; the daily build and the
prime share a lock), and the first data fills every row's whole window at once. Checked in game with
the simulation paused across a reload (`ticker: primed 10 entries at start`; test steps `pause` and
`expect-ticker-full`). The waiting text remains only for a city with no unlocked investments.

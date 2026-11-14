# City index fund (Index stock)

> **Status: experimental, off by default.** Enable in Options (works mid-game; persisted in
> `FinanceOverhaul.settings.txt`). Blocked on the [Save compatibility](./save-compatibility.md) test
> matrix; not yet verified in game.

## Value

Mayors can buy and hold a single **Index** stock that tracks all listed Financial Districts sector
stocks together — like a Vanguard-style total-market fund — instead of micromanaging thirteen
volatile names.

## Scope

| In                                                                | Out                                   |
| ----------------------------------------------------------------- | ------------------------------------- |
| One tradable stock named **Index** on the Investments board       | Sector ETFs, bonds, commodities, IPOs |
| Mark = equal-weight average of the other sector share prices      | Custom weights / smart beta           |
| Buy / sell through vanilla Investments chrome                     | Separate treasury UI                  |
| Fail soft if Financial Districts or listing hooks are unavailable | Rewriting vanilla Investment prefabs  |

Population / land-value drivers and black-swan dampening stay in
[World dynamics](./world-dynamics.md) for later; this slice only ties Index’s price to the live
sector basket.

## Contracts

1. **Listing** — With the mod enabled and Financial Districts present, the purchasable Investments
   list includes a stock titled **Index** (exact display name).
2. **Mark** — Index is a chain-linked **equal-weight geometric** index (Jevons): each daily update,
   `Index = previous Index × geometric mean of the members' price ratios (today ÷ yesterday)`.
   Members are the **unlocked share-priced** investments (no coins, no Index itself); each member's
   daily move is capped at a halving or a doubling, applied to the log ratio so the cap is
   symmetric. A brand-new Index seeds from the members' price average. The hook runs after vanilla
   random variance, so Index carries none. See [Index math](#index-math).
3. **Trading** — Players buy and sell Index with the same Buy / Sell / share-amount flow as any
   other investment.
4. **Fail soft** — If the Index listing cannot be created or sector prices cannot be read, log a
   clear message and leave the vanilla thirteen names alone.

## Implementation

- **Market** — `CityIndexBasket.TryComputeMark` owns the basket rules: exclude Index, keep only
  eligible share stocks with finite positive marks, equal weight, and return `false` on an empty
  basket so the Host keeps the vanilla value.
- **Host** — a Harmony postfix on `InvestmentCollection.Awake` clones a share-priced investment
  prefab (prefers one with no unlock milestone, clears the clone's milestone and relinks
  `InvestmentAI.m_info`), renames it **Index**, and registers it with
  `PrefabCollection<InvestmentInfo>`. Vanilla `EconomyManager.LateUpdateData` then creates one
  investment per loaded prefab, so listing, saving, and Buy / Sell all stay vanilla.
- **Mark** — one postfix on `InvestmentAI.SafeCalculateValue` (base class, after variance) sets the
  Index value from `CityIndexBasket.TryComputeChainedMark`, reading each member's last two values
  from its vanilla history (`allValues.GetFromTop(0/1)`). Prefixes on `GetLocalizedName` /
  `GetLocalizedDescription` supply the display text. `InvestmentCollection.OnDestroy` unregisters
  the prefab.
- **Removal** — if the mod is disabled, vanilla drops the investment whose prefab is missing on the
  next load. Index shares held at that point are lost and are not refunded.
- **History backfill** — a new or re-created Index rebuilds the days before its oldest real point by
  running the chain-linked rule backwards over the members' vanilla histories
  (`CityIndexBasket.BackfillOlder`), up to `Investment.kPastValueCount` points, so its chart shows
  the same window as other stocks instead of a flat line. Runs on `InvestmentAI.CreateInvestment`
  and when a loaded city's Index is shorter than its members. Real Index points are never changed.
- **Mid-game / hot reload** — `IndexListing.EnsureInLoadedCity` adopts an Index prefab an older
  assembly already registered, or clones and registers one, calls `BindPrefabs` (which appends
  unbound scene prefabs to the simulation list), then queues `EconomyManager.CreateInvestment` on
  the simulation thread. Runs on toggle-on, after `PatchAll`, and after level load.
- **Readout** — after a level loads, `finance-overhaul.log` shows a line like
  `index: listed=True value=… basket=… sectors=13`.

## In-game verification

- **2026-11-13 (hot reload, city loaded):** Index listed under Purchasable Investments (cloned from
  `TrafficJelly`, the only share stock with no unlock milestone; money-stack icon), bought 100
  shares, Your Portfolio row and **Sell: Index** dialog correct. Bug found and fixed: the cloned AI
  lost its `m_info` back-link and threw in `InvestmentAI.IsAvailable` every tick.
- **2026-11-13 history backfill verified:** an Index re-created after a vanilla re-save had one
  point (flat chart at ₡1,465, game paused). After hot reload the mod logged
  `backfilled 29 older day(s) before 1 real point(s)` and the Invest dialog showed a full 30-day
  line (~₡1,300–₡1,445) like the other stocks.
- **2026-11-13 volatility fix verified:** before, Index moved up to −11.91% per update (chart
  ₡1,100–₡1,800, average of prices plus vanilla variance). After the chain-linked equal-weight
  return mark, updates moved −0.30% to +2.51% over 7 unlocked members, and the chart tail held
  ~₡1,320–₡1,370.

## Acceptance

- Market unit tests cover equal-weight Index marks (empty basket, NaNs skipped, Index excluded from
  inputs).
- In-game: Index appears on Investments; its Value moves with the sector basket; Buy/Sell works.
- [Market analogs](./market-analogs.md) city-index section points here as the first shipped fund
  slice.

## Index math

**Properties** (unit tested over 200 seeded random baskets in `CityIndexPropertyTests`). For a fixed
basket of n members with prices p(i,t):

- **Path independence:** after T days, `Index_T = Index_0 · Π_i (p(i,T) / p(i,0))^(1/n)`, whatever
  happened in between.
- **Time reversal:** the same days played forward and then backward return the Index exactly to
  where it started.
- **Identity and proportionality:** no member moves → no change; every member × k → Index × k.
- **Mean-value bounds:** a day's move lies between the smallest and largest member move.
- **Commensurability:** a member's price level (₡50 or ₡5,000) does not change its weight.
- **No drift:** stocks that only oscillate between fixed levels leave the Index where it was.

**Why it changed (2079 in the test city):** the Index was first the arithmetic mean of members'
daily returns (a chained Carli index). By the AM–GM inequality that mean is ≥ the geometric mean
every day, strictly whenever members move differently, and the gap compounds. The game's stocks
swing hard around fairly stable levels (Oil moved between ₡46 and ₡73 from one day to the next), so
the old Index climbed far above its members. On the city's real last 30 days, the old rule gave
×1.191 against ×1.112 for the geometric rule (which equals the members' own end ÷ start). Replaying
those 30 days forward then backward, the old rule ended up ×1.148 instead of exactly ×1.000. The
backfill of older Index history uses the exact inverse of the same geometric step.

**One-time rebase (cities that ran the old rule):** an Index more than ×2 away from its members'
average price is scaled — its current value and its whole history, by one factor — to that average,
the level a fresh Index seeds at. Every day's move in the chart stays the same, so there is no
cliff. It runs at most once per city (marker `FinanceOverhaul.IndexRebase` in the mod's save data),
inside the Index's daily update, and waits while the city holds Index shares or has open Index
orders (rescaling would change a holding's value or fire orders set at the old level). Logged as
`index: rebase …`. In the test city (2079): ₡97,712 → ₡1,453, with the 30-day chart at
₡1,417–₡1,492.

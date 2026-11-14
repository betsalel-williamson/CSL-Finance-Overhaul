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
2. **Mark** — Index is a chain-linked **equal-weight return** index: each update,
   `Index = previous Index × (1 + average latest % change of the members)`. Members are the
   **unlocked share-priced** investments (no coins, no Index itself); each member's single-update
   move is clamped to −50%…+100%. A brand-new Index seeds from the members' price average. The hook
   runs after vanilla random variance, so Index carries none.
3. **Trading** — Players buy and sell Index with the same Buy / Sell / share-amount flow as any
   other investment. Probe bias, when enabled, may affect hooked sector marks and therefore the
   Index mark.
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
- **Open:** Index swings more than expected (for example -11.91% in one update, chart range
  ₡1,100–₡1,800). See [Known issues](./known-issues.md).

## Acceptance

- Market unit tests cover equal-weight Index marks (empty basket, NaNs skipped, Index excluded from
  inputs).
- In-game: Index appears on Investments; its Value moves with the sector basket; Buy/Sell works.
- [Market analogs](./market-analogs.md) city-index section points here as the first shipped fund
  slice.

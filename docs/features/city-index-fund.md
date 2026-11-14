# City index fund (Index stock)

## Value

Mayors can buy and hold a single **Index** stock that tracks all listed Financial Districts sector
stocks together — like a Vanguard-style total-market fund — instead of micromanaging thirteen
volatile names.

## Scope

| In                                                                 | Out                                      |
| ------------------------------------------------------------------ | ---------------------------------------- |
| One tradable stock named **Index** on the Investments board        | Sector ETFs, bonds, commodities, IPOs    |
| Mark = equal-weight average of the other sector share prices       | Custom weights / smart beta              |
| Buy / sell through vanilla Investments chrome                      | Separate treasury UI                     |
| Fail soft if Financial Districts or listing hooks are unavailable  | Rewriting vanilla Investment prefabs     |

Population / land-value drivers and black-swan dampening stay in [World dynamics](./world-dynamics.md)
for later; this slice only ties Index’s price to the live sector basket.

## Contracts

1. **Listing** — With the mod enabled and Financial Districts present, the purchasable Investments
   list includes a stock titled **Index** (exact display name).
2. **Mark** — Index’s share price is the equal-weight average of the other listed sector stocks’
   current prices (vanilla Financial Districts uses thirteen sector names; Index is the fourteenth
   listing and is **not** included in its own basket).
3. **Trading** — Players buy and sell Index with the same Buy / Sell / share-amount flow as any
   other investment. Probe bias, when enabled, may affect hooked sector marks and therefore the
   Index mark.
4. **Fail soft** — If the Index listing cannot be created or sector prices cannot be read, log a
   clear message and leave the vanilla thirteen names alone.

## Acceptance

- Market unit tests cover equal-weight Index marks (empty basket, NaNs skipped, Index excluded from
  inputs).
- In-game: Index appears on Investments; its Value moves with the sector basket; Buy/Sell works.
- [Market analogs](./market-analogs.md) city-index section points here as the first shipped fund
  slice.

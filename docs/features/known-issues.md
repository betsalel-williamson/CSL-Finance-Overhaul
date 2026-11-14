# Known issues

Open bugs to investigate, newest first. Each entry says what was seen, what we know, and the next
check. Move an entry to its feature shard (or delete it) once fixed.

## Index is more volatile than its components

- **Fix in progress (2026-11-13):** chain-linked equal-weight return mark, unlocked members only, no
  vanilla variance. Close this entry once verified in game.

- **Seen:** 2026-11-13, Index moved -11.91% and +2.5% between updates; sell-dialog chart spans
  ₡1,100–₡1,800, while individual stocks moved about ±1–2%.
- **Likely causes:**
  1. The mark is an average of **prices**, so high-priced names dominate and locked or inactive
     investments with large values swing it. A return-based equal weight (average of each stock's
     change since a base, times a base value) would fix this.
  2. Vanilla `InvestmentAI.SafeCalculateValue` adds random variance after `CalculateValue`, using
     the cloned source's (`TrafficJellyAI`) settings.
  3. The basket includes locked investments (for example ChirpAir 0/30).
- **Next check:** log the basket (name, value, available) on each Index update and compare.

## KTM shows `NaN` on the Stock Exchange ticker

- **Seen:** 2026-11-13, facade ticker boards on the in-game Stock Exchange show `KTM (NaN)` while
  other names show numbers (for example `FC (+0.54)`, `Oil (-6.04)`).
- **Known:** the Index stock was **off** when this was captured, and the mod log showed no errors,
  so it is most likely vanilla. Not yet ruled out: the old reflective stock hook (ADR 0001), which
  postfixes `InvestmentAI.CalculatePrice` and `StockExchangeAI.UpdateGuide` even with probe bias
  off.
- **Likely cause to check:** the ticker's change value is computed from an investment's value
  history; an investment with no history or a zero/locked value would divide by zero and render
  `NaN`.
- **Next checks:**
  1. Identify which investment `KTM` abbreviates (ticker names are initials of the investment
     names).
  2. Reproduce with Finance Overhaul disabled. If `NaN` remains, it is vanilla.
  3. Find the ticker's text source on the Stock Exchange building and how it computes the change.
- **Related:** the live ticker stretch goal (`plan.live-ticker`) in
  [Brokerage mechanics map](./brokerage-mechanics-map.md) must never show `NaN`.

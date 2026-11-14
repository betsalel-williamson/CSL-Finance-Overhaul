# Known issues

Open bugs to investigate, newest first. Each entry says what was seen, what we know, and the next
check. Move an entry to its feature shard (or delete it) once fixed.

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

# Known issues

Open bugs to investigate, newest first. Each entry says what was seen, what we know, and the next
check. Move an entry to its feature shard (or delete it) once fixed.

### Long frame stall with 254 listed stocks

Scenario 51 (2026-11-15): with 254 investments listed, one frame took 11.9 s (frames averaged 463 ms
over 10 s; an earlier run at 254 saw 1.7 s). The game's daily investment update stays tiny (0.004
ms), so the cost is on the UI side: likely the mod rebuilding ticker symbols, the LED marquee or
Stock Monitor rows for 254 names, or the Investments panel rebuilding 240 rows. Not an issue at 33
(worst frame 0.9 s, the same as without extra stocks in this busy test city). **Next check:**
profile the 254 step by turning the live ticker and Stock Monitor off in turn, before local listings
go past a few dozen names.

## Resolved

### Game bug: loading a city lists used investment slots as free (worked around)

`EconomyManager.DeserializeInvestments` builds a new investment table with every slot on the free
list, including the slots the save uses. The game never adds investments after loading, so it never
notices; anything that does (the Index, the test stock, local listings) gets a used slot once the
free top slots run out and overwrites a held investment. Scenario 51 found it by overwriting a test
city's 13 vanilla stocks (only in memory; the save was reloaded). **Fix:** a postfix on
`DeserializeInvestments` rebuilds the free list from the slots in use
(`StressStocks.RepairFreeSlots`); the stress test also copies every slot first and puts back any
that is handed out while in use.

### Ticker symbols could contain punctuation

"Stress 001 (dev)" became `S0(`: symbols took the first character of each word, punctuation
included. Symbols now use letters and digits only (unit test `SymbolsAreLettersAndDigitsOnly`).

### KTM shows `NaN` on the Stock Exchange ticker: not a bug (baked art)

- **Seen:** 2026-11-13, facade ticker boards on the Stock Exchange show `KTM (NaN)`.
- **Finding (2026-11-13):** the boards are **static art baked into the building's texture**, not
  live data.
  1. No game code feeds the facade: every reader of investment values (`Investment.value`,
     `allValues`, `InvestmentAI.CalculateChangeRate`) is the Economy panel, Buy/Sell dialog,
     tooltip, save/load, or simulation.
  2. `KTM` does not occur in `Assembly-CSharp.dll` or `ColossalManaged.dll`.
  3. Board values never change and do not match live change rates: two captures at different game
     times both show `TJL (-5.76)`, `TVLC (+5.66)`, `Oil (-6.04)`, `COE (+13.37)` while live Traffic
     Jelly Logistics was +3.21%, The Very Legit Company −0.28%/+1.96%, Oil −2.08%/+0.62%. All three
     boards repeat the same numbers.
  4. Several symbols are not investments in the game (`KTM`, `K&B`, `BFT`, `COE`, `B`), consistent
     with decorative art; `NaN` reads as a developer joke.
- **Two-date capture (03/06/2072 vs 04/06/2072):** the boards scroll (phase shifts between captures)
  but all 17 values are identical on both days:

  | Symbol | Value  | Likely investment       |
  | ------ | ------ | ----------------------- |
  | CC     | +2.1   | Chirp Coin              |
  | TFC    | +0.54  | Fuel Company Investment |
  | DS     | −0.04  | Deathcare Service Fund  |
  | HS     | −3.5   | Healthcare Service Fund |
  | OrI    | +0.005 | Ore Industry            |
  | TJL    | −5.76  | Traffic Jelly Logistics |
  | TVLC   | +5.66  | The Very Legit Company  |
  | COE    | +13.37 | none                    |
  | BFT    | +0.0   | none                    |
  | CCr    | +4.21  | Chirpy Cruises          |
  | Oil    | −6.04  | Oil Industry            |
  | FoI    | +2.24  | Forestry Industry       |
  | CAi    | −1.11  | ChirpAir                |
  | FaI    | −0.56  | Farming Industry        |
  | K&B    | −7.07  | none                    |
  | BB     | −2.1   | none                    |
  | KTM    | NaN    | none                    |

  Each board shows a differently offset slice of the same artwork (only the right board shows `COE`,
  only the left shows `K&B / BB / KTM`). Generic Industry and Index never appear.

- **Outcome:** no fix in code. The live ticker stretch goal (`plan.live-ticker`) must draw its own
  text over or instead of the baked texture.

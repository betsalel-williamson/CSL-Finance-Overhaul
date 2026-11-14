# Known issues

Open bugs to investigate, newest first. Each entry says what was seen, what we know, and the next
check. Move an entry to its feature shard (or delete it) once fixed.

## Resolved

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

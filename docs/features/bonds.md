# Bonds and treasury instruments

Single-piece flow: one instrument at a time, each end to end (math → engine → UI → game UI test →
Figma → docs), each behind its own Options flag, each stored in the mod's own save data
(`ISerializableDataExtension` style), never in vanilla structures (see
[Save compatibility](./save-compatibility.md)).

## Pieces, in order

1. **Issue city bonds (raise money)** — the city sells a bond, receives the face value now, pays a
   coupon every 30 game days, and repays the face at maturity; it can also redeem early. Cash moves
   through the game's own budget lines (`EconomyManager.AddResource(LoanAmount, …)` in,
   `FetchResource(LoanPayment, …)` out), so debt service shows up beside vanilla loans without using
   the three vanilla loan slots. _Status: cash-flow math shipped (below); book, engine and UI next._
2. **Buy bonds (lend surplus)** — the same schedule from the holder's side, plus a price for selling
   before maturity (needs price/yield math with its own proofs).
3. **Dividend investments** — holdings that pay a periodic cash dividend (which also makes dividend
   reinvestment a real automated order).
4. **IPOs** — city companies listing on the exchange (see [Market analogs](./market-analogs.md)).

## Cash-flow math (shipped: `BondMath`)

A fixed-rate **bullet** bond in integer **cents**: face `F`, annual rate `r` in basis points, `n`
periods of `d` days.

- **Day count 30/360:** a 30-day period is exactly 1/12 of a year (a common bond convention; the
  game calendar's uneven months would otherwise make "monthly" coupons unequal).
- **Total interest** `I = round(F × r × n × d / 360)`, half up, computed once for the whole bond.
- **Coupons:** `I` split over the `n` periods by largest remainder — each coupon is `⌊I/n⌋` or
  `⌈I/n⌉`, the first `I mod n` carry the extra cent — and the face is repaid with the last coupon.
- **Early redemption** after `k` coupons and `t` days into the period: `F + ⌊coupon(k+1) × t / d⌋`.
- **Bounds:** face up to ₡100,000,000, rate up to 20%, up to 360 periods of up to 365 days; every
  product stays below 2⁶³.

**Proved properties** (`BondMathTests`, 1,000 seeded random bonds plus exact examples): conservation
(coupons + principal = face + interest, to the cent); total interest is the rounded exact value;
each coupon is the floor or ceiling of the ideal; bullet shape (no negative payment, principal only
at maturity); more rate or more periods never means less interest; zero rate pays nothing; early
redemption lies between the face and face + next coupon, never falls within a period and equals
those at the period's ends; no overflow at the bounds; invalid terms are rejected. Example:
₡1,000,000 at 4.5% for 24 monthly periods → ₡90,000 interest, ₡3,750 a month.

## Open decisions for piece 1

- Terms and rates offered (e.g. 1, 2, 5 years) and whether the rate depends on the city's debt.
- A cap on outstanding bonds.
- What happens when the city cannot pay a coupon (vanilla loans let cash go negative).
- Without the mod, mod-stored bonds disappear: the city keeps the cash and the debt vanishes.
- Where the UI lives (Economy → Loans, next to vanilla loans, is the natural place).

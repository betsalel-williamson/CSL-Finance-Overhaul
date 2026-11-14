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
   the three vanilla loan slots. _Status: cash-flow math and pricing shipped (below); book, engine
   and UI next._
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

## City bond pricing — rate by debt (shipped: `BondPricing`)

- **Terms:** 1, 2 or 5 game years, monthly coupons (12, 24 or 60 periods of 30 days).
- **Base rate:** 3.00% (1 year), 3.50% (2 years), 4.50% (5 years).
- **Debt premium:** +1.50% per 1× of leverage, where leverage = (debt already owed — vanilla loans
  left + outstanding bond faces — plus this bond) ÷ the city's annual income; capped at +6.00%.
- **Cap:** an issue that would put leverage above 3× annual income is refused ("the city would owe
  more than 3× its annual income"); no income or invalid input is refused too.

**Proved properties** (`BondPricingTests`, 5,000 seeded random cities plus exact examples): the rate
always lies between the base rate and base + 6.00% and under the 20% bond maximum; more existing
debt, a bigger bond, less income or a longer term never gives a cheaper rate; the 3× cap is exact
(at 3× offered, above refused); bad inputs never get a rate.

## Decided for piece 1 (2026-11-14)

- Terms and rates: 1/2/5 years, rate by debt (above).
- When the city can't pay a coupon or the face: **pay anyway** — cash can go negative, like vanilla
  loans.
- Still open: without the mod, mod-stored bonds disappear (the city keeps the cash and the debt
  vanishes); the UI lives in Economy → Loans next to vanilla loans.

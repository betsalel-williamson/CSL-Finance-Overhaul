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
   the three vanilla loan slots. _Status: shipped behind the Options flag — math, pricing, book,
   daily engine and the Economy → Loans section (below)._
2. **Buy bonds (lend surplus)** — the same schedule from the holder's side, plus a price for selling
   before maturity (needs price/yield math with its own proofs).
3. **Dividend investments** — holdings that pay a periodic cash dividend (which also makes dividend
   reinvestment a real automated order).
4. **IPOs** — city companies listing on the exchange (see [Market analogs](./market-analogs.md)).

## Game money units (measured)

The budget calls take **cents**, the same unit as `BondMath`, so bond cash flows need no conversion.
Measured in a live city by game UI scenario 36 (`money-probe 100`, simulation paused):
`AddResource(LoanAmount, 100, …)` raised the cash field `m_cashAmount` by exactly 100 and
`EconomyManager.playerMoney` by ₡1; `FetchResource(LoanPayment, 100, …)` returned the cash field
exactly to its start. `playerMoney` is whole ₡ (what the order desk compares prices against), so a
bond of `F` cents moves it by about `F / 100`.

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

## Bond book and daily engine (shipped: `BondBook`, host `BondDesk`)

- **Book:** each sold bond keeps its terms, issue day and coupons paid. `Due(today)` pays every
  coupon (and the face with the last one) whose day has come, catching up skipped days, then drops
  matured bonds. `Redeem` pays anything already due, then the face plus interest accrued since the
  last coupon.
- **Saved** as versioned text in mod data `FinanceOverhaul.Bonds` (`v1`; one `issued|…` line per
  bond). Unknown versions and bad lines are skipped; vanilla ignores mod data, so a city saved with
  bonds still opens without the mod (the bonds, and what they owe, simply disappear; the cash
  already received stays). Reloaded from mod data after a hot reload or level load.
- **Cash:** once per game day, after the vanilla investment update, each due payment goes out
  through `FetchResource(LoanPayment, …)` and a sale comes in through `AddResource(LoanAmount, …)`,
  in cents, split into `int`-sized pieces. Payments are made even if cash goes below zero; a
  short-paid amount is logged as a warning.
- **City finances for pricing:** debt = vanilla loans' `m_amountLeft` + outstanding bond faces;
  annual income = the game's weekly income total (`GetIncomeAndExpenses`, all services) × 52.
  Checked in game: the weekly figure matched the Economy panel's total (residential ₡104,668 =
  38.86% → about ₡269k a week; the mod read ₡264k a moment earlier).
- **Flag:** Options → "City bonds: sell bonds to raise money (testing; adds mod save data)", off by
  default. It gates new sales only; bonds already sold keep paying.

**Proved properties** (`BondBookTests`, 500 seeded random books and day paths plus examples):
everything paid over a bond's life is face + total interest to the cent on any path of checked days
(daily, jumps, once at the end); what is paid by a day depends only on that day; checking a day
twice pays nothing more; no payment is made before its due day; early redemption brings the total
paid between face + coupons paid and face + total interest and removes only that bond; the codec
round-trips and skips what it cannot read.

**In game** (scenario 37, 2026-11-14): a ₡1,000 1-year bond sold at 3.02% raised cash by exactly
100,000 cents; backdated 30 days, its first coupon (₡2.52) was paid on the next day; redeemed for
₡1,000.08 (face + one day's accrual).

## UI (shipped: `CityBondsSection`; Figma UI Map band 11)

A **City bonds** section on Economy → Loans, in the help strip plus the gap above the three vanilla
loan cards (about 1230 × 312 UI units); the strip's "three loans" sentence becomes its header line.
Left, **sell a bond**: term tabs (1 / 2 / 5 years), amount field whose hint shows the most the city
can raise before 3× income, a live quote (rate as base + premium, coupon every 30 days, total
interest, repay date) and **Sell bond · receive ₡…**; over the cap the hint turns red with the
reason and the button is disabled. Right, **bonds you owe** (`FO/BondRow`): term, rate, face; next
coupon, coupons left, repay date; **Repay ₡…** = face + interest accrued this period; footer with
total owed and leverage vs the 3× cap; more rows scroll. Shown when the Options flag is on or while
the city still owes a bond. Pins and catalog ids: [UI Figma map](../developer/ui-figma-map.md).

In game it replaces the vanilla help strip (`LoanDesc`, hidden while the section shows and restored
when it hides or the mod reloads) at 1108 × 271 UI units. The amount accepts whole ₡ with commas or
spaces (`BondText.ParseAmountCents`); above one bond's ₡100,000,000 maximum the hint says so. The
shown maximum is exactly the market's limit (`BondTextTests`, 5,000 random cities: that amount is
offered, ₡1 more is refused). Game UI scenario 39 sells ₡1,000 through the button, checks the owed
row and both refused states, and repays it with the row's Repay button (₡1,000.00 the same day).

## Decided for piece 1 (2026-11-14)

- Terms and rates: 1/2/5 years, rate by debt (above).
- When the city can't pay a coupon or the face: **pay anyway** — cash can go negative, like vanilla
  loans.
- Still open: without the mod, mod-stored bonds disappear (the city keeps the cash and the debt
  vanishes); the UI lives in Economy → Loans next to vanilla loans.

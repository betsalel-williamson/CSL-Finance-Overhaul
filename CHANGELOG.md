# Changelog

## 0.1.0

First public release. Player guide: https://betsalel-williamson.github.io/CSL-Finance-Overhaul/

### Trading

- Limit buy, stop-loss, take-profit and trailing stop orders in the game's Invest and Sell dialogs,
  checked once per game day; orders stay open for 90 days by default (30 to 365 in Options).
- Open orders in Your Portfolio with Cancel and days left; average cost per share on every holding.
- Typed share amount in the Invest and Sell dialogs.

### Stock Monitor

- A window listing every stock with a 30-day sparkline, price and change; All, Portfolio and Orders
  filters; sort by change, symbol, price or value held, with a direction button.
- Stock detail with a 7 or 30 day chart, hover readout, holdings, gain and open orders, and Buy and
  Sell into the game's dialog; settings for opacity, compact rows, sparklines and ₡ or % change.
- The game's Share value charts drawn in the same style.

### Stock Exchange

- Live LED ticker boards on the Stock Exchange: change % or price and ₡ change, three brightness
  levels, optional in info views, full as soon as the city loads.

### Index stock

- Index (IDX): one stock that follows all of the city's unlocked share stocks (chain-linked
  geometric average), with its 30-day history filled in from the first day.

### City bonds

- Sell 1, 2 or 5 year bonds on Economy → Loans; the rate rises with the city's debt and is capped at
  three times annual income; coupons every 30 days, repayment at maturity, early repayment.

### Notices

- Chirps from City Brokerage (orders filled, expired or cancelled; Index shares restored) and City
  Treasury (bonds repaid, optionally every coupon); busy days roll up into one chirp.

### Saves

- Cities always open without the mod. Orders and bonds stay in the save; Index shares come back when
  the mod returns. A Welcome back window handles bond payments missed while the mod was off.
- Room for up to 254 investments, with every listing kept safe: new listings never take a slot the
  city already holds.

# Changelog

## 0.2.0

Two big features and a polish of almost everything from 0.1.0. Player guide, with a What's new page:
https://betsalel-williamson.github.io/CSL-Finance-Overhaul/

### The Investments tab

- Economy → Investments is one list of every stock: coin, symbol and name, a 30-day line, price and
  change, and for a holding the shares, average cost and gain since bought; open orders sit under
  their stock with Cancel and the days left. All, Owned, Orders, Local and Locked tabs, Group, and
  the Stock Monitor's sorts with a direction button. A click on a line opens the Invest dialog.
- An overview beside it with five views: Market (the market index, its 7 or 30 day chart, today's
  top gainers and losers), Portfolio (value, today and since bought, a chart, a donut of holdings,
  open orders), Heatmap (a treemap sized by market value, coloured by today's or the 7 or 30 day
  change), City (the city figure behind each stock and what moves it) and Feed (what is coming up
  and what happened lately; a chirp opens in the Chirper).
- One % / ₡ switch: a click on any change pill turns every pill, and the Stock Exchange's LED boards
  follow (new default in Options → "Stock Exchange ticker shows"). The tab and the Stock Monitor
  share one sort and one % / ₡ by default; Options can unlink them.

### Local IPOs

- A business that has done well for a year can go public: shops, offices and industry at the top
  level of their kind, industry areas at level 3 and unique factories at full production, once the
  Stock Exchange reaches level 2. Rare by design: at most two a year.
- The IPO: a City Brokerage chirp and a dialog (View headquarters), a 7-day offering window at a
  price from what the business earns (the city can buy up to 1,000 shares, a 10% stake), a first
  trading day that opens up or down, then a price that follows the business every day.
- The company's building is marked Historical and its panel shows the listing with Buy, Sell and
  Show in Investments. Buy-outs at a premium now and then; a company that loses its headquarters
  moves to another business of its kind, or closes after 90 days and pays the city its last price.
- Every value is an option, with Less likely, Default and More likely presets and your own saved
  ones. Companies are kept in the save and come back with your shares after time without the mod.

### Invest dialog

- Buy | Sell tabs turn an open dialog from buying to selling and back; Sell is greyed out, with a
  tooltip, when the city holds none.
- A Chirps tab: what Chirper said about the stock lately (the game's rising and falling chirps and
  the mod's notices).
- The stats the Stock Monitor's detail page had: 7D | 30D on the chart, the change since yesterday,
  yesterday, the 30-day high and low, shares owned, average cost, value held, gain, open orders. The
  dialog stays centred as it grows. The game's own look for tabs, fields, charts and colours.
- A Buy dialog with nothing left to buy no longer ignores every click: Buy is greyed out, and a Buy
  dialog opened anyway turns to Sell.

### Stock Monitor

- A click on a stock opens the game's Invest dialog. An Orders tab lists every open order with how
  far its stock is from the trigger and the days left, sorted by nearest, soonest or symbol.
- The game's grey window and caption, solid under the mouse, the whole window fading with the
  opacity setting, Esc closes it, heavier text on the pills, and the list fades at an edge with more
  to scroll.

### City bonds

- Bonds look like the game's loan cards, with a name you can type (saved with the city), the Total
  that paying now costs and Pay Now. Terms in weeks (52, 104 or 260), and the coupon goes out every
  week like a loan payment, under loan expenses. Bonds already owed are converted on load (same
  amount, rate and name; the interest left is never more than before).
- The amount box groups the number as you type.

### Everything else

- When two copies of the mod are switched on, the mod asks which to keep; switching one copy off no
  longer takes the order tabs, cards and Index out of the other.
- The Index has a coin icon of its own. "1 share", not "1 shares"; all wording now comes from one
  table that follows the game's language, ready for translations.
- The ticker follows a Stock Exchange that was turned or moved; the Welcome back dialog opens in the
  middle of any screen; the ticker option's text is no longer cut off.

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

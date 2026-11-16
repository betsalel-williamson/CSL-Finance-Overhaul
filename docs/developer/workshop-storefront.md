# Workshop storefront

Paste-ready Steam Workshop text for Finance Overhaul. How to publish:
[Release process](./release-process.md).

## Workshop item

| Field                 | Value                                                               |
| --------------------- | ------------------------------------------------------------------- |
| **Published file ID** | `3840721874` (first published 2026-11-15, 0.1.0)                    |
| **URL**               | <https://steamcommunity.com/sharedfiles/filedetails/?id=3840721874> |

Keep this ID for every Update. Never create a second item.

## Title

```text
Finance Overhaul
```

## Short description

`IUserMod.Description` (Content Manager) and the first line of the Workshop page:

```text
Stock Exchange overhaul for Financial Districts: limit, stop-loss, take-profit and trailing stop orders, a Stock Monitor, an Index stock, city bonds, a live LED ticker and Chirper notices.
```

## Plain text description

For the Share dialog's description field (plain text, no formatting):

```text
Finance Overhaul turns the Financial Districts Stock Exchange into a trading desk for your city's treasury.

Place limit buy, stop-loss, take-profit and trailing stop orders and let the city trade while you build. Follow every stock in the Stock Monitor window, watch live prices scroll on the Stock Exchange's LED boards, buy the Index stock to own the whole market at once, and sell city bonds on the Loans tab to raise money. Chirper tells you when orders fill and bonds are repaid.

Your city always opens without the mod. Orders, bonds and Index shares stay in the save and come back when the mod is turned on again.

Requires Financial Districts and Harmony.
Player guide: https://betsalel-williamson.github.io/CSL-Finance-Overhaul/
Source and issues: https://github.com/betsalel-williamson/CSL-Finance-Overhaul
```

## Change notes

For the Workshop change note on each Share or Update (plain text).

### 0.1.0

```text
First release.
- Orders: limit buy, stop-loss, take-profit and trailing stop, checked once a day.
- Open orders and average cost per share in Your Portfolio.
- Stock Monitor window with charts, filters and sorting.
- Live LED ticker on the Stock Exchange.
- Index stock that follows the whole market.
- City bonds on the Loans tab.
- Chirper notices for orders and bonds.
- Saves stay safe without the mod; a Welcome back window handles missed bond payments.
```

## Long description

```text
Version 0.1.0

Finance Overhaul turns the Financial Districts Stock Exchange into a proper trading desk for your city's treasury.

What you get
• Orders: limit buys, stop-loss, take-profit and trailing stop sells, right in the game's Invest and Sell dialogs. They check once a day and stay open for 90 days (change it in Options).
• Your Portfolio shows open orders with a Cancel button and the average cost per share of every holding.
• Stock Monitor: a window listing every stock with a 30-day sparkline, price and daily change. Filter, sort, open a stock for its chart, and buy or sell from there.
• Share value charts in the same style, with a hover readout for each day.
• Live LED ticker: the Stock Exchange boards scroll real prices. Choose percent or price and change, set the brightness, and choose whether it stays on in info views.
• Chirper notices from City Brokerage when an order fills, expires or is cancelled.

• Index stock: one stock that tracks all of the city's share stocks, so you can own the whole market at once.
• City bonds: raise money on Economy → Loans. The rate rises with your debt, coupons go out every 30 days, and you can repay early.
• Chirper notices from City Treasury when a bond matures.

Coming later
• Companies from your own city going public (local IPOs).

Requires
• Cities: Skylines with the Financial Districts DLC
• Harmony (2.2+), required item

Saves
• Your city always opens without the mod. Orders, bonds and Index shares are kept in the save and come back when the mod is enabled again. If bond payments were missed while the mod was off, a Welcome back window asks how to handle them.

Known limits
• Prices follow the game's own investment rules; your trades never move them.
• One Workshop item, early version: please report problems with your mod list and the log file finance-overhaul.log from your temp folder.

Source and issues: https://github.com/betsalel-williamson/CSL-Finance-Overhaul
```

## Tags

`economy` · `stocks` · `finance` · `investments` · `Financial Districts` · `stock exchange` ·
`bonds` · `UI`

## Share dialog and Steam page

| Field          | Value                                                                                      |
| -------------- | ------------------------------------------------------------------------------------------ |
| Required items | [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2040656402) only          |
| Preview        | `mod/PreviewImage.png` (512×512): in-game evening shot of the LED boards and Stock Monitor |
| Visibility     | Hidden or friends only for the soft launch, then public                                    |

# Workshop storefront

Paste-ready Steam Workshop text for Finance Overhaul. How to publish:
[Release process](./release-process.md).

## Workshop item

| Field                 | Value                              |
| --------------------- | ---------------------------------- |
| **Published file ID** | not published yet (record it here) |
| **URL**               | —                                  |

Keep this ID for every Update. Never create a second item.

## Title

```text
Finance Overhaul
```

## Short description

`IUserMod.Description` (Content Manager) and the first line of the Workshop page:

```text
Stock Exchange overhaul for Financial Districts: limit, stop-loss, take-profit and trailing stop orders, a Stock Monitor, a live LED ticker and Chirper notices.
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

Coming later
• An Index stock that tracks the whole market, city bonds, and companies from your own city going public.

Requires
• Cities: Skylines with the Financial Districts DLC
• Harmony (2.2+), required item

Saves
• Your city always opens without the mod. Open orders are kept in the save and come back when the mod is enabled again.

Known limits
• Prices follow the game's own investment rules; your trades never move them.
• One Workshop item, early version: please report problems with your mod list and the log file finance-overhaul.log from your temp folder.

Source and issues: https://github.com/betsalel-williamson/CSL-Finance-Overhaul
```

## Tags

`economy` · `stocks` · `finance` · `investments` · `Financial Districts` · `stock exchange` · `UI`

## Share dialog and Steam page

| Field          | Value                                                                                      |
| -------------- | ------------------------------------------------------------------------------------------ |
| Required items | [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2040656402) only          |
| Preview        | `mod/PreviewImage.png` (512×512): in-game evening shot of the LED boards and Stock Monitor |
| Visibility     | Hidden or friends only for the soft launch, then public                                    |

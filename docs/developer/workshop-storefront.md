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
Stock Exchange overhaul for Financial Districts: one Investments tab with a market overview, your city's own companies going public, limit, stop-loss, take-profit and trailing stop orders, a Stock Monitor, an Index stock, city bonds and a live LED ticker.
```

## Plain text description

For the Share dialog's description field (plain text, no formatting):

```text
Finance Overhaul turns the Financial Districts Stock Exchange into a trading desk for your city's treasury.

Economy -> Investments becomes one list of every stock with a market overview beside it: Market, Portfolio, Heatmap, City and Feed. Businesses that do well in your city can go public, and you can buy in at the offering price. Place limit buy, stop-loss, take-profit and trailing stop orders and let the city trade while you build. Follow every stock in the Stock Monitor, watch live prices on the Stock Exchange's LED boards, buy the Index stock to own the whole market at once, and sell city bonds on the Loans tab to raise money.

Your city always opens without the mod. Orders, bonds, Index shares and your local companies stay in the save and come back when the mod is turned on again.

Enjoying it? Please rate it, add it to your favourites and leave a comment.

Requires Financial Districts and Harmony.
Player guide: https://betsalel-williamson.github.io/CSL-Finance-Overhaul/
Community discussion (Discord): https://discord.com/channels/1024242828114673724/1571077936797192243
Source and issues: https://github.com/betsalel-williamson/CSL-Finance-Overhaul
```

## Change notes

For the Workshop change note on each Share or Update (plain text).

### 0.2.0

```text
Two big features, and almost everything from 0.1.0 polished. Full list and pictures in the player guide's What's new page.
- New Investments tab: one list of every stock, with your holdings and orders on each line, and an overview with Market, Portfolio, Heatmap, City and Feed views.
- Local IPOs: businesses that do well in your city go public; buy in at the offering price and follow them from their building.
- Invest dialog: Buy | Sell tabs, a Chirps tab, and the stock's stats.
- One % / ₡ switch for every pill, the Stock Monitor and the LED boards.
- Stock Monitor: opens the Invest dialog, new Orders tab, Esc closes it, the game's look.
- City bonds: loan-style cards with names, weekly coupons, terms in weeks.
- Many fixes; your saves carry over.
Enjoying it? Please rate it, add it to your favourites and leave a comment.
```

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
Version 0.2.0

Finance Overhaul turns the Financial Districts Stock Exchange into a proper trading desk for your city's treasury.

New in 0.2.0
• One Investments tab: every stock in one list, with its 30-day line, your shares, average cost and gain, and your open orders right under it. Filter by All, Owned, Orders, Local or Locked, group it, sort it.
• A market overview beside the list: Market (the market index and today's top movers), Portfolio (what your shares are worth, a chart and a donut of your holdings), Heatmap (every stock sized by market value and coloured by its change), City (the city figure behind each stock, like crime or oil productivity) and Feed (what's coming up and what happened lately).
• Local IPOs: a business that does well in your city for a year can go public once your Stock Exchange reaches level 2. Buy in at the offering price during its 7-day window, watch its first trading day, and follow it from its building. Rare by design, and every value is an option, with presets.
• The Invest dialog gets Buy | Sell tabs, a Chirps tab with what Chirper said about the stock, and the stock's stats.
• Click any % pill to see every change in points (₡); the Stock Monitor and the LED boards follow.

Also in the mod
• Orders: limit buys, stop-loss, take-profit and trailing stop sells. They check once a day and stay open for 90 days (change it in Options).
• Stock Monitor: a window listing every stock with a 30-day sparkline, price and daily change, and an Orders tab with how close each order is to its trigger.
• Live LED ticker: the Stock Exchange boards scroll real prices.
• Index stock: one stock that tracks all of the city's share stocks.
• City bonds: raise money on Economy → Loans without using a loan slot; weekly coupons, like loans.
• Chirper notices from City Brokerage and City Treasury.

Requires
• Cities: Skylines with the Financial Districts DLC
• Harmony (2.2+), required item

Saves
• Your city always opens without the mod. Orders, bonds, Index shares and local companies are kept in the save and come back when the mod is enabled again. If bond payments were missed while the mod was off, a Welcome back window asks how to handle them.

Known limits
• The game's own stock prices follow the game's rules; your trades never move them.
• Please report problems with your mod list and the log file finance-overhaul.log from your temp folder.

Player guide (everything, with pictures): https://betsalel-williamson.github.io/CSL-Finance-Overhaul/
Community discussion (Discord): https://discord.com/channels/1024242828114673724/1571077936797192243
Source and issues: https://github.com/betsalel-williamson/CSL-Finance-Overhaul

Enjoying Finance Overhaul? Please rate it, add it to your favourites and leave a comment: it helps other mayors find it, and every comment is read.
```

## Images (0.2.0)

Five feature images and a sixth that sends players to the guide, 1920 × 1080, built by
`scripts/workshop-images.sh` from in-game captures (the store-\* ones from
`tests/game-ui/store/captures.uitest`; the LED one from the `FinanceOverhaul.focus` trigger). Upload
in this order after the preview; Figma file `ZJ0oXFLXCOFOXQsLxE86Vh`, page "Release 0.2.0", lays
them out with these captions for review.

| #   | File                                   | Title                             |
| --- | -------------------------------------- | --------------------------------- |
| 1   | `img/workshop/1-investments-tab.jpg`   | One Investments tab               |
| 2   | `img/workshop/2-overview.jpg`          | See the whole market at a glance  |
| 3   | `img/workshop/3-local-ipos.jpg`        | Your city's businesses go public  |
| 4   | `img/workshop/4-orders-and-chirps.jpg` | Orders that trade while you build |
| 5   | `img/workshop/5-led-ticker.jpg`        | Live prices on the Stock Exchange |
| 6   | `img/workshop/6-player-guide.jpg`      | There's more in the player guide  |

## Announcement (0.2.0)

For the Workshop announcement and Discord:

```text
Finance Overhaul 0.2.0 is out!

Your city's treasury gets a proper market view:
• One Investments tab: every stock in one list, with your shares, cost and orders on each line, and an overview beside it with Market, Portfolio, Heatmap, City and Feed views.
• Local IPOs: businesses that thrive in your city for a year can go public. Buy in at the offering price and follow them from their building.
• A Chirps tab in the Invest dialog, Buy | Sell tabs, and the stock's stats right there.
• One click on any % pill switches everything to points, LED boards included.
• City bonds now pay weekly, like loans, and look like the game's loan cards.

Your saves carry over: just update. What's new, with pictures: https://betsalel-williamson.github.io/CSL-Finance-Overhaul/whats-new/

If you enjoy it, please rate it, add it to your favourites and leave a comment on the Workshop page. It really helps, and I read every one.
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

# Investments list and research (design)

Status: **step 1 built 2026-11-25** behind the dev flag "Investments list and research" (see
[Step 1 as built](#step-1-as-built-2026-11-25-reworked-after-a-first-try)); steps 2–6 not built.
Figma UI Map band 20; band 18 holds the earlier condensed-row sketches this grows from.

## Why

Economy → Investments was made for about thirteen stocks: two side-by-side lists ("Purchasable
investments" and "Your portfolio") of 165-tall cards, three on screen each. With the Index, local
companies and more listings to come, the player can't see the data at a glance, and scrolling is a
long way per stock. The game also gives no reason to pick one stock over another beyond the last
change rate. This is a game: research should be fun to read and, read carefully, give real hints.

## Decisions (2026-11-25)

1. **One sectioned list** replaces the two side-by-side lists. Research opens in the Stock Monitor's
   detail view (a click on a row), not in a second panel.
2. **News is mostly right.** Headlines come from real signals, some a few days early. Each source
   has a track record and the predictive ones are sometimes wrong: reading carefully pays off on
   average, never for certain.
3. **Research is free and grows with the Stock Exchange.** Basic news from the start; a higher Stock
   Exchange level adds sources. Every level and rate is an option, as for Local IPOs.
4. **News reaches three places:** the research tab (all of it), Chirper (big news only), and a
   marker on the stock's row in the Stock Monitor (recent news, headline on hover).

## The list (Economy → Investments)

One full-width list in place of the two columns.

- **Top bar:** filter tabs All · Owned · Local · Watchlist ★ · News (each with its count), then Sort
  (the Stock Monitor's sorts and direction button).
- **Sections**, each with a header and its total: **Your portfolio** (value, gain), **Local
  companies** ("2 of 12 listed", with the kinds' unlock bars), **Watchlist**, **All stocks** (locked
  stocks show their requirement and bar in their row). A stock appears in one section only.
- **Row (44 tall, one line; about 11 on screen):** coin, symbol and name, news marker, 30-day line,
  price, change pill; for a holding: shares · avg cost · gain and an orders badge; Buy and Sell; ★
  to watch. The game's description is the name's tooltip.
- **A click on a row** (not on its buttons) opens the Stock Monitor on that stock's detail, on the
  Research tab.
- **Scrolling:** a fixed distance per wheel notch (three rows), the game's scrollbar, section
  headers stay at the top while their rows scroll, and the list keeps its place through the daily
  update, a purchase or a sale (today it jumps).

## Research (Stock Monitor detail)

The detail view gets tabs: **Chart** (today's view) · **Research**.

- **What moves it:** one line per stock. A game stock names its city driver ("Follows how many
  passengers fly from your airports"); a local company its profit ("Earns from Park Oil Group's
  weekly profit"); the Index its basket.
- **Analyst rating** (from Stock Exchange level 3): Buy / Hold / Sell, with the desk's record
  ("right 7 of the last 10 calls").
- **News:** newest first, 60 days kept: date, source, headline, ▲ / ▼ / – tone. Unread news marks
  the row in the list and the Monitor.

## News sources

| Source      | Needs exchange | Reads                                                                                                   | Truth                               | Example                                                      |
| ----------- | -------------- | ------------------------------------------------------------------------------------------------------- | ----------------------------------- | ------------------------------------------------------------ |
| Market desk | level 1        | What happened: big daily moves, IPOs, first trading days, buy-outs, delistings                          | Always true, after the fact         | "Westdale Software opens 38% above its offering price"       |
| City desk   | level 2        | The stock's city driver over the month (output, passengers, deaths…); a company's weekly profit         | True facts; leading for some stocks | "Oil output up 12% this month"                               |
| Analysts    | level 3        | The game's hidden trend state for each of its stocks (rising or falling, slow to fast), a few days late | Right about 3 times in 4 (option)   | "Analysts upgrade Greasy Gasoline to Buy: momentum building" |
| Rumour mill | level 4        | Buy-out odds for local companies, a coming slump in a sector                                            | Right about 3 times in 5 (option)   | "Whispers of a takeover bid for Fashionable Fashion Shoppe"  |

The game keeps, for each of its stocks, a hidden state (stable, or rising / falling slowly, medium
or fast) that tends to persist from day to day and nudges the price ("random influence"); this is
what analysts read. Big headlines (takeover rumours, shocks, IPOs) also go to Chirper. All rates,
delays, levels and how long news is kept are options.

## Build order (one step at a time, each behind the flag "Investments list and research")

1. The sectioned list with filters, sort, rows and scrolling, replacing both columns.
2. Research tab with the market desk and "what moves it" (facts only).
3. City desk.
4. Analysts with ratings and track record.
5. Rumour mill, Chirper and the Monitor's news markers.
6. Tuning in a city left alone; options, docs, changeset.

## Spike: replacing the two lists (2026-11-25)

A throwaway dev spike put one list in place of the two in a running game (not kept in the code).

- **The page is easy to take over.** Economy → Investments is one panel (`Investments`, 1110 × 599)
  with five controls of its own: the two titles (`LabelPurchasable`, `LabelPortfolio`), the two
  lists with their scrollbars (`InvestmentsPanel`, `OwnedInvestmentsPanel`) and the Total value bar
  (`OwnedInvestmentsSummaryPanel`). Hiding those five and adding one panel at (16, 12) gives a 1078
  × 540 list in the game's frame. Showing them again gives the game's tab back. Nothing is patched
  to do it.
- **The game keeps building its cards, hidden.** It rebuilds them when the panel opens and updates
  them every frame while the tab is open (`EconomyPanel.UpdateInvestments`). That does no harm but
  costs time with many stocks; step 1 measures it and, if needed, skips the card loop while the new
  list is shown.
- **Buy and Sell need nothing new.** A row's button calls the game's
  `BuySellInvestmentPanel.ShowPanel`, and the mod's Buy now / Limit tabs come with it. The game
  closes the Buy dialog at once for a locked stock, so rows must show the lock (requirement and bar)
  as the game's cards do.
- **A virtual list scrolls well.** A fixed set of about 13 row controls is refilled from the scroll
  offset: three rows a notch, a scrollbar, and the place is kept when the panel is closed and opened
  again. Refreshing 13 rows a frame is cheap at any number of stocks.
- **What moves into the new list:** the mod's add-ons now tied to the game's cards (local company
  cards, open-order cards, avg cost, the wheel step) become sections and parts of rows. With the
  flag off they keep working on the game's cards as today.
- **For tests:** rows are named by stock symbol (`…Row-GGI`), not by pool position, so a scenario
  can find a stock wherever it has scrolled to.

### How step 1 is built

- **Library:** the list model (sections, filters, sort, which rows are visible for an offset, and
  keeping the same stock at the top when the list changes) in `FinanceOverhaul.Core`, unit tested.
- **Mod:** `InvestmentsList` on the overlay root: hides the five controls, draws the pooled rows,
  hands Buy and Sell to the game's dialog, puts everything back when the flag is off or the mod
  unloads (hot reload).
- **Flag:** Options → Feature flags → "Investments list and research", on in dev builds only.
- **Game UI scenario:** the list replaces the two, filters, sort, scroll keeps its place through a
  purchase and a reopen, Buy and Sell open the dialog, a locked stock shows its bar, flag off gives
  the game's tab back.

## Step 1 as built (2026-11-25, reworked after a first try)

The first version split All into Your portfolio and All stocks. Tried in game, that read as if the
list were two lists again: the player decided **All is one list**, sorted as a whole, and sections
appear only when asked for. Rows follow Figma band 18, "Condensed B".

- **Flag:** Options → Feature flags → "Investments list and research" (dev builds; pinned off in
  release builds). On, `InvestmentsList` hides the page's five controls and shows the list; off
  again (or a hot reload) shows them again.
- **Top bar:** sub tabs (secondary, under the Economy panel's own tabs) All · Owned · Orders · Local
  · Locked with their counts (Orders: stocks with an open order, 2026-11-26); **Group** (pressed:
  "Ungroup") splits the stocks shown into Your portfolio (value and gain), Local companies and All
  stocks; the Stock Monitor's sorts and direction. A new tab, grouping or sort starts from the top.
- **Sort and pills, as in the Stock Monitor (2026-11-25):** the sort is the Monitor's kind of
  control (a muted "Sort: Change %" to click through Change, Symbol, Price, Value held, then ▲/▼),
  and a click on any change pill shows every change in ₡ or %, with Sort: Change then sorting by the
  ₡ amount ("Sort: Change ₡"), as there. The list keeps its **own** saved sort, direction and unit
  (`investmentList*` settings), separate from the Monitor's; Options → "Investments list: sort and ₡
  / % follow the Stock Monitor" (off by default) makes it use and change the Monitor's instead.
  Scenarios 79 and 80 set and restore both windows' settings.
- **Rows (Condensed B):** coin, symbol (LOCAL badge for a local company) and name; for a holding a
  third line "500 shares · avg ₡6,616"; the 30-day line; price above the change pill, and for a
  holding the gain under it (no buttons since the two columns). A locked stock shows "Locked · 30 /
  50" and a bar, last in the list. **Open orders** sit under their stock, one line each ("Open order
  · SELL (stop-loss) · 500 shares if the price drops to ₡6,800 · 54 days left") with Cancel, as the
  lower part of the stock's card: the same fill, no gap, a thin rule between
  (`InvestmentRow.JoinsNext`). Four layouts for the empty space between the price and Buy / Sell
  (stats column, wider chart, order chip, 30-day range bar) are in Figma band 20, "Row layouts",
  waiting for a pick.
- **Two columns and the overview (2026-11-27):** the list is the left column (about half the tab)
  and an overview the right. Rows lost their Buy and Sell buttons: a click anywhere on a line opens
  the dialog (Buy, or Sell when nothing is left to buy), whose Buy | Sell tabs turn it. The
  overview's header is a sub tab group (Market · Portfolio · Heatmap · City · Feed) that picks one
  view at a time; a view not built yet has a disabled tab ("Not built yet"). The view and its 7D |
  30D range are kept in the settings (`InvestmentOverviewPage`, `InvestmentOverviewDays`). The views
  come one at a time from the Stitch designs (Figma band 20, "Two columns"), redrawn in the game's
  panel art and `FoTheme`:
  1. **Market overview** (built): the market index (`MarketOverview.Index`: every stock on sale
     rebased to 100 on the first day shown and averaged, each counted equally; a stock listed later
     joins on its first day), its change over the range, a 7D | 30D chart with its high and low,
     today's top three gainers and losers (a click opens the stock's dialog) and "9 up · 6 down · 1
     flat".
  2. Portfolio dashboard, 3. heatmap, 4. city and market, 5. market desk feed: designed, not built.
     Later, a click on a stock shows its trade in this column instead of the dialog.
- **Hover:** over a stock's 30-day line the game's own stock tooltip opens with its large "Share
  value" chart, beside the line (left of it near the screen edge), as over the game's cards; it
  closes when the pointer leaves. The list reads the mod's pointer each frame, so game UI tests can
  hover.
- **Hover and click:** the stock line under the pointer is outlined in the accent colour; a click
  anywhere on it (not on Buy, Sell or Cancel) opens the game's dialog (Buy, with Buy | Sell tabs to
  turn it). Figma component FO/InvestmentListRow. While a dialog is open over the panel (the game's
  Buy / Sell, any modal) nothing behind it is outlined or opens its chart.
- **Buy | Sell in the dialog:** the Invest dialog has a Buy | Sell tab group above the order types
  (`OrderTicket`). Picking the other side turns the open dialog to it in place, through the game's
  own set-up for that side, with that side's order types (Buy now, Limit / Sell now, Stop-loss,
  Take-profit, Trailing stop). Sell is disabled, with a tooltip, when the city holds none. This
  works for the game's own lists too.
- **Tab groups everywhere a choice is one of several (2026-11-25):** Kit `FoToggleTabs`, Figma
  FO/ToggleTabs: drawn exactly as the game's own tab strip at the top of the Economy panel (Taxes ·
  Budget · Loans · Investments): tabs joined edge to edge in the game's tab art, white text, the
  chosen tab lit with dark text, the game's disabled look. Used by the dialog's Buy | Sell and its
  order types, the Stock Monitor's All | Portfolio | Orders and 7D | 30D, this list's All | Owned |
  Orders | Local | Locked, and the bond term (52 | 104 | 260 weeks). A group under another is
  **secondary** (`FoToggleTabs.Secondary()`): the same tab art dimmed with grey text, the chosen tab
  plain grey with white text and a thin accent line under it. The dialog's order types (under Buy |
  Sell) and the Monitor's 7D | 30D (under its filters) and this list's filters (under the Economy
  panel's tabs) are secondary; the rest are primary. A click on the chosen tab is still passed on,
  and the Monitor's bar shows the filter the player left it on (a first build lit All over the
  Orders list and ignored the click on All). The group is a real `UITabstrip`, so the game's own
  state rules drive it: the chosen tab is the Focused state and stays lit under the mouse and while
  pressed, another tab shows the hover and pressed art, a disabled tab the game's disabled art (an
  earlier build drew the chosen look as the normal art, so hovering the chosen tab turned it grey).
  (Two earlier looks were tried and dropped: the tab art with white text on the lit tab, and joined
  menu buttons.)
- **Nothing left to buy (fixed 2026-11-25):** a Buy dialog for a stock the city holds all it may of
  has an empty share range, and the game's slider then puts its thumb at no position at all, where
  it takes every click in the dialog (only Escape closed it). The game never opens one (it greys out
  Buy); the mod's Buy buttons could. Now Buy is greyed out with a tooltip in the list and the Stock
  Monitor, a click on such a line opens Sell, the dialog's Buy tab is disabled, and a Buy dialog
  opened anyway turns to Sell or closes (`OrderTicket`; scenario 71). The tab group also follows the
  strip's own change of tab, so a quick second click, which the game sends as a double click, still
  switches.
- **Scrolling:** three plain rows a notch, the game's scrollbar; the line at the top of the view
  stays there when the list changes (a purchase, the daily update), and the place is kept when the
  panel is closed and opened.
- **Library:** `InvestmentList` (tabs, grouping, order lines, line heights, places, keeping the
  place, wording) with `InvestmentListTests`; `MarketQuotes` reads every stock for both the Monitor
  and this list.
- **Checked:** game UI scenario 79 (the list replaces the two, tabs, Group / Ungroup, sort and
  direction, a reopen, flag off); scenario 80 (TST: hover chart, outline, a click on a line, a
  purchase, grouping, the Buy | Sell switch), the same steps run on Oil in a city without TST, with
  the outline and a click on a line. The scenarios that check the game's own lists switch the new
  list off for their run and put it back.
- **Not yet:** the Watchlist and News tabs and the ★; a click on a row opening the Stock Monitor
  (step 2, with Research); the kinds' unlock bars in Local companies (the game's cards keep them
  while the flag is off); measuring what the game's hidden cards cost a frame with many stocks.

## Open questions

- Watchlist and search: both in step 1, or watchlist later?
- Which city figure drives each game stock: to be read from the game before step 3.
- Does a news history belong in the save (yes, bounded) and what happens to it without the mod
  (dropped, like other mod data)?

# Investments list and research (design)

Status: **proposal, nothing built** (decided 2026-11-25). Figma UI Map band 20; band 18 holds the
earlier condensed-row sketches this grows from.

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

## Open questions

- Watchlist and search: both in step 1, or watchlist later?
- Which city figure drives each game stock: to be read from the game before step 3.
- Does a news history belong in the save (yes, bounded) and what happens to it without the mod
  (dropped, like other mod data)?

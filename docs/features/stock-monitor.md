# Stock Monitor

## Value

A floating, see-through window that watches every listed stock while the mayor keeps building — like
a phone stocks app, styled like the City Vitals window players already know.

## List window (shipped)

- **Open it** with the round button in the top-left mod row (right-drag to move it; the place is
  remembered). The button looks pressed while the window is open.
- **Window:** centred title, the game's own gear and close buttons, a dark background behind the
  rows (35% by default; the gear cycles 35% → 60% → 90% → 0% until the settings popover ships). Drag
  the title to move it; the position is remembered.
- **Filters:** All · Portfolio (stocks the city holds) · Orders (stocks with open orders). **Sort**
  (click it) cycles Change (biggest gain first) → Symbol → Price → Value held. Change follows what
  the pills show and names its unit: "Sort: Change %" with % pills; with ₡ pills it orders by that ₡
  amount and reads "Sort: Change ₡" (a cheap stock with a big % move is a small ₡ move); proved over
  2,000 random lists, checked in scenario 45.
- **Sort direction:** a ▼/▲ button right of "Sort: …" flips the order (highest or Z first ↔ lowest
  or A first). Each sort starts in its natural direction (Change, Price, Value held highest first;
  Symbol A→Z); ties stay A→Z. Kept per player (`monitor.sortReversed`). Proved over 2,000 random
  lists for every sort (`MonitorSortDirectionTests`); game UI scenario 45 flips it in game.
- **Rows:** ticker symbol (same as the LED board; unique per stock, see
  [Brokerage mechanics map](./brokerage-mechanics-map.md)) and name; a cyan dot when the city holds
  it and a gold "N orders" badge for open orders; a sparkline of the game's 30 daily prices with a
  dashed line at yesterday's price, coloured by the change rate; today's price in whole ₡; a change
  pill with the game's change rate (the same number as the Economy panel and the LED), green / red /
  grey for 0.00, black text.
- Up to 10 rows show at once; the list scrolls with the mouse wheel. Footer: "Daily close · date".

## Detail view (shipped)

Click a row: the window shows that stock in place of the list (**« All stocks** goes back; the
window remembers which view was open).

- **Header:** symbol and name; today's price with its change in ₡ and % — the game's change rate,
  the same measure as the row's pill — and "Today · yesterday ₡…" for reference.
- **Chart:** **7D** or **30D** (the game keeps 30 daily prices) — line, shaded area, dashed line at
  yesterday's price, grid with ₡ labels at the high, the low and two steps between.
- **Stats:** today, yesterday, the range's high and low; shares held, avg cost / share
  ([Cost basis](./cost-basis.md)), gain or loss like Your Portfolio (current value − you invested;
  "—" with no shares) and current value; then **Open orders** across the full width — one or two
  orders in short form ("BUY ≤ ₡95 · SELL ≥ ₡125 (target)"), or "3 open (see Your Portfolio)". Each
  value is right-aligned in the space its label leaves and shrinks slightly (never below 0.6) rather
  than being cut off; scenario `33-tst-monitor-orders-fit` checks the list, detail and Your
  Portfolio cards with three open orders.
- **Buy / Sell** open the game's Invest dialog for that stock (with the order tabs). Sell is
  disabled when the city holds none.

## Hover (shipped)

Over any row sparkline or the detail chart: a crosshair, a dot on the line and a bubble with that
day's price ("Oil · 8 days ago · ₡925", "today", "yesterday"), like City Vitals' value bubble.
Hit-testing runs only while the mouse is over the window.

## Background on hover

Like City Vitals' "Transparent Unhovered": the background sits at the chosen opacity and fades to a
solid 90% while the mouse is over the window (`monitor.transparentUnhovered`, on by default).

## Settings popover (shipped)

The gear opens a popover beside the window (it follows the window, and flips to the left near the
screen edge):

- **Background opacity** slider, 0% (see-through, like City Vitals) to 90%, in 5% steps.
- **Solid while the mouse is over it** (City Vitals' "Transparent Unhovered").
- **Compact rows (hide names):** one line per stock — symbol, sparkline, price, pill.
- **Show sparklines.**
- **Change as ₡ instead of %:** the pill shows the same change as the % pill (the game's change
  rate) in ₡ — `price × r ÷ (1 + r)`, cents under ₡10 — so both modes always agree in sign and
  colour (unit tested over 5,000 random cases). Clicking any row's pill flips this too.
- **Stocks shown:** a checkbox per listed stock, kept by prefab name (the same in every language).

All of it is kept per player in the settings file (`monitor.compact`, `monitor.sparklines`,
`monitor.change`, `monitor.hidden`, …), never in the city save.

## Share value charts (shipped)

The game's own "Share value" charts — in the Invest dialog and in the Economy panel's hover tooltip
— are drawn in the same style as the detail view (`ShareChartView`): line and shaded area coloured
by the change rate, dashed line at yesterday's price, ₡ grid on the right, "30 days ago … today",
and the same hover (crosshair, dot, "Oil · 8 days ago · ₡925").

- **Fit:** the vanilla `UIGraph` stays in place (layouts do not move), and so does the game's dark
  chart panel. Its `GraphParent` sliced sprite draws about 9 UI units wider than the graph on each
  side and stops about 21 short of its bottom (measured in game at 1920×1200), so the chart is drawn
  inside that panel with no background of its own.
- **Updates:** after each vanilla update (`BuySellInvestmentPanel.UpdateBindings`,
  `InvestmentTooltip.Update`) the mod draws its chart and clears the curve the game just added. The
  tooltip updates every frame, so the chart redraws only when its stock or prices change.
- **Flag:** **Share value charts in the Stock Monitor style (testing)** (`shareChartsEnabled`, on by
  default). Off — and on mod disable or hot reload — the overlay is removed and the game redraws its
  own chart on its next update.

## One colour rule

Green, red and grey everywhere — row pills (% and ₡), row sparklines, the detail header and chart,
and the Invest dialog / tooltip charts — follow **one measure: the game's change rate**, the number
the Economy panel shows as "Change rate" and the LED board shows (`CalculateChangeRate(…, 5)`, a
short trend over the last few days, not just since yesterday). 0.00 is grey, as on the LED. The
dashed line marks yesterday's price as a reference only. **One ₡-change rule everywhere:** every ₡
change (Monitor pills, the detail header, the LED "price and ₡ change" mode, the ₡ sort) comes from
`StockMonitor.ChangeAmount` = price × r / (1 + r), shown with cents up to ₡9.99 and whole ₡ from ₡10
(decided on the rounded value, so never "₡10.00"); proved over 5,000 quotes in
`OneCurrencyChangeRuleTests`. The dashed line is drawn as thick as the price line (dash and gap 3×
that thickness), stays on top of the shaded area and of the grid lines, so it survives the chart
being scaled down in every view (sparklines, detail, share charts; `SparklineTests`).

## History limit

The game keeps exactly 30 daily prices per stock (`Investment.kPastValueCount`), so the monitor's
history is 7 to 30 days. Longer, mod-recorded history (6M–10Y) is parked.

## Save and reload safety

UI only: nothing is written to the city save. The open state, window and button positions, filter,
sort and opacity are kept per player in the settings file (`monitor.*` keys). It lives on the ticker
root, so a level unload or a DLL hot reload removes it and rebuilds it; UI left by an older assembly
is removed by name. Prices move once per game day, so rows are rebuilt only when a cheap signature
(day, holdings, open orders, filter, sort) changes, checked twice a second while open.

## Options flag (temporary)

**Stock Monitor window (testing)** — on by default. Off removes the button and the window at once.

## Later steps

Esc to close.

## Code

- Rules: `StockMonitor`, `Sparkline`, `StockMonitorSettings` in the Market library (unit tested).
- Host: `StockMonitorHost` (`mod/Host/StockMonitorWindow.cs`), added to the ticker root.
- Game UI scenarios `27-stock-monitor-list` and `29-tst-stock-monitor-detail` (UI only: no days
  advanced, no trades).
- Detail numbers and wording: `StockDetail` (Market, unit tested); host `StockMonitorDetail.cs`.
- Share charts: `mod/Host/ShareChart.cs`; scenario `32-tst-share-chart-dialog`.
- Figma: UI Map band 10; component `FO/ShareChart` restyled to match.

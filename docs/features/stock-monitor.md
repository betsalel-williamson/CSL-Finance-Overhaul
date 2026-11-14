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
  (click it) cycles Change (biggest gain first) → Symbol → Price → Value held.
- **Rows:** ticker symbol (same as the LED board; unique per stock, see
  [Brokerage mechanics map](./brokerage-mechanics-map.md)) and name; a cyan dot when the city holds
  it and a gold "N orders" badge for open orders; a sparkline of the game's 30 daily prices with a
  dashed line at yesterday's price, coloured by today's change; today's price in whole ₡; a change
  pill with the game's change rate (the same number as the Economy panel and the LED), green / red /
  grey for 0.00, black text.
- Up to 10 rows show at once; the list scrolls with the mouse wheel. Footer: "Daily close · date".

## Detail view (shipped)

Click a row: the window shows that stock in place of the list (**« All stocks** goes back; the
window remembers which view was open).

- **Header:** symbol and name; today's price with the change **vs yesterday** in ₡ and % (green /
  red / grey), and "Today · yesterday ₡…". The row pill shows the game's change rate; the header is
  day over day.
- **Chart:** **7D** or **30D** (the game keeps 30 daily prices) — line, shaded area, dashed line at
  yesterday's price, grid with ₡ labels at the high, the low and two steps between.
- **Stats:** today, yesterday, the range's high and low; shares held, avg cost / share
  ([Cost basis](./cost-basis.md)), gain or loss like Your Portfolio (current value − you invested;
  "—" with no shares), and open orders (e.g. "1 · SELL ≥ ₡120 (target)", or "3 open").
- **Buy / Sell** open the game's Invest dialog for that stock (with the order tabs). Sell is
  disabled when the city holds none.

## Background on hover

Like City Vitals' "Transparent Unhovered": the background sits at the chosen opacity and fades to a
solid 90% while the mouse is over the window (`monitor.transparentUnhovered`, on by default).

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

Row and chart hover (crosshair and that day's price), the settings popover (opacity slider, compact
rows, which stocks show, ₡ or % change), Esc to close.

## Code

- Rules: `StockMonitor`, `Sparkline`, `StockMonitorSettings` in the Market library (unit tested).
- Host: `StockMonitorHost` (`mod/Host/StockMonitorWindow.cs`), added to the ticker root.
- Game UI scenarios `27-stock-monitor-list` and `29-tst-stock-monitor-detail` (UI only: no days
  advanced, no trades).
- Detail numbers and wording: `StockDetail` (Market, unit tested); host `StockMonitorDetail.cs`.
- Figma: UI Map band 10.

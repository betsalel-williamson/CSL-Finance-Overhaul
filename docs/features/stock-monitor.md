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
- **Rows:** ticker symbol (same as the LED board) and name; a cyan dot when the city holds it and a
  gold "N orders" badge for open orders; a sparkline of the game's 30 daily prices with a dashed
  line at yesterday's price, coloured by today's change; today's price in whole ₡; a change pill
  with the game's change rate (the same number as the Economy panel and the LED), green / red / grey
  for 0.00, black text.
- Up to 10 rows show at once; the list scrolls with the mouse wheel. Footer: "Daily close · date".

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

Row hover (crosshair and that day's price), the detail view (7D / 30D chart, stats, Buy / Sell), the
settings popover (opacity slider, compact rows, which stocks show, ₡ or % change), Esc to close.

## Code

- Rules: `StockMonitor`, `Sparkline`, `StockMonitorSettings` in the Market library (unit tested).
- Host: `StockMonitorHost` (`mod/Host/StockMonitorWindow.cs`), added to the ticker root.
- Game UI scenario `27-stock-monitor-list` (UI only: no days advanced, no trades).
- Figma: UI Map band 10.

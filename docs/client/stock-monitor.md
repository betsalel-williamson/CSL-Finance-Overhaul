# Stock Monitor

A window that lists every stock at once. Open it with the round **Stock Monitor** button in the
top-left row of mod buttons; drag the button with the right mouse button to move it. **Esc** closes
it, as the game's windows close.

![Stock Monitor](./img/stock-monitor.jpg)

## The list

- Each row: symbol and name, a 30-day sparkline (the dashed line is yesterday's close), today's
  price and today's change.
- **All / Portfolio / Orders** filter the list to everything, what you own, or your open orders.
- **Sort** cycles Change, Symbol, Price and Value held; the arrow button beside it reverses the
  order. A click on a change pill shows every change in ₡ (points) or %.
- The list fades at the top or bottom when there are more stocks to scroll to.
- **A click on a stock** opens the game's Invest dialog with its chart and stats (see
  [Buying, selling and orders](./trading-and-orders.md)).

The sort and the % / ₡ are shared with the [Investments tab](./investments-tab.md) unless you unlink
them in [Options](./options.md).

## Orders tab

Every open order under its stock: the side and type, shares and trigger price, the days left, and
how far today's price is from the trigger. Order types switched off in Options are shown muted. The
tab sorts by nearest to its trigger, soonest to end, or symbol.

## Settings (gear button)

Background opacity (the whole window fades, and it turns solid under the mouse), compact rows,
sparklines on or off, change in ₡ instead of %, and which stocks are shown. The game's own **Share
value** charts in the Invest dialog use the same style.

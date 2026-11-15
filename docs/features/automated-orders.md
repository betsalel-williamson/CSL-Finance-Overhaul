# Automated orders

## Value

Mayors set a buy price once and keep building, like a limit order at Schwab or E*Trade, instead of
watching the Investments panel for a dip. Step 1 is the **limit buy**; step 2 the **stop-loss**;
step 3 the **take-profit**; step 4 the **trailing stop**.

## Contract (step 1: limit buy)

1. **Place.** In **Invest in: {name}** (Buy), the player can choose **Limit** instead of buying now,
   enter a limit price per share, and place the order for the slider's share count. The vanilla Buy
   (a market order) is unchanged.
2. **Daily check, like a daily close.** Prices change once per in-game day. Right after the vanilla
   daily investment update, each open order is checked against that day's mark.
3. **Fill.** When the mark is at or below the limit, the order buys through vanilla `BuyShares` at
   that day's mark (never above the limit), capped by the vanilla share limit
   (`AvailableForBuyingCount`); spend counts toward the Economy panel like a manual buy.
4. **Cash check.** Vanilla `BuyShares` does not check cash (the dialog does). If the city cannot
   afford the fill, the order is **cancelled** ("insufficient funds"), as a broker rejects it.
5. **Share limit.** If no shares are available to buy, the order is cancelled ("share limit").
6. **Expiry.** Orders are good for 90 game days by default, then expire (like Schwab's GTC cap). The
   player picks the lifetime in Options — **Orders stay open for (new orders)**: 30, 60, 90, 120,
   180 or 365 days (`ordersLifetimeDays`). It applies to orders placed from then on; each order
   keeps the expiry it was placed with (saved in the order book), so changing it never cancels or
   extends an open order.
7. **Notify.** Every fill, cancel and expiry is logged with the share count, price and reason.
8. **Save-safe.** Orders live in the mod's own save data (`FinanceOverhaul.Orders`, versioned text).
   Without the mod the game keeps the blob and ignores it: the city loads, holdings are untouched,
   and pending orders simply do not run. Holdings never move into mod data.
9. **Hot-reload safe.** The order book is read from and written to the game's in-memory mod data on
   every change, so replacing the DLL keeps open orders.

## Ticket (shipped)

Buy mode of **Invest in: {name}** has **Buy now** (vanilla, unchanged) and **Limit** tabs above the
share amount. Limit shows **Limit price (₡ per share)** (whole ₡, up to 7 digits, pre-filled with
today's price), a hint that wraps inside the dialog, and the vanilla Price row as **Est. cost (at
limit)**. The button reads **Place limit order**; it places the order and closes the dialog. An
empty or invalid price shows an error and never buys. Sell mode is unchanged. Every open resets to
Buy now with the vanilla labels.

## Stop-loss (shipped)

The **Sell** dialog has **Sell now** (vanilla, unchanged) and **Stop-loss** tabs. Stop-loss shows
**Stop price (₡ per share)**, pre-filled 10% below today's price, with the rule in plain words and
**Est. proceeds (at stop)** in place of the vanilla value rows; the button reads **Place
stop-loss**.

- **Place.** The stop must be below today's price (as brokers reject a sell stop at or above the
  market); otherwise the estimate row says why and the button is disabled.
- **Fill.** Once the daily price is at or below the stop, it sells through vanilla `SellShares` at
  that day's price, so after a sharp drop it sells for less than the stop. Never more than the city
  still holds; same bookkeeping as the vanilla Sell button (last sell time, portfolio card removed
  when nothing is left). The gain or loss is booked against the average cost
  ([Cost basis](./cost-basis.md)).
- **Cancel.** If the shares were already sold, it cancels ("no shares left to sell"). Expires after
  the order lifetime (Options, 90 days by default) like every order.
- **Saved** as a `stop-sell` line in the same versioned order book; older builds skip it.

## Take-profit (shipped)

A third Sell tab, **Take-profit** (a sell limit), shows **Target price (₡ per share)**, pre-filled
10% above today's price (rounded up), and **Est. proceeds (at target)**; the button reads **Place
take-profit**.

- **Place.** The target must be above today's price (a broker would fill a sell limit at or below
  the market right away); otherwise the estimate row says why and the button is disabled.
- **Fill.** Once the daily price is at or above the target, it sells at that day's price, so after a
  sharp rise it sells for more than the target. Capped by the shares still held, same bookkeeping
  and cancel rules as the stop-loss, the same expiry.
- **Saved** as a `profit-sell` line; older builds skip it.

Wording for every order kind (tabs, captions, hints, cards, log names) lives in `OrderText` in the
Market library and is unit tested.

## Trailing stop (shipped)

The fourth Sell tab, **Trailing stop**, takes **Trail (% below the peak)**, whole percent from 1 to
50, pre-filled with 10%. The hint names today's stop ("stop now ₡90"), and **Est. proceeds (at
today's stop)** uses it; the button reads **Place trailing stop**.

- **Peak.** Starts at the daily price when placed. Each daily check first raises the peak to a new
  high (logged as "trailing stop follows a new high"), so the stop only moves up.
- **Fill.** Once the daily price is at or below the stop (the trail below the peak, rounded down to
  whole ₡), it sells at that day's price; after a sharp drop that is less than the stop. Capped by
  the shares still held, same cancel rules and the same expiry as the other sells.
- **Card.** "Open order · SELL (trailing stop)" with "N shares, 10% trail, stop ₡X · days left"; the
  stop shown follows the peak.
- **Saved** as a `trail-sell` line with an eighth column, the peak (invariant float). Older builds
  only read seven-column lines and skip it; a damaged peak or a trail outside 1–50% is skipped.

## Open orders in Your Portfolio (shipped)

Each open order is an entry of **Economy → Investments → Your Portfolio**: a card right under its
stock's holding (or where that stock would be listed when not held), showing the stock, **Open order
· BUY**, shares and limit, days left, and **Cancel**. Vanilla re-sorts the list periodically and
only knows its own cards, so the mod re-places order cards right after each sort (postfix on
`EconomyPanel.UpdateInvestments`). A filled, cancelled or expired order's card disappears at the
next refresh. Fills keep the game's average cost basis ([Cost basis](./cost-basis.md)).

The detail line (shares, price, days left) runs up to the **Cancel** button and is never cut off: a
line that nearly fits is drawn slightly smaller (down to text scale 0.6, as in the Stock Monitor),
and one too long even then wraps and the card grows (`LabelFit`; scenario
`41-tst-portfolio-order-detail-fits`).

Known flaky interaction: right after a holding card is removed (a test selling all its shares), an
order card was once reported not visible for under a second; not reproduced in normal play.

## Options flags (temporary)

While the order types are being tested, Options → **Finance Overhaul** has one checkbox per order
type, all on by default: **Orders: Limit buys**, **Orders: Stop-loss sells**, **Orders: Take-profit
sells**, **Orders: Trailing stop sells**. Switching one off:

- hides that type's tab in the Invest dialog; when every order type of a mode (Buy or Sell) is off
  the whole tabs row is hidden and the dialog is the vanilla trade. If the dialog was on that tab it
  returns to the vanilla trade.
- keeps that type's open orders but pauses them: the daily check skips them entirely (no fill, no
  expiry countdown, no trailing peak update).
- shows " · paused in Options" in grey on their Your Portfolio cards (**Cancel** still works).

Switching it back on shows the tab again and the paused orders resume at the next daily check.
Settings keys: `ordersLimitBuyEnabled`, `ordersStopLossEnabled`, `ordersTakeProfitEnabled`,
`ordersTrailingStopEnabled` (`true`/`false`, missing means on).

## Game UI tests

`npm run test:game` drives the running game's UI (dev build, city with a Stock Exchange) through the
scenarios in `tests/game-ui/`: ticket layout and labels, nothing overflowing the window, Buy now
still a vanilla buy, invalid prices never buying, place and cancel, a fill on the next day, Sell now
still a vanilla sell, stop-loss and take-profit placing, filling after a gap, and cancelling with no
shares left, and the Options flags hiding tabs and pausing orders. See
[Testing](../developer/testing.md).

## Later steps

Recurring monthly investment, fill notifications in Chirper. Dividend reinvestment waits for
dividend shares ([Market analogs](./market-analogs.md)).

## Code

- Rules: `OrderRules`, `OrderBook`, `OrderBookCodec` in the Market library (unit tested).
- Host: daily evaluation on `EconomyManager.SimulationStepInvestments`; storage via
  `SimulationManager.m_SerializableDataWrapper`.

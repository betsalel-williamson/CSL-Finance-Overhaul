# Automated orders

## Value

Mayors set a buy price once and keep building, like a limit order at Schwab or E*Trade, instead of
watching the Investments panel for a dip. Step 1 is the **limit buy**.

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
6. **Expiry.** Orders are good for 60 game days, then expire (like Schwab's GTC cap).
7. **Notify.** Every fill, cancel and expiry is logged with the share count, price and reason.
8. **Save-safe.** Orders live in the mod's own save data (`FinanceOverhaul.Orders`, versioned text).
   Without the mod the game keeps the blob and ignores it: the city loads, holdings are untouched,
   and pending orders simply do not run. Holdings never move into mod data.
9. **Hot-reload safe.** The order book is read from and written to the game's in-memory mod data on
   every change, so replacing the DLL keeps open orders.

## Later steps

Stop-loss and take-profit sells, trailing stop, recurring monthly investment, an open-orders panel
in the Investments view, fill notifications in Chirper, and Figma ticket designs. Dividend
reinvestment waits for dividend shares ([Market analogs](./market-analogs.md)).

## Code

- Rules: `OrderRules`, `OrderBook`, `OrderBookCodec` in the Market library (unit tested).
- Host: daily evaluation on `EconomyManager.SimulationStepInvestments`; storage via
  `SimulationManager.m_SerializableDataWrapper`.

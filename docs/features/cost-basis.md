# Cost basis

## How the game accounts for buys at different prices

Financial Districts keeps an **average cost basis** per investment (read from the game code):

- **Buy** (`InvestmentAI.BuyShares`): adds `round(price × shares)` to _You invested_
  (`Investment.m_playerSpentMoney`) and the shares to the holding. Limit order fills use the same
  vanilla buy at the fill price, so their basis is exact.
- **Sell** (`CalculateSellingGainOrLoss`, `SellShares`): the shares sold carry
  `round(invested × sold ÷ owned)` of the basis; gain = proceeds − that basis, booked as investment
  profit. _You invested_ shrinks to the shares left (truncated) and resets to 0 when the position
  closes.

Example: 100 at ₡100 (₡10,000) plus a limit fill of 100 at ₡80 (₡8,000) → ₡18,000 invested, ₡90.00
average. Selling 50 at ₡95 (₡4,750) realizes +₡250 against ₡4,500 of basis; ₡13,500 stays invested.

Not modelled: tax lots (FIFO / specific lots), per-holding realized profit or trade history.

## Avg cost / share (shipped)

Every holding card in **Economy → Investments → Your Portfolio** shows **Avg cost / share** (You
invested ÷ Shares, two decimals, the game's money locale) under Gain/Loss. UI only and save-neutral;
refreshed right after the game updates the cards.

## Code

- Rule and format: `CostBasis` in the Market library (unit tested).
- Host: `PortfolioAvgCost` (postfix on `EconomyPanel.UpdateInvestments`).
- Game UI scenario `16-tst-avg-cost` buys TST at ₡100 and ₡80 and expects ₡90.00.

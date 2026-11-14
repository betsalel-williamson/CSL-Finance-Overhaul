using System;
using System.Globalization;

namespace FinanceOverhaul.Market
{
    /// <summary>
    /// Player-facing wording and placement rules per order kind, shared by the ticket, the
    /// Your Portfolio card and the log. Prices are whole ₡ per share.
    /// </summary>
    public static class OrderText
    {
        public static string TabLabel(OrderKind kind)
        {
            switch (kind)
            {
                case OrderKind.StopLossSell:
                    return "Stop-loss";
                case OrderKind.TakeProfitSell:
                    return "Take-profit";
                default:
                    return "Limit";
            }
        }

        public static string ButtonLabel(OrderKind kind)
        {
            switch (kind)
            {
                case OrderKind.StopLossSell:
                    return "Place stop-loss";
                case OrderKind.TakeProfitSell:
                    return "Place take-profit";
                default:
                    return "Place limit order";
            }
        }

        public static string PriceCaption(OrderKind kind)
        {
            switch (kind)
            {
                case OrderKind.StopLossSell:
                    return "Stop price (₡ per share)";
                case OrderKind.TakeProfitSell:
                    return "Target price (₡ per share)";
                default:
                    return "Limit price (₡ per share)";
            }
        }

        /// <summary>Label before the amount, e.g. "Est. proceeds (at target): ₡".</summary>
        public static string EstimateLabel(OrderKind kind)
        {
            switch (kind)
            {
                case OrderKind.StopLossSell:
                    return "Est. proceeds (at stop): ₡";
                case OrderKind.TakeProfitSell:
                    return "Est. proceeds (at target): ₡";
                default:
                    return "Est. cost (at limit): ₡";
            }
        }

        public static string Hint(OrderKind kind, int price)
        {
            string p = Money(price);
            string good = " Good for " + TradeOrder.LifetimeDays + " days.";
            switch (kind)
            {
                case OrderKind.StopLossSell:
                    return "Sells when the daily price is at or below ₡"
                        + p
                        + ". After a sharp drop it can sell for less."
                        + good;
                case OrderKind.TakeProfitSell:
                    return "Sells when the daily price is at or above ₡"
                        + p
                        + ". After a sharp rise it can sell for more."
                        + good;
                default:
                    return "Buys when the daily price is at or below ₡" + p + "." + good;
            }
        }

        /// <summary>
        /// Pre-filled price: a limit at today's price, a stop 10% below it, a target 10%
        /// above it (whole ₡, at least 1).
        /// </summary>
        public static int SuggestedPrice(OrderKind kind, float mark)
        {
            if (float.IsNaN(mark) || float.IsInfinity(mark) || mark <= 0f)
            {
                return 1;
            }

            switch (kind)
            {
                case OrderKind.StopLossSell:
                    return Math.Max(1, (int)Math.Floor(mark * 0.9));
                case OrderKind.TakeProfitSell:
                    // Round away float noise first: 100 × 1.1 must be 110, not 111.
                    return Math.Max(1, (int)Math.Ceiling(Math.Round(mark * 1.1, 4)));
                default:
                    return Math.Max(1, (int)Math.Floor(mark));
            }
        }

        /// <summary>
        /// Why a sell trigger is on the wrong side of today's price (a broker rejects a sell
        /// stop at or above the market, and a sell limit at or below it), or null.
        /// </summary>
        public static string PriceSideProblem(OrderKind kind, int price, float mark)
        {
            if (float.IsNaN(mark) || float.IsInfinity(mark) || mark <= 0f)
            {
                return null;
            }

            if (kind == OrderKind.StopLossSell && price >= mark)
            {
                return "Set the stop below today's price (₡" + Money((int)Math.Floor(mark)) + ").";
            }

            if (kind == OrderKind.TakeProfitSell && price <= mark)
            {
                return "Set the target above today's price (₡"
                    + Money((int)Math.Floor(mark))
                    + ").";
            }

            return null;
        }

        /// <summary>The Your Portfolio card's side line.</summary>
        public static string CardSide(OrderKind kind)
        {
            switch (kind)
            {
                case OrderKind.StopLossSell:
                    return "Open order · SELL (stop-loss)";
                case OrderKind.TakeProfitSell:
                    return "Open order · SELL (take-profit)";
                default:
                    return "Open order · BUY";
            }
        }

        /// <summary>The Your Portfolio card's detail line, e.g. "10 shares if the price rises to ₡120 · 5 days left".</summary>
        public static string CardDetail(OrderKind kind, uint shares, int price, int daysLeft)
        {
            string what;
            switch (kind)
            {
                case OrderKind.StopLossSell:
                    what = " shares if the price drops to ₡";
                    break;
                case OrderKind.TakeProfitSell:
                    what = " shares if the price rises to ₡";
                    break;
                default:
                    what = " shares at or below ₡";
                    break;
            }

            int left = Math.Max(0, daysLeft);
            return Money(shares)
                + what
                + Money(price)
                + " · "
                + left.ToString(CultureInfo.InvariantCulture)
                + (left == 1 ? " day left" : " days left");
        }

        /// <summary>Log name, e.g. "take-profit sell".</summary>
        public static string LogName(OrderKind kind)
        {
            switch (kind)
            {
                case OrderKind.StopLossSell:
                    return "stop-loss sell";
                case OrderKind.TakeProfitSell:
                    return "take-profit sell";
                default:
                    return "limit buy";
            }
        }

        /// <summary>Log word for the trigger, e.g. "target".</summary>
        public static string TriggerWord(OrderKind kind)
        {
            switch (kind)
            {
                case OrderKind.StopLossSell:
                    return "stop";
                case OrderKind.TakeProfitSell:
                    return "target";
                default:
                    return "limit";
            }
        }

        private static string Money(long value)
        {
            return value.ToString("N0", CultureInfo.InvariantCulture);
        }
    }
}

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
                case OrderKind.TrailingStopSell:
                    return "Trailing stop";
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
                case OrderKind.TrailingStopSell:
                    return "Place trailing stop";
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
                case OrderKind.TrailingStopSell:
                    return "Trail (% below the peak)";
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
                case OrderKind.TrailingStopSell:
                    return "Est. proceeds (at today's stop): ₡";
                default:
                    return "Est. cost (at limit): ₡";
            }
        }

        /// <param name="price">The field's value: ₡ per share, or the trail percent.</param>
        /// <param name="mark">Today's price (trailing stop: shows today's stop).</param>
        public static string Hint(
            OrderKind kind,
            int price,
            float mark = 0f,
            int lifetimeDays = TradeOrder.DefaultLifetimeDays
        )
        {
            string p = Money(price);
            string good = " Good for " + TradeOrder.ClampLifetime(lifetimeDays) + " days.";
            switch (kind)
            {
                case OrderKind.TrailingStopSell:
                    return "Sells if the daily price falls "
                        + p
                        + "% below its highest since you place it (stop now ₡"
                        + Money(TradeOrder.TrailingStop(mark, price))
                        + "). The stop only moves up."
                        + good;
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
        /// Pre-filled field: a limit at today's price, a stop 10% below it, a target 10%
        /// above it (whole ₡, at least 1); a trailing stop's trail of 10%.
        /// </summary>
        public static int SuggestedPrice(OrderKind kind, float mark)
        {
            if (kind == OrderKind.TrailingStopSell)
            {
                return 10;
            }

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

        /// <summary>Per-share price the estimate row uses: the field's price, or today's trailing stop.</summary>
        public static int EstimatePerShare(OrderKind kind, int price, float mark)
        {
            return kind == OrderKind.TrailingStopSell
                ? TradeOrder.TrailingStop(mark, price)
                : price;
        }

        /// <summary>
        /// Why this trigger can't be placed, or null: a sell stop at or above today's price, a
        /// sell target at or below it (a broker would reject or fill them at once), or a trail
        /// outside 1–50%.
        /// </summary>
        public static string PlacementProblem(OrderKind kind, int price, float mark)
        {
            if (
                kind == OrderKind.TrailingStopSell
                && (price < OrderRules.MinTrailPercent || price > OrderRules.MaxTrailPercent)
            )
            {
                return "Set the trail between "
                    + OrderRules.MinTrailPercent
                    + "% and "
                    + OrderRules.MaxTrailPercent
                    + "%.";
            }

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

        /// <summary>Options dropdown entry for an order lifetime, e.g. "90 days".</summary>
        public static string LifetimeLabel(int days)
        {
            return days.ToString(CultureInfo.InvariantCulture) + " days";
        }

        /// <summary>Options toggle label for an order type's feature flag.</summary>
        public static string OptionLabel(OrderKind kind)
        {
            switch (kind)
            {
                case OrderKind.StopLossSell:
                    return "Orders: Stop-loss sells";
                case OrderKind.TakeProfitSell:
                    return "Orders: Take-profit sells";
                case OrderKind.TrailingStopSell:
                    return "Orders: Trailing stop sells";
                default:
                    return "Orders: Limit buys";
            }
        }

        /// <summary>Side line of a card whose order type is switched off in Options.</summary>
        public static string CardSide(OrderKind kind, bool paused)
        {
            return paused ? CardSide(kind) + " · paused in Options" : CardSide(kind);
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
                case OrderKind.TrailingStopSell:
                    return "Open order · SELL (trailing stop)";
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

        /// <summary>The Your Portfolio card's detail line for an open order.</summary>
        public static string CardDetail(TradeOrder order, int today)
        {
            if (order.Kind != OrderKind.TrailingStopSell)
            {
                return CardDetail(
                    order.Kind,
                    order.Shares,
                    order.TriggerPrice,
                    order.ExpiresDay - today
                );
            }

            int left = Math.Max(0, order.ExpiresDay - today);
            return Money(order.Shares)
                + " shares, "
                + order.TrailPercent.ToString(CultureInfo.InvariantCulture)
                + "% trail, stop ₡"
                + Money(order.TriggerPrice)
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
                case OrderKind.TrailingStopSell:
                    return "trailing stop sell";
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
                case OrderKind.TrailingStopSell:
                    return "stop";
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

using System.Globalization;

namespace FinanceOverhaul.Core
{
    /// <summary>
    /// Lines about orders for the mod's log file. English on purpose: the log is read by whoever
    /// is fixing a bug, not by the player, so it never goes through the string table.
    /// </summary>
    public static class OrderLog
    {
        public static string Name(OrderKind kind)
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

        public static string TriggerWord(OrderKind kind)
        {
            switch (kind)
            {
                case OrderKind.TakeProfitSell:
                    return "target";
                case OrderKind.LimitBuy:
                    return "limit";
                default:
                    return "stop";
            }
        }

        /// <param name="mark">Today's price (a trailing stop starts its peak there).</param>
        public static string Placed(TradeOrder order, float mark)
        {
            string trigger;
            switch (order.Kind)
            {
                case OrderKind.TrailingStopSell:
                    trigger =
                        " trailing "
                        + order.TrailPercent
                        + "% from "
                        + Price(mark)
                        + " (stop "
                        + order.TriggerPrice
                        + ")";
                    break;
                case OrderKind.TakeProfitSell:
                    trigger = " at or above " + order.TriggerPrice;
                    break;
                default:
                    trigger = " at or below " + order.TriggerPrice;
                    break;
            }

            return "orders: placed #"
                + order.Id
                + " "
                + Name(order.Kind)
                + " "
                + order.Shares
                + " "
                + order.InvestmentKey
                + trigger
                + " (expires day "
                + order.ExpiresDay
                + ")";
        }

        public static string Filled(TradeOrder order, uint shares, float mark, long amount)
        {
            return "orders: filled #"
                + order.Id
                + (order.IsSell ? " sold " : " bought ")
                + shares
                + " "
                + order.InvestmentKey
                + " at "
                + Price(mark)
                + " ("
                + TriggerWord(order.Kind)
                + " "
                + order.TriggerPrice
                + ") for "
                + amount;
        }

        public static string Cancelled(TradeOrder order, string reason)
        {
            return "orders: cancelled #"
                + order.Id
                + " "
                + Name(order.Kind)
                + " "
                + order.Shares
                + " "
                + order.InvestmentKey
                + ": "
                + reason;
        }

        public static string PeakRaised(TradeOrder order, float mark)
        {
            return "orders: #"
                + order.Id
                + " trailing stop follows a new high "
                + Price(mark)
                + " (stop "
                + order.TriggerPrice
                + ")";
        }

        private static string Price(float mark)
        {
            return mark.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }
}

namespace FinanceOverhaul.Core
{
    /// <summary>
    /// Words for an order in the mod's log file. English on purpose: the log is read by whoever
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
    }
}

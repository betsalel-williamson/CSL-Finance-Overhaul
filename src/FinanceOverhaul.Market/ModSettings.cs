namespace FinanceOverhaul.Market
{
    /// <summary>
    /// Mod Options toggles — pure data, no Cities Options UI.
    /// </summary>
    public sealed class ModSettings
    {
        /// <summary>
        /// Experimental Index stock. Off by default because it adds a mod-only
        /// investment to the save; see docs/features/save-compatibility.md.
        /// Read when the investment collection loads, so toggle before loading a city.
        /// </summary>
        public bool IndexStockEnabled { get; set; }

        /// <summary>
        /// Live Stock Exchange ticker overlay. Visual only and save-neutral, so on by default.
        /// </summary>
        public bool LiveTickerEnabled { get; set; } = true;

        /// <summary>
        /// Dev builds only: lists "Test Stock (dev)" (TST) for game UI tests. Adds an
        /// investment to the save, so off by default and meant for a test save.
        /// </summary>
        public bool DevTestStockEnabled { get; set; }

        // Automated orders, one temporary feature flag per order type (on by default; orders
        // live in the mod's own save data, so turning one off never touches holdings). Off:
        // its ticket tab is hidden and its open orders stay saved but are not checked.
        public bool LimitBuyEnabled { get; set; } = true;

        public bool StopLossEnabled { get; set; } = true;

        public bool TakeProfitEnabled { get; set; } = true;

        public bool TrailingStopEnabled { get; set; } = true;

        public bool IsEnabled(OrderKind kind)
        {
            switch (kind)
            {
                case OrderKind.LimitBuy:
                    return LimitBuyEnabled;
                case OrderKind.StopLossSell:
                    return StopLossEnabled;
                case OrderKind.TakeProfitSell:
                    return TakeProfitEnabled;
                case OrderKind.TrailingStopSell:
                    return TrailingStopEnabled;
                default:
                    return false; // a kind this build doesn't know
            }
        }

        public void SetEnabled(OrderKind kind, bool enabled)
        {
            switch (kind)
            {
                case OrderKind.LimitBuy:
                    LimitBuyEnabled = enabled;
                    break;
                case OrderKind.StopLossSell:
                    StopLossEnabled = enabled;
                    break;
                case OrderKind.TakeProfitSell:
                    TakeProfitEnabled = enabled;
                    break;
                case OrderKind.TrailingStopSell:
                    TrailingStopEnabled = enabled;
                    break;
            }
        }

        /// <summary>Every order type, in ticket order, for the Options toggles.</summary>
        public static readonly OrderKind[] OrderKinds =
        {
            OrderKind.LimitBuy,
            OrderKind.StopLossSell,
            OrderKind.TakeProfitSell,
            OrderKind.TrailingStopSell,
        };
    }
}

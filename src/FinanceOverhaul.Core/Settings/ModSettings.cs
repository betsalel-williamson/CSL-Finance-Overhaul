using System.Collections.Generic; // List

namespace FinanceOverhaul.Core
{
    /// <summary>
    /// Mod Options toggles — pure data, no Cities Options UI.
    /// </summary>
    public sealed class ModSettings
    {
        /// <summary>
        /// Feature flags (whether a feature exists at all) are dev-only: release builds hide them
        /// and use these values, so a player never meets a half-finished feature or gets stuck
        /// with a hidden flag switched off. Preferences (how a shipped feature looks or behaves)
        /// stay in Options in every build. See docs/features/ui-catalog.md.
        /// </summary>
        public void PinReleaseFlags()
        {
            // Shipped in 0.1 (confirmed with the player-facing owner, 2026-11-15): on.
            LimitBuyEnabled = true;
            StopLossEnabled = true;
            TakeProfitEnabled = true;
            TrailingStopEnabled = true;
            Monitor.Enabled = true;
            ShareChartsEnabled = true;
            IndexStockEnabled = true;
            CityBondsEnabled = true;

            // Not ready for players: still being built.
            LocalIposEnabled = false;
            InvestmentListEnabled = false;

            // Dev only.
            DevTestStockEnabled = false;
        }

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

        /// <summary>
        /// Temporary feature flag: the game's "Share value" charts (Invest dialog, Economy
        /// tooltip) drawn in the Stock Monitor style. UI only; off shows the vanilla chart.
        /// </summary>
        public bool ShareChartsEnabled { get; set; } = true;

        /// <summary>
        /// Temporary feature flag: the city may sell bonds (Economy → Loans). Off by default
        /// while unproven. It gates new issues only — bonds already sold keep paying, so turning
        /// it off never strands a debt.
        /// </summary>
        public bool CityBondsEnabled { get; set; }

        /// <summary>
        /// Feature flag, off by default and off in release builds while it is being built:
        /// Local IPOs. For now it only counts how long each business has stayed healthy and adds
        /// that count to the save's mod data.
        /// </summary>
        public bool LocalIposEnabled { get; set; }

        /// <summary>
        /// Feature flag, off by default and off in release builds while it is being built: one
        /// sectioned list in place of Economy → Investments' two (design:
        /// investments-and-research.md). UI only and save-neutral; off gives the game's tab back.
        /// </summary>
        public bool InvestmentListEnabled { get; set; }

        /// <summary>
        /// Preference: the Investments list uses the Stock Monitor's sort, direction and ₡ / %
        /// pills instead of its own. Off by default: each window keeps its own.
        /// </summary>
        public bool InvestmentListSortSynced { get; set; }

        /// <summary>The Investments list's own sort (when not synced with the Stock Monitor).</summary>
        public MonitorSort InvestmentListSort { get; set; } = MonitorSort.Change;

        /// <summary>The Investments list's own sort direction (the ▲/▼).</summary>
        public bool InvestmentListSortReversed { get; set; }

        /// <summary>The Investments list's own pills show the change in ₡ instead of %.</summary>
        public bool InvestmentListChangeAsCurrency { get; set; }

        /// <summary>The overview page the Investments tab's right-hand column last showed.</summary>
        public OverviewPage InvestmentOverviewPage { get; set; } = OverviewPage.Market;

        /// <summary>The days the overview's charts span (7 or 30).</summary>
        public int InvestmentOverviewDays { get; set; } = StockDetail.LongRange;

        /// <summary>
        /// Preference: show the IPO dialog when a local company is announced. The dialog's
        /// "Don't show this for future IPOs" checkbox is this same value, read the other way.
        /// </summary>
        public bool IpoDialogEnabled { get; set; } = true;

        /// <summary>
        /// Every Local IPOs value (Options: the player's groups and, in dev builds, the finer
        /// workings; <see cref="IpoTuning"/>). Saved as <c>ipo.*</c> lines, only where a value
        /// differs from its default.
        /// </summary>
        public IpoRules Ipo { get; set; } = new IpoRules();

        /// <summary>Local IPOs presets the player saved by name (<see cref="IpoPresets"/>).</summary>
        public List<IpoUserPreset> IpoUserPresets { get; set; } = new List<IpoUserPreset>();

        /// <summary>How the Stock Exchange LED ticker shows the change (visual only).</summary>
        public TickerDisplay TickerShows { get; set; }

        /// <summary>LED board brightness (visual only); Bright by default for daylight.</summary>
        public LedBrightness TickerBrightness { get; set; } = LedBrightness.Bright;

        /// <summary>
        /// Keep the live LED boards while an info view (traffic, population…) is open. Off: they
        /// hide there and the facade shows the game's own info-view look, like the original.
        /// </summary>
        public bool TickerInInfoViews { get; set; } = true;

        /// <summary>Temporary feature flag: order and bond notices in Chirper (UI only, on).</summary>
        public bool NoticesEnabled { get; set; } = true;

        /// <summary>Also chirp every bond coupon (off: monthly per bond is chatty).</summary>
        public bool NoticesBondCoupons { get; set; }

        /// <summary>Whether the live boards draw now (the ticker on, and not hidden by an info view).</summary>
        public bool TickerShownNow(bool infoViewOpen)
        {
            return LiveTickerEnabled && (TickerInInfoViews || !infoViewOpen);
        }

        /// <summary>Stock Monitor flag and per-player window state (UI only, never in saves).</summary>
        public StockMonitorSettings Monitor { get; } = new StockMonitorSettings();

        // Automated orders, one temporary feature flag per order type (on by default; orders
        // live in the mod's own save data, so turning one off never touches holdings). Off:
        // its ticket tab is hidden and its open orders stay saved but are not checked.
        public bool LimitBuyEnabled { get; set; } = true;

        public bool StopLossEnabled { get; set; } = true;

        public bool TakeProfitEnabled { get; set; } = true;

        public bool TrailingStopEnabled { get; set; } = true;

        private int orderLifetimeDays = TradeOrder.DefaultLifetimeDays;

        /// <summary>How long new orders stay open, in game days (Options; default 90).</summary>
        public int OrderLifetimeDays
        {
            get { return orderLifetimeDays; }
            set { orderLifetimeDays = TradeOrder.ClampLifetime(value); }
        }

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

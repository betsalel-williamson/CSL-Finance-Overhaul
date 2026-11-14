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
    }
}

namespace FinanceOverhaul.Market
{
    /// <summary>
    /// Probe bias settings — pure data, no Cities Options UI.
    /// </summary>
    public sealed class ProbeSettings
    {
        public bool ProbeBiasEnabled { get; set; }

        public float ProbeBiasFactor { get; set; } = PriceBias.DefaultProbeFactor;

        /// <summary>
        /// Experimental Index stock. Off by default because it adds a mod-only
        /// investment to the save; see docs/features/save-compatibility.md.
        /// Read when the investment collection loads, so toggle before loading a city.
        /// </summary>
        public bool IndexStockEnabled { get; set; }
    }
}

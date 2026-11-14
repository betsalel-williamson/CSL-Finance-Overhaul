namespace FinanceOverhaul.Market
{
    /// <summary>
    /// Probe bias settings — pure data, no Cities Options UI.
    /// </summary>
    public sealed class ProbeSettings
    {
        public bool ProbeBiasEnabled { get; set; }

        public float ProbeBiasFactor { get; set; } = PriceBias.DefaultProbeFactor;
    }
}

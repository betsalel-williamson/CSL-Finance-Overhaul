namespace FinanceOverhaul.Market
{
    /// <summary>
    /// Applies probe bias to a discovered price member. Cities Harmony calls this
    /// from a postfix; unit tests drive it with plain objects.
    /// </summary>
    public static class ProbeBiasApplier
    {
        private static readonly string[] PriceMemberNames =
        {
            "m_price",
            "m_sharePrice",
            "m_currentPrice",
            "price",
            "SharePrice",
            "CurrentPrice",
            "Price",
        };

        public static bool TryApply(object instance, ProbeSettings settings)
        {
            if (settings == null || !settings.ProbeBiasEnabled || instance == null)
            {
                return false;
            }

            float factor = settings.ProbeBiasFactor;
            for (int i = 0; i < PriceMemberNames.Length; i++)
            {
                string member = PriceMemberNames[i];
                float price = StockMarketProbe.ReadFloatMember(instance, member);
                if (float.IsNaN(price))
                {
                    continue;
                }

                float biased = PriceBias.Apply(price, factor);
                if (StockMarketProbe.TryWriteFloatMember(instance, member, biased))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

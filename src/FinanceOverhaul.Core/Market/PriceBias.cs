namespace FinanceOverhaul.Core
{
    /// <summary>
    /// Pure price helpers for index/ETF marks.
    /// Cities-free — unit and performance tests own this surface.
    /// </summary>
    public static class PriceBias
    {
        /// <summary>Vanilla Financial Districts lists thirteen sector stocks.</summary>
        public const int VanillaSectorCount = 13;

        public static float WeightedAverage(float[] prices, float[] weights)
        {
            if (prices == null || weights == null || prices.Length == 0)
            {
                return 0f;
            }

            int n = prices.Length < weights.Length ? prices.Length : weights.Length;
            float sumW = 0f;
            float sum = 0f;
            for (int i = 0; i < n; i++)
            {
                float w = weights[i];
                if (w <= 0f || float.IsNaN(w) || float.IsInfinity(w))
                {
                    continue;
                }

                float p = prices[i];
                if (float.IsNaN(p) || float.IsInfinity(p))
                {
                    continue;
                }

                sumW += w;
                sum += p * w;
            }

            if (sumW <= 0f)
            {
                return 0f;
            }

            return sum / sumW;
        }

        /// <summary>
        /// Equal-weight city index over the given sector prices (padded/truncated to
        /// <see cref="VanillaSectorCount"/> when building fixtures).
        /// </summary>
        public static float EqualWeightIndex(float[] sectorPrices)
        {
            if (sectorPrices == null || sectorPrices.Length == 0)
            {
                return 0f;
            }

            // Same result as WeightedAverage with all weights 1 (non-finite prices
            // skipped), without allocating a weights array on every call.
            float sum = 0f;
            int count = 0;
            for (int i = 0; i < sectorPrices.Length; i++)
            {
                float p = sectorPrices[i];
                if (float.IsNaN(p) || float.IsInfinity(p))
                {
                    continue;
                }

                sum += p;
                count++;
            }

            return count == 0 ? 0f : sum / count;
        }
    }
}

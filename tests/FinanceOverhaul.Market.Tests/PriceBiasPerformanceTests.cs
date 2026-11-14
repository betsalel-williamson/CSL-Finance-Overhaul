using System;
using System.Diagnostics;
using FinanceOverhaul.Market;
using Xunit;

namespace FinanceOverhaul.Market.Tests
{
    /// <summary>
    /// Keeps index math cheap enough for simulation ticks. These budgets are for
    /// the Market library only — not Cities frame time.
    /// </summary>
    public class PriceBiasPerformanceTests
    {
        [Fact]
        public void EqualWeightIndex_ThirteenSectors_StaysUnderBudget()
        {
            float[] prices = new float[PriceBias.VanillaSectorCount];
            for (int i = 0; i < prices.Length; i++)
            {
                prices[i] = 100f + i;
            }

            // Warmup
            for (int i = 0; i < 1000; i++)
            {
                PriceBias.EqualWeightIndex(prices);
            }

            const int iterations = 100_000;
            Stopwatch sw = Stopwatch.StartNew();
            float last = 0f;
            for (int i = 0; i < iterations; i++)
            {
                last = PriceBias.EqualWeightIndex(prices);
            }

            sw.Stop();
            Assert.True(last > 0f);
            // Generous CI budget: 100k equal-weight index marks under 250ms.
            Assert.True(
                sw.ElapsedMilliseconds < 250,
                "EqualWeightIndex too slow: " + sw.ElapsedMilliseconds + "ms for " + iterations
            );
        }

        [Fact]
        public void Apply_ProbeBias_StaysUnderBudget()
        {
            const int iterations = 200_000;
            Stopwatch sw = Stopwatch.StartNew();
            float last = 0f;
            for (int i = 0; i < iterations; i++)
            {
                last = PriceBias.Apply(100f + (i % 13), PriceBias.DefaultProbeFactor);
            }

            sw.Stop();
            Assert.True(last > 0f);
            Assert.True(
                sw.ElapsedMilliseconds < 100,
                "PriceBias.Apply too slow: " + sw.ElapsedMilliseconds + "ms for " + iterations
            );
        }
    }
}

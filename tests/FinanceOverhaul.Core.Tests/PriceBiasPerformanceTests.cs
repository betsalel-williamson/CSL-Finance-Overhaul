using System;
using System.Diagnostics;
using FinanceOverhaul.Core;
using Xunit;

namespace FinanceOverhaul.Core.Tests
{
    /// <summary>
    /// Keeps index math cheap enough for simulation ticks. These budgets are for
    /// the Market library only — not Cities frame time.
    /// <para>
    /// Robust to a busy machine (the game running, CI neighbours): timing uses the best
    /// of several batches, because other load can only make a batch slower, never
    /// faster. Allocation checks are exact and do not depend on timing at all.
    /// </para>
    /// </summary>
    public class PriceBiasPerformanceTests
    {
        private const int Batches = 9;

        [Fact]
        public void EqualWeightIndex_ThirteenSectors_StaysUnderBudget()
        {
            float[] prices = SectorPrices();
            float last = 0f;
            double best = BestBatchMilliseconds(
                20_000,
                () => last = PriceBias.EqualWeightIndex(prices)
            );

            Assert.True(last > 0f);
            // ~20 ns per call on a laptop; budget 1 µs per call (20 ms per batch).
            Assert.True(
                best < 20.0,
                "EqualWeightIndex too slow: best batch " + best.ToString("0.0") + "ms / 20k calls"
            );
        }

        [Fact]
        public void EqualWeightIndex_DoesNotAllocate()
        {
            float[] prices = SectorPrices();
            PriceBias.EqualWeightIndex(prices); // JIT
            long before = GC.GetAllocatedBytesForCurrentThread();
            float sum = 0f;
            for (int i = 0; i < 10_000; i++)
            {
                sum += PriceBias.EqualWeightIndex(prices);
            }

            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.True(sum > 0f);
            Assert.Equal(0L, allocated);
        }

        private static float[] SectorPrices()
        {
            float[] prices = new float[PriceBias.VanillaSectorCount];
            for (int i = 0; i < prices.Length; i++)
            {
                prices[i] = 100f + i;
            }

            return prices;
        }

        /// <summary>Runs one warm-up batch, then returns the fastest of <see cref="Batches"/> batches.</summary>
        private static double BestBatchMilliseconds(int callsPerBatch, Action call)
        {
            for (int i = 0; i < callsPerBatch; i++)
            {
                call();
            }

            double best = double.MaxValue;
            var sw = new Stopwatch();
            for (int b = 0; b < Batches; b++)
            {
                sw.Restart();
                for (int i = 0; i < callsPerBatch; i++)
                {
                    call();
                }

                sw.Stop();
                best = Math.Min(best, sw.Elapsed.TotalMilliseconds);
            }

            return best;
        }
    }
}

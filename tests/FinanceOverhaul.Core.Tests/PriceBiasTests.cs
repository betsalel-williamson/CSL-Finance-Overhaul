using FinanceOverhaul.Core;
using Xunit;

namespace FinanceOverhaul.Core.Tests
{
    public class PriceBiasTests
    {
        [Fact]
        public void WeightedAverage_MatchesEqualWeights()
        {
            float avg = PriceBias.WeightedAverage(new[] { 10f, 20f, 30f }, new[] { 1f, 1f, 1f });
            Assert.Equal(20f, avg, 3);
        }

        [Fact]
        public void WeightedAverage_RespectsWeights()
        {
            float avg = PriceBias.WeightedAverage(new[] { 10f, 30f }, new[] { 1f, 3f });
            Assert.Equal(25f, avg, 3);
        }

        [Fact]
        public void WeightedAverage_EmptyReturnsZero()
        {
            Assert.Equal(0f, PriceBias.WeightedAverage(null, null));
            Assert.Equal(0f, PriceBias.WeightedAverage(new float[0], new float[0]));
        }

        [Fact]
        public void EqualWeightIndex_AveragesThirteenSectors()
        {
            float[] prices = new float[PriceBias.VanillaSectorCount];
            for (int i = 0; i < prices.Length; i++)
            {
                prices[i] = 10f + i;
            }

            float expected = (10f + 22f) / 2f;
            Assert.Equal(expected, PriceBias.EqualWeightIndex(prices), 3);
        }

        [Theory]
        [InlineData(new[] { 100f, 101f, 102f })]
        [InlineData(new[] { 100f, float.NaN, 50f, float.PositiveInfinity })]
        [InlineData(new[] { float.NaN })]
        [InlineData(new[] { -5f, 5f, 0f })]
        public void EqualWeightIndex_MatchesWeightedAverageWithUnitWeights(float[] prices)
        {
            var ones = new float[prices.Length];
            for (int i = 0; i < ones.Length; i++)
            {
                ones[i] = 1f;
            }

            Assert.Equal(
                PriceBias.WeightedAverage(prices, ones),
                PriceBias.EqualWeightIndex(prices),
                5
            );
        }
    }
}

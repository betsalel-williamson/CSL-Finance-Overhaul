using FinanceOverhaul.Market;
using Xunit;

namespace FinanceOverhaul.Market.Tests
{
    public class PriceBiasTests
    {
        [Fact]
        public void Apply_MultipliesByFactor()
        {
            Assert.Equal(101f, PriceBias.Apply(100f, 1.01f), 3);
        }

        [Fact]
        public void Apply_RejectsNonPositiveFactor()
        {
            Assert.Equal(50f, PriceBias.Apply(50f, 0f));
            Assert.Equal(50f, PriceBias.Apply(50f, -1f));
        }

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
    }
}

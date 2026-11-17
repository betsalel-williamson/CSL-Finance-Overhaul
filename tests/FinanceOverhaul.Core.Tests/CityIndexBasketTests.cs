using FinanceOverhaul.Core;
using Xunit;

namespace FinanceOverhaul.Core.Tests
{
    public class CityIndexBasketTests
    {
        [Fact]
        public void IsIndexName_MatchesExactDisplayName()
        {
            Assert.True(CityIndexBasket.IsIndexName("Index"));
            Assert.False(CityIndexBasket.IsIndexName("index"));
            Assert.False(CityIndexBasket.IsIndexName("Indexes"));
            Assert.False(CityIndexBasket.IsIndexName(null));
        }

        [Fact]
        public void TryComputeMark_AveragesEligibleSectors()
        {
            string[] names = { "Oil", "Tech", "Farm" };
            float[] values = { 10f, 20f, 30f };
            bool[] eligible = { true, true, true };

            Assert.True(CityIndexBasket.TryComputeMark(names, values, eligible, out float mark));
            Assert.Equal(20f, mark, 3);
        }

        [Fact]
        public void TryComputeMark_ExcludesIndexItself()
        {
            string[] names = { "Oil", CityIndexBasket.IndexName, "Tech" };
            float[] values = { 10f, 1000f, 30f };
            bool[] eligible = { true, true, true };

            Assert.True(CityIndexBasket.TryComputeMark(names, values, eligible, out float mark));
            Assert.Equal(20f, mark, 3);
        }

        [Fact]
        public void TryComputeMark_SkipsIneligibleAndNonFinite()
        {
            string[] names = { "Oil", "Coin", "Tech", "Broken", "Neg" };
            float[] values = { 10f, 500f, 30f, float.NaN, -5f };
            bool[] eligible = { true, false, true, true, true };

            Assert.True(CityIndexBasket.TryComputeMark(names, values, eligible, out float mark));
            Assert.Equal(20f, mark, 3);
        }

        [Fact]
        public void TryComputeMark_EmptyBasketFailsSoft()
        {
            Assert.False(CityIndexBasket.TryComputeMark(null, null, null, out float mark));
            Assert.Equal(0f, mark);

            string[] names = { CityIndexBasket.IndexName, "Coin" };
            float[] values = { 50f, 50f };
            bool[] eligible = { true, false };
            Assert.False(CityIndexBasket.TryComputeMark(names, values, eligible, out mark));
        }

        [Fact]
        public void TryComputeMark_MismatchedLengthsUsesShortest()
        {
            string[] names = { "Oil", "Tech", "Farm" };
            float[] values = { 10f, 30f };
            bool[] eligible = { true, true, true };

            Assert.True(CityIndexBasket.TryComputeMark(names, values, eligible, out float mark));
            Assert.Equal(20f, mark, 3);
        }
    }
}

using FinanceOverhaul.Core;
using Xunit;

namespace FinanceOverhaul.Core.Tests
{
    public class TestStockTests
    {
        [Fact]
        public void HasTheTstSymbol()
        {
            Assert.Equal("TST", TickerSymbols.For(TestStock.DisplayName));
        }

        [Fact]
        public void PinnedPriceWins()
        {
            Assert.Equal(80f, TestStock.NextMark(80f, 100f));
        }

        [Fact]
        public void WithoutAPinThePriceStaysFlat()
        {
            Assert.Equal(123f, TestStock.NextMark(0f, 123f));
        }

        [Fact]
        public void StartsAtTheBasePrice()
        {
            Assert.Equal(TestStock.BasePrice, TestStock.NextMark(0f, 0f));
            Assert.Equal(TestStock.BasePrice, TestStock.NextMark(float.NaN, float.NaN));
        }

        [Fact]
        public void RecognisesItsPrefabName()
        {
            Assert.True(TestStock.IsTestName(TestStock.PrefabName));
            Assert.False(TestStock.IsTestName("Oil Industry"));
            Assert.False(TestStock.IsTestName(null));
        }
    }
}

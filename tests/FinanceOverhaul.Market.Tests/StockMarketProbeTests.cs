using System.Reflection;
using FinanceOverhaul.Market;
using Xunit;

namespace FinanceOverhaul.Market.Tests
{
    public class StockMarketProbeTests
    {
        private sealed class FakeStock
        {
            public float m_price = 42f;
        }

        [Theory]
        [InlineData("StockData", true)]
        [InlineData("InvestmentPanel", true)]
        [InlineData("SharePriceUtil", true)]
        [InlineData("CameraController", false)]
        public void LooksLikeStockType_MatchesNames(string name, bool expected)
        {
            Assert.Equal(expected, StockMarketProbe.LooksLikeStockType(name));
        }

        [Theory]
        [InlineData("UpdatePrices", true)]
        [InlineData("SimulationStep", true)]
        [InlineData("OnTick", true)]
        [InlineData("ToString", false)]
        public void LooksLikeUpdateMethod_MatchesNames(string name, bool expected)
        {
            Assert.Equal(expected, StockMarketProbe.LooksLikeUpdateMethod(name));
        }

        [Fact]
        public void Discover_FindsFakeStockTypeInThisAssembly()
        {
            Assembly asm = typeof(StockMarketProbeTests).Assembly;
            StockMarketProbe.DiscoveryResult result = StockMarketProbe.Discover(asm);
            Assert.Contains(result.CandidateTypeNames, n => n != null && n.Contains("FakeStock"));
        }

        [Fact]
        public void ReadAndWriteFloatMember_RoundTrips()
        {
            var stock = new FakeStock();
            Assert.Equal(42f, StockMarketProbe.ReadFloatMember(stock, "m_price"));
            Assert.True(StockMarketProbe.TryWriteFloatMember(stock, "m_price", 50f));
            Assert.Equal(50f, stock.m_price);
        }

        [Fact]
        public void ProbeBiasApplier_AppliesWhenEnabled()
        {
            var stock = new FakeStock();
            var settings = new ProbeSettings { ProbeBiasEnabled = true, ProbeBiasFactor = 1.01f };
            Assert.True(ProbeBiasApplier.TryApply(stock, settings));
            Assert.Equal(42f * 1.01f, stock.m_price, 3);
        }

        [Fact]
        public void ProbeBiasApplier_NoOpWhenDisabled()
        {
            var stock = new FakeStock();
            var settings = new ProbeSettings { ProbeBiasEnabled = false };
            Assert.False(ProbeBiasApplier.TryApply(stock, settings));
            Assert.Equal(42f, stock.m_price);
        }
    }
}

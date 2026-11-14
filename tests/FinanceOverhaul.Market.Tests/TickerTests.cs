using FinanceOverhaul.Market;
using Xunit;

namespace FinanceOverhaul.Market.Tests
{
    public class TickerSymbolsTests
    {
        [Theory]
        // Symbols the vanilla Stock Exchange artwork already uses.
        [InlineData("Traffic Jelly Logistics", "TJL")]
        [InlineData("The Very Legit Company", "TVLC")]
        [InlineData("Deathcare Service Fund", "DS")]
        [InlineData("Healthcare Service Fund", "HS")]
        [InlineData("Farming Industry", "FaI")]
        [InlineData("Forestry Industry", "FoI")]
        [InlineData("Ore Industry", "OrI")]
        [InlineData("Oil Industry", "Oil")]
        [InlineData("ChirpAir", "CAi")]
        [InlineData("Chirpy Cruises", "CCr")]
        [InlineData("Chirp Coin", "CC")]
        // New symbols for names the artwork does not cover.
        [InlineData("Generic Industry", "GeI")]
        [InlineData("Greasy Gasoline Inc.", "GGI")]
        [InlineData("Index", "IDX")]
        public void KnownNames(string name, string symbol)
        {
            Assert.Equal(symbol, TickerSymbols.For(name));
        }

        [Theory]
        [InlineData("Some New Fund", "SNF")]
        [InlineData("Alpha Beta Gamma Delta Epsilon", "ABGD")]
        [InlineData("MegaCorp", "MCo")]
        [InlineData("Zapper", "Zap")]
        [InlineData("  spaced   out  ", "SO")]
        public void FallbackRule(string name, string symbol)
        {
            Assert.Equal(symbol, TickerSymbols.For(name));
        }

        [Fact]
        public void EmptyNameGivesPlaceholder()
        {
            Assert.Equal("?", TickerSymbols.For(null));
            Assert.Equal("?", TickerSymbols.For("   "));
        }
    }

    public class TickerLineTests
    {
        [Fact]
        public void FormatsLikeTheBoardWithPanelRounding()
        {
            string line = TickerLine.Format(
                new[] { "TJL", "Oil", "IDX", "FLAT" },
                new[] { 3.2149f, -2.0751f, 1.5f, 0f }
            );
            Assert.Equal("TJL ( +3.21 )  Oil ( -2.08 )  IDX ( +1.50 )  FLAT ( 0.00 )", line);
        }

        [Fact]
        public void NeverPrintsNaN()
        {
            string line = TickerLine.Format(
                new[] { "KTM", "INF" },
                new[] { float.NaN, float.PositiveInfinity }
            );
            Assert.Equal("KTM ( -- )  INF ( -- )", line);
            Assert.DoesNotContain("NaN", line);
        }

        [Fact]
        public void EmptyOrMismatchedInputs()
        {
            Assert.Equal(string.Empty, TickerLine.Format(null, null));
            Assert.Equal("A ( +1.00 )", TickerLine.Format(new[] { "A", "B" }, new[] { 1f }));
        }
    }
}

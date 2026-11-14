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

namespace FinanceOverhaul.Market.Tests
{
    public class TickerRowsTests
    {
        private static readonly string[] Syms =
        {
            "Oil",
            "FaI",
            "DS",
            "HS",
            "IDX",
            "TJL",
            "GGI",
            "TVLC",
        };
        private static readonly float[] Rates =
        {
            7.7f,
            0.64f,
            3.22f,
            2.03f,
            1.2f,
            -4.23f,
            -2.81f,
            float.NaN,
        };

        [Fact]
        public void SplitsEntriesEvenlyIntoRows()
        {
            string[] rows = TickerLine.FormatRichRows(Syms, Rates, 3);
            Assert.Equal(3, rows.Length);
            Assert.Equal(3, Count(rows[0], "( "));
            Assert.Equal(3, Count(rows[1], "( "));
            Assert.Equal(2, Count(rows[2], "( "));
        }

        [Fact]
        public void ColorsUseFigmaTickerTokens()
        {
            string[] rows = TickerLine.FormatRichRows(Syms, Rates, 3);
            Assert.StartsWith(
                "<color=#EFB058>Oil</color> <color=#4EF465>( +7.70 )</color>",
                rows[0]
            );
            Assert.Contains("<color=#E9424F>( -4.23 )</color>", rows[1]);
            Assert.Contains("<color=#9FB0C0>( -- )</color>", rows[2]);
            Assert.DoesNotContain("NaN", string.Join("|", rows));
        }

        [Fact]
        public void FewerEntriesThanRowsLeavesEmptyRows()
        {
            string[] rows = TickerLine.FormatRichRows(new[] { "A" }, new[] { 1f }, 3);
            Assert.Equal(3, rows.Length);
            Assert.Equal(string.Empty, rows[1]);
            Assert.Equal(string.Empty, rows[2]);
        }

        [Fact]
        public void NoEntriesOrBadRowCount()
        {
            Assert.Equal(new[] { string.Empty }, TickerLine.FormatRichRows(null, null, 1));
            Assert.Single(TickerLine.FormatRichRows(Syms, Rates, 0));
        }

        private static int Count(string s, string sub)
        {
            int n = 0;
            for (int i = s.IndexOf(sub); i >= 0; i = s.IndexOf(sub, i + 1))
            {
                n++;
            }

            return n;
        }
    }
}

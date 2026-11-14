using FinanceOverhaul.Market;
using Xunit;

namespace FinanceOverhaul.Market.Tests
{
    public class SettingsFileTests
    {
        [Fact]
        public void RoundTrip_KeepsToggles()
        {
            var original = new ProbeSettings { ProbeBiasEnabled = true, IndexStockEnabled = true };
            ProbeSettings parsed = SettingsFile.Parse(SettingsFile.Format(original));
            Assert.True(parsed.ProbeBiasEnabled);
            Assert.True(parsed.IndexStockEnabled);
        }

        [Fact]
        public void RoundTrip_LiveTickerOff()
        {
            var original = new ProbeSettings { LiveTickerEnabled = false };
            Assert.False(SettingsFile.Parse(SettingsFile.Format(original)).LiveTickerEnabled);
            Assert.True(SettingsFile.Parse("").LiveTickerEnabled);
        }

        [Fact]
        public void Parse_EmptyOrNullGivesSaveSafeDefaults()
        {
            foreach (string text in new[] { null, "", "   \n" })
            {
                ProbeSettings s = SettingsFile.Parse(text);
                Assert.False(s.ProbeBiasEnabled);
                Assert.False(s.IndexStockEnabled);
            }
        }

        [Fact]
        public void Parse_IgnoresUnknownKeysCommentsAndGarbage()
        {
            ProbeSettings s = SettingsFile.Parse(
                "# Finance Overhaul\nfutureThing=1\nnot a pair\nindexStockEnabled = TRUE\r\n"
            );
            Assert.True(s.IndexStockEnabled);
            Assert.False(s.ProbeBiasEnabled);
        }

        [Fact]
        public void Parse_InvalidBooleanKeepsDefault()
        {
            ProbeSettings s = SettingsFile.Parse("indexStockEnabled=maybe");
            Assert.False(s.IndexStockEnabled);
        }
    }
}

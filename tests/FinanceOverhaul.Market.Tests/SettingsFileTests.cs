using FinanceOverhaul.Market;
using Xunit;

namespace FinanceOverhaul.Market.Tests
{
    public class SettingsFileTests
    {
        [Fact]
        public void RoundTrip_KeepsToggles()
        {
            var original = new ModSettings { IndexStockEnabled = true };
            ModSettings parsed = SettingsFile.Parse(SettingsFile.Format(original));
            Assert.True(parsed.IndexStockEnabled);
        }

        [Fact]
        public void RoundTrip_LiveTickerOff()
        {
            var original = new ModSettings { LiveTickerEnabled = false };
            Assert.False(SettingsFile.Parse(SettingsFile.Format(original)).LiveTickerEnabled);
            Assert.True(SettingsFile.Parse("").LiveTickerEnabled);
        }

        [Fact]
        public void Parse_EmptyOrNullGivesSaveSafeDefaults()
        {
            foreach (string text in new[] { null, "", "   \n" })
            {
                ModSettings s = SettingsFile.Parse(text);
                Assert.False(s.IndexStockEnabled);
            }
        }

        [Fact]
        public void Parse_IgnoresUnknownKeysCommentsAndGarbage()
        {
            ModSettings s = SettingsFile.Parse(
                "# Finance Overhaul\nfutureThing=1\nnot a pair\nindexStockEnabled = TRUE\r\n"
            );
            Assert.True(s.IndexStockEnabled);
        }

        [Fact]
        public void Parse_LegacyProbeBiasKeyIsIgnored()
        {
            // Settings written by versions that had the MVP probe bias option.
            ModSettings s = SettingsFile.Parse(
                "probeBiasEnabled=true\nindexStockEnabled=true\nliveTickerEnabled=false\n"
            );
            Assert.True(s.IndexStockEnabled);
            Assert.False(s.LiveTickerEnabled);
            Assert.DoesNotContain("probeBias", SettingsFile.Format(s));
        }

        [Fact]
        public void DevTestStock_IsOffByDefaultAndRoundTrips()
        {
            Assert.False(SettingsFile.Parse("").DevTestStockEnabled);
            var on = new ModSettings { DevTestStockEnabled = true };
            Assert.True(SettingsFile.Parse(SettingsFile.Format(on)).DevTestStockEnabled);
        }

        [Fact]
        public void Parse_InvalidBooleanKeepsDefault()
        {
            ModSettings s = SettingsFile.Parse("indexStockEnabled=maybe");
            Assert.False(s.IndexStockEnabled);
        }
    }
}

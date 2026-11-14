using FinanceOverhaul.Market;
using Xunit;

namespace FinanceOverhaul.Market.Tests
{
    public class ModSettingsTests
    {
        [Fact]
        public void Defaults_AreSaveSafe()
        {
            var settings = new ModSettings();
            Assert.False(settings.IndexStockEnabled);
            // Visual-only and save-neutral, so on by default.
            Assert.True(settings.LiveTickerEnabled);
        }
    }
}

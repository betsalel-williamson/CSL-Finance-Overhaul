using FinanceOverhaul.Core;
using Xunit;

namespace FinanceOverhaul.Core.Tests
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

using FinanceOverhaul.Market;
using Xunit;

namespace FinanceOverhaul.Market.Tests
{
    public class ProbeSettingsTests
    {
        [Fact]
        public void Defaults_AreSaveSafe()
        {
            var settings = new ProbeSettings();
            Assert.False(settings.ProbeBiasEnabled);
            Assert.False(settings.IndexStockEnabled);
        }
    }
}

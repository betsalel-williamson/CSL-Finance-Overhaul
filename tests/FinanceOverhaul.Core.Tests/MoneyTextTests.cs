using FinanceOverhaul.Core; // MoneyText, StockMonitor
using Xunit; // Assert, FactAttribute

namespace FinanceOverhaul.Core.Tests
{
    public class MoneyTextTests
    {
        [Fact]
        public void CentsUnderTenWholeFromTen()
        {
            Assert.Equal("+₡0.40", MoneyText.Change(0.4));
            Assert.Equal("−₡9.99", MoneyText.Change(-9.99));
            Assert.Equal("+₡10", MoneyText.Change(9.996)); // decided on the rounded value
            Assert.Equal("−₡1,234", MoneyText.Change(-1234.4));
            Assert.Equal("₡0", MoneyText.Change(0));
            Assert.Equal("₡0", MoneyText.Change(0.001));
            Assert.Equal("+₡0.01", MoneyText.Change(0.001, true));
            Assert.Equal("--", MoneyText.Change(double.NaN));
        }

        [Fact]
        public void ThePillTextFollowsThePillsUnit()
        {
            Assert.Equal("+1.00%", StockMonitor.PillText(101f, 1f, false));
            Assert.Equal(
                StockMonitor.ChangeAmountFromRate(101f, 1f),
                StockMonitor.PillText(101f, 1f, true)
            );
            Assert.Equal("+₡1.00", StockMonitor.PillText(101f, 1f, true));
        }
    }
}

using FinanceOverhaul.Core; // DialogSide, DialogSides
using Xunit; // Assert, FactAttribute

namespace FinanceOverhaul.Core.Tests
{
    public class DialogSidesTests
    {
        [Fact]
        public void ALineOpensBuyThenSellThenNothing()
        {
            Assert.Equal(DialogSide.Buy, DialogSides.ForLine(3, false, 500, 0));
            Assert.Equal(DialogSide.Buy, DialogSides.ForLine(3, false, 500, 500));
            Assert.Equal(DialogSide.Sell, DialogSides.ForLine(3, false, 0, 1000));
            Assert.Equal(DialogSide.None, DialogSides.ForLine(3, false, 0, 0));
            Assert.Equal(DialogSide.None, DialogSides.ForLine(3, true, 500, 0));
            Assert.Equal(DialogSide.None, DialogSides.ForLine(0, false, 500, 0));
        }
    }
}

using FinanceOverhaul.Market;
using Xunit;

namespace FinanceOverhaul.Market.Tests
{
    public class CostBasisTests
    {
        [Fact]
        public void AveragesWhatWasSpentOverSharesHeld()
        {
            // 100 at ₡100 + 100 at ₡80 = ₡18,000 for 200 shares.
            Assert.Equal("₡90.00", CostBasis.FormatAverage(18000, 200));
        }

        [Fact]
        public void KeepsCentsBecauseBasisIsRarelyWhole()
        {
            Assert.Equal("₡126.29", CostBasis.FormatAverage(125656, 995)); // 126.287…
        }

        [Fact]
        public void NoSharesMeansNoAverage()
        {
            Assert.Null(CostBasis.FormatAverage(0, 0));
            Assert.Null(CostBasis.FormatAverage(500, 0));
        }

        [Fact]
        public void LargeHoldingsUseThousandsSeparators()
        {
            Assert.Equal("₡23,000.50", CostBasis.FormatAverage(46001, 2));
        }
    }
}

using FinanceOverhaul.Core; // StockDetail
using Xunit; // Assert, FactAttribute

namespace FinanceOverhaul.Core.Tests
{
    public class StockDetailEndsTests
    {
        [Fact]
        public void EndsSkipUnknownDays()
        {
            float first,
                last;
            Assert.Equal(
                3,
                StockDetail.Ends(
                    new[] { float.NaN, 5f, float.PositiveInfinity, 6f, 4f, float.NaN },
                    out first,
                    out last
                )
            );
            Assert.Equal(5f, first);
            Assert.Equal(4f, last);
            Assert.Equal(0, StockDetail.Ends(null, out first, out last));
            Assert.True(float.IsNaN(first) && float.IsNaN(last));
        }

        [Fact]
        public void TheTrendComparesTheEnds()
        {
            Assert.Equal(1, StockDetail.Trend(new[] { 1f, 0.5f, 2f }));
            Assert.Equal(-1, StockDetail.Trend(new[] { float.NaN, 3f, 2f }));
            Assert.Equal(0, StockDetail.Trend(new[] { 2f, 9f, 2f }));
            Assert.Equal(0, StockDetail.Trend(new float[0]));
        }
    }
}

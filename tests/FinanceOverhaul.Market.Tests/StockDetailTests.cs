using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace FinanceOverhaul.Market.Tests
{
    public class StockDetailTests
    {
        private static float[] Days(int n, float start = 100f) =>
            Enumerable.Range(0, n).Select(i => start + i).ToArray();

        [Fact]
        public void RangesAreTheLastDays()
        {
            float[] h = Days(30);
            Assert.Equal(
                new[] { 123f, 124f, 125f, 126f, 127f, 128f, 129f },
                StockDetail.Range(h, 7)
            );
            Assert.Equal(30, StockDetail.Range(h, 30).Length);
            Assert.Equal(5, StockDetail.Range(Days(5), 7).Length);
            Assert.Empty(StockDetail.Range(null, 7));
            Assert.Equal(
                2,
                StockDetail.Range(new[] { float.NaN, 1f, float.PositiveInfinity, 2f }, 7).Length
            );
        }

        [Fact]
        public void YesterdayAndHeader()
        {
            Assert.Equal(128f, StockDetail.Yesterday(Days(30)));
            Assert.Null(StockDetail.Yesterday(new[] { 5f }));
            // Header = the game's change rate, like the list pills (one rule everywhere).
            Assert.Equal("+₡4.03 (+0.29%)", StockDetail.ChangeHeader(1394f, 0.29f));
            Assert.Equal("−₡8.03 (-0.45%)", StockDetail.ChangeHeader(1776f, -0.45f));
            Assert.Equal("₡0 (0.00%)", StockDetail.ChangeHeader(932f, 0.001f));
            Assert.Equal("--", StockDetail.ChangeHeader(932f, float.NaN));
            Assert.Equal("Today · yesterday ₡932", StockDetail.YesterdayLine(932f));
        }

        [Fact]
        public void HighLowAndAxis()
        {
            float[] r = { 900f, 990f, 840f, 942f };
            Assert.Equal(990f, StockDetail.High(r));
            Assert.Equal(840f, StockDetail.Low(r));
            Assert.Equal(new[] { 990f, 940f, 890f, 840f }, StockDetail.AxisValues(r, 4));
            Assert.True(float.IsNaN(StockDetail.High(new float[0])));
            Assert.Empty(StockDetail.AxisValues(new float[0], 4));
            Assert.Equal("7-day high", StockDetail.HighLabel(7));
            Assert.Equal("30-day low", StockDetail.LowLabel(30));
            Assert.Equal("7D", StockDetail.RangeChip(7));
        }

        [Fact]
        public void HoldingsLikeTheGame()
        {
            // Your Portfolio: Current value round(price × shares) − You invested.
            Assert.Equal("Loss", StockDetail.GainLossLabel(43600, 200, 152f));
            Assert.Equal("−₡13,200", StockDetail.GainLossText(43600, 200, 152f));
            Assert.Equal("Gain", StockDetail.GainLossLabel(1000, 10, 110f));
            Assert.Equal("₡100", StockDetail.GainLossText(1000, 10, 110f));
            Assert.Equal("—", StockDetail.GainLossText(0, 0, 110f));
            Assert.Equal("200 shares", StockDetail.SharesText(200));
            Assert.Equal("1 share", StockDetail.SharesText(1));
            Assert.Equal("none", StockDetail.SharesText(0));
        }

        [Fact]
        public void OrdersSummaryPerKind()
        {
            Assert.Equal("—", StockDetail.OrdersSummary(new List<TradeOrder>()));
            Assert.Equal(
                "1 · BUY ≤ ₡900",
                StockDetail.OrdersSummary(new[] { TradeOrder.LimitBuy(1, "x", 5, 900, 0) })
            );
            Assert.Equal(
                "1 · SELL ≤ ₡95 (stop)",
                StockDetail.OrdersSummary(new[] { TradeOrder.StopLossSell(1, "x", 5, 95, 0) })
            );
            Assert.Equal(
                "1 · SELL ≥ ₡120 (target)",
                StockDetail.OrdersSummary(new[] { TradeOrder.TakeProfitSell(1, "x", 5, 120, 0) })
            );
            Assert.Equal(
                "1 · SELL, 10% trail",
                StockDetail.OrdersSummary(
                    new[] { TradeOrder.TrailingStopSell(1, "x", 5, 10, 100f, 0) }
                )
            );
            Assert.Equal(
                "2 open",
                StockDetail.OrdersSummary(
                    new[]
                    {
                        TradeOrder.LimitBuy(1, "x", 5, 900, 0),
                        TradeOrder.StopLossSell(2, "x", 5, 95, 0),
                    }
                )
            );
        }
    }

    public class ChartAreaTests
    {
        [Fact]
        public void AreaFillsBelowTheLineOnly()
        {
            byte[] m = Sparkline.Render(new[] { 0f, 10f }, 10, 10, 1, fillArea: true);
            // left column: line at the bottom, nothing below; right column: line on top, area below
            Assert.Equal(Sparkline.Line, m[0 * 10 + 9]);
            Assert.Equal(Sparkline.Area, m[9 * 10 + 9]);
            Assert.NotEqual(Sparkline.Area, m[0 * 10 + 0]);
            Assert.DoesNotContain(Sparkline.Area, Sparkline.Render(new[] { 0f, 10f }, 10, 10));
        }

        [Fact]
        public void RowOfMatchesRender()
        {
            float[] v = { 840f, 990f, 900f };
            byte[] m = Sparkline.Render(v, 40, 30, 3);
            int y = Sparkline.RowOf(990f, 840f, 990f, 30, 3);
            Assert.Equal(Sparkline.Line, m[y * 40 + 20]);
            Assert.Equal(1, Sparkline.RowOf(990f, 840f, 990f, 30, 3));
            Assert.Equal(28, Sparkline.RowOf(840f, 840f, 990f, 30, 3));
        }
    }
}

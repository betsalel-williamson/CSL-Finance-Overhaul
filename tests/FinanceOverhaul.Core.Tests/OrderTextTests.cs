using Xunit;

namespace FinanceOverhaul.Core.Tests
{
    public class OrderTextTests
    {
        [Theory]
        [InlineData(OrderKind.LimitBuy, 100f, 100)]
        [InlineData(OrderKind.LimitBuy, 100.9f, 100)]
        [InlineData(OrderKind.StopLossSell, 100f, 90)]
        [InlineData(OrderKind.TakeProfitSell, 100f, 110)]
        [InlineData(OrderKind.TakeProfitSell, 101f, 112)] // 111.1 rounds up: strictly above
        [InlineData(OrderKind.StopLossSell, 0.5f, 1)]
        [InlineData(OrderKind.TakeProfitSell, float.NaN, 1)]
        public void SuggestsAPriceOnTheRightSide(OrderKind kind, float mark, int expected)
        {
            Assert.Equal(expected, OrderText.SuggestedPrice(kind, mark));
        }

        [Fact]
        public void SuggestedTargetIsAlwaysAboveTodaysPrice()
        {
            for (int m = 1; m < 400; m++)
            {
                int t = OrderText.SuggestedPrice(OrderKind.TakeProfitSell, m);
                Assert.Null(OrderText.PlacementProblem(OrderKind.TakeProfitSell, t, m));
            }
        }

        [Fact]
        public void TargetMustBeAboveTodaysPrice()
        {
            Assert.Equal(
                "Set the target above today's price (₡100).",
                OrderText.PlacementProblem(OrderKind.TakeProfitSell, 100, 100.4f)
            );
            Assert.NotNull(OrderText.PlacementProblem(OrderKind.TakeProfitSell, 90, 100f));
            Assert.Null(OrderText.PlacementProblem(OrderKind.TakeProfitSell, 101, 100.4f));
        }

        [Fact]
        public void StopMustBeBelowTodaysPrice()
        {
            Assert.Equal(
                "Set the stop below today's price (₡100).",
                OrderText.PlacementProblem(OrderKind.StopLossSell, 120, 100f)
            );
            Assert.Null(OrderText.PlacementProblem(OrderKind.StopLossSell, 95, 100f));
        }

        [Fact]
        public void LimitBuysHaveNoSideRule()
        {
            Assert.Null(OrderText.PlacementProblem(OrderKind.LimitBuy, 500, 100f));
        }

        [Fact]
        public void TakeProfitWording()
        {
            Assert.Equal("Take-profit", OrderText.TabLabel(OrderKind.TakeProfitSell));
            Assert.Equal("Place take-profit", OrderText.ButtonLabel(OrderKind.TakeProfitSell));
            Assert.Equal(
                "Target price (₡ per share)",
                OrderText.PriceCaption(OrderKind.TakeProfitSell)
            );
            Assert.Equal(
                "Sells when the daily price is at or above ₡1,200. After a sharp rise it can sell for more. Good for 90 days.",
                OrderText.Hint(OrderKind.TakeProfitSell, 1200)
            );
            Assert.Equal(
                "Open order · SELL (take-profit)",
                OrderText.CardSide(OrderKind.TakeProfitSell)
            );
            Assert.Equal(
                "10 shares if the price rises to ₡120 · 1 day left",
                OrderText.CardDetail(OrderKind.TakeProfitSell, 10, 120, 1)
            );
        }

        [Fact]
        public void ExistingWordingIsUnchanged()
        {
            Assert.Equal(
                "10 shares if the price drops to ₡95 · 60 days left",
                OrderText.CardDetail(OrderKind.StopLossSell, 10, 95, 60)
            );
            Assert.Equal(
                "5 shares at or below ₡80 · 0 days left",
                OrderText.CardDetail(OrderKind.LimitBuy, 5, 80, -3)
            );
            Assert.Equal(
                "Buys when the daily price is at or below ₡80. Good for 90 days.",
                OrderText.Hint(OrderKind.LimitBuy, 80)
            );
            Assert.Equal(
                "Est. proceeds (at stop): ₡",
                OrderText.EstimateLabel(OrderKind.StopLossSell)
            );
        }

        [Fact]
        public void TrailingStopWording()
        {
            Assert.Equal("Trailing stop", OrderText.TabLabel(OrderKind.TrailingStopSell));
            Assert.Equal("Place trailing stop", OrderText.ButtonLabel(OrderKind.TrailingStopSell));
            Assert.Equal(
                "Trail (% below the peak)",
                OrderText.PriceCaption(OrderKind.TrailingStopSell)
            );
            Assert.Equal(10, OrderText.SuggestedPrice(OrderKind.TrailingStopSell, 250f));
            Assert.Equal(
                "Sells if the daily price falls 10% below its highest since you place it (stop now ₡90). The stop only moves up. Good for 90 days.",
                OrderText.Hint(OrderKind.TrailingStopSell, 10, 100f)
            );
            Assert.Equal(90, OrderText.EstimatePerShare(OrderKind.TrailingStopSell, 10, 100f));
            Assert.Equal(95, OrderText.EstimatePerShare(OrderKind.StopLossSell, 95, 100f));
            Assert.Equal(
                "Open order · SELL (trailing stop)",
                OrderText.CardSide(OrderKind.TrailingStopSell)
            );
            TradeOrder o = TradeOrder.TrailingStopSell(1, "Index", 20, 10, 120f, 10);
            Assert.Equal(
                "20 shares, 10% trail, stop ₡108 · 63 days left",
                OrderText.CardDetail(o, 37)
            );
        }

        [Fact]
        public void TrailMustBeBetweenOneAndFiftyPercent()
        {
            Assert.Equal(
                "Set the trail between 1% and 50%.",
                OrderText.PlacementProblem(OrderKind.TrailingStopSell, 0, 100f)
            );
            Assert.NotNull(OrderText.PlacementProblem(OrderKind.TrailingStopSell, 51, 100f));
            Assert.Null(OrderText.PlacementProblem(OrderKind.TrailingStopSell, 50, 100f));
            Assert.Null(OrderText.PlacementProblem(OrderKind.TrailingStopSell, 1, 100f));
        }

        [Fact]
        public void CardDetailFromAnOrderMatchesTheOtherKinds()
        {
            TradeOrder stop = TradeOrder.StopLossSell(1, "Index", 10, 95, 0);
            Assert.Equal(
                "10 shares if the price drops to ₡95 · 90 days left",
                OrderText.CardDetail(stop, 0)
            );
        }
    }
}

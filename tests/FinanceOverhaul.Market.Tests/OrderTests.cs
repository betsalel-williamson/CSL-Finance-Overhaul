using System.Collections.Generic;
using FinanceOverhaul.Market;
using Xunit;

namespace FinanceOverhaul.Market.Tests
{
    public class OrderRulesTests
    {
        private static TradeOrder Limit(uint shares = 100, int limit = 800, int placed = 10) =>
            TradeOrder.LimitBuy(1, "Oil Industry", shares, limit, placed);

        [Fact]
        public void WaitsWhileTheMarkIsAboveTheLimit()
        {
            OrderDecision d = OrderRules.Evaluate(Limit(), 11, 812.5f, 1000, 10_000_000);
            Assert.Equal(OrderAction.Wait, d.Action);
        }

        [Fact]
        public void FillsAtTheDailyMarkWhenAtOrBelowTheLimit()
        {
            OrderDecision d = OrderRules.Evaluate(Limit(), 11, 793.4f, 1000, 10_000_000);
            Assert.Equal(OrderAction.Fill, d.Action);
            Assert.Equal(100u, d.Shares);
            Assert.Equal(79_340, d.Cost); // vanilla CalculatePrice: round(mark × shares)

            Assert.Equal(
                OrderAction.Fill,
                OrderRules.Evaluate(Limit(), 11, 800f, 1000, 10_000_000).Action
            );
        }

        [Fact]
        public void FillIsCappedByTheVanillaShareLimit()
        {
            OrderDecision d = OrderRules.Evaluate(Limit(shares: 100), 11, 700f, 40, 10_000_000);
            Assert.Equal(OrderAction.Fill, d.Action);
            Assert.Equal(40u, d.Shares);
            Assert.Equal(28_000, d.Cost);
        }

        [Fact]
        public void CancelsWhenNoSharesAreAvailable()
        {
            OrderDecision d = OrderRules.Evaluate(Limit(), 11, 700f, 0, 10_000_000);
            Assert.Equal(OrderAction.Cancel, d.Action);
            Assert.Equal(OrderRules.ReasonShareLimit, d.Reason);
        }

        [Fact]
        public void CancelsWhenTheCityCannotAffordIt()
        {
            // 100 × ₡700 = ₡70,000 = 7,000,000 cents; one cent short.
            OrderDecision d = OrderRules.Evaluate(Limit(), 11, 700f, 1000, 6_999_999);
            Assert.Equal(OrderAction.Cancel, d.Action);
            Assert.Equal(OrderRules.ReasonFunds, d.Reason);
            Assert.Equal(
                OrderAction.Fill,
                OrderRules.Evaluate(Limit(), 11, 700f, 1000, 7_000_000).Action
            );
        }

        [Fact]
        public void ExpiresAfterSixtyGameDays()
        {
            TradeOrder o = Limit(placed: 10);
            Assert.Equal(70, o.ExpiresDay);
            Assert.Equal(OrderAction.Wait, OrderRules.Evaluate(o, 70, 900f, 1000, 1).Action);
            OrderDecision d = OrderRules.Evaluate(o, 71, 700f, 1000, 10_000_000);
            Assert.Equal(OrderAction.Cancel, d.Action);
            Assert.Equal(OrderRules.ReasonExpired, d.Reason);
        }

        [Fact]
        public void BadMarksNeverFill()
        {
            Assert.Equal(
                OrderAction.Wait,
                OrderRules.Evaluate(Limit(), 11, float.NaN, 1000, 10_000_000).Action
            );
            Assert.Equal(
                OrderAction.Wait,
                OrderRules.Evaluate(Limit(), 11, 0f, 1000, 10_000_000).Action
            );
        }
    }

    public class OrderBookTests
    {
        [Fact]
        public void AddAssignsIncreasingIdsAndCancelRemoves()
        {
            var book = new OrderBook();
            TradeOrder a = book.AddLimitBuy("Oil Industry", 100, 800, 10);
            TradeOrder b = book.AddLimitBuy("Ore Industry", 50, 600, 10);
            Assert.Equal(1, a.Id);
            Assert.Equal(2, b.Id);
            Assert.True(book.Cancel(1));
            Assert.False(book.Cancel(1));
            Assert.Single(book.Orders);
            Assert.Equal(3, book.AddLimitBuy("X", 1, 1, 10).Id); // ids are never reused
        }

        [Fact]
        public void RejectsMeaninglessOrders()
        {
            var book = new OrderBook();
            Assert.Null(book.AddLimitBuy("Oil Industry", 0, 800, 10));
            Assert.Null(book.AddLimitBuy("Oil Industry", 10, 0, 10));
            Assert.Null(book.AddLimitBuy("", 10, 800, 10));
            Assert.Empty(book.Orders);
        }
    }

    public class OrderBookCodecTests
    {
        [Fact]
        public void RoundTrips()
        {
            var book = new OrderBook();
            book.AddLimitBuy("Oil Industry", 100, 800, 10);
            book.AddLimitBuy("Index", 25, 2400, 12);
            book.Cancel(1);
            OrderBook back = OrderBookCodec.Parse(OrderBookCodec.Format(book));
            Assert.Single(back.Orders);
            TradeOrder o = back.Orders[0];
            Assert.Equal(2, o.Id);
            Assert.Equal("Index", o.InvestmentKey);
            Assert.Equal(25u, o.Shares);
            Assert.Equal(2400, o.LimitPrice);
            Assert.Equal(12, o.PlacedDay);
            Assert.Equal(72, o.ExpiresDay);
            Assert.Equal(3, back.AddLimitBuy("Y", 1, 1, 1).Id); // next id survives
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("garbage")]
        [InlineData("v99\nnext=5\n")]
        public void UnreadableDataGivesAnEmptyBook(string text)
        {
            Assert.Empty(OrderBookCodec.Parse(text).Orders);
        }

        [Fact]
        public void SkipsBadLinesButKeepsGoodOnes()
        {
            string text =
                "v1\nnext=9\nlimit-buy|3|Oil Industry|100|800|10|70\nlimit-buy|x|Bad|1\nfuture-kind|4|A|1|1|1|1\n";
            OrderBook book = OrderBookCodec.Parse(text);
            Assert.Single(book.Orders);
            Assert.Equal(3, book.Orders[0].Id);
            Assert.Equal(9, book.AddLimitBuy("Z", 1, 1, 1).Id);
        }

        [Fact]
        public void NamesWithSeparatorsAreEscaped()
        {
            var book = new OrderBook();
            book.AddLimitBuy("Weird|Name\nCo", 1, 5, 1);
            Assert.Equal(
                "Weird|Name\nCo",
                OrderBookCodec.Parse(OrderBookCodec.Format(book)).Orders[0].InvestmentKey
            );
        }
    }
}

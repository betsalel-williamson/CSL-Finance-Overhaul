// FinanceOverhaul.Core: ModSettings, OrderAction, OrderBook, OrderBookCodec, OrderDecision,
//   OrderKind, OrderRules, OrderText, SettingsFile, TradeOrder
using FinanceOverhaul.Core;
using Xunit; // Assert, FactAttribute, InlineDataAttribute, TheoryAttribute

namespace FinanceOverhaul.Core.Tests
{
    public class OrderRulesTests
    {
        private static TradeOrder Limit(uint shares = 100, int limit = 800, int placed = 10) =>
            TradeOrder.LimitBuy(1, "Oil Industry", shares, limit, placed);

        [Fact]
        public void WaitsWhileTheMarkIsAboveTheLimit()
        {
            OrderDecision d = OrderRules.Evaluate(Limit(), 11, 812.5f, 1000, 100_000);
            Assert.Equal(OrderAction.Wait, d.Action);
        }

        [Fact]
        public void FillsAtTheDailyMarkWhenAtOrBelowTheLimit()
        {
            OrderDecision d = OrderRules.Evaluate(Limit(), 11, 793.4f, 1000, 100_000);
            Assert.Equal(OrderAction.Fill, d.Action);
            Assert.Equal(100u, d.Shares);
            Assert.Equal(79_340, d.Cost); // vanilla CalculatePrice: round(mark × shares)

            Assert.Equal(
                OrderAction.Fill,
                OrderRules.Evaluate(Limit(), 11, 800f, 1000, 100_000).Action
            );
        }

        [Fact]
        public void FillIsCappedByTheVanillaShareLimit()
        {
            OrderDecision d = OrderRules.Evaluate(Limit(shares: 100), 11, 700f, 40, 100_000);
            Assert.Equal(OrderAction.Fill, d.Action);
            Assert.Equal(40u, d.Shares);
            Assert.Equal(28_000, d.Cost);
        }

        [Fact]
        public void CancelsWhenNoSharesAreAvailable()
        {
            OrderDecision d = OrderRules.Evaluate(Limit(), 11, 700f, 0, 100_000);
            Assert.Equal(OrderAction.Cancel, d.Action);
            Assert.Equal(OrderRules.ReasonShareLimit, d.Reason);
        }

        [Fact]
        public void CancelsWhenTheCityCannotAffordIt()
        {
            // 100 × ₡700 = ₡70,000; cash is whole ₡ (vanilla playerMoney), ₡1 short.
            OrderDecision d = OrderRules.Evaluate(Limit(), 11, 700f, 1000, 69_999);
            Assert.Equal(OrderAction.Cancel, d.Action);
            Assert.Equal(OrderRules.ReasonFunds, d.Reason);
            Assert.Equal(
                OrderAction.Fill,
                OrderRules.Evaluate(Limit(), 11, 700f, 1000, 70_000).Action
            );
        }

        [Fact]
        public void ExpiresAfterSixtyGameDays()
        {
            TradeOrder o = Limit(placed: 10);
            Assert.Equal(100, o.ExpiresDay);
            Assert.Equal(OrderAction.Wait, OrderRules.Evaluate(o, 70, 900f, 1000, 1).Action);
            OrderDecision d = OrderRules.Evaluate(o, 101, 700f, 1000, 100_000);
            Assert.Equal(OrderAction.Cancel, d.Action);
            Assert.Equal(OrderRules.ReasonExpired, d.Reason);
        }

        [Fact]
        public void BadMarksNeverFill()
        {
            Assert.Equal(
                OrderAction.Wait,
                OrderRules.Evaluate(Limit(), 11, float.NaN, 1000, 100_000).Action
            );
            Assert.Equal(
                OrderAction.Wait,
                OrderRules.Evaluate(Limit(), 11, 0f, 1000, 100_000).Action
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
            Assert.Equal(2400, o.TriggerPrice);
            Assert.Equal(12, o.PlacedDay);
            Assert.Equal(102, o.ExpiresDay);
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

namespace FinanceOverhaul.Core.Tests
{
    public class StopLossRulesTests
    {
        private static TradeOrder Stop(uint shares = 50, int stop = 90, int placed = 10) =>
            TradeOrder.StopLossSell(7, "Oil Industry", shares, stop, placed);

        [Fact]
        public void WaitsWhileThePriceStaysAboveTheStop()
        {
            OrderDecision d = OrderRules.Evaluate(Stop(), 11, 95f, 0, 0, owned: 100);
            Assert.Equal(OrderAction.Wait, d.Action);
        }

        [Fact]
        public void SellsAtTheDailyPriceOnceAtOrBelowTheStop()
        {
            OrderDecision d = OrderRules.Evaluate(Stop(), 11, 90f, 0, 0, owned: 100);
            Assert.Equal(OrderAction.Fill, d.Action);
            Assert.Equal(50u, d.Shares);
            Assert.Equal(4500, d.Cost); // proceeds: round(mark × shares)
        }

        [Fact]
        public void AfterAGapDownItSellsLowerThanTheStop()
        {
            OrderDecision d = OrderRules.Evaluate(Stop(), 11, 70f, 0, 0, owned: 100);
            Assert.Equal(OrderAction.Fill, d.Action);
            Assert.Equal(3500, d.Cost);
        }

        [Fact]
        public void NeverSellsMoreThanIsOwned()
        {
            OrderDecision d = OrderRules.Evaluate(Stop(shares: 50), 11, 80f, 0, 0, owned: 20);
            Assert.Equal(20u, d.Shares);
        }

        [Fact]
        public void CancelsWhenNothingIsLeftToSell()
        {
            OrderDecision d = OrderRules.Evaluate(Stop(), 11, 80f, 0, 0, owned: 0);
            Assert.Equal(OrderAction.Cancel, d.Action);
            Assert.Equal(OrderRules.ReasonNoShares, d.Reason);
        }

        [Fact]
        public void ExpiresLikeEveryOrder()
        {
            OrderDecision d = OrderRules.Evaluate(Stop(placed: 10), 101, 80f, 0, 0, owned: 100);
            Assert.Equal(OrderAction.Cancel, d.Action);
            Assert.Equal(OrderRules.ReasonExpired, d.Reason);
        }

        [Fact]
        public void IsASell()
        {
            Assert.True(Stop().IsSell);
            Assert.False(TradeOrder.LimitBuy(1, "Oil Industry", 1, 1, 1).IsSell);
        }
    }

    public class StopLossBookTests
    {
        [Fact]
        public void BookAddsAndRoundTripsStopLosses()
        {
            var book = new OrderBook();
            book.AddLimitBuy("Oil Industry", 10, 80, 5);
            TradeOrder stop = book.AddStopLoss("Index", 25, 21000, 6);
            Assert.Equal(OrderKind.StopLossSell, stop.Kind);
            OrderBook back = OrderBookCodec.Parse(OrderBookCodec.Format(book));
            Assert.Equal(2, back.Orders.Count);
            Assert.Equal(OrderKind.LimitBuy, back.Orders[0].Kind);
            TradeOrder s = back.Orders[1];
            Assert.Equal(OrderKind.StopLossSell, s.Kind);
            Assert.Equal("Index", s.InvestmentKey);
            Assert.Equal(25u, s.Shares);
            Assert.Equal(21000, s.TriggerPrice);
            Assert.Equal(96, s.ExpiresDay);
        }

        [Fact]
        public void RejectsMeaninglessStops()
        {
            var book = new OrderBook();
            Assert.Null(book.AddStopLoss("Oil Industry", 0, 90, 1));
            Assert.Null(book.AddStopLoss("Oil Industry", 5, 0, 1));
            Assert.Empty(book.Orders);
        }
    }
}

namespace FinanceOverhaul.Core.Tests
{
    public class TakeProfitRulesTests
    {
        private static TradeOrder Target(uint shares = 50, int target = 110, int placed = 10) =>
            TradeOrder.TakeProfitSell(8, "Oil Industry", shares, target, placed);

        [Fact]
        public void WaitsWhileThePriceStaysBelowTheTarget()
        {
            OrderDecision d = OrderRules.Evaluate(Target(), 11, 109f, 0, 0, owned: 100);
            Assert.Equal(OrderAction.Wait, d.Action);
        }

        [Fact]
        public void SellsAtTheDailyPriceOnceAtOrAboveTheTarget()
        {
            OrderDecision d = OrderRules.Evaluate(Target(), 11, 110f, 0, 0, owned: 100);
            Assert.Equal(OrderAction.Fill, d.Action);
            Assert.Equal(50u, d.Shares);
            Assert.Equal(5500, d.Cost);
        }

        [Fact]
        public void AfterAGapUpItSellsHigherThanTheTarget()
        {
            OrderDecision d = OrderRules.Evaluate(Target(), 11, 130f, 0, 0, owned: 100);
            Assert.Equal(OrderAction.Fill, d.Action);
            Assert.Equal(6500, d.Cost);
        }

        [Fact]
        public void NeverSellsMoreThanIsOwned()
        {
            OrderDecision d = OrderRules.Evaluate(Target(shares: 50), 11, 120f, 0, 0, owned: 20);
            Assert.Equal(20u, d.Shares);
        }

        [Fact]
        public void CancelsWhenNothingIsLeftToSell()
        {
            OrderDecision d = OrderRules.Evaluate(Target(), 11, 120f, 0, 0, owned: 0);
            Assert.Equal(OrderAction.Cancel, d.Action);
            Assert.Equal(OrderRules.ReasonNoShares, d.Reason);
        }

        [Fact]
        public void WaitsOnABadPrice()
        {
            Assert.Equal(
                OrderAction.Wait,
                OrderRules.Evaluate(Target(), 11, float.NaN, 0, 0, owned: 100).Action
            );
            Assert.Equal(
                OrderAction.Wait,
                OrderRules.Evaluate(Target(), 11, float.PositiveInfinity, 0, 0, owned: 100).Action
            );
        }

        [Fact]
        public void ExpiresLikeEveryOrder()
        {
            OrderDecision d = OrderRules.Evaluate(Target(placed: 10), 101, 120f, 0, 0, owned: 100);
            Assert.Equal(OrderAction.Cancel, d.Action);
            Assert.Equal(OrderRules.ReasonExpired, d.Reason);
        }

        [Fact]
        public void IsASell()
        {
            Assert.True(Target().IsSell);
        }
    }

    public class TakeProfitBookTests
    {
        [Fact]
        public void BookAddsAndRoundTripsTakeProfits()
        {
            var book = new OrderBook();
            book.AddStopLoss("Oil Industry", 10, 80, 5);
            TradeOrder target = book.AddTakeProfit("Index", 25, 23000, 6);
            Assert.Equal(OrderKind.TakeProfitSell, target.Kind);
            OrderBook back = OrderBookCodec.Parse(OrderBookCodec.Format(book));
            Assert.Equal(2, back.Orders.Count);
            Assert.Equal(OrderKind.StopLossSell, back.Orders[0].Kind);
            TradeOrder t = back.Orders[1];
            Assert.Equal(OrderKind.TakeProfitSell, t.Kind);
            Assert.Equal(25u, t.Shares);
            Assert.Equal(23000, t.TriggerPrice);
            Assert.Equal(96, t.ExpiresDay);
        }

        [Fact]
        public void SavesAsItsOwnLineThatOlderBuildsSkip()
        {
            var book = new OrderBook();
            book.AddTakeProfit("Oil Industry", 5, 120, 1);
            Assert.Contains(
                "\nprofit-sell|1|Oil Industry|5|120|1|91\n",
                OrderBookCodec.Format(book)
            );
        }

        [Fact]
        public void RejectsMeaninglessTargets()
        {
            var book = new OrderBook();
            Assert.Null(book.AddTakeProfit("Oil Industry", 0, 90, 1));
            Assert.Null(book.AddTakeProfit("Oil Industry", 5, 0, 1));
            Assert.Null(book.AddTakeProfit("", 5, 90, 1));
            Assert.Empty(book.Orders);
        }
    }
}

namespace FinanceOverhaul.Core.Tests
{
    public class TrailingStopRulesTests
    {
        private static TradeOrder Trail(
            uint shares = 20,
            int percent = 10,
            float peak = 100f,
            int placed = 10
        ) => TradeOrder.TrailingStopSell(9, "Index", shares, percent, peak, placed);

        [Fact]
        public void StopStartsTheTrailBelowTodaysPrice()
        {
            Assert.Equal(90, Trail().TriggerPrice);
            Assert.Equal(85, Trail(percent: 15).TriggerPrice);
            Assert.Equal(90, TradeOrder.TrailingStop(100.9f, 10)); // 90.81, rounded down
            Assert.Equal(0, TradeOrder.TrailingStop(float.NaN, 10));
        }

        [Fact]
        public void WaitsAboveTheStop()
        {
            OrderDecision d = OrderRules.Evaluate(Trail(), 11, 95f, 0, 0, owned: 100);
            Assert.Equal(OrderAction.Wait, d.Action);
            Assert.False(d.PeakRaised);
        }

        [Fact]
        public void ANewHighRaisesTheStop()
        {
            TradeOrder o = Trail();
            OrderDecision d = OrderRules.Evaluate(o, 11, 120f, 0, 0, owned: 100);
            Assert.Equal(OrderAction.Wait, d.Action);
            Assert.True(d.PeakRaised);
            Assert.Equal(120f, o.Peak);
            Assert.Equal(108, o.TriggerPrice);
        }

        [Fact]
        public void TheStopNeverMovesDown()
        {
            TradeOrder o = Trail();
            OrderRules.Evaluate(o, 11, 120f, 0, 0, owned: 100);
            OrderDecision d = OrderRules.Evaluate(o, 12, 110f, 0, 0, owned: 100);
            Assert.Equal(OrderAction.Wait, d.Action);
            Assert.Equal(120f, o.Peak);
            Assert.Equal(108, o.TriggerPrice);
        }

        [Fact]
        public void SellsAtTheDailyPriceOnceAtOrBelowTheRaisedStop()
        {
            TradeOrder o = Trail();
            OrderRules.Evaluate(o, 11, 120f, 0, 0, owned: 100);
            OrderDecision d = OrderRules.Evaluate(o, 12, 108f, 0, 0, owned: 100);
            Assert.Equal(OrderAction.Fill, d.Action);
            Assert.Equal(20u, d.Shares);
            Assert.Equal(2160, d.Cost);
        }

        [Fact]
        public void AfterAGapDownItSellsLowerThanTheStop()
        {
            OrderDecision d = OrderRules.Evaluate(Trail(), 11, 70f, 0, 0, owned: 100);
            Assert.Equal(OrderAction.Fill, d.Action);
            Assert.Equal(1400, d.Cost);
        }

        [Fact]
        public void NeverSellsMoreThanIsOwnedAndCancelsWithNone()
        {
            Assert.Equal(5u, OrderRules.Evaluate(Trail(), 11, 80f, 0, 0, owned: 5).Shares);
            OrderDecision d = OrderRules.Evaluate(Trail(), 11, 80f, 0, 0, owned: 0);
            Assert.Equal(OrderAction.Cancel, d.Action);
            Assert.Equal(OrderRules.ReasonNoShares, d.Reason);
        }

        [Fact]
        public void WaitsOnABadPriceWithoutMovingThePeak()
        {
            TradeOrder o = Trail();
            Assert.Equal(
                OrderAction.Wait,
                OrderRules.Evaluate(o, 11, float.PositiveInfinity, 0, 0, owned: 100).Action
            );
            Assert.Equal(100f, o.Peak);
        }

        [Fact]
        public void ExpiresLikeEveryOrder()
        {
            OrderDecision d = OrderRules.Evaluate(Trail(placed: 10), 101, 80f, 0, 0, owned: 100);
            Assert.Equal(OrderRules.ReasonExpired, d.Reason);
        }

        [Fact]
        public void OnlyTrailingStopsHaveAPeak()
        {
            TradeOrder stop = TradeOrder.StopLossSell(1, "Index", 1, 90, 1);
            Assert.False(stop.RaisePeak(500f));
            Assert.Equal(90, stop.TriggerPrice);
            Assert.True(Trail().IsSell);
        }
    }

    public class TrailingStopBookTests
    {
        [Fact]
        public void BookAddsAndRoundTripsTrailingStopsWithThePeak()
        {
            var book = new OrderBook();
            book.AddLimitBuy("Oil Industry", 10, 80, 5);
            TradeOrder trail = book.AddTrailingStop("Index", 20, 10, 212.5f, 6);
            trail.RaisePeak(230.25f);
            OrderBook back = OrderBookCodec.Parse(OrderBookCodec.Format(book));
            Assert.Equal(2, back.Orders.Count);
            TradeOrder t = back.Orders[1];
            Assert.Equal(OrderKind.TrailingStopSell, t.Kind);
            Assert.Equal(20u, t.Shares);
            Assert.Equal(10, t.TrailPercent);
            Assert.Equal(230.25f, t.Peak);
            Assert.Equal(207, t.TriggerPrice);
            Assert.Equal(96, t.ExpiresDay);
        }

        [Fact]
        public void SavesAnEighthColumnThatOlderBuildsSkip()
        {
            var book = new OrderBook();
            book.AddTrailingStop("Index", 5, 10, 100f, 1);
            string text = OrderBookCodec.Format(book);
            Assert.Contains("\ntrail-sell|1|Index|5|10|1|91|100\n", text);
        }

        [Fact]
        public void SkipsDamagedTrailingLines()
        {
            string text =
                "v1\nnext=9\n"
                + "trail-sell|1|Index|5|10|1|61\n" // no peak
                + "trail-sell|2|Index|5|10|1|61|NaN\n"
                + "trail-sell|3|Index|5|0|1|61|100\n" // trail out of range
                + "trail-sell|4|Index|5|80|1|61|100\n"
                + "trail-sell|5|Index|5|10|1|61|100\n";
            OrderBook book = OrderBookCodec.Parse(text);
            Assert.Single(book.Orders);
            Assert.Equal(5, book.Orders[0].Id);
        }

        [Fact]
        public void RejectsMeaninglessTrails()
        {
            var book = new OrderBook();
            Assert.Null(book.AddTrailingStop("Index", 0, 10, 100f, 1));
            Assert.Null(book.AddTrailingStop("Index", 5, 0, 100f, 1));
            Assert.Null(book.AddTrailingStop("Index", 5, 51, 100f, 1));
            Assert.Null(book.AddTrailingStop("Index", 5, 10, 0f, 1));
            Assert.Null(book.AddTrailingStop("Index", 5, 10, float.NaN, 1));
            Assert.Empty(book.Orders);
        }
    }
}

namespace FinanceOverhaul.Core.Tests
{
    public class OrderLifetimeTests
    {
        [Fact]
        public void NewOrdersLastNinetyDaysByDefault()
        {
            Assert.Equal(90, TradeOrder.DefaultLifetimeDays);
            var book = new OrderBook();
            Assert.Equal(110, book.AddLimitBuy("Oil", 5, 80, 20).ExpiresDay);
        }

        [Fact]
        public void TheBookStampsTheChosenLifetimeOnEveryKind()
        {
            var book = new OrderBook { LifetimeDays = 365 };
            Assert.Equal(375, book.AddLimitBuy("Oil", 5, 80, 10).ExpiresDay);
            Assert.Equal(375, book.AddStopLoss("Oil", 5, 80, 10).ExpiresDay);
            Assert.Equal(375, book.AddTakeProfit("Oil", 5, 120, 10).ExpiresDay);
            Assert.Equal(375, book.AddTrailingStop("Oil", 5, 10, 100f, 10).ExpiresDay);
        }

        [Fact]
        public void ChangingTheLifetimeKeepsOpenOrdersAsPlaced()
        {
            var book = new OrderBook { LifetimeDays = 30 };
            TradeOrder early = book.AddLimitBuy("Oil", 5, 80, 0);
            book.LifetimeDays = 180;
            TradeOrder late = book.AddLimitBuy("Oil", 5, 80, 0);
            Assert.Equal(30, early.ExpiresDay);
            Assert.Equal(180, late.ExpiresDay);
            OrderBook back = OrderBookCodec.Parse(OrderBookCodec.Format(book));
            Assert.Equal(30, back.Orders[0].ExpiresDay); // saved expiry wins over any setting
        }

        [Fact]
        public void UnusableLifetimesFallBackToTheDefault()
        {
            Assert.Equal(90, new OrderBook { LifetimeDays = 0 }.LifetimeDays);
            Assert.Equal(90, new OrderBook { LifetimeDays = -5 }.LifetimeDays);
            Assert.Equal(90, new OrderBook { LifetimeDays = 99999 }.LifetimeDays);
            Assert.Equal(120, new OrderBook { LifetimeDays = 120 }.LifetimeDays);
        }

        [Fact]
        public void SettingDefaultsRoundTripsAndRejectsJunk()
        {
            Assert.Equal(90, SettingsFile.Parse("").OrderLifetimeDays);
            var s = new ModSettings { OrderLifetimeDays = 180 };
            Assert.Equal(180, SettingsFile.Parse(SettingsFile.Format(s)).OrderLifetimeDays);
            Assert.Equal(90, SettingsFile.Parse("ordersLifetimeDays=forever\n").OrderLifetimeDays);
            Assert.Equal(90, SettingsFile.Parse("ordersLifetimeDays=0\n").OrderLifetimeDays);
            Assert.Contains(90, TradeOrder.LifetimeChoices);
        }

        [Fact]
        public void HintsSayTheChosenLifetime()
        {
            Assert.EndsWith("Good for 90 days.", OrderText.Hint(OrderKind.LimitBuy, 80));
            Assert.EndsWith("Good for 365 days.", OrderText.Hint(OrderKind.LimitBuy, 80, 0f, 365));
            Assert.Equal("120 days", OrderText.LifetimeLabel(120));
        }
    }
}

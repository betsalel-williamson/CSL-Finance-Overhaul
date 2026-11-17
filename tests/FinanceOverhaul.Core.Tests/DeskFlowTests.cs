using System.Collections.Generic;
using FinanceOverhaul.Core;
using Xunit;

namespace FinanceOverhaul.Core.Tests
{
    /// <summary>The desks' daily decisions (the mod only reads the game and carries them out).</summary>
    public class OrderDayTests
    {
        private static TradeOrder Limit(uint shares = 100, int limit = 800, int placed = 10) =>
            TradeOrder.LimitBuy(7, "Oil Industry Investment", shares, limit, placed);

        private static ListingState Listed(float mark, uint available = 1000, uint owned = 0) =>
            new ListingState
            {
                Listed = true,
                Mark = mark,
                Available = available,
                Owned = owned,
                Symbol = "Oil",
            };

        [Fact]
        public void TheGameClockTurnsIntoAGameDay()
        {
            Assert.Equal(0, OrderDay.DayOf(0));
            Assert.Equal(1, OrderDay.DayOf(System.TimeSpan.TicksPerDay));
            Assert.Equal(1, OrderDay.DayOf(System.TimeSpan.TicksPerDay * 2 - 1));
        }

        [Fact]
        public void AFrozenOrSwitchedOffOrderIsLeftAlone()
        {
            // It would fill (700 ≤ 800) and it would expire (day 999), but neither happens.
            Assert.Equal(
                OrderStepKind.None,
                OrderDay.Step(Limit(), 11, Listed(700f), 1_000_000, true, true).Kind
            );
            Assert.Equal(
                OrderStepKind.None,
                OrderDay.Step(Limit(), 999, Listed(700f), 1_000_000, false, false).Kind
            );
            Assert.False(OrderDay.Step(Limit(), 11, Listed(700f), 1, false, false).BookChanged);
        }

        [Fact]
        public void AnUnlistedStockWaitsUntilTheOrderExpires()
        {
            var missing = new ListingState { Listed = false, Symbol = "Oil Industry Investment" };
            TradeOrder o = Limit();
            Assert.Equal(
                OrderStepKind.None,
                OrderDay.Step(o, o.ExpiresDay, missing, 1_000_000, true, false).Kind
            );

            OrderStep gone = OrderDay.Step(o, o.ExpiresDay + 1, missing, 1_000_000, true, false);
            Assert.Equal(OrderStepKind.Remove, gone.Kind);
            Assert.Equal(NoticeKind.Expired, gone.Notice.Kind);
            Assert.Contains("expired", gone.LogLine);
        }

        [Fact]
        public void ABuyAtOrBelowTheLimitFillsAndReportsWhatTheGamePaid()
        {
            TradeOrder o = Limit();
            ListingState oil = Listed(793.4f);
            OrderStep step = OrderDay.Step(o, 11, oil, 100_000, true, false);
            Assert.Equal(OrderStepKind.Buy, step.Kind);
            Assert.Equal(100u, step.Shares);
            Assert.Null(step.Notice); // nothing is said until the game has traded

            OrderStep done = OrderDay.Filled(o, step.Shares, oil, 79_340);
            Assert.Equal(
                "Limit buy filled: bought 100 Oil at ₡793 (limit ₡800) for ₡79,340. #orders",
                done.Notice.Text
            );
            Assert.Equal(
                "orders: filled #7 bought 100 Oil Industry Investment at 793.4 (limit 800) for 79340",
                done.LogLine
            );
            Assert.True(done.BookChanged);
        }

        [Fact]
        public void AStopLossSellsWhatTheCityHolds()
        {
            TradeOrder o = TradeOrder.StopLossSell(3, "Oil Industry Investment", 50, 90, 10);
            ListingState oil = Listed(88f, owned: 50);
            OrderStep step = OrderDay.Step(o, 11, oil, 0, true, false);
            Assert.Equal(OrderStepKind.Sell, step.Kind);
            Assert.Equal(50u, step.Shares);
            Assert.Contains("sold 50", OrderDay.Filled(o, 50, oil, 4_400).LogLine);
            Assert.Contains("(stop 90)", OrderDay.Filled(o, 50, oil, 4_400).LogLine);
        }

        [Fact]
        public void ABuyTheCityCannotAffordIsCancelledWithTheCostInTheNotice()
        {
            OrderStep step = OrderDay.Step(Limit(), 11, Listed(700f), 69_999, true, false);
            Assert.Equal(OrderStepKind.Remove, step.Kind);
            Assert.Equal(NoticeKind.Cancelled, step.Notice.Kind);
            Assert.Contains("needs ₡70,000", step.Notice.Text);
            Assert.EndsWith(": " + OrderRules.ReasonFunds, step.LogLine);
        }

        [Fact]
        public void ATrailingStopThatFollowsANewHighOnlyAsksForASave()
        {
            var book = new OrderBook();
            TradeOrder o = book.Place(
                OrderKind.TrailingStopSell,
                "Oil Industry Investment",
                10,
                10,
                100f,
                5
            );
            Assert.Equal(90, o.TriggerPrice);
            OrderStep step = OrderDay.Step(o, 6, Listed(120f, owned: 10), 0, true, false);
            Assert.Equal(OrderStepKind.PeakRaised, step.Kind);
            Assert.True(step.BookChanged);
            Assert.Null(step.Notice);
            Assert.Equal(108, o.TriggerPrice);
            Assert.Contains("new high 120 (stop 108)", step.LogLine);
        }

        [Fact]
        public void PlaceMakesTheOrderForItsKindAndTheLogSaysHow()
        {
            var book = new OrderBook();
            TradeOrder buy = book.Place(OrderKind.LimitBuy, "Oil", 5, 80, 100f, 1);
            TradeOrder target = book.Place(OrderKind.TakeProfitSell, "Oil", 5, 120, 100f, 1);
            TradeOrder trail = book.Place(OrderKind.TrailingStopSell, "Oil", 5, 10, 100f, 1);
            Assert.Equal(OrderKind.LimitBuy, buy.Kind);
            Assert.Equal(OrderKind.TakeProfitSell, target.Kind);
            Assert.Equal(3, book.Orders.Count);
            Assert.Null(book.Place(OrderKind.StopLossSell, "Oil", 0, 90, 100f, 1));

            Assert.StartsWith(
                "orders: placed #1 limit buy 5 Oil at or below 80",
                OrderLog.Placed(buy, 100f)
            );
            Assert.Contains(
                "take-profit sell 5 Oil at or above 120",
                OrderLog.Placed(target, 100f)
            );
            Assert.Contains("trailing 10% from 100 (stop 90)", OrderLog.Placed(trail, 100f));
        }

        [Fact]
        public void IndexOrdersAreCounted()
        {
            var book = new OrderBook();
            book.Place(OrderKind.LimitBuy, CityIndexBasket.IndexName, 5, 80, 100f, 1);
            book.Place(OrderKind.LimitBuy, "Oil", 5, 80, 100f, 1);
            Assert.Equal(1, OrderDay.CountForIndex(book.Orders));
            Assert.Equal(0, OrderDay.CountForIndex(null));
        }
    }

    public class BondDayTests
    {
        [Fact]
        public void DebtIsLoansLeftPlusBondsAndIncomeIsFiftyTwoWeeks()
        {
            long debt,
                income;
            BondDay.Finances(new long[] { 100, 0, -5, 250 }, 1_000, 10, out debt, out income);
            Assert.Equal(1_350, debt);
            Assert.Equal(520, income);

            BondDay.Finances(null, 0, -40, out debt, out income);
            Assert.Equal(0, debt);
            Assert.Equal(0, income); // a city losing money has nothing to borrow against
        }

        [Fact]
        public void ALargeMovementIsCutIntoPiecesTheBudgetAccepts()
        {
            Assert.Empty(BondDay.Pieces(0));
            Assert.Equal(new[] { 1234 }, BondDay.Pieces(1234));
            long big = (long)int.MaxValue * 2 + 7;
            int[] pieces = BondDay.Pieces(big);
            Assert.Equal(new[] { int.MaxValue, int.MaxValue, 7 }, pieces);
        }

        [Fact]
        public void OnlyProceedsComeIn()
        {
            Assert.True(BondDay.IsCashIn(new BondCash { Kind = BondCashKind.Proceeds }));
            Assert.False(BondDay.IsCashIn(new BondCash { Kind = BondCashKind.Coupon }));
            Assert.False(BondDay.IsCashIn(new BondCash { Kind = BondCashKind.Principal }));
            Assert.False(BondDay.IsCashIn(new BondCash { Kind = BondCashKind.Redemption }));
        }

        [Fact]
        public void CouponsAreChirpedOnlyWhenAskedAndMaturityAlways()
        {
            var book = new BondBook();
            IssuedBond bond;
            book.Issue(new BondTerms(100_000_000, 300, 12, 30), 1000, out bond);

            List<BondCash> first = book.Due(1030);
            Assert.Single(first);
            Assert.Empty(BondDay.Notices(book, first, false));
            List<Notice> chirped = BondDay.Notices(book, first, true);
            Assert.Single(chirped);
            Assert.Equal(NoticeKind.BondCoupon, chirped[0].Kind);
            Assert.Contains("(11 of 12 left)", chirped[0].Text);

            List<BondCash> rest = book.Due(1000 + 12 * 30);
            List<Notice> end = BondDay.Notices(book, rest, false);
            Assert.Single(end);
            Assert.Equal(NoticeKind.BondMatured, end[0].Kind);
            Assert.Contains("plus the last", end[0].Text);
            // With coupons on, the matured bond still gets one notice, not a coupon chirp too
            // for its last payment.
            Assert.DoesNotContain(
                BondDay.Notices(book, rest, true),
                n => n.Kind == NoticeKind.BondCoupon
            );
            Assert.Empty(BondDay.Notices(book, null, true));
        }

        [Fact]
        public void BackdatingMovesTheWholeScheduleEarlier()
        {
            var book = new BondBook();
            IssuedBond bond;
            book.Issue(new BondTerms(100_000_000, 300, 12, 30), 1000, out bond);
            Assert.Empty(book.Due(1010));
            Assert.True(book.Backdate(bond.Id, 25));
            Assert.Single(book.Due(1010));
            Assert.False(book.Backdate(99, 25));
        }

        [Fact]
        public void TheLogSaysWhenTheGameMovedLess()
        {
            var c = new BondCash
            {
                BondId = 2,
                Kind = BondCashKind.Coupon,
                Cents = 190_833,
                Day = 1030,
            };
            bool shortfall;
            Assert.Equal(
                "bonds: #2 coupon ₡1,908.33 (190833 cents, due day 1030)",
                BondLog.Moved(c, 190_833, out shortfall)
            );
            Assert.False(shortfall);
            Assert.EndsWith("the game moved only 100 cents", BondLog.Moved(c, 100, out shortfall));
            Assert.True(shortfall);
        }
    }

    public class IndexDayTests
    {
        private static readonly string[] Names = { "Oil", "Ore", CityIndexBasket.IndexName };
        private static readonly bool[] All = { true, true, true };

        [Fact]
        public void TheLevelChainsSeedsHoldsOrIsLeftToTheGame()
        {
            var last = new[] { 110f, 220f, 500f };
            var prev = new[] { 100f, 200f, 480f };
            var none = new[] { float.NaN, float.NaN, float.NaN };

            IndexMark chain = IndexDay.Mark(1000f, Names, last, prev, All);
            Assert.Equal(IndexMarkMode.Chain, chain.Mode);
            Assert.Equal(1100f, chain.Value, 1);

            IndexMark seed = IndexDay.Mark(0f, Names, last, prev, All);
            Assert.Equal(IndexMarkMode.Seed, seed.Mode);
            Assert.Equal(165f, seed.Value, 1);

            IndexMark hold = IndexDay.Mark(1000f, Names, none, none, All);
            Assert.Equal(IndexMarkMode.Hold, hold.Mode);
            Assert.Equal(1000f, hold.Value);

            Assert.Equal(IndexMarkMode.None, IndexDay.Mark(0f, Names, none, none, All).Mode);
        }

        [Fact]
        public void MembersExcludeTheIndexAndTheDevListings()
        {
            Assert.True(IndexDay.IsMemberName("Oil Industry Investment"));
            Assert.False(IndexDay.IsMemberName(CityIndexBasket.IndexName));
            Assert.False(IndexDay.IsMemberName(TestStock.PrefabName));
            Assert.False(IndexDay.IsMemberName(StressStock.PrefabName(3)));
            Assert.Equal(2, IndexDay.CountMembers(Names, All));
            Assert.Equal(1, IndexDay.CountMembers(Names, new[] { true, false, true }));
            Assert.Equal(0, IndexDay.CountMembers(null, All));
        }

        [Fact]
        public void TheCloneSourcePrefersAStockWithNoMilestone()
        {
            var locked = new IndexSource
            {
                Name = "Ore",
                SharePriced = true,
                HasMilestone = true,
            };
            var open = new IndexSource { Name = "Oil", SharePriced = true };
            var coin = new IndexSource { Name = "Chirp Coin", SharePriced = false };
            var index = new IndexSource { Name = CityIndexBasket.IndexName, SharePriced = true };
            var test = new IndexSource { Name = TestStock.PrefabName, SharePriced = true };

            Assert.Equal(3, IndexDay.PickSource(new[] { coin, index, locked, open }));
            Assert.Equal(1, IndexDay.PickSource(new[] { coin, locked, test }));
            Assert.Equal(
                -1,
                IndexDay.PickSource(new[] { coin, index, test, default(IndexSource) })
            );
            Assert.Equal(-1, IndexDay.PickSource(null));
        }

        [Fact]
        public void RebuiltDaysGoInFrontOfTheRealOnesOldestFirst()
        {
            // Newest first: real points 5, 4; rebuilt older days 3, 2, 1.
            Assert.Equal(
                new[] { 1f, 2f, 3f, 4f, 5f },
                IndexDay.WithOlderDays(new[] { 5f, 4f }, new[] { 3f, 2f, 1f })
            );
            Assert.Equal(new[] { 4f, 5f }, IndexDay.WithOlderDays(new[] { 5f, 4f }, null));
        }

        [Fact]
        public void TheDailyUpdateIsLoggedTwentyTimesThenEveryThirtieth()
        {
            Assert.True(IndexDay.LogsUpdate(1));
            Assert.True(IndexDay.LogsUpdate(20));
            Assert.False(IndexDay.LogsUpdate(21));
            Assert.True(IndexDay.LogsUpdate(30));
            Assert.False(IndexDay.LogsUpdate(31));
            Assert.Equal(
                "index: update #3 mode=chain 1000 -> 1012.5 (+1.25%) members=9",
                IndexDay.UpdateLine(3, IndexMarkMode.Chain, 1000f, 1012.5f, 9)
            );
        }
    }
}

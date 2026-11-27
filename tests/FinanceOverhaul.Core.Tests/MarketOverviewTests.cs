using System.Collections.Generic; // List
// FinanceOverhaul.Core: CityIndexBasket, MarketOverview, MonitorQuote, OverviewPage, OverviewPages
using FinanceOverhaul.Core;
using Xunit; // Assert, FactAttribute

namespace FinanceOverhaul.Core.Tests
{
    public class MarketOverviewTests
    {
        private static MonitorQuote Q(
            string symbol,
            float rate,
            float[] history,
            bool locked = false
        )
        {
            return new MonitorQuote
            {
                Symbol = symbol,
                ChangeRate = rate,
                History = history,
                Locked = locked,
                Price = history.Length > 0 ? history[history.Length - 1] : 0f,
            };
        }

        [Fact]
        public void WithTheIndexFundListedTheIndexIsTheFundsOwnChart()
        {
            var fund = Q("IDX", 0.4f, new[] { 3000f, 3050f, 3056f });
            fund.Prefab = CityIndexBasket.IndexName;
            var quotes = new List<MonitorQuote> { Q("A", 1f, new[] { 10f, 15f, 20f }), fund };
            Assert.Same(fund, MarketOverview.IndexFund(quotes));
            Assert.Equal(new[] { 3050f, 3056f }, MarketOverview.Index(quotes, 2));
            Assert.Equal("Market index (IDX)", MarketOverview.IndexLabel(fund));
            Assert.Equal("Market index", MarketOverview.IndexLabel(null));
            Assert.Equal("104.2", MarketOverview.IndexText(104.24f, false));
        }

        [Fact]
        public void WithoutTheFundTheIndexFollowsTheFundsRuleTheGeometricMeanOfDailyMoves()
        {
            var quotes = new List<MonitorQuote>
            {
                // A doubles, B halves each day: their geometric mean is 1, the index stays at 100
                // (a plain average of rebased prices would climb to 212.5).
                Q("A", 1f, new[] { 10f, 20f, 40f }),
                Q("B", -1f, new[] { 1000f, 500f, 250f }),
                Q("L", 5f, new[] { 1f, 50f, 99f }, locked: true),
            };
            float[] index = MarketOverview.Index(quotes, 30);
            Assert.Equal(3, index.Length);
            foreach (float v in index)
            {
                Assert.Equal(100f, v, 3);
            }

            // One member up 21% over two days (10% a day): the level moves by the same rule.
            float[] one = MarketOverview.Index(
                new List<MonitorQuote> { Q("A", 1f, new[] { 100f, 110f, 121f }) },
                30
            );
            Assert.Equal(121f, one[2], 2);
            Assert.Equal(21f, MarketOverview.RangeChange(one), 2);
        }

        [Fact]
        public void AStockListedLaterJoinsTheIndexWithoutAJump()
        {
            var quotes = new List<MonitorQuote>
            {
                Q("A", 0f, new[] { 10f, 10f, 10f, 10f }),
                // Listed two days ago: its first price moves nothing, its next day counts.
                Q("NEW", 0f, new[] { float.NaN, float.NaN, 50f, 100f }),
            };
            float[] index = MarketOverview.Index(quotes, 30);
            Assert.Equal(100f, index[2], 3);
            // Mean of ln(1) and ln(2): ×√2.
            Assert.Equal(141.42f, index[3], 1);
        }

        [Fact]
        public void TheRangeKeepsOnlyTheLastDays()
        {
            var quotes = new List<MonitorQuote> { Q("A", 0f, new[] { 1f, 2f, 4f, 8f }) };
            Assert.Equal(new[] { 100f, 200f }, MarketOverview.Index(quotes, 2));
            Assert.Empty(MarketOverview.Index(new List<MonitorQuote>(), 30));
        }

        [Fact]
        public void MoversAreTheLargestEachWayAndBreadthCountsUpDownAndFlat()
        {
            var quotes = new List<MonitorQuote>
            {
                Q("A", 4.88f, new[] { 1f, 2f }),
                Q("B", 3.4f, new[] { 1f, 2f }),
                Q("C", 0.5f, new[] { 1f, 2f }),
                Q("D", 2f, new[] { 1f, 2f }),
                Q("E", -3.09f, new[] { 1f, 2f }),
                Q("F", -0.2f, new[] { 1f, 2f }),
                Q("G", 0f, new[] { 1f, 2f }),
                Q("L", 9f, new[] { 1f, 2f }, locked: true),
            };
            Assert.Equal(
                new[] { "A", "B", "D" },
                MarketOverview.Movers(quotes, true, 3).ConvertAll(q => q.Symbol).ToArray()
            );
            Assert.Equal(
                new[] { "E", "F" },
                MarketOverview.Movers(quotes, false, 3).ConvertAll(q => q.Symbol).ToArray()
            );
            Assert.Equal("4 up · 2 down · 1 flat", MarketOverview.Breadth(quotes));
        }

        [Fact]
        public void OnlyBuiltViewsAreShownAndEachHasATab()
        {
            Assert.Equal(5, OverviewPages.All.Length);
            Assert.Equal(OverviewPage.Market, OverviewPages.Shown(OverviewPage.Market));
            Assert.Equal(OverviewPage.Market, OverviewPages.Shown(OverviewPage.Feed));
            Assert.Equal("Market", OverviewPages.Tab(OverviewPage.Market));
            Assert.Equal("Feed", OverviewPages.Tab(OverviewPage.Feed));
            Assert.Equal("+4.2% over 30 days", MarketOverview.RangeChangeText(4.2f, 30));
        }
    }
}

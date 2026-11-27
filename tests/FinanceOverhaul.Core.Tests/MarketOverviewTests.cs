using System.Collections.Generic; // List
using FinanceOverhaul.Core; // MarketOverview, MonitorQuote, OverviewPage, OverviewPages
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
        public void TheIndexCountsEveryStockOnSaleEquallyFrom100()
        {
            var quotes = new List<MonitorQuote>
            {
                // ₡10 → ₡20 (+100%) and ₡1,000 → ₡1,000 (flat): the index ends at 150, not near 101.
                Q("A", 1f, new[] { 10f, 15f, 20f }),
                Q("B", 0f, new[] { 1000f, 1000f, 1000f }),
                Q("L", 5f, new[] { 1f, 50f, 99f }, locked: true),
            };
            float[] index = MarketOverview.Index(quotes, 30);
            Assert.Equal(new[] { 100f, 125f, 150f }, index);
            Assert.Equal(50f, MarketOverview.RangeChange(index), 3);
        }

        [Fact]
        public void AStockListedLaterJoinsTheIndexOnItsFirstDayAtTheSameDay()
        {
            var quotes = new List<MonitorQuote>
            {
                Q("A", 0f, new[] { 10f, 10f, 10f, 10f }),
                // Listed two days ago (NaN before): its first price counts as 100 that day.
                Q("NEW", 0f, new[] { float.NaN, float.NaN, 50f, 100f }),
            };
            float[] index = MarketOverview.Index(quotes, 30);
            Assert.Equal(new[] { 100f, 100f, 100f, 150f }, index);
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

using System.Collections.Generic; // List
// FinanceOverhaul.Core: MonitorQuote, OverviewPage, OverviewPages, PortfolioOverview,
//   PortfolioSlice, TradeOrder
using FinanceOverhaul.Core;
using Xunit; // Assert, FactAttribute

namespace FinanceOverhaul.Core.Tests
{
    public class PortfolioOverviewTests
    {
        private static MonitorQuote Q(string symbol, uint owned, long spent, float[] history)
        {
            return new MonitorQuote
            {
                Symbol = symbol,
                Owned = owned,
                Spent = spent,
                History = history,
                Price = history[history.Length - 1],
                // The game's rate for the move since yesterday, as the pill reads it.
                ChangeRate =
                    (history[history.Length - 1] / history[history.Length - 2] - 1f) * 100f,
            };
        }

        private static List<MonitorQuote> City()
        {
            return new List<MonitorQuote>
            {
                Q("TVLC", 500, 35000, new[] { 72f, 71f, 69f }), // worth 34,500, −500 since bought
                Q("DS", 500, 451500, new[] { 900f, 890f, 885f }), // worth 442,500, −9,000
                Q("GGI", 200, 82000, new[] { 400f, 410f, 420f }), // worth 84,000, +2,000
                Q("CC", 0, 0, new[] { 250f, 260f, 270f }),
            };
        }

        [Fact]
        public void ValueGainAndTodayAddUpTheHoldings()
        {
            List<MonitorQuote> city = City();
            Assert.Equal(
                new[] { "DS", "GGI", "TVLC" },
                PortfolioOverview.Holdings(city).ConvertAll(q => q.Symbol).ToArray()
            );
            Assert.Equal(561000d, PortfolioOverview.Value(city), 3);
            Assert.Equal(-7500d, PortfolioOverview.Gain(city), 3);
            // Since yesterday: 500 × −2 + 500 × −5 + 200 × +10 = −1,500.
            Assert.Equal(-1500d, PortfolioOverview.DayChange(city), 1);
            Assert.Equal("−₡1,500 (−0.3%)", PortfolioOverview.DayChangeText(city));
            Assert.Equal("−₡7,500 (−1.3%)", PortfolioOverview.GainText(city));
            Assert.Equal("₡0", PortfolioOverview.Change(0, 0).Replace("+", ""));
        }

        [Fact]
        public void TheWorthHistoryIsTodaysHoldingsAtEachDaysPrices()
        {
            float[] worth = PortfolioOverview.WorthHistory(City(), 30);
            Assert.Equal(new[] { 566000f, 562500f, 561000f }, worth);
            Assert.Empty(PortfolioOverview.WorthHistory(new List<MonitorQuote>(), 30));
        }

        [Fact]
        public void TheLongestOrdersSpellOutEveryPartOfTheirTerms()
        {
            List<TradeOrder> longest = PortfolioOverview.LongestOrders("prefab-X", 1);
            Assert.Equal(4, longest.Count);
            var sides = new List<string>();
            foreach (TradeOrder o in longest)
            {
                string detail = PortfolioOverview.OrderDetail(o, 1);
                sides.Add(PortfolioOverview.OrderSide(o));
                Assert.Contains("9,999,999 shares", detail);
                Assert.Contains("365 days left", detail);
            }

            Assert.Equal(
                new[] { "SELL (stop-loss)", "SELL (take-profit)", "SELL (trailing stop)", "BUY" },
                sides.ToArray()
            );
            // The longest of all: "9,999,999 shares if the price drops to ₡9,999,999 · 365 days left".
            Assert.Equal(
                "9,999,999 shares if the price drops to ₡9,999,999 · 365 days left",
                PortfolioOverview.OrderDetail(longest[0], 1)
            );
        }

        [Fact]
        public void TheDonutNamesTheLargestAndFoldsTheRestIntoOther()
        {
            List<PortfolioSlice> two = PortfolioOverview.Allocation(City(), 2);
            Assert.Equal(2, two.Count);
            Assert.Equal("DS", two[0].Label);
            Assert.True(two[1].Other);
            Assert.Equal(118500d, two[1].Value, 3);
            Assert.Equal(1f, two[0].Share + two[1].Share, 4);
            Assert.Equal(3, PortfolioOverview.Allocation(City(), 5).Count);
            Assert.True(OverviewPages.IsBuilt(OverviewPage.Portfolio));
            TradeOrder buy = TradeOrder.LimitBuy(5, "prefab-Oil", 5, 100, 1);
            Assert.Equal("BUY", PortfolioOverview.OrderSide(buy));
            Assert.Contains("₡100", PortfolioOverview.OrderDetail(buy, 1));
        }
    }
}

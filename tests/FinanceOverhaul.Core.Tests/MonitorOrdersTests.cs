using System.Collections.Generic; // HashSet, List
// FinanceOverhaul.Core: ModSettings, MonitorOrderGroup, MonitorOrderLine, MonitorOrders,
//   MonitorQuote, MonitorSort, OrderKind, OrdersSort, SettingsFile, TradeOrder
using FinanceOverhaul.Core;
using Xunit; // Assert, FactAttribute

namespace FinanceOverhaul.Core.Tests
{
    public class MonitorOrdersTests
    {
        private const int Today = 1000;

        private static MonitorQuote Quote(string symbol, float price)
        {
            return new MonitorQuote
            {
                Symbol = symbol,
                Prefab = symbol + " prefab",
                Name = symbol,
                Price = price,
            };
        }

        // The design's example: six orders on four stocks.
        private static List<MonitorQuote> Quotes()
        {
            return new List<MonitorQuote>
            {
                Quote("IDX", 54191f),
                Quote("Oil", 942f),
                Quote("GGI", 924f),
                Quote("TVLC", 66f),
                Quote("CHC", 4563f), // no orders
            };
        }

        private static List<TradeOrder> Orders()
        {
            return new List<TradeOrder>
            {
                TradeOrder.TrailingStopSell(1, "IDX prefab", 25, 10, 54190f, Today, 365),
                TradeOrder.LimitBuy(2, "Oil prefab", 50, 900, Today - 1, 90),
                TradeOrder.TakeProfitSell(3, "Oil prefab", 200, 1050, Today - 18, 90),
                TradeOrder.StopLossSell(4, "GGI prefab", 120, 850, Today - 60, 90),
                TradeOrder.TakeProfitSell(5, "GGI prefab", 120, 1000, Today - 60, 90),
                TradeOrder.LimitBuy(6, "TVLC prefab", 3, 60, Today - 1, 90),
            };
        }

        private static string Symbols(List<MonitorOrderGroup> groups)
        {
            var s = new List<string>();
            foreach (MonitorOrderGroup g in groups)
            {
                s.Add(g.Quote.Symbol);
            }

            return string.Join(" ", s.ToArray());
        }

        private static string Ids(MonitorOrderGroup g)
        {
            var s = new List<string>();
            foreach (MonitorOrderLine l in g.Lines)
            {
                s.Add(l.OrderId.ToString());
            }

            return string.Join(" ", s.ToArray());
        }

        private static bool StopLossPaused(OrderKind kind)
        {
            return kind != OrderKind.StopLossSell;
        }

        [Fact]
        public void DistanceIsAPercentOfTodaysPrice()
        {
            Assert.Equal(9.09, MonitorOrders.DistancePercent(66f, 60), 2);
            Assert.Equal(4.46, MonitorOrders.DistancePercent(942f, 900), 2);
            Assert.True(double.IsNaN(MonitorOrders.DistancePercent(float.NaN, 60)));
            Assert.True(double.IsNaN(MonitorOrders.DistancePercent(0f, 60)));
        }

        [Fact]
        public void DistanceTextShowsCentsUnderOneAndNeverZeroAway()
        {
            Assert.Equal("₡6 away · 9.1%", MonitorOrders.DistanceText(66f, 60));
            Assert.Equal("₡42 away · 4.5%", MonitorOrders.DistanceText(942f, 900));
            Assert.Equal("₡0.40 away · 0.4%", MonitorOrders.DistanceText(100.4f, 100));
            Assert.Equal("at the trigger", MonitorOrders.DistanceText(100f, 100));
            Assert.Null(MonitorOrders.DistanceText(float.NaN, 100));
        }

        [Fact]
        public void TypeNamesTheSideAndKind()
        {
            Assert.Equal("BUY · Limit", MonitorOrders.TypeText(OrderKind.LimitBuy, false));
            Assert.Equal("SELL · Stop-loss", MonitorOrders.TypeText(OrderKind.StopLossSell, false));
            Assert.Equal(
                "SELL · Take-profit · paused in Options",
                MonitorOrders.TypeText(OrderKind.TakeProfitSell, true)
            );
            Assert.Equal(
                "SELL · Trailing stop",
                MonitorOrders.TypeText(OrderKind.TrailingStopSell, false)
            );
        }

        [Fact]
        public void LineUsesThePortfolioCardWording()
        {
            MonitorOrderLine line = MonitorOrders.Line(Orders()[5], 66f, Today, false);
            Assert.Equal("3 shares at or below ₡60 · 89 days left", line.Detail);
            Assert.Equal("BUY · Limit", line.Type);
            Assert.Equal("₡6 away · 9.1%", line.Distance);
            Assert.Equal("BUY ≤ ₡60 · 3 shares · 89 days left", line.Compact);
            Assert.Equal("₡6 away", line.CompactDistance);
            Assert.False(line.IsSell);
        }

        [Fact]
        public void CompactTextCoversEveryKind()
        {
            List<TradeOrder> o = Orders();
            Assert.Equal(
                "SELL, 10% trail · stop ₡48,771 · 25 shares · 365 days left",
                MonitorOrders.CompactText(o[0], Today, false)
            );
            Assert.Equal(
                "SELL ≥ ₡1,050 (target) · 200 shares · 72 days left",
                MonitorOrders.CompactText(o[2], Today, false)
            );
            Assert.Equal(
                "SELL ≤ ₡850 (stop) · 120 shares · 30 days left · paused in Options",
                MonitorOrders.CompactText(o[3], Today, true)
            );
        }

        [Fact]
        public void APausedOrderHasNoDistance()
        {
            MonitorOrderLine line = MonitorOrders.Line(Orders()[3], 924f, Today, true);
            Assert.True(line.Paused);
            Assert.Null(line.Distance);
            Assert.Null(line.CompactDistance);
            Assert.True(double.IsNaN(line.DistancePercent));
            Assert.Equal("SELL · Stop-loss · paused in Options", line.Type);
        }

        [Fact]
        public void NearestRanksGroupsByTheirClosestActiveOrder()
        {
            List<MonitorOrderGroup> groups = MonitorOrders.Groups(
                Quotes(),
                Orders(),
                Today,
                StopLossPaused,
                null,
                OrdersSort.Nearest,
                false
            );
            // Oil 4.5% → GGI 8.2% (its stop-loss is paused) → TVLC 9.1% → IDX 10.0%.
            Assert.Equal("Oil GGI TVLC IDX", Symbols(groups));
            Assert.Equal("2 3", Ids(groups[0])); // limit 4.5% before take-profit 11.5%
            Assert.Equal("5 4", Ids(groups[1])); // the paused stop-loss goes last
            Assert.Equal(6, MonitorOrders.Count(groups));
        }

        [Fact]
        public void ReversedRunsFarthestFirstButPausedStaysLast()
        {
            List<MonitorOrderGroup> groups = MonitorOrders.Groups(
                Quotes(),
                Orders(),
                Today,
                StopLossPaused,
                null,
                OrdersSort.Nearest,
                true
            );
            Assert.Equal("IDX TVLC GGI Oil", Symbols(groups));
            Assert.Equal("3 2", Ids(groups[3]));
            Assert.Equal("5 4", Ids(groups[2]));
        }

        [Fact]
        public void ExpiresRanksGroupsByFewestDaysLeft()
        {
            List<MonitorOrderGroup> groups = MonitorOrders.Groups(
                Quotes(),
                Orders(),
                Today,
                null,
                null,
                OrdersSort.Expires,
                false
            );
            // GGI 30 days → Oil 72 → TVLC 89 → IDX 365.
            Assert.Equal("GGI Oil TVLC IDX", Symbols(groups));
            Assert.Equal("3 2", Ids(groups[1])); // 72 days before 89
            Assert.Equal("4 5", Ids(groups[0])); // both 30 days: placed first
        }

        [Fact]
        public void SymbolOrdersGroupsOnlyAndLinesStayNearestFirst()
        {
            List<MonitorOrderGroup> az = MonitorOrders.Groups(
                Quotes(),
                Orders(),
                Today,
                null,
                null,
                OrdersSort.Symbol,
                false
            );
            Assert.Equal("GGI IDX Oil TVLC", Symbols(az));
            List<MonitorOrderGroup> za = MonitorOrders.Groups(
                Quotes(),
                Orders(),
                Today,
                null,
                null,
                OrdersSort.Symbol,
                true
            );
            Assert.Equal("TVLC Oil IDX GGI", Symbols(za));
            Assert.Equal("2 3", Ids(za[1])); // flipping the groups does not flip the lines
        }

        [Fact]
        public void AGroupWithOnlyPausedOrdersGoesToTheEndEitherWay()
        {
            foreach (bool reversed in new[] { false, true })
            {
                List<MonitorOrderGroup> groups = MonitorOrders.Groups(
                    Quotes(),
                    Orders(),
                    Today,
                    kind => kind != OrderKind.LimitBuy, // limit buys paused: TVLC has only one
                    null,
                    OrdersSort.Nearest,
                    reversed
                );
                Assert.Equal("TVLC", groups[groups.Count - 1].Quote.Symbol);
            }
        }

        [Fact]
        public void HiddenStocksAndStocksWithoutOrdersAreLeftOut()
        {
            var hidden = new HashSet<string> { "Oil prefab" };
            List<MonitorOrderGroup> groups = MonitorOrders.Groups(
                Quotes(),
                Orders(),
                Today,
                null,
                hidden,
                OrdersSort.Symbol,
                false
            );
            Assert.Equal("GGI IDX TVLC", Symbols(groups));
            Assert.Equal(4, MonitorOrders.Count(groups));
        }

        [Fact]
        public void SortCyclesNearestExpiresSymbol()
        {
            Assert.Equal(OrdersSort.Expires, MonitorOrders.NextSort(OrdersSort.Nearest));
            Assert.Equal(OrdersSort.Symbol, MonitorOrders.NextSort(OrdersSort.Expires));
            Assert.Equal(OrdersSort.Nearest, MonitorOrders.NextSort(OrdersSort.Symbol));
            Assert.Equal("Sort: Nearest", MonitorOrders.SortLabel(OrdersSort.Nearest));
            Assert.Equal("Sort: Expires", MonitorOrders.SortLabel(OrdersSort.Expires));
            Assert.Equal(
                "Nearest first (click to flip)",
                MonitorOrders.DirectionText(OrdersSort.Nearest, false)
            );
            Assert.Equal(
                "Latest first (click to flip)",
                MonitorOrders.DirectionText(OrdersSort.Expires, true)
            );
            Assert.Equal(
                "Z → A (click to flip)",
                MonitorOrders.DirectionText(OrdersSort.Symbol, true)
            );
        }

        [Fact]
        public void TabShowsTheNumberOfOpenOrders()
        {
            Assert.Equal("Orders (6)", MonitorOrders.TabLabel(6));
            Assert.Equal("Orders", MonitorOrders.TabLabel(0));
        }

        [Fact]
        public void TheOrdersSortIsRememberedSeparatelyFromTheListSort()
        {
            var m = new ModSettings();
            m.Monitor.Sort = MonitorSort.Price;
            m.Monitor.OrdersSort = OrdersSort.Expires;
            m.Monitor.OrdersSortReversed = true;
            ModSettings back = SettingsFile.Parse(SettingsFile.Format(m));
            Assert.Equal(MonitorSort.Price, back.Monitor.Sort);
            Assert.Equal(OrdersSort.Expires, back.Monitor.OrdersSort);
            Assert.True(back.Monitor.OrdersSortReversed);
            Assert.Equal(OrdersSort.Nearest, new ModSettings().Monitor.OrdersSort);
        }
    }
}

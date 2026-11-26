using System; // Random
using System.Collections.Generic; // List
// FinanceOverhaul.Core: InvestmentFilter, InvestmentList, InvestmentRow, InvestmentRowKind,
//   InvestmentSection, MonitorQuote, MonitorSort, TradeOrder
using FinanceOverhaul.Core;
using Xunit; // Assert, FactAttribute

namespace FinanceOverhaul.Core.Tests
{
    public class InvestmentListTests
    {
        private static MonitorQuote Q(
            string symbol,
            float price,
            float change,
            uint owned = 0,
            long spent = 0,
            bool local = false,
            bool locked = false
        )
        {
            return new MonitorQuote
            {
                Symbol = symbol,
                Name = symbol,
                Prefab = "prefab-" + symbol,
                Price = price,
                ChangeRate = change,
                Owned = owned,
                Spent = spent,
                Local = local,
                Locked = locked,
                UnlockText = locked ? "64 / 200" : null,
            };
        }

        private static List<MonitorQuote> City()
        {
            return new List<MonitorQuote>
            {
                Q("GGI", 7595f, 1.2f, 500, 3308000),
                Q("DS", 885f, 3.73f, 500, 451500),
                Q("WSO", 52f, 0f, local: true),
                Q("FFS", 115f, 2.1f, 100, 11000, local: true),
                Q("CC", 97769f, -1.96f),
                Q("HS", 347f, -0.6f),
                Q("FoI", 0f, 0f, locked: true),
                Q("IDX", 9421f, 0.4f),
            };
        }

        private static string Keys(List<InvestmentRow> rows)
        {
            var keys = new List<string>();
            foreach (InvestmentRow r in rows)
            {
                keys.Add(r.Key);
            }

            return string.Join(",", keys.ToArray());
        }

        [Fact]
        public void AllIsOneListSortedAsAWholeWithLockedStocksLast()
        {
            List<InvestmentRow> rows = InvestmentList.Rows(
                City(),
                InvestmentFilter.All,
                MonitorSort.Change,
                false
            );
            // No sections: held, local and other stocks mix by change; the locked one last.
            Assert.Equal("DS,FFS,GGI,IDX,WSO,HS,CC,FoI", Keys(rows));
        }

        [Fact]
        public void GroupSplitsTheSameStocksIntoSections()
        {
            List<InvestmentRow> rows = InvestmentList.Rows(
                City(),
                InvestmentFilter.All,
                MonitorSort.Change,
                false,
                true,
                null
            );
            // Held first (a held local company is in Your portfolio), then local, then the rest.
            Assert.Equal("#Portfolio,DS,FFS,GGI,#Local,WSO,#All,IDX,HS,CC,FoI", Keys(rows));
            Assert.Equal(3, rows[0].Count);
        }

        [Fact]
        public void EachTabListsItsStocks()
        {
            Assert.Equal(
                "DS,FFS,GGI",
                Keys(InvestmentList.Rows(City(), InvestmentFilter.Owned, MonitorSort.Change, false))
            );
            Assert.Equal(
                "FFS,WSO",
                Keys(InvestmentList.Rows(City(), InvestmentFilter.Local, MonitorSort.Symbol, false))
            );
            Assert.Equal(
                "FoI",
                Keys(
                    InvestmentList.Rows(City(), InvestmentFilter.Locked, MonitorSort.Change, false)
                )
            );
            Assert.Equal(
                "",
                Keys(
                    InvestmentList.Rows(
                        new List<MonitorQuote>(),
                        InvestmentFilter.Owned,
                        MonitorSort.Change,
                        false
                    )
                )
            );
        }

        [Fact]
        public void WithPillsInCurrencyChangeSortsByTheAmount()
        {
            // As the Stock Monitor: CC moves −1.96% of ₡97,769 (about −₡1,955), the biggest ₡ fall;
            // GGI's +1.20% of ₡7,595 is the biggest ₡ rise.
            List<InvestmentRow> rows = InvestmentList.Rows(
                City(),
                InvestmentFilter.All,
                MonitorSort.Change,
                false,
                false,
                null,
                true
            );
            Assert.StartsWith("GGI,", Keys(rows));
            Assert.Contains(",CC,FoI", Keys(rows));
        }

        [Fact]
        public void TheSortAndItsDirectionApplyWithinEachSection()
        {
            Assert.Equal(
                "#Portfolio,GGI,FFS,DS,#Local,WSO,#All,IDX,HS,CC,FoI",
                Keys(
                    InvestmentList.Rows(
                        City(),
                        InvestmentFilter.All,
                        MonitorSort.Symbol,
                        true,
                        true,
                        null
                    )
                )
            );
        }

        [Fact]
        public void OpenOrdersSitUnderTheirStock()
        {
            var orders = new List<TradeOrder>
            {
                TradeOrder.StopLossSell(4, "prefab-GGI", 500, 6800, 1),
                TradeOrder.LimitBuy(5, "prefab-HS", 10, 300, 1),
            };
            List<InvestmentRow> rows = InvestmentList.Rows(
                City(),
                InvestmentFilter.All,
                MonitorSort.Change,
                false,
                false,
                orders
            );
            Assert.Equal("DS,FFS,GGI,order:4,IDX,WSO,HS,order:5,CC,FoI", Keys(rows));
            Assert.Equal(InvestmentRowKind.Order, rows[3].Kind);
            Assert.Equal(InvestmentList.OrderHeight, rows[3].Height);
            Assert.Equal(InvestmentList.HeldHeight, rows[2].Height);
            Assert.Equal(InvestmentList.RowHeight, rows[4].Height);
            Assert.Equal(
                "Open order · SELL (stop-loss) · 500 shares if the price drops to ₡6,800 · 54 days left",
                InvestmentList.OrderLine(orders[0], 37)
            );
        }

        [Fact]
        public void TopsAndTheFirstVisibleRowFollowTheRowHeights()
        {
            List<InvestmentRow> rows = InvestmentList.Rows(
                City(),
                InvestmentFilter.All,
                MonitorSort.Change,
                false,
                true,
                null
            );
            float[] tops = InvestmentList.Tops(rows);
            Assert.Equal(0f, tops[0]);
            Assert.Equal(InvestmentList.HeadingHeight, tops[1]);
            Assert.Equal(
                3 * InvestmentList.HeadingHeight
                    + 3 * InvestmentList.HeldHeight
                    + 5 * InvestmentList.RowHeight,
                tops[11]
            );
            Assert.Equal(0, InvestmentList.FirstVisible(tops, 0f));
            Assert.Equal(0, InvestmentList.FirstVisible(tops, 29f));
            Assert.Equal(1, InvestmentList.FirstVisible(tops, 30f));
            Assert.Equal(10, InvestmentList.FirstVisible(tops, 10000f));
            Assert.Equal(0f, InvestmentList.MaxOffset(tops, 5000f));
            Assert.Equal(tops[11] - 200f, InvestmentList.Clamp(9999f, tops, 200f));
        }

        [Fact]
        public void APurchaseKeepsTheStockAtTheTopInView()
        {
            List<MonitorQuote> city = City();
            List<InvestmentRow> before = InvestmentList.Rows(
                city,
                InvestmentFilter.All,
                MonitorSort.Change,
                false
            );
            float[] tops = InvestmentList.Tops(before);
            int hs = before.FindIndex(r => r.Key == "HS");
            float offset = tops[hs] + 10f;

            // The city buys IDX (read afresh, as the mod does): its line grows, HS moves down.
            city = City();
            city.Find(q => q.Symbol == "IDX").Owned = 10;
            List<InvestmentRow> after = InvestmentList.Rows(
                city,
                InvestmentFilter.All,
                MonitorSort.Change,
                false
            );
            float kept = InvestmentList.KeepInView(before, offset, after, 100f);
            float[] newTops = InvestmentList.Tops(after);
            int hsAfter = after.FindIndex(r => r.Key == "HS");
            Assert.Equal(newTops[hsAfter] + 10f, kept);
        }

        [Fact]
        public void KeepInViewNeverLeavesTheList()
        {
            var rnd = new Random(7);
            for (int run = 0; run < 500; run++)
            {
                var before = new List<MonitorQuote>();
                var after = new List<MonitorQuote>();
                int n = rnd.Next(0, 30);
                for (int i = 0; i < n; i++)
                {
                    string s = "S" + i;
                    before.Add(Q(s, rnd.Next(1, 999), rnd.Next(-50, 50), (uint)rnd.Next(0, 3)));
                    if (rnd.Next(4) > 0)
                    {
                        after.Add(Q(s, rnd.Next(1, 999), rnd.Next(-50, 50), (uint)rnd.Next(0, 3)));
                    }
                }

                List<InvestmentRow> b = InvestmentList.Rows(
                    before,
                    InvestmentFilter.All,
                    MonitorSort.Change,
                    false
                );
                List<InvestmentRow> a = InvestmentList.Rows(
                    after,
                    InvestmentFilter.All,
                    MonitorSort.Change,
                    false
                );
                float view = rnd.Next(50, 600);
                float offset = rnd.Next(0, 2000);
                float kept = InvestmentList.KeepInView(b, offset, a, view);
                Assert.InRange(
                    kept,
                    0f,
                    a.Count == 0 ? 0f : InvestmentList.MaxOffset(InvestmentList.Tops(a), view)
                );
            }
        }

        [Fact]
        public void TheWordingNamesCountsHoldingsGainsAndLocks()
        {
            List<MonitorQuote> city = City();
            Assert.Equal("All 8", InvestmentList.FilterLabel(InvestmentFilter.All, city));
            Assert.Equal("Locked 1", InvestmentList.FilterLabel(InvestmentFilter.Locked, city));
            Assert.Equal("Group", InvestmentList.GroupLabel(false));
            Assert.Equal("Ungroup", InvestmentList.GroupLabel(true));
            Assert.Equal("Owned 3", InvestmentList.FilterLabel(InvestmentFilter.Owned, city));
            Assert.Equal("Local 2", InvestmentList.FilterLabel(InvestmentFilter.Local, city));
            Assert.Equal("Your portfolio", InvestmentList.Heading(InvestmentSection.Portfolio));
            MonitorQuote ggi = city[0];
            Assert.Equal("500 shares · avg ₡6,616", InvestmentList.Holding(ggi));
            Assert.Equal("+₡489,500 (+14.8%)", InvestmentList.Gain(ggi));
            Assert.Equal(1, InvestmentList.GainSign(ggi));
            Assert.Equal("−₡9,000 (−2.0%)", InvestmentList.Gain(city[1]));
            Assert.Null(InvestmentList.Holding(city[4]));
            Assert.Null(InvestmentList.Orders(city[4]));
            city[4].OpenOrders = 2;
            Assert.Equal("2 orders", InvestmentList.Orders(city[4]));
            Assert.Equal("Locked · 64 / 200", InvestmentList.LockLine(city[6]));
            List<InvestmentRow> rows = InvestmentList.Rows(
                city,
                InvestmentFilter.All,
                MonitorSort.Change,
                false,
                true,
                null
            );
            Assert.Equal(
                "₡4,251,500 · +₡481,000",
                InvestmentList.HeadingDetail(InvestmentSection.Portfolio, rows)
            );
            Assert.Equal("1 stock", InvestmentList.HeadingDetail(InvestmentSection.Local, rows));
            Assert.Equal("4 stocks", InvestmentList.HeadingDetail(InvestmentSection.All, rows));
        }
    }
}

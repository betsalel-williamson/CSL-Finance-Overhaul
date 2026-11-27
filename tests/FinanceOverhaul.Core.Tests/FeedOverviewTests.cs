using System.Collections.Generic; // List
// FinanceOverhaul.Core: CompanyStatus, FeedEntry, FeedItem, FeedLog, FeedOverview, FeedTone,
//   LocalCompany, MonitorQuote, Notice, NoticeKind, TradeOrder
using FinanceOverhaul.Core;
using Xunit; // Assert, FactAttribute

namespace FinanceOverhaul.Core.Tests
{
    public class FeedOverviewTests
    {
        private static MonitorQuote Q(string symbol, float price, float rate, float[] history)
        {
            return new MonitorQuote
            {
                Symbol = symbol,
                Prefab = "prefab-" + symbol,
                Price = price,
                ChangeRate = rate,
                History = history,
            };
        }

        private static float[] Days(int count, float start, float step)
        {
            var d = new float[count];
            for (int i = 0; i < count; i++)
            {
                d[i] = start + step * i;
            }

            return d;
        }

        [Fact]
        public void UpcomingListsOfferingsCloseOrdersEndingOrdersAndUnlocks()
        {
            var quotes = new List<MonitorQuote>
            {
                Q("OIL", 1000f, 0f, Days(3, 1000f, 0f)),
                Q("ORE", 500f, 0f, Days(3, 500f, 0f)),
                new MonitorQuote
                {
                    Symbol = "SHP",
                    Prefab = "prefab-SHP",
                    Locked = true,
                    UnlockProgress = 0.8f,
                    UnlockText = "Reach 4,000 citizens",
                },
                new MonitorQuote
                {
                    Symbol = "AIR",
                    Prefab = "prefab-AIR",
                    Locked = true,
                    UnlockProgress = 0.2f,
                },
            };
            var orders = new List<TradeOrder>
            {
                // 2% above its stop: close.
                TradeOrder.StopLossSell(1, "prefab-OIL", 10, 980, 100, 90),
                // 20% under its limit buy: not close, but it ends in 3 days.
                TradeOrder.LimitBuy(2, "prefab-ORE", 10, 400, 100, 33),
            };
            var companies = new List<LocalCompany>
            {
                new LocalCompany
                {
                    Symbol = "ACME",
                    Name = "Acme Oil",
                    Status = CompanyStatus.Offering,
                    FirstTradingDay = 135,
                },
            };

            List<FeedItem> items = FeedOverview.Upcoming(quotes, orders, companies, 130);
            Assert.Equal(4, items.Count);
            Assert.Equal("OIL is 2.0% from your stop-loss at ₡980", items[0].Text);
            Assert.Equal("Now", items[0].When);
            Assert.Equal(FeedTone.Loss, items[0].Tone);
            Assert.Equal("Your ORE limit buy ends", items[1].Text);
            Assert.Equal("in 3 days", items[1].When);
            Assert.Equal("ACME (Acme Oil) starts trading", items[2].Text);
            Assert.Equal("in 5 days", items[2].When);
            Assert.Equal("SHP unlocks soon: Reach 4,000 citizens", items[3].Text);
            Assert.Equal("SHP", items[3].Symbol);
        }

        [Fact]
        public void TheTriggerGapIsHowFarThePriceHasToMove()
        {
            TradeOrder take = TradeOrder.TakeProfitSell(1, "k", 1, 110, 0, 30);
            Assert.Equal(10f, FeedOverview.TriggerGap(take, 100f), 3);
            Assert.Equal(0f, FeedOverview.TriggerGap(take, 120f), 3);
            TradeOrder buy = TradeOrder.LimitBuy(2, "k", 1, 90, 0, 30);
            Assert.Equal(10f, FeedOverview.TriggerGap(buy, 100f), 3);
            Assert.True(float.IsNaN(FeedOverview.TriggerGap(buy, float.NaN)));
        }

        [Fact]
        public void LatelyShowsMovesHighsListingsAndNoticesNewestFirst()
        {
            var quotes = new List<MonitorQuote>
            {
                Q("UP", 130f, 12.5f, Days(12, 100f, 2f)), // jumped and a 30-day high
                Q("DN", 70f, -11f, Days(12, 100f, -2f)), // dropped and a 30-day low
                Q("CALM", 100f, 1f, Days(12, 100f, 0f)),
            };
            var log = new FeedLog();
            log.Add(
                98,
                new Notice
                {
                    Kind = NoticeKind.Filled,
                    Symbol = "CALM",
                    Text = "Bought 10 CALM",
                }
            );
            log.Add(99, new Notice { Kind = NoticeKind.IpoFirstDay, Text = "skipped" });
            log.Add(50, new Notice { Kind = NoticeKind.Expired, Text = "too old" });
            var companies = new List<LocalCompany>
            {
                new LocalCompany
                {
                    Symbol = "ACME",
                    Name = "Acme Oil",
                    Status = CompanyStatus.Trading,
                    FirstTradingDay = 99,
                },
            };

            List<FeedItem> items = FeedOverview.Lately(quotes, log.Snapshot(), companies, 100);
            Assert.Equal(
                new[]
                {
                    "UP jumped +12.50%",
                    "DN dropped -11.00%",
                    "UP hit a 30-day high at ₡130",
                    "DN hit a 30-day low at ₡70",
                    "ACME (Acme Oil) began trading",
                    "Bought 10 CALM",
                },
                items.ConvertAll(i => i.Text).ToArray()
            );
            Assert.Equal("Today", items[0].When);
            Assert.Equal("1 day ago", items[4].When);
            Assert.Equal("2 days ago", items[5].When);
            Assert.Equal(FeedTone.Accent, items[5].Tone);
            Assert.Equal(2, items[5].Days);
        }

        [Fact]
        public void TheLogKeepsTheNewestEntries()
        {
            var log = new FeedLog();
            for (int i = 0; i < FeedLog.Capacity + 5; i++)
            {
                log.Add(i, new Notice { Kind = NoticeKind.Filled, Text = "n" + i });
            }

            log.Add(1, null);
            log.Add(1, new Notice { Text = "" });
            List<FeedEntry> kept = log.Snapshot();
            Assert.Equal(FeedLog.Capacity, kept.Count);
            Assert.Equal("n5", kept[0].Text);
            log.Clear();
            Assert.Empty(log.Snapshot());
        }

        [Fact]
        public void HighsNeedTenDays()
        {
            Assert.Equal(0, FeedOverview.HighOrLow(Q("S", 1f, 0f, Days(9, 1f, 1f))));
            Assert.Equal(1, FeedOverview.HighOrLow(Q("S", 1f, 0f, Days(10, 1f, 1f))));
            Assert.Equal(0, FeedOverview.HighOrLow(Q("S", 1f, 0f, Days(10, 1f, 0f))));
            Assert.Equal("in 1 day", FeedOverview.InDays(1));
            Assert.Equal("Today", FeedOverview.Ago(0));
        }
    }
}

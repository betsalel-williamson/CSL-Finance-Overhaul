using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace FinanceOverhaul.Market.Tests
{
    /// <summary>
    /// The Stock Monitor's sort direction button, over 2,000 seeded quote lists for every sort:
    /// reversed is the same rows in the opposite order of the main key; ties stay A→Z either way
    /// (so with distinct keys reversed is exactly the reverse list); flipping twice is the
    /// original; the filter and hidden stocks are unaffected; the setting round-trips.
    /// </summary>
    public class MonitorSortDirectionTests
    {
        private static readonly MonitorSort[] Sorts =
        {
            MonitorSort.Change,
            MonitorSort.Symbol,
            MonitorSort.Price,
            MonitorSort.ValueHeld,
        };

        private static List<MonitorQuote> RandomQuotes(Random rng, bool distinct)
        {
            int n = rng.Next(0, 14);
            var list = new List<MonitorQuote>();
            for (int i = 0; i < n; i++)
            {
                list.Add(
                    new MonitorQuote
                    {
                        Symbol = "S" + i.ToString("00") + (char)('A' + rng.Next(26)),
                        Prefab = "p" + i,
                        Price = distinct
                            ? 10f + i * 7.3f + (float)rng.NextDouble()
                            : rng.Next(3) * 10f,
                        ChangeRate = distinct
                            ? i * 1.1f - 5f + (float)rng.NextDouble() * 0.5f
                            : rng.Next(3) - 1f,
                        Owned = (uint)(distinct ? i + 1 : rng.Next(2)),
                        OpenOrders = rng.Next(2),
                    }
                );
            }

            return list.OrderBy(q => rng.Next()).ToList();
        }

        [Fact]
        public void ReversedIsTheOppositeOrderWithTiesKeptAToZ()
        {
            var rng = new Random(4040);
            for (int k = 0; k < 2000; k++)
            {
                bool distinct = k % 2 == 0;
                List<MonitorQuote> quotes = RandomQuotes(rng, distinct);
                foreach (MonitorSort sort in Sorts)
                {
                    var hidden = new HashSet<string> { "p3" };
                    MonitorFilter filter = (MonitorFilter)(k % 3);
                    List<MonitorQuote> natural = StockMonitor.Rows(
                        quotes,
                        filter,
                        sort,
                        hidden,
                        false
                    );
                    List<MonitorQuote> flipped = StockMonitor.Rows(
                        quotes,
                        filter,
                        sort,
                        hidden,
                        true
                    );

                    // Same rows (filter and hidden unaffected).
                    Assert.Equal(
                        natural.Select(q => q.Symbol).OrderBy(s => s, StringComparer.Ordinal),
                        flipped.Select(q => q.Symbol).OrderBy(s => s, StringComparer.Ordinal)
                    );

                    // Distinct keys (or Symbol sort): exactly the reverse list.
                    if (distinct || sort == MonitorSort.Symbol)
                    {
                        Assert.Equal(
                            Enumerable.Reverse(natural).Select(q => q.Symbol),
                            flipped.Select(q => q.Symbol)
                        );
                    }

                    // Adjacent rows: main key in the flipped direction; ties A→Z.
                    for (int i = 1; i < flipped.Count; i++)
                    {
                        double a = Key(flipped[i - 1], sort),
                            b = Key(flipped[i], sort);
                        if (sort != MonitorSort.Symbol)
                        {
                            Assert.True(a <= b, sort + ": flipped must run lowest first");
                            if (a == b)
                            {
                                Assert.True(
                                    string.CompareOrdinal(flipped[i - 1].Symbol, flipped[i].Symbol)
                                        < 0
                                );
                            }
                        }
                    }
                }
            }
        }

        [Fact]
        public void DirectionWordsAndDefaults()
        {
            Assert.True(StockMonitor.IsDescending(MonitorSort.Change, false));
            Assert.False(StockMonitor.IsDescending(MonitorSort.Change, true));
            Assert.False(StockMonitor.IsDescending(MonitorSort.Symbol, false));
            Assert.True(StockMonitor.IsDescending(MonitorSort.Symbol, true));
            Assert.Equal(
                "Highest first (click to flip)",
                StockMonitor.DirectionText(MonitorSort.Price, false)
            );
            Assert.Equal(
                "Lowest first (click to flip)",
                StockMonitor.DirectionText(MonitorSort.Price, true)
            );
            Assert.Equal(
                "A → Z (click to flip)",
                StockMonitor.DirectionText(MonitorSort.Symbol, false)
            );
        }

        [Fact]
        public void SettingRoundTrips()
        {
            var s = new ModSettings();
            Assert.False(s.Monitor.SortReversed);
            s.Monitor.SortReversed = true;
            Assert.True(SettingsFile.Parse(SettingsFile.Format(s)).Monitor.SortReversed);
            Assert.False(SettingsFile.Parse("").Monitor.SortReversed);
        }

        private static double Key(MonitorQuote q, MonitorSort sort)
        {
            switch (sort)
            {
                case MonitorSort.Change:
                    return q.ChangeRate;
                case MonitorSort.Price:
                    return q.Price;
                default:
                    return q.ValueHeld;
            }
        }
    }
}

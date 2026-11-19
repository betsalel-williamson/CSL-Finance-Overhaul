using System; // Math, Random, StringComparer
using System.Collections.Generic; // HashSet, List
using System.Linq; // Enumerable, OrderBy(), Select(), ToList()
using Xunit; // Assert, FactAttribute, InlineDataAttribute, TheoryAttribute

namespace FinanceOverhaul.Core.Tests
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

namespace FinanceOverhaul.Core.Tests
{
    /// <summary>
    /// Sort: Change follows what the pills show. With ₡ pills, over 2,000 seeded lists: rows run by
    /// the ₡ amount (highest first, or lowest with the direction flipped), the amount agrees with
    /// the pill's sign, and a cheap stock with a big % move sorts below an expensive one with a
    /// bigger ₡ move. With % pills the order is unchanged (by rate).
    /// </summary>
    public class MonitorSortByShownChangeTests
    {
        [Fact]
        public void CurrencyPillsSortByAmount()
        {
            var cheapBigPercent = new MonitorQuote
            {
                Symbol = "AAA",
                Price = 70,
                ChangeRate = 20,
            }; // ≈ +₡11.67
            var dearSmallPercent = new MonitorQuote
            {
                Symbol = "BBB",
                Price = 6000,
                ChangeRate = 2,
            }; // ≈ +₡117.65
            var quotes = new[] { cheapBigPercent, dearSmallPercent };
            Assert.Equal(
                new[] { "AAA", "BBB" },
                StockMonitor
                    .Rows(quotes, MonitorFilter.All, MonitorSort.Change, null, false, false)
                    .Select(q => q.Symbol)
            );
            Assert.Equal(
                new[] { "BBB", "AAA" },
                StockMonitor
                    .Rows(quotes, MonitorFilter.All, MonitorSort.Change, null, false, true)
                    .Select(q => q.Symbol)
            );
            Assert.Equal("Sort: Change ₡", StockMonitor.SortLabel(MonitorSort.Change, true));
            Assert.Equal("Sort: Change %", StockMonitor.SortLabel(MonitorSort.Change, false));
            Assert.Equal("Sort: Price", StockMonitor.SortLabel(MonitorSort.Price, true));
        }

        [Fact]
        public void OrderFollowsTheShownAmount()
        {
            var rng = new Random(909);
            for (int k = 0; k < 2000; k++)
            {
                var quotes = new List<MonitorQuote>();
                int n = rng.Next(2, 14);
                for (int i = 0; i < n; i++)
                {
                    quotes.Add(
                        new MonitorQuote
                        {
                            Symbol = "S" + i.ToString("00"),
                            Price = (float)Math.Exp(rng.NextDouble() * 9) + 1,
                            ChangeRate =
                                k % 7 == 0 && i == 0
                                    ? float.NaN
                                    : (float)((rng.NextDouble() - 0.5) * 40),
                        }
                    );
                }

                foreach (bool reversed in new[] { false, true })
                {
                    List<MonitorQuote> rows = StockMonitor.Rows(
                        quotes,
                        MonitorFilter.All,
                        MonitorSort.Change,
                        null,
                        reversed,
                        true
                    );
                    for (int i = 1; i < rows.Count; i++)
                    {
                        double a = StockMonitor.ChangeAmount(
                                rows[i - 1].Price,
                                rows[i - 1].ChangeRate
                            ),
                            b = StockMonitor.ChangeAmount(rows[i].Price, rows[i].ChangeRate);
                        Assert.True(reversed ? a <= b : a >= b);
                    }

                    foreach (MonitorQuote q in rows)
                    {
                        double amount = StockMonitor.ChangeAmount(q.Price, q.ChangeRate);
                        string pill = StockMonitor.ChangeAmountFromRate(q.Price, q.ChangeRate);
                        if (amount > 0 && amount != double.MinValue)
                        {
                            Assert.StartsWith("+₡", pill);
                        }
                        else if (amount < 0 && amount != double.MinValue)
                        {
                            Assert.StartsWith("−₡", pill);
                        }
                    }
                }
            }
        }
    }
}

namespace FinanceOverhaul.Core.Tests
{
    /// <summary>
    /// One ₡-change rule everywhere: over 5,000 seeded quotes the Monitor's ₡ pill, the detail
    /// header's ₡ part and the LED "price and ₡ change" entry all print the same text, built from
    /// the one value the ₡ sort uses (StockMonitor.ChangeAmount), and that text's number is that
    /// value rounded (cents under ₡10, whole ₡ above), with the same sign.
    /// </summary>
    public class OneCurrencyChangeRuleTests
    {
        [Theory]
        [InlineData(1000f, 9.994, "+₡9.99")]
        [InlineData(1000f, 9.995, "+₡10")]
        [InlineData(1000f, 9.996, "+₡10")]
        [InlineData(1000f, 10.4, "+₡10")]
        [InlineData(1f, 0.004, "+₡0.01")] // 0.40% on ₡1: never rounded to ₡0
        public void CentsUpTo999ThenWholeNumbers(float price, double amount, string expected)
        {
            // A rate giving exactly this ₡ change: price × r/(1+r) = amount.
            float rate = (float)(100.0 * amount / (price - amount));
            Assert.Equal(expected, StockMonitor.ChangeAmountFromRate(price, rate));
        }

        [Fact]
        public void EveryCurrencyChangeAgrees()
        {
            var rng = new Random(5150);
            for (int k = 0; k < 5000; k++)
            {
                float price = (float)Math.Exp(rng.NextDouble() * Math.Log(9_000_000)) + 0.5f;
                float rate =
                    k % 50 == 0
                        ? float.NaN
                        : (float)((rng.NextDouble() - 0.5) * (k % 5 == 0 ? 0.02 : 80));
                string pill = StockMonitor.ChangeAmountFromRate(price, rate);
                string header = StockDetail.ChangeHeader(price, rate);
                string led = LedText.ChangePart(price, rate, TickerDisplay.PriceAndChange);
                if (pill != "--")
                {
                    Assert.StartsWith(pill + " (", header);
                }

                Assert.EndsWith(" " + pill, led);

                double amount = StockMonitor.ChangeAmount(price, rate);
                if (amount == double.MinValue)
                {
                    Assert.Equal("--", pill);
                    continue;
                }

                if (amount == 0d)
                {
                    Assert.Equal("₡0", pill);
                    continue;
                }

                double shown = double.Parse(
                    pill.Replace("+₡", "").Replace("−₡", "").Replace(",", ""),
                    System.Globalization.CultureInfo.InvariantCulture
                );
                double abs = Math.Abs(amount);
                double cents = Math.Max(0.01, Math.Round(abs, 2));
                double expected = cents < 10 ? cents : Math.Round(abs);
                Assert.Equal(expected, shown, 6);
                // Two decimals only up to 9.99; whole numbers from 10.
                string number = pill.Substring(2);
                Assert.Equal(cents < 10, number.Contains("."));
                Assert.False(number.StartsWith("10."), pill);
                Assert.Equal(amount > 0, pill.StartsWith("+₡"));
            }
        }
    }
}

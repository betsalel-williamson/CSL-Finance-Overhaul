using System; // Math, StringComparison
using System.Collections.Generic; // IEnumerable, List
using System.Globalization; // CultureInfo

namespace FinanceOverhaul.Core
{
    /// <summary>The views the Investments tab's right-hand column shows, one at a time.</summary>
    public enum OverviewPage
    {
        /// <summary>The whole market: an index of every stock, today's movers, up and down.</summary>
        Market,

        /// <summary>What the city holds: its value, its gain, its open orders.</summary>
        Portfolio,

        /// <summary>Every stock as a tile coloured by its change.</summary>
        Heatmap,

        /// <summary>The city figures behind each stock.</summary>
        City,

        /// <summary>What happened lately and what is coming.</summary>
        Feed,
    }

    /// <summary>
    /// The overview's views, picked with a sub tab group (Figma band 20, "Two columns"); a view
    /// not built yet has a disabled tab and is never shown. Cities-free.
    /// </summary>
    public static class OverviewPages
    {
        /// <summary>The views, in tab order.</summary>
        public static readonly OverviewPage[] All =
        {
            OverviewPage.Market,
            OverviewPage.Portfolio,
            OverviewPage.Heatmap,
            OverviewPage.City,
            OverviewPage.Feed,
        };

        /// <summary>Whether a view is built (its tab can be picked).</summary>
        public static bool IsBuilt(OverviewPage page)
        {
            return page == OverviewPage.Market;
        }

        /// <summary>The view to show for a saved choice: it, or the first built one.</summary>
        public static OverviewPage Shown(OverviewPage saved)
        {
            return IsBuilt(saved) ? saved : OverviewPage.Market;
        }

        /// <summary>The view's tab label ("Market").</summary>
        public static string Tab(OverviewPage page)
        {
            return Strings.Get("overview.tab." + page);
        }
    }

    /// <summary>
    /// The Market page of the overview: one index of every stock on sale, today's biggest
    /// movers each way, and how many stocks rose, fell or stayed. Only what the game has (daily
    /// prices and its change rate). Cities-free.
    /// </summary>
    public static class MarketOverview
    {
        /// <summary>A change smaller than this (in percent) counts as flat.</summary>
        public const float FlatPercent = 0.005f;

        /// <summary>The index on the first day shown.</summary>
        public const float IndexBase = 100f;

        private static bool Counts(MonitorQuote q)
        {
            return q != null && !q.Locked && q.History != null;
        }

        /// <summary>
        /// The market index over the last <paramref name="days"/> days: each stock on sale
        /// rebased to <see cref="IndexBase"/> on its first price in the range and all of them
        /// averaged, each counted equally (a ₡97,000 stock moves it no more than a ₡50 one). The
        /// days line up from today backwards; a day no stock has a price for is NaN. Empty when
        /// no stock has two prices.
        /// </summary>
        public static float[] Index(IEnumerable<MonitorQuote> quotes, int days)
        {
            var ranges = new List<float[]>();
            int longest = 0;
            foreach (MonitorQuote q in quotes ?? new List<MonitorQuote>())
            {
                if (!Counts(q))
                {
                    continue;
                }

                float[] range = StockDetail.Range(q.History, days);
                float first = float.NaN;
                foreach (float v in range)
                {
                    if (Usable(v))
                    {
                        first = v;
                        break;
                    }
                }

                if (float.IsNaN(first))
                {
                    continue;
                }

                var rebased = new float[range.Length];
                for (int i = 0; i < range.Length; i++)
                {
                    rebased[i] = Usable(range[i]) ? range[i] / first * IndexBase : float.NaN;
                }

                ranges.Add(rebased);
                longest = Math.Max(longest, rebased.Length);
            }

            if (longest < 2)
            {
                return new float[0];
            }

            var index = new float[longest];
            for (int d = 0; d < longest; d++)
            {
                double sum = 0;
                int n = 0;
                foreach (float[] r in ranges)
                {
                    // Line up at today: the last value of every range is the same day.
                    int i = d - (longest - r.Length);
                    if (i >= 0 && !float.IsNaN(r[i]))
                    {
                        sum += r[i];
                        n++;
                    }
                }

                index[d] = n == 0 ? float.NaN : (float)(sum / n);
            }

            return index;
        }

        private static bool Usable(float v)
        {
            return !float.IsNaN(v) && !float.IsInfinity(v) && v > 0f;
        }

        /// <summary>The index's change over the range, in percent (0 when it has too few days).</summary>
        public static float RangeChange(float[] index)
        {
            float first = float.NaN,
                last = float.NaN;
            foreach (float v in index ?? new float[0])
            {
                if (!float.IsNaN(v))
                {
                    if (float.IsNaN(first))
                    {
                        first = v;
                    }

                    last = v;
                }
            }

            return float.IsNaN(first) || first <= 0f ? 0f : (last / first - 1f) * 100f;
        }

        /// <summary>
        /// Today's biggest movers one way: rising (<paramref name="rising"/>) or falling, by the
        /// game's change rate, largest first, at most <paramref name="count"/>; ties A–Z.
        /// </summary>
        public static List<MonitorQuote> Movers(
            IEnumerable<MonitorQuote> quotes,
            bool rising,
            int count
        )
        {
            var movers = new List<MonitorQuote>();
            foreach (MonitorQuote q in quotes ?? new List<MonitorQuote>())
            {
                if (!Counts(q) || Math.Abs(q.ChangeRate) < FlatPercent)
                {
                    continue;
                }

                if (rising ? q.ChangeRate > 0f : q.ChangeRate < 0f)
                {
                    movers.Add(q);
                }
            }

            movers.Sort(
                (a, b) =>
                {
                    int byRate = rising
                        ? b.ChangeRate.CompareTo(a.ChangeRate)
                        : a.ChangeRate.CompareTo(b.ChangeRate);
                    return byRate != 0
                        ? byRate
                        : string.Compare(a.Symbol, b.Symbol, StringComparison.Ordinal);
                }
            );
            if (movers.Count > count)
            {
                movers.RemoveRange(count, movers.Count - count);
            }

            return movers;
        }

        /// <summary>"9 up · 6 down · 1 flat": today's stocks on sale, by the game's change rate.</summary>
        public static string Breadth(IEnumerable<MonitorQuote> quotes)
        {
            int up = 0,
                down = 0,
                flat = 0;
            foreach (MonitorQuote q in quotes ?? new List<MonitorQuote>())
            {
                if (!Counts(q))
                {
                    continue;
                }

                if (Math.Abs(q.ChangeRate) < FlatPercent)
                {
                    flat++;
                }
                else if (q.ChangeRate > 0f)
                {
                    up++;
                }
                else
                {
                    down++;
                }
            }

            return Strings.Format(
                "overview.market.breadth",
                up.ToString(CultureInfo.InvariantCulture),
                down.ToString(CultureInfo.InvariantCulture),
                flat.ToString(CultureInfo.InvariantCulture)
            );
        }

        /// <summary>"104.2": the index's value as shown.</summary>
        public static string IndexText(float value)
        {
            return float.IsNaN(value) ? "--" : value.ToString("0.0", CultureInfo.InvariantCulture);
        }

        /// <summary>"+4.2% over 30 days".</summary>
        public static string RangeChangeText(float percent, int days)
        {
            return Strings.Format(
                "overview.market.rangeChange",
                (percent >= 0f ? "+" : "−")
                    + Math.Abs(percent).ToString("0.0", CultureInfo.InvariantCulture)
                    + "%",
                days.ToString(CultureInfo.InvariantCulture)
            );
        }
    }
}

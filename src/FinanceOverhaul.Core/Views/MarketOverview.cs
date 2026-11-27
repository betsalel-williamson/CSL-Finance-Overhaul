using System; // Array, Math, StringComparison
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
            return Array.IndexOf(All, page) >= 0;
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

        /// <summary>The city index fund (IDX) when it is listed and has a history, else null.</summary>
        public static MonitorQuote IndexFund(IEnumerable<MonitorQuote> quotes)
        {
            foreach (MonitorQuote q in quotes ?? new List<MonitorQuote>())
            {
                if (q != null && q.History != null && CityIndexBasket.IsIndexName(q.Prefab))
                {
                    return q;
                }
            }

            return null;
        }

        /// <summary>A stock the market's breadth, movers and index count: on sale and not IDX itself.</summary>
        private static bool Member(MonitorQuote q)
        {
            return Counts(q) && !CityIndexBasket.IsIndexName(q.Prefab);
        }

        /// <summary>
        /// The market over the last <paramref name="days"/> days, oldest first. With the city
        /// index fund listed it is the fund's own daily prices (the chart matches IDX's). Without
        /// it, the fund's rule rebuilt from the members' prices, starting at
        /// <see cref="IndexBase"/>: each day the level moves by the geometric mean of the
        /// members' price ratios, each capped at a halving or a doubling
        /// (<see cref="CityIndexBasket.TryComputeChainedMark"/>); a stock listed later joins on
        /// its second day. Empty when there are fewer than two days.
        /// </summary>
        public static float[] Index(IEnumerable<MonitorQuote> quotes, int days)
        {
            MonitorQuote fund = IndexFund(quotes);
            if (fund != null)
            {
                float[] own = StockDetail.Range(fund.History, days);
                return own.Length < 2 ? new float[0] : own;
            }

            var names = new List<string>();
            var ranges = new List<float[]>();
            int longest = 0;
            foreach (MonitorQuote q in quotes ?? new List<MonitorQuote>())
            {
                // The fund's basket: share-priced stocks only (no coins such as ChirpCoin).
                if (!Member(q) || q.Coin)
                {
                    continue;
                }

                float[] range = StockDetail.Range(q.History, days);
                if (range.Length == 0)
                {
                    continue;
                }

                names.Add(q.Prefab ?? q.Symbol);
                ranges.Add(range);
                longest = Math.Max(longest, range.Length);
            }

            if (longest < 2)
            {
                return new float[0];
            }

            var index = new float[longest];
            index[0] = IndexBase;
            var eligible = new List<bool>();
            foreach (string unused in names)
            {
                eligible.Add(true);
            }

            for (int d = 1; d < longest; d++)
            {
                var last = new List<float>();
                var previous = new List<float>();
                foreach (float[] r in ranges)
                {
                    // Line up at today: the last value of every range is the same day.
                    int i = d - (longest - r.Length);
                    last.Add(i >= 1 ? r[i] : float.NaN);
                    previous.Add(i >= 1 ? r[i - 1] : float.NaN);
                }

                float mark;
                index[d] = CityIndexBasket.TryComputeChainedMark(
                    index[d - 1],
                    names,
                    last,
                    previous,
                    eligible,
                    out mark
                )
                    ? mark
                    : index[d - 1];
            }

            return index;
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
        /// Today's biggest movers one way: rising (<paramref name="rising"/>) or falling,
        /// largest first, at most <paramref name="count"/>; ties A–Z. Ranked as the pills show
        /// the change: by the game's change rate, or with the pills in points by that change in
        /// ₡ (<paramref name="byAmount"/>), as the list's Change sort does.
        /// </summary>
        public static List<MonitorQuote> Movers(
            IEnumerable<MonitorQuote> quotes,
            bool rising,
            int count,
            bool byAmount = false
        )
        {
            var movers = new List<MonitorQuote>();
            foreach (MonitorQuote q in quotes ?? new List<MonitorQuote>())
            {
                if (!Member(q) || Math.Abs(q.ChangeRate) < FlatPercent)
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
                    double ka = byAmount
                        ? StockMonitor.ChangeAmount(a.Price, a.ChangeRate)
                        : a.ChangeRate;
                    double kb = byAmount
                        ? StockMonitor.ChangeAmount(b.Price, b.ChangeRate)
                        : b.ChangeRate;
                    int byRate = rising ? kb.CompareTo(ka) : ka.CompareTo(kb);
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
                if (!Member(q))
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

        /// <summary>
        /// The index's value as shown: the fund's price ("₡3,056") when it is listed, else the
        /// rebuilt level ("104.2").
        /// </summary>
        public static string IndexText(float value, bool fund)
        {
            if (float.IsNaN(value))
            {
                return "--";
            }

            return fund
                ? StockMonitor.PriceText(value)
                : value.ToString("0.0", CultureInfo.InvariantCulture);
        }

        /// <summary>"Market index (IDX)" with the fund listed, else "Market index".</summary>
        public static string IndexLabel(MonitorQuote fund)
        {
            return fund == null
                ? Strings.Get("overview.market.index")
                : Strings.Format("overview.market.indexFund", fund.Symbol);
        }

        /// <summary>
        /// The index's change over the range as its pill shows it: in percent ("−2.70%") or,
        /// after a click on any pill, in points: ₡ for the fund ("−₡85"), index points for the
        /// rebuilt level ("−2.7 pts"). "--" with fewer than two days.
        /// </summary>
        public static string RangePill(float[] index, bool fund, bool points)
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

            if (float.IsNaN(first) || first <= 0f)
            {
                return "--";
            }

            if (!points)
            {
                return StockMonitor.ChangeText(RangeChange(index));
            }

            double diff = last - first;
            string sign =
                diff > 0 ? "+"
                : diff < 0 ? "−"
                : string.Empty;
            double size = Math.Abs(diff);
            if (fund)
            {
                // As the ₡ pills: cents up to ₡9.99, whole ₡ from ₡10.
                double cents = Math.Round(size, 2);
                return sign
                    + "₡"
                    + (
                        cents < 10.0
                            ? cents.ToString("0.00", CultureInfo.InvariantCulture)
                            : Math.Round(size).ToString("N0", CultureInfo.InvariantCulture)
                    );
            }

            return Strings.Format(
                "overview.market.points",
                sign + size.ToString("0.0", CultureInfo.InvariantCulture)
            );
        }

        /// <summary>"over 30 days", beside the index's pill.</summary>
        public static string OverDays(int days)
        {
            return Strings.Format(
                "overview.market.overDays",
                days.ToString(CultureInfo.InvariantCulture)
            );
        }
    }
}

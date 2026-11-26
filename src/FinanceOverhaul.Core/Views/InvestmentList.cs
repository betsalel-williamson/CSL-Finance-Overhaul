using System; // Math
using System.Collections.Generic; // IEnumerable, IList, List
using System.Globalization; // CultureInfo

namespace FinanceOverhaul.Core
{
    /// <summary>Which stocks Economy → Investments lists.</summary>
    public enum InvestmentFilter
    {
        All,

        /// <summary>Stocks the city holds.</summary>
        Owned,

        /// <summary>Local companies (Local IPOs), held or not.</summary>
        Local,
    }

    /// <summary>The list's sections, in the order they are shown.</summary>
    public enum InvestmentSection
    {
        Portfolio,
        Local,
        All,
    }

    /// <summary>One line of the list: a section heading or a stock.</summary>
    public sealed class InvestmentRow
    {
        public InvestmentSection Section;

        /// <summary>Null for a heading.</summary>
        public MonitorQuote Stock;

        /// <summary>A heading's stock count.</summary>
        public int Count;

        public bool IsHeading
        {
            get { return Stock == null; }
        }

        /// <summary>Stable key: the stock's symbol, or the section for a heading.</summary>
        public string Key
        {
            get { return Stock == null ? "#" + Section : Stock.Symbol; }
        }
    }

    /// <summary>
    /// The one sectioned list that replaces Economy → Investments' two (design:
    /// investments-and-research.md): which stocks show for a filter, in which section and order,
    /// the rows' places for a scroll offset, keeping the same stock in view when the list changes,
    /// and the list's wording. Cities-free.
    /// </summary>
    public static class InvestmentList
    {
        /// <summary>Height of a stock row, with its gap.</summary>
        public const float RowHeight = 44f;

        /// <summary>Height of a section heading, with its gap.</summary>
        public const float HeadingHeight = 30f;

        /// <summary>Rows a wheel notch moves.</summary>
        public const int RowsPerNotch = 3;

        public static readonly InvestmentFilter[] Filters =
        {
            InvestmentFilter.All,
            InvestmentFilter.Owned,
            InvestmentFilter.Local,
        };

        /// <summary>
        /// The rows for a filter: Your portfolio (held), Local companies (not held), All stocks (the
        /// rest, locked ones last); a stock sits in one section; empty sections are left out.
        /// "Owned" and "Local" list one section each. Within a section, the Stock Monitor's sort.
        /// </summary>
        public static List<InvestmentRow> Rows(
            IEnumerable<MonitorQuote> quotes,
            InvestmentFilter filter,
            MonitorSort sort,
            bool reversed
        )
        {
            var portfolio = new List<MonitorQuote>();
            var local = new List<MonitorQuote>();
            var rest = new List<MonitorQuote>();
            var locked = new List<MonitorQuote>();
            foreach (MonitorQuote q in quotes ?? new List<MonitorQuote>())
            {
                if (q == null)
                {
                    continue;
                }

                if (filter == InvestmentFilter.Owned)
                {
                    if (q.Owned > 0)
                    {
                        portfolio.Add(q);
                    }
                }
                else if (filter == InvestmentFilter.Local)
                {
                    if (q.Local)
                    {
                        local.Add(q);
                    }
                }
                else if (q.Owned > 0)
                {
                    portfolio.Add(q);
                }
                else if (q.Local)
                {
                    local.Add(q);
                }
                else if (q.Locked)
                {
                    locked.Add(q);
                }
                else
                {
                    rest.Add(q);
                }
            }

            var rows = new List<InvestmentRow>();
            Add(rows, InvestmentSection.Portfolio, Sorted(portfolio, sort, reversed));
            Add(rows, InvestmentSection.Local, Sorted(local, sort, reversed));
            List<MonitorQuote> all = Sorted(rest, sort, reversed);
            all.AddRange(Sorted(locked, MonitorSort.Symbol, false));
            Add(rows, InvestmentSection.All, all);
            return rows;
        }

        /// <summary>Each row's top edge in the list, and the list's whole height last.</summary>
        public static float[] Tops(IList<InvestmentRow> rows)
        {
            var tops = new float[rows.Count + 1];
            float y = 0f;
            for (int i = 0; i < rows.Count; i++)
            {
                tops[i] = y;
                y += rows[i].IsHeading ? HeadingHeight : RowHeight;
            }

            tops[rows.Count] = y;
            return tops;
        }

        /// <summary>The furthest the list scrolls: its height less the view's, never below 0.</summary>
        public static float MaxOffset(float[] tops, float viewHeight)
        {
            return Math.Max(0f, tops[tops.Length - 1] - viewHeight);
        }

        public static float Clamp(float offset, float[] tops, float viewHeight)
        {
            return Math.Max(0f, Math.Min(MaxOffset(tops, viewHeight), offset));
        }

        /// <summary>The first row showing at that offset (any part of it).</summary>
        public static int FirstVisible(float[] tops, float offset)
        {
            int lo = 0;
            int hi = tops.Length - 2;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (tops[mid] <= offset)
                {
                    lo = mid;
                }
                else
                {
                    hi = mid - 1;
                }
            }

            return Math.Max(0, lo);
        }

        /// <summary>
        /// The offset that keeps the row that was at the top of the view where it was when the list
        /// changes (a purchase moves a stock into Your portfolio, the daily sort): found by key,
        /// else the same offset.
        /// </summary>
        public static float KeepInView(
            IList<InvestmentRow> before,
            float offset,
            IList<InvestmentRow> after,
            float viewHeight
        )
        {
            if (before == null || before.Count == 0 || after == null || after.Count == 0)
            {
                return 0f;
            }

            float[] oldTops = Tops(before);
            int top = FirstVisible(oldTops, offset);
            float into = offset - oldTops[top];
            string key = before[top].Key;
            float[] newTops = Tops(after);
            for (int i = 0; i < after.Count; i++)
            {
                if (after[i].Key == key)
                {
                    return Clamp(newTops[i] + into, newTops, viewHeight);
                }
            }

            return Clamp(offset, newTops, viewHeight);
        }

        /// <summary>"All 17", "Owned 3", "Local 2": a filter tab with its count.</summary>
        public static string FilterLabel(InvestmentFilter filter, IEnumerable<MonitorQuote> quotes)
        {
            int n = 0;
            foreach (MonitorQuote q in quotes ?? new List<MonitorQuote>())
            {
                if (
                    q != null
                    && (
                        filter == InvestmentFilter.All
                        || (filter == InvestmentFilter.Owned && q.Owned > 0)
                        || (filter == InvestmentFilter.Local && q.Local)
                    )
                )
                {
                    n++;
                }
            }

            return Strings.Format("investments.filter." + filter, Number(n));
        }

        /// <summary>"Your portfolio", "Local companies", "All stocks".</summary>
        public static string Heading(InvestmentSection section)
        {
            return Strings.Get("investments.section." + section);
        }

        /// <summary>
        /// A heading's right-hand side: Your portfolio's value and gain ("₡3,284,000 · −₡510,500"),
        /// otherwise how many stocks ("3 stocks").
        /// </summary>
        public static string HeadingDetail(InvestmentSection section, IList<InvestmentRow> rows)
        {
            int count = 0;
            double worth = 0;
            long spent = 0;
            foreach (InvestmentRow r in rows)
            {
                if (r.Stock != null && r.Section == section)
                {
                    count++;
                    worth += r.Stock.ValueHeld;
                    spent += r.Stock.Spent;
                }
            }

            if (section != InvestmentSection.Portfolio)
            {
                return Strings.Plural("investments.section.count", count, Number(count));
            }

            long worthCents = (long)Math.Round(worth * 100.0);
            return Strings.Format(
                "investments.section.portfolioDetail",
                BondText.WholeMoney(worthCents),
                SignedMoney(worthCents - spent * 100L)
            );
        }

        /// <summary>"500 sh · avg ₡6,616" for a holding; null when the city holds none.</summary>
        public static string Holding(MonitorQuote q)
        {
            if (q == null || q.Owned == 0)
            {
                return null;
            }

            return Strings.Format(
                "investments.row.holding",
                Number((int)Math.Min(int.MaxValue, q.Owned)),
                BondText.WholeMoney((long)Math.Round(100.0 * q.Spent / q.Owned))
            );
        }

        /// <summary>"+₡489,500 (+14.8%)" for a holding: current value less what was paid; null with none.</summary>
        public static string Gain(MonitorQuote q)
        {
            if (q == null || q.Owned == 0)
            {
                return null;
            }

            long worthCents = (long)Math.Round(q.ValueHeld * 100.0);
            long gainCents = worthCents - q.Spent * 100L;
            if (q.Spent <= 0)
            {
                return SignedMoney(gainCents);
            }

            return Strings.Format(
                "investments.row.gainPercent",
                SignedMoney(gainCents),
                (gainCents / (q.Spent * 100.0) * 100.0).ToString(
                    "+0.0;−0.0;0.0",
                    CultureInfo.InvariantCulture
                )
            );
        }

        /// <summary>1, −1 or 0: whether a holding is up, down or even.</summary>
        public static int GainSign(MonitorQuote q)
        {
            if (q == null || q.Owned == 0)
            {
                return 0;
            }

            return Math.Sign(Math.Round(q.ValueHeld * 100.0) - q.Spent * 100.0);
        }

        /// <summary>"2 orders" (or "1 order"); null without open orders.</summary>
        public static string Orders(MonitorQuote q)
        {
            return q == null || q.OpenOrders <= 0
                ? null
                : Strings.Plural("investments.row.orders", q.OpenOrders, Number(q.OpenOrders));
        }

        /// <summary>"Locked · 64 / 200": the game's requirement on a locked stock's row.</summary>
        public static string LockLine(MonitorQuote q)
        {
            return Strings.Format("investments.row.locked", q == null ? "" : q.UnlockText ?? "");
        }

        private static string SignedMoney(long cents)
        {
            string whole = BondText.WholeMoney(Math.Abs(cents));
            return cents > 0 ? "+" + whole
                : cents < 0 ? "−" + whole
                : whole;
        }

        private static List<MonitorQuote> Sorted(
            List<MonitorQuote> quotes,
            MonitorSort sort,
            bool reversed
        )
        {
            return StockMonitor.Rows(quotes, MonitorFilter.All, sort, null, reversed);
        }

        private static void Add(
            List<InvestmentRow> rows,
            InvestmentSection section,
            List<MonitorQuote> stocks
        )
        {
            if (stocks.Count == 0)
            {
                return;
            }

            rows.Add(new InvestmentRow { Section = section, Count = stocks.Count });
            foreach (MonitorQuote q in stocks)
            {
                rows.Add(new InvestmentRow { Section = section, Stock = q });
            }
        }

        private static string Number(int value)
        {
            return value.ToString("#,0", CultureInfo.InvariantCulture);
        }
    }
}

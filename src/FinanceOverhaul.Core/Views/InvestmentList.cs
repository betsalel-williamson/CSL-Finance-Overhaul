using System; // Math
using System.Collections.Generic; // IEnumerable, IList, List
using System.Globalization; // CultureInfo

namespace FinanceOverhaul.Core
{
    /// <summary>Which stocks Economy → Investments lists (the filter tabs).</summary>
    public enum InvestmentFilter
    {
        All,

        /// <summary>Stocks the city holds.</summary>
        Owned,

        /// <summary>Local companies (Local IPOs), held or not.</summary>
        Local,

        /// <summary>Stocks whose requirement is not met yet.</summary>
        Locked,
    }

    /// <summary>The sections the Group button splits the list into, in the order shown.</summary>
    public enum InvestmentSection
    {
        Portfolio,
        Local,
        All,
    }

    /// <summary>What a line of the list is.</summary>
    public enum InvestmentRowKind
    {
        /// <summary>A section heading (only with Group on).</summary>
        Heading,
        Stock,

        /// <summary>An open order, tucked under its stock.</summary>
        Order,
    }

    /// <summary>One line of the list: a section heading, a stock, or an open order under it.</summary>
    public sealed class InvestmentRow
    {
        public InvestmentRowKind Kind;
        public InvestmentSection Section;

        /// <summary>The stock (an order's stock too); null for a heading.</summary>
        public MonitorQuote Stock;

        /// <summary>An order line's order.</summary>
        public TradeOrder Order;

        /// <summary>A heading's stock count.</summary>
        public int Count;

        public bool IsHeading
        {
            get { return Kind == InvestmentRowKind.Heading; }
        }

        /// <summary>Stable key: the section, the stock's symbol, or the order's id.</summary>
        public string Key
        {
            get
            {
                switch (Kind)
                {
                    case InvestmentRowKind.Heading:
                        return "#" + Section;
                    case InvestmentRowKind.Order:
                        return "order:" + Order.Id.ToString(CultureInfo.InvariantCulture);
                    default:
                        return Stock.Symbol;
                }
            }
        }

        /// <summary>The line's height with its gap: a holding takes a third line.</summary>
        public float Height
        {
            get
            {
                switch (Kind)
                {
                    case InvestmentRowKind.Heading:
                        return InvestmentList.HeadingHeight;
                    case InvestmentRowKind.Order:
                        return InvestmentList.OrderHeight;
                    default:
                        return Stock.Owned > 0
                            ? InvestmentList.HeldHeight
                            : InvestmentList.RowHeight;
                }
            }
        }
    }

    /// <summary>
    /// The list that replaces Economy → Investments' two (design: investments-and-research.md,
    /// Figma band 18 "Condensed B"): which stocks show for a filter tab and in which order; one
    /// list sorted as a whole unless the player turns Group on, which splits it into Your
    /// portfolio, Local companies and All stocks; open orders under their stock; the lines'
    /// places for a scroll offset; keeping the same line in view when the list changes; and the
    /// list's wording. Cities-free.
    /// </summary>
    public static class InvestmentList
    {
        /// <summary>Height of a stock line (symbol and name), with its gap.</summary>
        public const float RowHeight = 48f;

        /// <summary>Height of a held stock's line (a third line: shares and average cost).</summary>
        public const float HeldHeight = 64f;

        /// <summary>Height of an open order's line under its stock.</summary>
        public const float OrderHeight = 26f;

        /// <summary>Height of a section heading, with its gap.</summary>
        public const float HeadingHeight = 30f;

        /// <summary>Pixels a wheel notch moves: three plain lines.</summary>
        public const float NotchPixels = 3f * RowHeight;

        public static readonly InvestmentFilter[] Filters =
        {
            InvestmentFilter.All,
            InvestmentFilter.Owned,
            InvestmentFilter.Local,
            InvestmentFilter.Locked,
        };

        /// <summary>Whether a stock shows under a filter tab.</summary>
        public static bool Matches(MonitorQuote q, InvestmentFilter filter)
        {
            switch (filter)
            {
                case InvestmentFilter.Owned:
                    return q.Owned > 0;
                case InvestmentFilter.Local:
                    return q.Local;
                case InvestmentFilter.Locked:
                    return q.Locked;
                default:
                    return true;
            }
        }

        public static List<InvestmentRow> Rows(
            IEnumerable<MonitorQuote> quotes,
            InvestmentFilter filter,
            MonitorSort sort,
            bool reversed
        )
        {
            return Rows(quotes, filter, sort, reversed, false, null);
        }

        /// <summary>
        /// The lines for a filter tab, sorted as one list (locked stocks last, A–Z), each stock
        /// followed by its open orders. With <paramref name="grouped"/> the same stocks are split
        /// into Your portfolio (held), Local companies (not held) and All stocks (the rest), each
        /// under a heading, empty ones left out; the sort applies within each.
        /// </summary>
        /// <param name="orders">Open orders; matched to stocks by <see cref="MonitorQuote.Prefab"/>.</param>
        public static List<InvestmentRow> Rows(
            IEnumerable<MonitorQuote> quotes,
            InvestmentFilter filter,
            MonitorSort sort,
            bool reversed,
            bool grouped,
            IList<TradeOrder> orders
        )
        {
            var shown = new List<MonitorQuote>();
            foreach (MonitorQuote q in quotes ?? new List<MonitorQuote>())
            {
                if (q != null && Matches(q, filter))
                {
                    shown.Add(q);
                }
            }

            var rows = new List<InvestmentRow>();
            if (!grouped)
            {
                Add(rows, null, InvestmentSection.All, Ordered(shown, sort, reversed), orders);
                return rows;
            }

            var portfolio = new List<MonitorQuote>();
            var local = new List<MonitorQuote>();
            var rest = new List<MonitorQuote>();
            foreach (MonitorQuote q in shown)
            {
                (
                    q.Owned > 0 ? portfolio
                    : q.Local ? local
                    : rest
                ).Add(q);
            }

            Add(rows, InvestmentSection.Portfolio, Ordered(portfolio, sort, reversed), orders);
            Add(rows, InvestmentSection.Local, Ordered(local, sort, reversed), orders);
            Add(rows, InvestmentSection.All, Ordered(rest, sort, reversed), orders);
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
                y += rows[i].Height;
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

        /// <summary>"All 17", "Owned 3", "Local 2", "Locked 3": a filter tab with its count.</summary>
        public static string FilterLabel(InvestmentFilter filter, IEnumerable<MonitorQuote> quotes)
        {
            int n = 0;
            foreach (MonitorQuote q in quotes ?? new List<MonitorQuote>())
            {
                if (q != null && Matches(q, filter))
                {
                    n++;
                }
            }

            return Strings.Format("investments.filter." + filter, Number(n));
        }

        /// <summary>The Group button's text: "Group" or, while grouped, "Ungroup".</summary>
        public static string GroupLabel(bool grouped)
        {
            return Strings.Get(grouped ? "investments.ungroup" : "investments.group");
        }

        /// <summary>"Stop-loss · sell 500 if the price drops to ₡6,800 · 54 days left": an order line.</summary>
        public static string OrderLine(TradeOrder order, int today)
        {
            return Strings.Format(
                "investments.row.order",
                OrderText.CardSide(order.Kind),
                OrderText.CardDetail(order, today)
            );
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
                if (r.Kind == InvestmentRowKind.Stock && r.Section == section)
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

        /// <summary>"500 shares · avg ₡6,616" for a holding; null when the city holds none.</summary>
        public static string Holding(MonitorQuote q)
        {
            if (q == null || q.Owned == 0)
            {
                return null;
            }

            return Strings.Plural(
                "investments.row.holding",
                q.Owned,
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

        /// <summary>The sort, with locked stocks (no price yet) last in A–Z order.</summary>
        private static List<MonitorQuote> Ordered(
            List<MonitorQuote> quotes,
            MonitorSort sort,
            bool reversed
        )
        {
            var open = new List<MonitorQuote>();
            var locked = new List<MonitorQuote>();
            foreach (MonitorQuote q in quotes)
            {
                (q.Locked ? locked : open).Add(q);
            }

            List<MonitorQuote> ordered = StockMonitor.Rows(
                open,
                MonitorFilter.All,
                sort,
                null,
                reversed
            );
            ordered.AddRange(
                StockMonitor.Rows(locked, MonitorFilter.All, MonitorSort.Symbol, null)
            );
            return ordered;
        }

        /// <param name="section">Null: no heading (the list is not grouped).</param>
        private static void Add(
            List<InvestmentRow> rows,
            InvestmentSection? section,
            InvestmentSection where,
            List<MonitorQuote> stocks,
            IList<TradeOrder> orders
        )
        {
            if (stocks.Count == 0)
            {
                return;
            }

            if (section.HasValue)
            {
                rows.Add(
                    new InvestmentRow
                    {
                        Kind = InvestmentRowKind.Heading,
                        Section = where,
                        Count = stocks.Count,
                    }
                );
            }

            foreach (MonitorQuote q in stocks)
            {
                rows.Add(
                    new InvestmentRow
                    {
                        Kind = InvestmentRowKind.Stock,
                        Section = where,
                        Stock = q,
                    }
                );
                foreach (TradeOrder o in orders ?? new List<TradeOrder>())
                {
                    if (o != null && q.Prefab != null && o.InvestmentKey == q.Prefab)
                    {
                        rows.Add(
                            new InvestmentRow
                            {
                                Kind = InvestmentRowKind.Order,
                                Section = where,
                                Stock = q,
                                Order = o,
                            }
                        );
                    }
                }
            }
        }

        private static void Add(
            List<InvestmentRow> rows,
            InvestmentSection section,
            List<MonitorQuote> stocks,
            IList<TradeOrder> orders
        )
        {
            Add(rows, section, section, stocks, orders);
        }

        private static string Number(int value)
        {
            return value.ToString("#,0", CultureInfo.InvariantCulture);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;

namespace FinanceOverhaul.Core
{
    /// <summary>What a notice reports (priority 1, Figma band 13).</summary>
    public enum NoticeKind
    {
        Filled,
        Expired,
        Cancelled,
        Summary,
        BondMatured,
        BondCoupon,

        /// <summary>Index shares lost while the mod was off, restored from the mod's copy.</summary>
        IndexRestored,
    }

    /// <summary>One chirp: who says it and what (the host posts it to Chirper, UI only).</summary>
    public sealed class Notice
    {
        public NoticeKind Kind;
        public string Sender;
        public string Text;

        /// <summary>The stock symbol for order notices (used by the daily summary).</summary>
        public string Symbol;

        public bool IsOrder
        {
            get
            {
                return Kind == NoticeKind.Filled
                    || Kind == NoticeKind.Expired
                    || Kind == NoticeKind.Cancelled
                    || Kind == NoticeKind.Summary;
            }
        }
    }

    /// <summary>
    /// Wording for order and bond notices. Numbers use the same formats as the rest of the mod
    /// (whole ₡ prices and totals, bond money in ₡ with cents). See NoticesTests.
    /// </summary>
    public static class NoticeText
    {
        public static string Brokerage
        {
            get { return Strings.Get("notice.sender.brokerage"); }
        }

        public static string Treasury
        {
            get { return Strings.Get("notice.sender.treasury"); }
        }

        /// <param name="price">The daily price the order filled at.</param>
        /// <param name="amount">Whole ₡ paid (buy) or received (sell).</param>
        public static Notice Filled(
            OrderKind kind,
            uint shares,
            string symbol,
            float price,
            int trigger,
            long amount
        )
        {
            string text = Strings.Format(
                "notice.filled." + OrderText.Key(kind),
                Count(shares),
                symbol,
                StockMonitor.PriceText(price),
                Whole(trigger),
                Whole(amount)
            );
            return Order(NoticeKind.Filled, symbol, text);
        }

        public static Notice Expired(OrderKind kind, uint shares, string symbol, int lifetimeDays)
        {
            string text = Strings.Plural(
                "notice.expired",
                lifetimeDays,
                Title(kind),
                Count(shares),
                symbol,
                lifetimeDays.ToString(CultureInfo.InvariantCulture)
            );
            return Order(NoticeKind.Expired, symbol, text);
        }

        /// <param name="reason">An <see cref="OrderRules"/> reason.</param>
        /// <param name="cost">Whole ₡ the buy needed (for "not enough cash").</param>
        public static Notice Cancelled(
            OrderKind kind,
            uint shares,
            string symbol,
            string reason,
            long cost
        )
        {
            string why;
            if (reason == OrderRules.ReasonFunds)
            {
                why =
                    cost > 0
                        ? Strings.Format("notice.why.fundsNeeds", Whole(cost))
                        : Strings.Get("notice.why.funds");
            }
            else if (reason == OrderRules.ReasonShareLimit)
            {
                why = Strings.Get("notice.why.shareLimit");
            }
            else if (reason == OrderRules.ReasonNoShares)
            {
                why = Strings.Format("notice.why.noShares", symbol);
            }
            else
            {
                why = reason ?? Strings.Get("notice.why.cancelled");
            }

            string text = Strings.Format(
                "notice.cancelled",
                Title(kind),
                Count(shares),
                symbol,
                why
            );
            return Order(NoticeKind.Cancelled, symbol, text);
        }

        public static Notice BondMatured(int bondId, long faceCents, long lastCouponCents)
        {
            string id = bondId.ToString(CultureInfo.InvariantCulture);
            string text =
                lastCouponCents > 0
                    ? Strings.Format(
                        "notice.bond.maturedCoupon",
                        id,
                        BondText.WholeMoney(faceCents),
                        BondText.Money(lastCouponCents)
                    )
                    : Strings.Format("notice.bond.matured", id, BondText.WholeMoney(faceCents));
            return new Notice
            {
                Kind = NoticeKind.BondMatured,
                Sender = Treasury,
                Text = Tagged(text, "notice.tag.bonds"),
            };
        }

        /// <summary>"Your Index shares (120) are back. They were kept safe while Finance Overhaul was off."</summary>
        public static Notice IndexRestored(long shares)
        {
            return new Notice
            {
                Kind = NoticeKind.IndexRestored,
                Sender = Brokerage,
                Text = Tagged(
                    Strings.Format(
                        "notice.index.restored",
                        shares.ToString("#,0", CultureInfo.InvariantCulture)
                    ),
                    "notice.tag.orders"
                ),
                Symbol = "IDX",
            };
        }

        public static Notice BondCoupon(int bondId, long couponCents, int left, int periods)
        {
            string text = Strings.Format(
                "notice.bond.coupon",
                bondId.ToString(CultureInfo.InvariantCulture),
                BondText.Money(couponCents),
                left.ToString(CultureInfo.InvariantCulture),
                periods.ToString(CultureInfo.InvariantCulture)
            );
            return new Notice
            {
                Kind = NoticeKind.BondCoupon,
                Sender = Treasury,
                Text = Tagged(text, "notice.tag.bonds"),
            };
        }

        public static string Title(OrderKind kind)
        {
            return Strings.Get("notice.title." + OrderText.Key(kind));
        }

        /// <summary>A notice's sentence followed by its hashtag.</summary>
        internal static string Tagged(string text, string tagId)
        {
            return Strings.Format("notice.tagged", text, Strings.Get(tagId));
        }

        private static Notice Order(NoticeKind kind, string symbol, string text)
        {
            return new Notice
            {
                Kind = kind,
                Sender = Brokerage,
                Text = Tagged(text, "notice.tag.orders"),
                Symbol = symbol,
            };
        }

        private static string Count(uint n)
        {
            return n.ToString("N0", CultureInfo.InvariantCulture);
        }

        private static string Whole(long v)
        {
            return "₡" + v.ToString("N0", CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// Keeps Chirper from flooding: more than <see cref="MaxOrderNotices"/> order notices from one
    /// game day become one summary chirp ("4 order updates today: 3 filled (IDX, GGI, Oil), 1
    /// expired (TJL)…"); bond notices pass through unchanged, in order. See NoticesTests.
    /// </summary>
    public static class NoticeBatch
    {
        public const int MaxOrderNotices = 3;

        public static List<Notice> Collapse(IList<Notice> day)
        {
            var result = new List<Notice>();
            if (day == null)
            {
                return result;
            }

            var orders = new List<Notice>();
            foreach (Notice n in day)
            {
                if (n == null)
                {
                    continue;
                }

                if (n.IsOrder)
                {
                    orders.Add(n);
                }
                else
                {
                    result.Add(n);
                }
            }

            if (orders.Count <= MaxOrderNotices)
            {
                result.InsertRange(0, orders);
                return result;
            }

            result.Insert(0, Summary(orders));
            return result;
        }

        private static Notice Summary(List<Notice> orders)
        {
            var parts = new List<string>();
            foreach (
                KeyValuePair<NoticeKind, string> part in new[]
                {
                    new KeyValuePair<NoticeKind, string>(
                        NoticeKind.Filled,
                        "notice.summary.filled"
                    ),
                    new KeyValuePair<NoticeKind, string>(
                        NoticeKind.Expired,
                        "notice.summary.expired"
                    ),
                    new KeyValuePair<NoticeKind, string>(
                        NoticeKind.Cancelled,
                        "notice.summary.cancelled"
                    ),
                }
            )
            {
                var symbols = new List<string>();
                foreach (Notice n in orders)
                {
                    if (n.Kind == part.Key)
                    {
                        symbols.Add(n.Symbol ?? "?");
                    }
                }

                if (symbols.Count > 0)
                {
                    parts.Add(
                        Strings.Format(
                            part.Value,
                            symbols.Count.ToString(CultureInfo.InvariantCulture),
                            Strings.List(symbols)
                        )
                    );
                }
            }

            string text = Strings.Format(
                "notice.summary",
                orders.Count.ToString(CultureInfo.InvariantCulture),
                Strings.List(parts)
            );
            return new Notice
            {
                Kind = NoticeKind.Summary,
                Sender = NoticeText.Brokerage,
                Text = NoticeText.Tagged(text, "notice.tag.orders"),
            };
        }
    }
}

using System; // Math, MidpointRounding
using System.Collections.Generic; // IList, KeyValuePair, List
using System.Globalization; // CultureInfo

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

        /// <summary>A local company was announced and its offering window is open.</summary>
        IpoAnnounced,

        /// <summary>A local company's first trading day: where it opened.</summary>
        IpoFirstDay,

        /// <summary>A local company was bought out and left the exchange.</summary>
        IpoBoughtOut,

        /// <summary>A local company lost its headquarters and moved to another building.</summary>
        IpoMoved,

        /// <summary>A local company lost its headquarters with nowhere to move: it closes after a grace period.</summary>
        IpoHomeLost,
        IpoHomeRestored,

        /// <summary>A local company closed: the city was paid its last price.</summary>
        IpoDefunct,
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
        /// <summary>"Westdale Software is going public at ₡52 a share. The city can buy at the offering price for 7 days. #ipo"</summary>
        public static Notice IpoAnnounced(string company, long offerPriceCents, int offerDays)
        {
            return Ipo(
                NoticeKind.IpoAnnounced,
                Strings.Plural(
                    "notice.ipo.announced",
                    offerDays,
                    company,
                    BondText.WholeMoney(offerPriceCents),
                    offerDays.ToString(CultureInfo.InvariantCulture)
                )
            );
        }

        /// <summary>"Westdale Software opened at ₡60 on its first trading day, 15% above the offering price. #ipo"</summary>
        public static Notice IpoFirstDay(string company, long offerPriceCents, long firstPriceCents)
        {
            string price = BondText.WholeMoney(firstPriceCents);
            if (firstPriceCents == offerPriceCents || offerPriceCents <= 0)
            {
                return Ipo(
                    NoticeKind.IpoFirstDay,
                    Strings.Format("notice.ipo.firstDay.flat", company, price)
                );
            }

            decimal change = Math.Abs(firstPriceCents - offerPriceCents) * 100m / offerPriceCents;
            return Ipo(
                NoticeKind.IpoFirstDay,
                Strings.Format(
                    firstPriceCents > offerPriceCents
                        ? "notice.ipo.firstDay.up"
                        : "notice.ipo.firstDay.down",
                    company,
                    price,
                    Math.Round(change, MidpointRounding.AwayFromZero)
                        .ToString("0", CultureInfo.InvariantCulture)
                )
            );
        }

        /// <summary>
        /// "Westdale Software was bought out at ₡73 a share, 40% above its price. The city
        /// received ₡73,000 for its 1,000 shares. #ipo" — or, with no shares, that it held none.
        /// </summary>
        public static Notice IpoBoughtOut(
            string company,
            long pricePerShareCents,
            double premium,
            uint shares,
            long paidCents
        )
        {
            string price = BondText.WholeMoney(pricePerShareCents);
            string above = Math.Round(premium * 100d, MidpointRounding.AwayFromZero)
                .ToString("0", CultureInfo.InvariantCulture);
            return Ipo(
                NoticeKind.IpoBoughtOut,
                shares == 0
                    ? Strings.Format("notice.ipo.boughtOut.none", company, price, above)
                    : Strings.Plural(
                        "notice.ipo.boughtOut",
                        shares,
                        company,
                        price,
                        above,
                        BondText.WholeMoney(paidCents),
                        Count(shares)
                    )
            );
        }

        /// <summary>A company lost its headquarters and moved to another building of its kind.</summary>
        public static Notice IpoMoved(string company, string newHome)
        {
            return Ipo(NoticeKind.IpoMoved, Strings.Format("notice.ipo.moved", company, newHome));
        }

        /// <summary>A company in its grace period has its headquarters back (switched on again, or rebuilt).</summary>
        public static Notice IpoHomeRestored(string company)
        {
            return Ipo(
                NoticeKind.IpoHomeRestored,
                Strings.Format("notice.ipo.homeRestored", company)
            );
        }

        /// <summary>A company lost its headquarters and found none to move to.</summary>
        public static Notice IpoHomeLost(string company, int graceDays)
        {
            return Ipo(
                NoticeKind.IpoHomeLost,
                Strings.Plural(
                    "notice.ipo.homeLost",
                    (uint)Math.Max(0, graceDays),
                    company,
                    Count((uint)Math.Max(0, graceDays))
                )
            );
        }

        /// <summary>A company closed at the end of its grace period; the city was paid its last price.</summary>
        public static Notice IpoDefunct(
            string company,
            long pricePerShareCents,
            uint shares,
            long paidCents
        )
        {
            string price = BondText.WholeMoney(pricePerShareCents);
            return Ipo(
                NoticeKind.IpoDefunct,
                shares == 0
                    ? Strings.Format("notice.ipo.defunct.none", company, price)
                    : Strings.Plural(
                        "notice.ipo.defunct",
                        shares,
                        company,
                        price,
                        BondText.WholeMoney(paidCents),
                        Count(shares)
                    )
            );
        }

        private static Notice Ipo(NoticeKind kind, string text)
        {
            return new Notice
            {
                Kind = kind,
                Sender = Brokerage,
                Text = Tagged(text, "notice.tag.ipo"),
            };
        }

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

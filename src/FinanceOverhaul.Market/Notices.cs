using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace FinanceOverhaul.Market
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
        public const string Brokerage = "City Brokerage";
        public const string Treasury = "City Treasury";
        public const string OrdersTag = " #orders";
        public const string BondsTag = " #citybonds";

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
            bool sell = kind != OrderKind.LimitBuy;
            string text =
                Title(kind)
                + " filled: "
                + (sell ? "sold " : "bought ")
                + Count(shares)
                + " "
                + symbol
                + " at "
                + StockMonitor.PriceText(price)
                + " ("
                + OrderText.TriggerWord(kind)
                + " "
                + Whole(trigger)
                + ") for "
                + Whole(amount)
                + "."
                + OrdersTag;
            return Order(NoticeKind.Filled, symbol, text);
        }

        public static Notice Expired(OrderKind kind, uint shares, string symbol, int lifetimeDays)
        {
            string text =
                Title(kind)
                + " for "
                + Count(shares)
                + " "
                + symbol
                + " expired after "
                + lifetimeDays
                + (lifetimeDays == 1 ? " day" : " days")
                + " without filling."
                + OrdersTag;
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
                why = "not enough cash" + (cost > 0 ? " (needs " + Whole(cost) + ")" : "");
            }
            else if (reason == OrderRules.ReasonShareLimit)
            {
                why = "the exchange has no more shares to sell";
            }
            else if (reason == OrderRules.ReasonNoShares)
            {
                why = "the city no longer holds any " + symbol;
            }
            else
            {
                why = reason ?? "cancelled";
            }

            string text =
                Title(kind)
                + " for "
                + Count(shares)
                + " "
                + symbol
                + " cancelled: "
                + why
                + "."
                + OrdersTag;
            return Order(NoticeKind.Cancelled, symbol, text);
        }

        public static Notice BondMatured(int bondId, long faceCents, long lastCouponCents)
        {
            string text =
                "Bond #"
                + bondId
                + " repaid at maturity: "
                + BondText.WholeMoney(faceCents)
                + (
                    lastCouponCents > 0
                        ? " plus the last " + BondText.Money(lastCouponCents) + " coupon"
                        : ""
                )
                + "."
                + BondsTag;
            return new Notice
            {
                Kind = NoticeKind.BondMatured,
                Sender = Treasury,
                Text = text,
            };
        }

        public static Notice BondCoupon(int bondId, long couponCents, int left, int periods)
        {
            string text =
                "Bond #"
                + bondId
                + " coupon paid: "
                + BondText.Money(couponCents)
                + " ("
                + left
                + " of "
                + periods
                + " left)."
                + BondsTag;
            return new Notice
            {
                Kind = NoticeKind.BondCoupon,
                Sender = Treasury,
                Text = text,
            };
        }

        public static string Title(OrderKind kind)
        {
            switch (kind)
            {
                case OrderKind.StopLossSell:
                    return "Stop-loss";
                case OrderKind.TakeProfitSell:
                    return "Take-profit";
                case OrderKind.TrailingStopSell:
                    return "Trailing stop";
                default:
                    return "Limit buy";
            }
        }

        private static Notice Order(NoticeKind kind, string symbol, string text)
        {
            return new Notice
            {
                Kind = kind,
                Sender = Brokerage,
                Text = text,
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
            var sb = new StringBuilder();
            sb.Append(orders.Count).Append(" order updates today: ");
            bool first = true;
            foreach (
                KeyValuePair<NoticeKind, string> part in new[]
                {
                    new KeyValuePair<NoticeKind, string>(NoticeKind.Filled, "filled"),
                    new KeyValuePair<NoticeKind, string>(NoticeKind.Expired, "expired"),
                    new KeyValuePair<NoticeKind, string>(NoticeKind.Cancelled, "cancelled"),
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

                if (symbols.Count == 0)
                {
                    continue;
                }

                if (!first)
                {
                    sb.Append(", ");
                }

                first = false;
                sb.Append(symbols.Count)
                    .Append(' ')
                    .Append(part.Value)
                    .Append(" (")
                    .Append(string.Join(", ", symbols.ToArray()))
                    .Append(')');
            }

            sb.Append(". Details in Economy → Investments.").Append(NoticeText.OrdersTag);
            return new Notice
            {
                Kind = NoticeKind.Summary,
                Sender = NoticeText.Brokerage,
                Text = sb.ToString(),
            };
        }
    }
}

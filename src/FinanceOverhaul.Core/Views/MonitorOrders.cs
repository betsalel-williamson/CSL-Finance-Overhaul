using System;
using System.Collections.Generic;
using System.Globalization;

namespace FinanceOverhaul.Core
{
    /// <summary>
    /// Order of the Stock Monitor's Orders tab. Its rows are grouped by stock, so the list
    /// sorts (change, price, value held) do not apply: they are stock numbers that say nothing
    /// about an order. All three run ascending by nature; the direction button flips them.
    /// </summary>
    public enum OrdersSort
    {
        /// <summary>The order closest to its trigger first (as % of today's price).</summary>
        Nearest,

        /// <summary>The order with the fewest days left first.</summary>
        Expires,

        /// <summary>Stock groups A→Z; lines inside a group stay nearest first.</summary>
        Symbol,
    }

    /// <summary>One open order as a line on the Orders tab.</summary>
    public sealed class MonitorOrderLine
    {
        public int OrderId;
        public OrderKind Kind;
        public bool IsSell;

        /// <summary>The order's type is switched off in Options: it waits, untouched.</summary>
        public bool Paused;

        /// <summary>"BUY · Limit", "SELL · Stop-loss · paused in Options".</summary>
        public string Type;

        /// <summary>The Your Portfolio card's wording, e.g. "3 shares at or below ₡60 · 89 days left".</summary>
        public string Detail;

        /// <summary>"₡6 away · 9.1%"; null when paused or the price is unknown.</summary>
        public string Distance;

        /// <summary>Compact rows: the whole order on one line.</summary>
        public string Compact;

        /// <summary>Compact rows: "₡6 away"; null when paused or the price is unknown.</summary>
        public string CompactDistance;

        /// <summary>Distance to the trigger as % of today's price; NaN when unknown.</summary>
        public double DistancePercent = double.NaN;
        public int DaysLeft;
    }

    /// <summary>A stock and its open orders, in the order the tab shows them.</summary>
    public sealed class MonitorOrderGroup
    {
        public MonitorQuote Quote;
        public readonly List<MonitorOrderLine> Lines = new List<MonitorOrderLine>();
    }

    /// <summary>Grouping, ordering and wording for the Stock Monitor's Orders tab (Cities-free).</summary>
    public static class MonitorOrders
    {
        public static readonly OrdersSort[] SortCycle =
        {
            OrdersSort.Nearest,
            OrdersSort.Expires,
            OrdersSort.Symbol,
        };

        public static OrdersSort NextSort(OrdersSort sort)
        {
            int i = Array.IndexOf(SortCycle, sort);
            return SortCycle[(i + 1) % SortCycle.Length];
        }

        public static string SortLabel(OrdersSort sort)
        {
            switch (sort)
            {
                case OrdersSort.Expires:
                    return Strings.Get("monitor.sort.expires");
                case OrdersSort.Symbol:
                    return Strings.Get("monitor.sort.symbol");
                default:
                    return Strings.Get("monitor.sort.nearest");
            }
        }

        /// <summary>The arrow points down when the list runs farthest, latest or Z first.</summary>
        public static bool IsDescending(bool reversed)
        {
            return reversed;
        }

        /// <summary>Tooltip for the direction button, e.g. "Nearest first (click to flip)".</summary>
        public static string DirectionText(OrdersSort sort, bool reversed)
        {
            string what;
            switch (sort)
            {
                case OrdersSort.Expires:
                    what = reversed ? "monitor.direction.latest" : "monitor.direction.soonest";
                    break;
                case OrdersSort.Symbol:
                    what = reversed ? "monitor.direction.za" : "monitor.direction.az";
                    break;
                default:
                    what = reversed ? "monitor.direction.farthest" : "monitor.direction.nearest";
                    break;
            }

            return Strings.Format("monitor.direction.flip", Strings.Get(what));
        }

        /// <summary>"Orders (6)" on the tab; plain "Orders" when there are none.</summary>
        public static string TabLabel(int openOrders)
        {
            return openOrders > 0
                ? Strings.Format(
                    "monitor.filter.ordersCount",
                    openOrders.ToString(CultureInfo.InvariantCulture)
                )
                : Strings.Get("monitor.filter.orders");
        }

        /// <summary>
        /// How far today's price is from the trigger, as % of today's price, so a ₡66 stock and
        /// a ₡54,191 one compare fairly. NaN when the price is unknown or not positive.
        /// </summary>
        public static double DistancePercent(float price, int trigger)
        {
            if (float.IsNaN(price) || float.IsInfinity(price) || price <= 0f)
            {
                return double.NaN;
            }

            return Math.Abs((double)price - trigger) / price * 100.0;
        }

        /// <summary>
        /// "₡6 away": whole ₡, or cents when the gap is under ₡1 so it is never "₡0 away".
        /// Null when unknown.
        /// </summary>
        public static string DistanceAmountText(float price, int trigger)
        {
            if (double.IsNaN(DistancePercent(price, trigger)))
            {
                return null;
            }

            double gap = Math.Abs((double)price - trigger);
            if (AtTrigger(gap))
            {
                return Strings.Get("orders.atTrigger");
            }

            string digits =
                Math.Round(gap) < 1.0
                    ? Math.Max(0.01, Math.Round(gap, 2))
                        .ToString("0.00", CultureInfo.InvariantCulture)
                    : Math.Round(gap).ToString("N0", CultureInfo.InvariantCulture);
            return Strings.Format("orders.away", digits);
        }

        /// <summary>"₡6 away · 9.1%". Null when unknown.</summary>
        public static string DistanceText(float price, int trigger)
        {
            string amount = DistanceAmountText(price, trigger);
            if (amount == null || AtTrigger(Math.Abs((double)price - trigger)))
            {
                return amount;
            }

            double percent = DistancePercent(price, trigger);
            return Strings.Format(
                "orders.awayPercent",
                amount,
                percent.ToString("0.0", CultureInfo.InvariantCulture)
            );
        }

        private static bool AtTrigger(double gap)
        {
            return Math.Round(gap, 2) < 0.005;
        }

        /// <summary>"BUY · Limit", "SELL · Trailing stop", with " · paused in Options" when paused.</summary>
        public static string TypeText(OrderKind kind, bool paused)
        {
            string id;
            switch (kind)
            {
                case OrderKind.StopLossSell:
                    id = "orders.type.stopLoss";
                    break;
                case OrderKind.TakeProfitSell:
                    id = "orders.type.takeProfit";
                    break;
                case OrderKind.TrailingStopSell:
                    id = "orders.type.trailingStop";
                    break;
                default:
                    id = "orders.type.limitBuy";
                    break;
            }

            string type = Strings.Get(id);
            return paused ? Strings.Format("orders.paused", type) : type;
        }

        /// <summary>
        /// Compact rows: the order on one line, e.g. "BUY ≤ ₡900 · 50 shares · 89 days left",
        /// "SELL, 10% trail · stop ₡48,771 · 25 shares · 365 days left".
        /// </summary>
        public static string CompactText(TradeOrder order, int today, bool paused)
        {
            string price = order.TriggerPrice.ToString("N0", CultureInfo.InvariantCulture);
            string what;
            switch (order.Kind)
            {
                case OrderKind.StopLossSell:
                    what = Strings.Format("orders.short.stopLoss", price);
                    break;
                case OrderKind.TakeProfitSell:
                    what = Strings.Format("orders.short.takeProfit", price);
                    break;
                case OrderKind.TrailingStopSell:
                    what = Strings.Format(
                        "orders.compact.trailingStop",
                        order.TrailPercent.ToString(CultureInfo.InvariantCulture),
                        price
                    );
                    break;
                default:
                    what = Strings.Format("orders.short.limitBuy", price);
                    break;
            }

            int left = Math.Max(0, order.ExpiresDay - today);
            string line = Strings.Format(
                "orders.compact",
                what,
                Strings.Plural(
                    "shares",
                    order.Shares,
                    order.Shares.ToString("N0", CultureInfo.InvariantCulture)
                ),
                Strings.Plural("daysLeft", left, left.ToString(CultureInfo.InvariantCulture))
            );
            return paused ? Strings.Format("orders.paused", line) : line;
        }

        /// <summary>One order as the tab shows it, against today's price of its stock.</summary>
        public static MonitorOrderLine Line(TradeOrder order, float price, int today, bool paused)
        {
            var line = new MonitorOrderLine
            {
                OrderId = order.Id,
                Kind = order.Kind,
                IsSell = order.IsSell,
                Paused = paused,
                Type = TypeText(order.Kind, paused),
                Detail = OrderText.CardDetail(order, today),
                Compact = CompactText(order, today, paused),
                DaysLeft = Math.Max(0, order.ExpiresDay - today),
            };
            if (!paused)
            {
                // A paused order cannot trigger, so it has no distance to show or sort by.
                line.DistancePercent = DistancePercent(price, order.TriggerPrice);
                line.Distance = DistanceText(price, order.TriggerPrice);
                line.CompactDistance = DistanceAmountText(price, order.TriggerPrice);
            }

            return line;
        }

        /// <summary>
        /// The Orders tab: every shown stock that has open orders, with a line per order.
        /// The sort key orders the groups (each ranked by its best active order) and the lines
        /// inside them; with <see cref="OrdersSort.Symbol"/> the lines stay nearest first.
        /// Paused orders go last in their group, and a group with only paused orders goes to
        /// the end, whichever way the list runs. Ties: symbol, then the order placed first.
        /// </summary>
        /// <param name="hidden">Prefab names the player chose not to show (may be null).</param>
        /// <param name="enabled">Whether an order type is switched on in Options (null: all on).</param>
        public static List<MonitorOrderGroup> Groups(
            IEnumerable<MonitorQuote> quotes,
            IEnumerable<TradeOrder> orders,
            int today,
            Func<OrderKind, bool> enabled,
            ICollection<string> hidden,
            OrdersSort sort,
            bool reversed
        )
        {
            var groups = new List<MonitorOrderGroup>();
            if (quotes == null || orders == null)
            {
                return groups;
            }

            var byPrefab = new Dictionary<string, MonitorOrderGroup>(StringComparer.Ordinal);
            foreach (MonitorQuote q in quotes)
            {
                if (
                    q == null
                    || q.Prefab == null
                    || (hidden != null && hidden.Contains(q.Prefab))
                    || byPrefab.ContainsKey(q.Prefab)
                )
                {
                    continue;
                }

                byPrefab[q.Prefab] = new MonitorOrderGroup { Quote = q };
            }

            foreach (TradeOrder o in orders)
            {
                MonitorOrderGroup g;
                if (
                    o == null
                    || o.InvestmentKey == null
                    || !byPrefab.TryGetValue(o.InvestmentKey, out g)
                )
                {
                    continue;
                }

                bool paused = enabled != null && !enabled(o.Kind);
                g.Lines.Add(Line(o, g.Quote.Price, today, paused));
            }

            // Symbol orders the groups only: its lines keep the default key, never flipped.
            OrdersSort lineKey = sort == OrdersSort.Symbol ? OrdersSort.Nearest : sort;
            bool linesReversed = sort != OrdersSort.Symbol && reversed;
            foreach (MonitorOrderGroup g in byPrefab.Values)
            {
                if (g.Lines.Count == 0)
                {
                    continue;
                }

                g.Lines.Sort((a, b) => CompareLines(a, b, lineKey, linesReversed));
                groups.Add(g);
            }

            groups.Sort((a, b) => CompareGroups(a, b, sort, reversed));
            return groups;
        }

        /// <summary>Open orders on shown stocks: the number on the tab.</summary>
        public static int Count(List<MonitorOrderGroup> groups)
        {
            int n = 0;
            if (groups != null)
            {
                foreach (MonitorOrderGroup g in groups)
                {
                    n += g.Lines.Count;
                }
            }

            return n;
        }

        private static double Key(MonitorOrderLine line, OrdersSort sort)
        {
            return sort == OrdersSort.Expires ? line.DaysLeft : line.DistancePercent;
        }

        /// <summary>Sorts last: a paused order, or one whose distance is unknown.</summary>
        private static bool Unranked(MonitorOrderLine line, OrdersSort sort)
        {
            return line.Paused || double.IsNaN(Key(line, sort));
        }

        private static int CompareLines(
            MonitorOrderLine a,
            MonitorOrderLine b,
            OrdersSort sort,
            bool reversed
        )
        {
            bool ua = Unranked(a, sort),
                ub = Unranked(b, sort);
            if (ua != ub)
            {
                return ua ? 1 : -1;
            }

            int c = ua ? 0 : Key(a, sort).CompareTo(Key(b, sort));
            if (c != 0)
            {
                return reversed ? -c : c;
            }

            return a.OrderId.CompareTo(b.OrderId);
        }

        /// <summary>A group's rank under an order key: its best active order's value.</summary>
        private static double GroupKey(MonitorOrderGroup g, OrdersSort sort)
        {
            double best = double.NaN;
            foreach (MonitorOrderLine line in g.Lines)
            {
                if (Unranked(line, sort))
                {
                    continue;
                }

                double k = Key(line, sort);
                if (double.IsNaN(best) || k < best)
                {
                    best = k;
                }
            }

            return best;
        }

        private static int CompareGroups(
            MonitorOrderGroup a,
            MonitorOrderGroup b,
            OrdersSort sort,
            bool reversed
        )
        {
            int bySymbol = string.CompareOrdinal(a.Quote.Symbol ?? "", b.Quote.Symbol ?? "");
            if (sort == OrdersSort.Symbol)
            {
                return reversed ? -bySymbol : bySymbol;
            }

            double ka = GroupKey(a, sort),
                kb = GroupKey(b, sort);
            bool ua = double.IsNaN(ka),
                ub = double.IsNaN(kb);
            if (ua != ub)
            {
                return ua ? 1 : -1; // nothing active in the group: after every ranked group
            }

            int c = ua ? 0 : ka.CompareTo(kb);
            return c != 0 ? (reversed ? -c : c) : bySymbol;
        }
    }
}

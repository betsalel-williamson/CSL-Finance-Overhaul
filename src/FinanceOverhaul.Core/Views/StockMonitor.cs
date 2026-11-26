using System; // Array, DateTime, Math
using System.Collections.Generic; // ICollection, IEnumerable, List
using System.Globalization; // CultureInfo

namespace FinanceOverhaul.Core
{
    /// <summary>Which stocks the Stock Monitor lists.</summary>
    public enum MonitorFilter
    {
        All,

        /// <summary>Stocks the city holds.</summary>
        Portfolio,

        /// <summary>Stocks with open orders.</summary>
        Orders,
    }

    /// <summary>Row order in the Stock Monitor.</summary>
    public enum MonitorSort
    {
        /// <summary>Biggest gain first.</summary>
        Change,
        Symbol,

        /// <summary>Highest price first.</summary>
        Price,

        /// <summary>Largest holding (price × shares) first.</summary>
        ValueHeld,
    }

    /// <summary>One listed stock as the Stock Monitor shows it.</summary>
    public sealed class MonitorQuote
    {
        public string Symbol;
        public string Name;

        /// <summary>Prefab name: stable key for the player's "stocks shown" choice.</summary>
        public string Prefab;

        /// <summary>Today's value per share.</summary>
        public float Price;

        /// <summary>The game's change rate in percent (as the Economy panel and the LED show it).</summary>
        public float ChangeRate;

        /// <summary>The game's daily prices, oldest first (at most 30).</summary>
        public float[] History;
        public uint Owned;
        public int OpenOrders;

        /// <summary>Game investment id (host use only).</summary>
        public int Id;

        /// <summary>What the city paid for the shares it holds (the game's "You invested").</summary>
        public long Spent;

        /// <summary>A local company the city grew (Local IPOs), not one of the game's stocks.</summary>
        public bool Local;

        /// <summary>Not yet on sale: the game's requirement is not met.</summary>
        public bool Locked;

        /// <summary>Towards the requirement, 0 to 1 (locked stocks).</summary>
        public float UnlockProgress;

        /// <summary>The game's requirement text, e.g. "64 / 200" (locked stocks).</summary>
        public string UnlockText;

        public double ValueHeld
        {
            get { return (double)Price * Owned; }
        }
    }

    /// <summary>Filtering, ordering and wording for the Stock Monitor list (Cities-free).</summary>
    public static class StockMonitor
    {
        public static readonly MonitorSort[] SortCycle =
        {
            MonitorSort.Change,
            MonitorSort.Symbol,
            MonitorSort.Price,
            MonitorSort.ValueHeld,
        };

        public static List<MonitorQuote> Rows(
            IEnumerable<MonitorQuote> quotes,
            MonitorFilter filter,
            MonitorSort sort
        )
        {
            return Rows(quotes, filter, sort, null);
        }

        public static List<MonitorQuote> Rows(
            IEnumerable<MonitorQuote> quotes,
            MonitorFilter filter,
            MonitorSort sort,
            ICollection<string> hidden
        )
        {
            return Rows(quotes, filter, sort, hidden, false);
        }

        public static List<MonitorQuote> Rows(
            IEnumerable<MonitorQuote> quotes,
            MonitorFilter filter,
            MonitorSort sort,
            ICollection<string> hidden,
            bool reversed
        )
        {
            return Rows(quotes, filter, sort, hidden, reversed, false);
        }

        /// <param name="hidden">Prefab names the player chose not to show (may be null).</param>
        /// <param name="reversed">Flip the sort's natural direction (the ▲/▼ button).</param>
        /// <param name="changeInCurrency">The pills show ₡: sort Change by that ₡ amount.</param>
        public static List<MonitorQuote> Rows(
            IEnumerable<MonitorQuote> quotes,
            MonitorFilter filter,
            MonitorSort sort,
            ICollection<string> hidden,
            bool reversed,
            bool changeInCurrency
        )
        {
            var rows = new List<MonitorQuote>();
            if (quotes == null)
            {
                return rows;
            }

            foreach (MonitorQuote q in quotes)
            {
                if (q == null || (hidden != null && q.Prefab != null && hidden.Contains(q.Prefab)))
                {
                    continue;
                }

                if (filter == MonitorFilter.Portfolio && q.Owned == 0)
                {
                    continue;
                }

                if (filter == MonitorFilter.Orders && q.OpenOrders <= 0)
                {
                    continue;
                }

                rows.Add(q);
            }

            rows.Sort((a, b) => Compare(a, b, sort, reversed, changeInCurrency));
            return rows;
        }

        /// <summary>
        /// Whether the list runs highest (or Z) first: Change, Price and Value held naturally do,
        /// Symbol naturally runs A→Z; the direction button flips it.
        /// </summary>
        public static bool IsDescending(MonitorSort sort, bool reversed)
        {
            return (sort != MonitorSort.Symbol) != reversed;
        }

        /// <summary>Tooltip for the direction button, e.g. "Highest first (click to flip)".</summary>
        public static string DirectionText(MonitorSort sort, bool reversed)
        {
            bool desc = IsDescending(sort, reversed);
            string what =
                sort == MonitorSort.Symbol
                    ? (desc ? "monitor.direction.za" : "monitor.direction.az")
                : desc ? "monitor.direction.highest"
                : "monitor.direction.lowest";
            return Strings.Format("monitor.direction.flip", Strings.Get(what));
        }

        private static int Compare(
            MonitorQuote a,
            MonitorQuote b,
            MonitorSort sort,
            bool reversed,
            bool changeInCurrency
        )
        {
            int c;
            switch (sort)
            {
                case MonitorSort.Change when changeInCurrency:
                    c = ChangeAmount(b.Price, b.ChangeRate)
                        .CompareTo(ChangeAmount(a.Price, a.ChangeRate));
                    break;
                case MonitorSort.Change:
                    c = Finite(b.ChangeRate).CompareTo(Finite(a.ChangeRate));
                    break;
                case MonitorSort.Price:
                    c = Finite(b.Price).CompareTo(Finite(a.Price));
                    break;
                case MonitorSort.ValueHeld:
                    c = b.ValueHeld.CompareTo(a.ValueHeld);
                    break;
                default:
                    c = 0;
                    break;
            }

            int bySymbol = string.CompareOrdinal(a.Symbol ?? "", b.Symbol ?? "");
            if (sort == MonitorSort.Symbol)
            {
                return reversed ? -bySymbol : bySymbol;
            }

            // Ties stay A→Z whichever way the main key runs.
            return c != 0 ? (reversed ? -c : c) : bySymbol;
        }

        private static float Finite(float v)
        {
            return float.IsNaN(v) || float.IsInfinity(v) ? float.MinValue : v;
        }

        public static MonitorSort NextSort(MonitorSort sort)
        {
            int i = Array.IndexOf(SortCycle, sort);
            return SortCycle[(i + 1) % SortCycle.Length];
        }

        public static string FilterLabel(MonitorFilter filter)
        {
            switch (filter)
            {
                case MonitorFilter.Portfolio:
                    return Strings.Get("monitor.filter.portfolio");
                case MonitorFilter.Orders:
                    return Strings.Get("monitor.filter.orders");
                default:
                    return Strings.Get("monitor.filter.all");
            }
        }

        /// <summary>
        /// The label, naming the unit the Change sort follows: "Change %" with % pills,
        /// "Change ₡" with ₡ pills.
        /// </summary>
        public static string SortLabel(MonitorSort sort, bool changeInCurrency)
        {
            return sort == MonitorSort.Change && changeInCurrency
                ? Strings.Get("monitor.sort.changeCurrency")
                : SortLabel(sort);
        }

        /// <summary>
        /// The ₡ change the ₡ pill shows, unrounded: <c>price × r / (1 + r)</c>; 0 when the %
        /// pill shows 0.00%; the lowest value when unknown (sorted last).
        /// </summary>
        public static double ChangeAmount(float price, float rate)
        {
            if (
                float.IsNaN(price)
                || float.IsInfinity(price)
                || float.IsNaN(rate)
                || float.IsInfinity(rate)
                || rate <= -100f
            )
            {
                return double.MinValue;
            }

            if (ChangeColour(rate) == LedText.Flat)
            {
                return 0d;
            }

            double r = rate / 100.0;
            return price * r / (1.0 + r);
        }

        public static string SortLabel(MonitorSort sort)
        {
            switch (sort)
            {
                case MonitorSort.Symbol:
                    return Strings.Get("monitor.sort.symbol");
                case MonitorSort.Price:
                    return Strings.Get("monitor.sort.price");
                case MonitorSort.ValueHeld:
                    return Strings.Get("monitor.sort.valueHeld");
                default:
                    return Strings.Get("monitor.sort.change");
            }
        }

        /// <summary>Whole ₡ like the game's "Value", e.g. "₡54,191"; "--" when unknown.</summary>
        public static string PriceText(float price)
        {
            if (float.IsNaN(price) || float.IsInfinity(price))
            {
                return "--";
            }

            return "₡" + Math.Round(price).ToString("N0", CultureInfo.InvariantCulture);
        }

        /// <summary>The change pill: the LED's two-decimal rate plus "%", e.g. "+1.07%", "0.00%".</summary>
        public static string ChangeText(float rate)
        {
            string s = TickerLine.FormatRate(rate);
            return s == "--" ? s : s + "%";
        }

        /// <summary>
        /// The pill in ₡ mode: the same change as the % pill (the game's change rate), in ₡ —
        /// <c>price × r / (1 + r)</c>, i.e. today's price minus the price the rate compares it
        /// with. Always the same sign as the % pill; under ₡10 it shows cents so a small move is
        /// never rounded to ₡0 ("−₡0.20"). "--" when unknown.
        /// </summary>
        public static string ChangeAmountFromRate(float price, float rate)
        {
            // The one ₡-change rule (ChangeAmount) — pills, detail header, LED, sort all use it.
            double amount = ChangeAmount(price, rate);
            if (amount == double.MinValue)
            {
                return "--";
            }

            if (amount == 0d)
            {
                return "₡0"; // the % pill shows 0.00%: same here
            }

            // Cents up to ₡9.99, whole ₡ from ₡10 — decided on the rounded value, so 9.996 shows
            // "₡10", never "₡10.00".
            double cents = Math.Max(0.01, Math.Round(Math.Abs(amount), 2));
            string digits =
                cents < 10.0
                    ? cents.ToString("0.00", CultureInfo.InvariantCulture)
                    : Math.Round(Math.Abs(amount)).ToString("N0", CultureInfo.InvariantCulture);
            return (rate > 0 ? "+₡" : "−₡") + digits;
        }

        /// <summary>Opacity as the popover shows it, e.g. "35%".</summary>
        public static string PercentText(float fraction)
        {
            return Math.Round(fraction * 100).ToString(CultureInfo.InvariantCulture) + "%";
        }

        /// <summary>Slider value from a position along the track: 0–90% in 5% steps.</summary>
        public static float OpacityAt(float fraction)
        {
            float f = Math.Max(0f, Math.Min(1f, float.IsNaN(fraction) ? 0f : fraction));
            return (float)(Math.Round(f * StockMonitorSettings.MaxOpacity * 20) / 20.0);
        }

        /// <summary>Pill colour, decided on the rounded value like the LED (0.00 is flat grey).</summary>
        public static byte ChangeColour(float rate)
        {
            return LedText.RateColour(rate);
        }

        /// <summary>"1 order" / "2 orders", or null when there are none.</summary>
        public static string OrdersBadge(int openOrders)
        {
            if (openOrders <= 0)
            {
                return null;
            }

            return Strings.Plural(
                "monitor.badge",
                openOrders,
                openOrders.ToString(CultureInfo.InvariantCulture)
            );
        }

        /// <summary>Footer, e.g. "Daily close · 29 Oct 2077" (prices move once per game day).</summary>
        public static string Footer(DateTime gameDate)
        {
            return Strings.Format(
                "monitor.footer",
                gameDate.ToString(Strings.Get("monitor.footer.date"), CultureInfo.InvariantCulture)
            );
        }

        /// <summary>
        /// Hover bubble for point <paramref name="index"/> of a range of <paramref name="count"/>
        /// daily prices ending today: "Oil · 8 days ago · ₡925", "Oil · yesterday · ₡932",
        /// "Oil · today · ₡942".
        /// </summary>
        public static string HoverText(string symbol, int index, int count, float price)
        {
            int ago = Math.Max(0, count - 1 - index);
            string when =
                ago == 0 ? Strings.Get("monitor.hover.today")
                : ago == 1 ? Strings.Get("monitor.hover.yesterday")
                : Strings.Plural("daysAgo", ago, ago.ToString(CultureInfo.InvariantCulture));
            return string.IsNullOrEmpty(symbol)
                ? Strings.Format("monitor.hover", when, PriceText(price))
                : Strings.Format("monitor.hover.symbol", symbol, when, PriceText(price));
        }

        /// <summary>Text shown when a filter leaves nothing to list.</summary>
        public static string EmptyText(MonitorFilter filter)
        {
            switch (filter)
            {
                case MonitorFilter.Portfolio:
                    return Strings.Get("monitor.empty.portfolio");
                case MonitorFilter.Orders:
                    return Strings.Get("monitor.empty.orders");
                default:
                    return Strings.Get("monitor.empty.all");
            }
        }
    }
}

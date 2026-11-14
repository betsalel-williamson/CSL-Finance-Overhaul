using System;
using System.Collections.Generic;
using System.Globalization;

namespace FinanceOverhaul.Market
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
            var rows = new List<MonitorQuote>();
            if (quotes == null)
            {
                return rows;
            }

            foreach (MonitorQuote q in quotes)
            {
                if (q == null)
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

            rows.Sort((a, b) => Compare(a, b, sort));
            return rows;
        }

        private static int Compare(MonitorQuote a, MonitorQuote b, MonitorSort sort)
        {
            int c;
            switch (sort)
            {
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

            return c != 0 ? c : string.CompareOrdinal(a.Symbol ?? "", b.Symbol ?? "");
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
                    return "Portfolio";
                case MonitorFilter.Orders:
                    return "Orders";
                default:
                    return "All";
            }
        }

        public static string SortLabel(MonitorSort sort)
        {
            switch (sort)
            {
                case MonitorSort.Symbol:
                    return "Sort: Symbol";
                case MonitorSort.Price:
                    return "Sort: Price";
                case MonitorSort.ValueHeld:
                    return "Sort: Value held";
                default:
                    return "Sort: Change";
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

            return openOrders.ToString(CultureInfo.InvariantCulture)
                + (openOrders == 1 ? " order" : " orders");
        }

        /// <summary>Footer, e.g. "Daily close · 29 Oct 2077" (prices move once per game day).</summary>
        public static string Footer(DateTime gameDate)
        {
            return "Daily close · " + gameDate.ToString("d MMM yyyy", CultureInfo.InvariantCulture);
        }

        /// <summary>Text shown when a filter leaves nothing to list.</summary>
        public static string EmptyText(MonitorFilter filter)
        {
            switch (filter)
            {
                case MonitorFilter.Portfolio:
                    return "The city holds no shares yet.";
                case MonitorFilter.Orders:
                    return "No open orders.";
                default:
                    return "No stocks are listed yet.";
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;

namespace FinanceOverhaul.Market
{
    /// <summary>
    /// Numbers and wording for the Stock Monitor's detail view (Cities-free). History is the
    /// game's daily prices, oldest first; the game keeps 30, so ranges are 7 or 30 days.
    /// </summary>
    public static class StockDetail
    {
        public const int ShortRange = 7;
        public const int LongRange = 30;

        /// <summary>The last <paramref name="days"/> finite prices (fewer when the game has fewer).</summary>
        public static float[] Range(float[] history, int days)
        {
            var finite = new List<float>();
            if (history != null)
            {
                foreach (float v in history)
                {
                    if (!float.IsNaN(v) && !float.IsInfinity(v))
                    {
                        finite.Add(v);
                    }
                }
            }

            int n = Math.Max(0, Math.Min(days, finite.Count));
            return finite.GetRange(finite.Count - n, n).ToArray();
        }

        /// <summary>Yesterday's price: the second-to-last daily price, or null.</summary>
        public static float? Yesterday(float[] history)
        {
            float[] all = Range(history, LongRange);
            return all.Length >= 2 ? all[all.Length - 2] : (float?)null;
        }

        /// <summary>
        /// The detail header's change: the game's change rate — the same measure as the list's
        /// pills and line colour, the Economy panel and the LED — in ₡ and %, e.g.
        /// "+₡4.03 (+0.29%)".
        /// </summary>
        public static string ChangeHeader(float price, float rate)
        {
            string pct = StockMonitor.ChangeText(rate);
            return pct == "--"
                ? "--"
                : StockMonitor.ChangeAmountFromRate(price, rate) + " (" + pct + ")";
        }

        public static string YesterdayLine(float? yesterday)
        {
            return yesterday.HasValue
                ? "Today · yesterday " + StockMonitor.PriceText(yesterday.Value)
                : "Today";
        }

        public static string RangeChip(int days)
        {
            return days == ShortRange ? "7D" : "30D";
        }

        public static string HighLabel(int days)
        {
            return days.ToString(CultureInfo.InvariantCulture) + "-day high";
        }

        public static string LowLabel(int days)
        {
            return days.ToString(CultureInfo.InvariantCulture) + "-day low";
        }

        public static float High(float[] range)
        {
            float h = float.NaN;
            foreach (float v in range ?? new float[0])
            {
                h = float.IsNaN(h) ? v : Math.Max(h, v);
            }

            return h;
        }

        public static float Low(float[] range)
        {
            float l = float.NaN;
            foreach (float v in range ?? new float[0])
            {
                l = float.IsNaN(l) ? v : Math.Min(l, v);
            }

            return l;
        }

        /// <summary>
        /// Axis labels for a chart drawn by <see cref="Sparkline"/> over this range: the high,
        /// the low and evenly spaced values between, top first.
        /// </summary>
        public static float[] AxisValues(float[] range, int count)
        {
            float hi = High(range),
                lo = Low(range);
            if (float.IsNaN(hi) || count < 2)
            {
                return new float[0];
            }

            var values = new float[count];
            for (int i = 0; i < count; i++)
            {
                values[i] = hi - (hi - lo) * i / (count - 1);
            }

            return values;
        }

        /// <summary>"Gain" or "Loss" and its amount, as the game's Your Portfolio shows it.</summary>
        public static string GainLossLabel(long invested, uint owned, float price)
        {
            return GainLoss(invested, owned, price) < 0 ? "Loss" : "Gain";
        }

        public static long GainLoss(long invested, uint owned, float price)
        {
            return owned == 0 ? 0 : OrderRules.Price(price, owned) - invested;
        }

        public static string GainLossText(long invested, uint owned, float price)
        {
            if (owned == 0)
            {
                return "—";
            }

            long g = GainLoss(invested, owned, price);
            return (g < 0 ? "−₡" : "₡") + Math.Abs(g).ToString("N0", CultureInfo.InvariantCulture);
        }

        public static string SharesText(uint owned)
        {
            return owned == 0
                ? "none"
                : owned.ToString("N0", CultureInfo.InvariantCulture)
                    + (owned == 1 ? " share" : " shares");
        }

        /// <summary>
        /// "—", "1 · BUY ≤ ₡900", "1 · SELL ≤ ₡95 (stop)", "1 · SELL ≥ ₡120 (target)",
        /// "1 · SELL, 10% trail", or "3 open" for several.
        /// </summary>
        public static string OrdersSummary(IList<TradeOrder> orders)
        {
            if (orders == null || orders.Count == 0)
            {
                return "—";
            }

            if (orders.Count > 1)
            {
                return orders.Count.ToString(CultureInfo.InvariantCulture) + " open";
            }

            TradeOrder o = orders[0];
            string p = "₡" + o.TriggerPrice.ToString("N0", CultureInfo.InvariantCulture);
            switch (o.Kind)
            {
                case OrderKind.StopLossSell:
                    return "1 · SELL ≤ " + p + " (stop)";
                case OrderKind.TakeProfitSell:
                    return "1 · SELL ≥ " + p + " (target)";
                case OrderKind.TrailingStopSell:
                    return "1 · SELL, "
                        + o.TrailPercent.ToString(CultureInfo.InvariantCulture)
                        + "% trail";
                default:
                    return "1 · BUY ≤ " + p;
            }
        }
    }
}

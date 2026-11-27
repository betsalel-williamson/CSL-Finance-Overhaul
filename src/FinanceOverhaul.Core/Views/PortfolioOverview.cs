using System; // Math, StringComparison
using System.Collections.Generic; // IEnumerable, List
using System.Globalization; // CultureInfo

namespace FinanceOverhaul.Core
{
    /// <summary>One slice of the holdings donut: a stock (or "Other"), its value and share.</summary>
    public struct PortfolioSlice
    {
        /// <summary>The stock's symbol, or "Other" for the rest.</summary>
        public string Label;

        /// <summary>What the city holds of it, in ₡.</summary>
        public double Value;

        /// <summary>Its part of the whole, 0 to 1.</summary>
        public float Share;

        /// <summary>The rest of the holdings folded together.</summary>
        public bool Other;
    }

    /// <summary>
    /// The Portfolio view of the overview (Figma band 20, "Two columns"): what the city's
    /// shares are worth, how that moved today and since they were bought, a chart of what
    /// today's holdings were worth each day, the holdings by value, and the open orders. Money
    /// in whole ₡, as the game keeps what the city spent. Cities-free.
    /// </summary>
    public static class PortfolioOverview
    {
        /// <summary>The stocks the city holds, largest value first (ties A–Z).</summary>
        public static List<MonitorQuote> Holdings(IEnumerable<MonitorQuote> quotes)
        {
            var held = new List<MonitorQuote>();
            foreach (MonitorQuote q in quotes ?? new List<MonitorQuote>())
            {
                if (q != null && q.Owned > 0)
                {
                    held.Add(q);
                }
            }

            held.Sort(
                (a, b) =>
                {
                    int byValue = b.ValueHeld.CompareTo(a.ValueHeld);
                    return byValue != 0
                        ? byValue
                        : string.Compare(a.Symbol, b.Symbol, StringComparison.Ordinal);
                }
            );
            return held;
        }

        /// <summary>What the holdings are worth at today's prices.</summary>
        public static double Value(IEnumerable<MonitorQuote> quotes)
        {
            double sum = 0;
            foreach (MonitorQuote q in Holdings(quotes))
            {
                sum += q.ValueHeld;
            }

            return sum;
        }

        /// <summary>What the holdings are worth less what the city paid for them.</summary>
        public static double Gain(IEnumerable<MonitorQuote> quotes)
        {
            double sum = 0;
            foreach (MonitorQuote q in Holdings(quotes))
            {
                sum += q.ValueHeld - q.Spent;
            }

            return sum;
        }

        /// <summary>What the city paid for the shares it holds.</summary>
        public static double Paid(IEnumerable<MonitorQuote> quotes)
        {
            double sum = 0;
            foreach (MonitorQuote q in Holdings(quotes))
            {
                sum += q.Spent;
            }

            return sum;
        }

        /// <summary>
        /// How the holdings' worth moved today: each holding's shares × the ₡ change its pill
        /// shows (<see cref="StockMonitor.ChangeAmount"/>, the game's change rate), so the view
        /// agrees with the pills. A stock with no known change adds nothing.
        /// </summary>
        public static double DayChange(IEnumerable<MonitorQuote> quotes)
        {
            double sum = 0;
            foreach (MonitorQuote q in Holdings(quotes))
            {
                double amount = StockMonitor.ChangeAmount(q.Price, q.ChangeRate);
                if (amount != double.MinValue)
                {
                    sum += q.Owned * amount;
                }
            }

            return sum;
        }

        /// <summary>
        /// What today's holdings were worth on each of the last <paramref name="days"/> days,
        /// oldest first: the shares held now at each day's price (the game keeps no record of
        /// what the city held on earlier days). A stock with no price that day adds nothing; a
        /// day no holding has a price for is NaN. Empty with no holdings or fewer than two days.
        /// </summary>
        public static float[] WorthHistory(IEnumerable<MonitorQuote> quotes, int days)
        {
            var ranges = new List<float[]>();
            var owned = new List<uint>();
            int longest = 0;
            foreach (MonitorQuote q in Holdings(quotes))
            {
                float[] range = StockDetail.Range(q.History, days);
                if (range.Length == 0)
                {
                    continue;
                }

                ranges.Add(range);
                owned.Add(q.Owned);
                longest = Math.Max(longest, range.Length);
            }

            if (longest < 2)
            {
                return new float[0];
            }

            var worth = new float[longest];
            for (int d = 0; d < longest; d++)
            {
                double sum = 0;
                bool any = false;
                for (int k = 0; k < ranges.Count; k++)
                {
                    float[] r = ranges[k];
                    // Line up at today: the last value of every range is the same day.
                    int i = d - (longest - r.Length);
                    if (i >= 0 && !float.IsNaN(r[i]) && !float.IsInfinity(r[i]))
                    {
                        sum += (double)owned[k] * r[i];
                        any = true;
                    }
                }

                worth[d] = any ? (float)sum : float.NaN;
            }

            return worth;
        }

        /// <summary>
        /// The holdings by value for the donut: the largest <paramref name="slices"/> − 1 by
        /// name and the rest as "Other" (all of them by name when they fit).
        /// </summary>
        public static List<PortfolioSlice> Allocation(IEnumerable<MonitorQuote> quotes, int slices)
        {
            List<MonitorQuote> held = Holdings(quotes);
            double total = 0;
            foreach (MonitorQuote q in held)
            {
                total += q.ValueHeld;
            }

            var result = new List<PortfolioSlice>();
            if (total <= 0 || slices < 1)
            {
                return result;
            }

            int named = held.Count <= slices ? held.Count : slices - 1;
            double rest = 0;
            for (int i = 0; i < held.Count; i++)
            {
                if (i < named)
                {
                    result.Add(
                        new PortfolioSlice
                        {
                            Label = held[i].Symbol,
                            Value = held[i].ValueHeld,
                            Share = (float)(held[i].ValueHeld / total),
                        }
                    );
                }
                else
                {
                    rest += held[i].ValueHeld;
                }
            }

            if (rest > 0)
            {
                result.Add(
                    new PortfolioSlice
                    {
                        Label = Strings.Get("overview.portfolio.other"),
                        Value = rest,
                        Share = (float)(rest / total),
                        Other = true,
                    }
                );
            }

            return result;
        }

        /// <summary>"₡4,782,000".</summary>
        public static string Money(double amount)
        {
            return "₡" + Math.Round(Math.Abs(amount)).ToString("N0", CultureInfo.InvariantCulture);
        }

        /// <summary>"+₡668,700 (+16.3%)": a change and its part of <paramref name="from"/>.</summary>
        public static string Change(double amount, double from)
        {
            long whole = (long)Math.Round(amount);
            string sign =
                whole > 0 ? "+"
                : whole < 0 ? "−"
                : string.Empty;
            string text = sign + Money(amount);
            if (from > 0)
            {
                double pct = amount / from * 100.0;
                text +=
                    " ("
                    + (pct >= 0 ? "+" : "−")
                    + Math.Abs(pct).ToString("0.0", CultureInfo.InvariantCulture)
                    + "%)";
            }

            return text;
        }

        /// <summary>
        /// Today's change against yesterday's worth: "−₡12,340 (−0.3%)".
        /// </summary>
        public static string DayChangeText(IEnumerable<MonitorQuote> quotes)
        {
            double change = DayChange(quotes);
            return Change(change, Value(quotes) - change);
        }

        /// <summary>The gain since bought against what was paid: "+₡668,700 (+16.3%)".</summary>
        public static string GainText(IEnumerable<MonitorQuote> quotes)
        {
            return Change(Gain(quotes), Paid(quotes));
        }

        /// <summary>
        /// An open order's side in the Portfolio view, beside its stock under "Open orders":
        /// "BUY", "SELL (stop-loss)".
        /// </summary>
        public static string OrderSide(TradeOrder order)
        {
            return Strings.Get("overview.portfolio.side." + OrderText.Key(order.Kind));
        }

        /// <summary>The order's terms under it: "500 shares at or below ₡830 · 88 days left".</summary>
        public static string OrderDetail(TradeOrder order, int today)
        {
            return OrderText.CardDetail(order, today);
        }

        /// <summary>The most shares an order can show (tests: the longest wording).</summary>
        public const uint LongestShares = 9999999;

        /// <summary>The highest price an order can show (tests: the longest wording).</summary>
        public const int LongestPrice = 9999999;

        /// <summary>
        /// One order of each kind with the longest numbers it can show (9,999,999 shares at
        /// ₡9,999,999, a 99% trail, a full year left), for checking the view fits its longest
        /// text. The longest stock symbol comes from the caller.
        /// </summary>
        public static List<TradeOrder> LongestOrders(string investmentKey, int today)
        {
            int year = TradeOrder.LifetimeChoices[TradeOrder.LifetimeChoices.Length - 1];
            return new List<TradeOrder>
            {
                TradeOrder.StopLossSell(
                    -1,
                    investmentKey,
                    LongestShares,
                    LongestPrice,
                    today,
                    year
                ),
                TradeOrder.TakeProfitSell(
                    -2,
                    investmentKey,
                    LongestShares,
                    LongestPrice,
                    today,
                    year
                ),
                TradeOrder.TrailingStopSell(
                    -3,
                    investmentKey,
                    LongestShares,
                    99,
                    LongestPrice,
                    today,
                    year
                ),
                TradeOrder.LimitBuy(-4, investmentKey, LongestShares, LongestPrice, today, year),
            };
        }
    }
}

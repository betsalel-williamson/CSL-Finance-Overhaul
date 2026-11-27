using System; // Array, Math, StringComparison
using System.Collections.Generic; // IEnumerable, IList, List
using System.Globalization; // CultureInfo

namespace FinanceOverhaul.Core
{
    /// <summary>One tile of the heatmap: a stock, its change over the span and how hot it is.</summary>
    public struct HeatTile
    {
        public MonitorQuote Quote;

        /// <summary>The change over the span in percent; NaN when unknown (locked, too new).</summary>
        public float Change;

        /// <summary>The change in ₡ over the span; NaN when unknown.</summary>
        public double Amount;

        /// <summary>−1 (the biggest fall shown) to +1 (the biggest rise); 0 flat or unknown.</summary>
        public float Heat;

        /// <summary>The city holds shares of it (outlined).</summary>
        public bool Held;

        /// <summary>Not on sale yet (drawn grey, the smallest, at the end).</summary>
        public bool Locked;

        /// <summary>The tile's area relative to the others (<see cref="HeatmapOverview.Weight"/>).</summary>
        public double Weight;
    }

    /// <summary>Where one tile of the treemap goes, in the area's own units.</summary>
    public struct HeatRect
    {
        public float X;
        public float Y;
        public float Width;
        public float Height;
    }

    /// <summary>
    /// The Heatmap view of the overview (Figma band 20, "Two columns"): every stock as a tile
    /// of a treemap, its area by the stock's market value (price × shares, square-root damped
    /// so a ₡30 stock is still readable beside a ₡9,000 one), coloured from orange (falling) to
    /// green (rising) by its change today or over 7 or 30 days, the stocks the city holds
    /// outlined. The colour scale stretches to the typical
    /// move (four tiles in five), rounded up to a round step, so one outlier does not grey
    /// out the rest and a quiet day still shows its leaders.
    /// Cities-free.
    /// </summary>
    public static class HeatmapOverview
    {
        /// <summary>The span "Today": the game's change rate, as the pills.</summary>
        public const int Today = 1;

        /// <summary>The spans in tab order: Today, 7 days, 30 days.</summary>
        public static readonly int[] Spans =
        {
            Today,
            StockDetail.ShortRange,
            StockDetail.LongRange,
        };

        /// <summary>The scale's steps in percent: the smallest that holds the biggest move.</summary>
        public static readonly float[] ScaleSteps = { 1f, 2f, 5f, 10f, 20f, 50f, 100f };

        /// <summary>Shares counted for a stock that shows none (locked): the game's usual 1,000.</summary>
        public const double DefaultShares = 1000d;

        /// <summary>A saved span that is not one of <see cref="Spans"/> reads as Today.</summary>
        public static int Span(int saved)
        {
            return Array.IndexOf(Spans, saved) >= 0 ? saved : Today;
        }

        /// <summary>The change of one stock over the span, in percent (NaN when unknown).</summary>
        public static float Change(MonitorQuote q, int span)
        {
            if (q == null || q.Locked)
            {
                return float.NaN;
            }

            if (span <= Today)
            {
                return float.IsInfinity(q.ChangeRate) ? float.NaN : q.ChangeRate;
            }

            float[] range = StockDetail.Range(q.History, span);
            float first,
                last;
            return Ends(range, out first, out last) && first > 0f
                ? (last / first - 1f) * 100f
                : float.NaN;
        }

        /// <summary>The change of one stock over the span, in ₡ (NaN when unknown).</summary>
        public static double Amount(MonitorQuote q, int span)
        {
            if (q == null || q.Locked)
            {
                return double.NaN;
            }

            if (span <= Today)
            {
                double amount = StockMonitor.ChangeAmount(q.Price, q.ChangeRate);
                return amount == double.MinValue ? double.NaN : amount;
            }

            float[] range = StockDetail.Range(q.History, span);
            float first,
                last;
            return Ends(range, out first, out last) ? (double)last - first : double.NaN;
        }

        private static bool Ends(float[] range, out float first, out float last)
        {
            first = float.NaN;
            last = float.NaN;
            int count = 0;
            foreach (float v in range ?? new float[0])
            {
                if (float.IsNaN(v) || float.IsInfinity(v))
                {
                    continue;
                }

                if (count == 0)
                {
                    first = v;
                }

                last = v;
                count++;
            }

            return count >= 2;
        }

        /// <summary>The share of tiles the scale holds without clamping: one outlier does not wash the rest out.</summary>
        public const float ScalePercentile = 0.8f;

        /// <summary>
        /// The scale in percent: the smallest of <see cref="ScaleSteps"/> that holds the moves
        /// of <see cref="ScalePercentile"/> of the tiles (the largest step when that is bigger
        /// still). Bigger moves show at full colour.
        /// </summary>
        public static float Scale(IEnumerable<HeatTile> tiles)
        {
            var sizes = new List<float>();
            foreach (HeatTile t in tiles ?? new List<HeatTile>())
            {
                if (!float.IsNaN(t.Change))
                {
                    sizes.Add(Math.Abs(t.Change));
                }
            }

            float typical = 0f;
            if (sizes.Count > 0)
            {
                sizes.Sort();
                int at = (int)Math.Ceiling(sizes.Count * ScalePercentile) - 1;
                typical = sizes[Math.Max(0, Math.Min(sizes.Count - 1, at))];
            }

            foreach (float step in ScaleSteps)
            {
                if (typical <= step)
                {
                    return step;
                }
            }

            return ScaleSteps[ScaleSteps.Length - 1];
        }

        /// <summary>
        /// A stock's tile area before scaling: the square root of its market value (price ×
        /// the shares there are, the city's and those on sale), so area follows value but a
        /// cheap stock keeps a readable tile. At least 1.
        /// </summary>
        public static double Weight(MonitorQuote q)
        {
            if (q == null)
            {
                return 1d;
            }

            double price =
                float.IsNaN(q.Price) || float.IsInfinity(q.Price) || q.Price < 1f ? 1d : q.Price;
            double shares = (double)q.Owned + q.Available;
            if (shares <= 0d)
            {
                shares = DefaultShares;
            }

            return Math.Max(1d, Math.Sqrt(price * shares));
        }

        /// <summary>
        /// Every stock as a tile (not the city index fund, which averages them; coins such as
        /// ChirpCoin stay), largest first (ties A–Z), the locked ones after them A–Z at
        /// the smallest size on sale, so the treemap lays them out in that order. Each tile's
        /// heat is its change over the scale, clamped to −1…+1.
        /// </summary>
        public static List<HeatTile> Tiles(IEnumerable<MonitorQuote> quotes, int span)
        {
            var open = new List<HeatTile>();
            var locked = new List<HeatTile>();
            foreach (MonitorQuote q in quotes ?? new List<MonitorQuote>())
            {
                // The index fund is the average of the tiles: it shows by the heading, not as one.
                if (q == null || CityIndexBasket.IsIndexName(q.Prefab))
                {
                    continue;
                }

                var tile = new HeatTile
                {
                    Quote = q,
                    Change = Change(q, span),
                    Amount = Amount(q, span),
                    Held = q.Owned > 0,
                    Locked = q.Locked,
                    Weight = Weight(q),
                };
                (q.Locked ? locked : open).Add(tile);
            }

            open.Sort(
                (a, b) =>
                {
                    int byWeight = b.Weight.CompareTo(a.Weight);
                    return byWeight != 0 ? byWeight : BySymbol(a, b);
                }
            );
            locked.Sort(BySymbol);

            float scale = Scale(open);
            double smallest = open.Count > 0 ? open[open.Count - 1].Weight : 1d;
            for (int i = 0; i < open.Count; i++)
            {
                HeatTile t = open[i];
                t.Heat = float.IsNaN(t.Change) ? 0f : Math.Max(-1f, Math.Min(1f, t.Change / scale));
                open[i] = t;
            }

            for (int i = 0; i < locked.Count; i++)
            {
                HeatTile t = locked[i];
                t.Weight = smallest;
                locked[i] = t;
            }

            open.AddRange(locked);
            return open;
        }

        private static int BySymbol(HeatTile a, HeatTile b)
        {
            return string.Compare(a.Quote.Symbol, b.Quote.Symbol, StringComparison.Ordinal);
        }

        /// <summary>
        /// A squarified treemap (Bruls, Huizing and van Wijk): the area split into one
        /// rectangle per weight, each with its share of the area, rows chosen so the tiles stay
        /// as near square as they can. Weights in the order given (largest first lays out
        /// best); the result is in the same order. <paramref name="gap"/> is left between
        /// neighbours; the outer tiles touch the area's edges.
        /// </summary>
        public static HeatRect[] Treemap(
            IList<double> weights,
            float width,
            float height,
            float gap
        )
        {
            int n = weights == null ? 0 : weights.Count;
            var rects = new HeatRect[n];
            double total = 0d;
            for (int i = 0; i < n; i++)
            {
                total += Math.Max(0d, weights[i]);
            }

            if (n == 0 || total <= 0d || width <= 0f || height <= 0f)
            {
                return rects;
            }

            // Lay out on an area one gap bigger, then take half a gap off every side of each
            // tile: a gap between neighbours and none at the edges.
            double x = -gap / 2.0,
                y = -gap / 2.0,
                w = width + gap,
                h = height + gap;
            double scale = w * h / total;
            var areas = new double[n];
            for (int i = 0; i < n; i++)
            {
                areas[i] = Math.Max(0d, weights[i]) * scale;
            }

            int start = 0;
            while (start < n)
            {
                double side = Math.Min(w, h);
                int end = start + 1;
                double sum = areas[start];
                double worst = Worst(areas, start, end, sum, side);
                while (end < n)
                {
                    double next = Worst(areas, start, end + 1, sum + areas[end], side);
                    if (next > worst)
                    {
                        break;
                    }

                    sum += areas[end];
                    worst = next;
                    end++;
                }

                // The row along the shorter side; the rest of the area beside it.
                bool column = w >= h;
                double thick = sum <= 0d ? 0d : sum / (column ? h : w);
                double along = 0d;
                for (int i = start; i < end; i++)
                {
                    double length = thick <= 0d ? 0d : areas[i] / thick;
                    double rx = column ? x : x + along,
                        ry = column ? y + along : y,
                        rw = column ? thick : length,
                        rh = column ? length : thick;
                    rects[i] = new HeatRect
                    {
                        X = (float)(rx + gap / 2.0),
                        Y = (float)(ry + gap / 2.0),
                        Width = (float)Math.Max(0d, rw - gap),
                        Height = (float)Math.Max(0d, rh - gap),
                    };
                    along += length;
                }

                if (column)
                {
                    x += thick;
                    w -= thick;
                }
                else
                {
                    y += thick;
                    h -= thick;
                }

                start = end;
            }

            return rects;
        }

        /// <summary>The worst aspect ratio of a row of areas[start..end) laid along <paramref name="side"/>.</summary>
        private static double Worst(double[] areas, int start, int end, double sum, double side)
        {
            if (sum <= 0d || side <= 0d)
            {
                return double.MaxValue;
            }

            double max = 0d,
                min = double.MaxValue;
            for (int i = start; i < end; i++)
            {
                max = Math.Max(max, areas[i]);
                min = Math.Min(min, areas[i]);
            }

            if (min <= 0d)
            {
                return double.MaxValue;
            }

            double s2 = side * side,
                sum2 = sum * sum;
            return Math.Max(s2 * max / sum2, sum2 / (s2 * min));
        }

        /// <summary>A big tile's second line: "₡924", or "₡69 · 500 shares" when held.</summary>
        public static string Detail(HeatTile tile)
        {
            MonitorQuote q = tile.Quote;
            string price = StockMonitor.PriceText(q.Price);
            return tile.Held
                ? Strings.Format(
                    "overview.heatmap.detailHeld",
                    price,
                    Strings.Plural(
                        "shares",
                        q.Owned,
                        q.Owned.ToString("N0", CultureInfo.InvariantCulture)
                    )
                )
                : price;
        }

        /// <summary>
        /// A tile's change as its pill would show it: "+1.07%" or, with the pills in points,
        /// "+₡12" (cents under ₡10); "--" when unknown.
        /// </summary>
        public static string TileText(HeatTile tile, bool points)
        {
            if (tile.Locked)
            {
                return Strings.Get("overview.heatmap.locked");
            }

            if (float.IsNaN(tile.Change))
            {
                return "--";
            }

            return points ? MoneyChange(tile.Amount) : StockMonitor.ChangeText(tile.Change);
        }

        /// <summary>"+₡12", "−₡0.40", "₡0": a change in ₡ as the ₡ pills show it.</summary>
        public static string MoneyChange(double amount)
        {
            if (double.IsNaN(amount) || double.IsInfinity(amount))
            {
                return "--";
            }

            double cents = Math.Round(Math.Abs(amount), 2);
            if (cents == 0d)
            {
                return "₡0";
            }

            string digits =
                cents < 10.0
                    ? cents.ToString("0.00", CultureInfo.InvariantCulture)
                    : Math.Round(Math.Abs(amount)).ToString("N0", CultureInfo.InvariantCulture);
            return (amount > 0 ? "+₡" : "−₡") + digits;
        }

        /// <summary>"Today's change", "Change over 7 days": the heading over the tiles.</summary>
        public static string Heading(int span)
        {
            return span <= Today
                ? Strings.Get("overview.heatmap.today")
                : Strings.Format(
                    "overview.heatmap.overDays",
                    span.ToString(CultureInfo.InvariantCulture)
                );
        }

        /// <summary>
        /// The city index fund's change over the span as its pill shows it beside the heading
        /// ("-1.03%", or "−₡31" in points); null when the fund is not listed (or locked).
        /// </summary>
        public static string IndexChange(IEnumerable<MonitorQuote> quotes, int span, bool points)
        {
            MonitorQuote fund = IndexFund(quotes);
            if (fund == null)
            {
                return null;
            }

            var tile = new HeatTile
            {
                Quote = fund,
                Change = Change(fund, span),
                Amount = Amount(fund, span),
            };
            return TileText(tile, points);
        }

        /// <summary>The city index fund when listed and on sale, else null.</summary>
        public static MonitorQuote IndexFund(IEnumerable<MonitorQuote> quotes)
        {
            foreach (MonitorQuote q in quotes ?? new List<MonitorQuote>())
            {
                if (q != null && !q.Locked && CityIndexBasket.IsIndexName(q.Prefab))
                {
                    return q;
                }
            }

            return null;
        }

        /// <summary>"Today", "7D", "30D": the span tabs.</summary>
        public static string SpanTab(int span)
        {
            return span <= Today
                ? Strings.Get("overview.heatmap.tab.today")
                : StockDetail.RangeChip(span);
        }

        /// <summary>The scale's ends under the tiles: "−5%" and "+5%".</summary>
        public static string ScaleEnd(float scale, bool rising)
        {
            return (rising ? "+" : "−")
                + scale.ToString("0.##", CultureInfo.InvariantCulture)
                + "%";
        }

        /// <summary>A tile's tooltip: "Oil Industry (OIL) · ₡1,234", plus " · 500 shares held".</summary>
        public static string Tip(HeatTile tile)
        {
            MonitorQuote q = tile.Quote;
            string name = q.Name ?? q.Symbol;
            string price = StockMonitor.PriceText(q.Price);
            if (!tile.Held)
            {
                return Strings.Format("overview.heatmap.tip", name, q.Symbol, price);
            }

            string shares = Strings.Plural(
                "shares",
                q.Owned,
                q.Owned.ToString("N0", CultureInfo.InvariantCulture)
            );
            return Strings.Format("overview.heatmap.tipHeld", name, q.Symbol, price, shares);
        }
    }
}

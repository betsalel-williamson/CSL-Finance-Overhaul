using System; // Array, Math, StringComparison
using System.Collections.Generic; // IEnumerable, List
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

        /// <summary>Not on sale yet (drawn grey, at the end).</summary>
        public bool Locked;
    }

    /// <summary>How the heatmap's tiles fit its area.</summary>
    public struct HeatGrid
    {
        public int Columns;
        public int Rows;
        public float TileWidth;
        public float TileHeight;
    }

    /// <summary>
    /// The Heatmap view of the overview (Figma band 20, "Two columns"): every stock as a tile,
    /// coloured from orange (falling) to green (rising) by its change today or over 7 or 30
    /// days, the stocks the city holds outlined. The colour scale stretches to the typical
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

        /// <summary>The tiles never grow taller than this, however few stocks there are.</summary>
        public const float MaxTileHeight = 64f;

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
        /// Every stock as a tile: those on sale from the biggest rise to the biggest fall
        /// (unknown changes after them, ties A–Z), then the locked ones A–Z. Each tile's heat
        /// is its change over the scale, clamped to −1…+1.
        /// </summary>
        public static List<HeatTile> Tiles(IEnumerable<MonitorQuote> quotes, int span)
        {
            var open = new List<HeatTile>();
            var locked = new List<HeatTile>();
            foreach (MonitorQuote q in quotes ?? new List<MonitorQuote>())
            {
                if (q == null)
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
                };
                (q.Locked ? locked : open).Add(tile);
            }

            open.Sort(
                (a, b) =>
                {
                    bool aKnown = !float.IsNaN(a.Change),
                        bKnown = !float.IsNaN(b.Change);
                    if (aKnown != bKnown)
                    {
                        return aKnown ? -1 : 1;
                    }

                    int byChange = aKnown ? b.Change.CompareTo(a.Change) : 0;
                    return byChange != 0 ? byChange : BySymbol(a, b);
                }
            );
            locked.Sort(BySymbol);

            float scale = Scale(open);
            for (int i = 0; i < open.Count; i++)
            {
                HeatTile t = open[i];
                t.Heat = float.IsNaN(t.Change) ? 0f : Math.Max(-1f, Math.Min(1f, t.Change / scale));
                open[i] = t;
            }

            open.AddRange(locked);
            return open;
        }

        private static int BySymbol(HeatTile a, HeatTile b)
        {
            return string.Compare(a.Quote.Symbol, b.Quote.Symbol, StringComparison.Ordinal);
        }

        /// <summary>
        /// The grid that gives <paramref name="count"/> tiles the biggest size in the area, as
        /// near a 3:2 tile as the area allows, with <paramref name="gap"/> between tiles; never
        /// taller than <see cref="MaxTileHeight"/>.
        /// </summary>
        public static HeatGrid Grid(int count, float width, float height, float gap)
        {
            var best = new HeatGrid { Columns = 1, Rows = Math.Max(1, count) };
            if (count <= 0 || width <= 0f || height <= 0f)
            {
                return best;
            }

            float bestScore = float.MinValue;
            for (int cols = 1; cols <= count; cols++)
            {
                int rows = (count + cols - 1) / cols;
                float w = (width - gap * (cols - 1)) / cols;
                float h = Math.Min(MaxTileHeight, (height - gap * (rows - 1)) / rows);
                if (w <= 0f || h <= 0f)
                {
                    continue;
                }

                // The biggest 3:2 box that fits in the tile: its area is the tile's score.
                float score = Math.Min(w, h * 1.5f) * Math.Min(h, w / 1.5f);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = new HeatGrid
                    {
                        Columns = cols,
                        Rows = rows,
                        TileWidth = w,
                        TileHeight = h,
                    };
                }
            }

            return best;
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

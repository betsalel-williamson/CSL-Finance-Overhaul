using System; // Func, Math
using System.Collections.Generic; // List

namespace FinanceOverhaul.Core
{
    /// <summary>
    /// Rasterises a price series into a small pixel mask for the Stock Monitor rows: the price
    /// line plus a dashed line at yesterday's price. Row 0 is the top. Allocation is the mask
    /// itself; the host copies it into a texture once per game day.
    /// </summary>
    public static class Sparkline
    {
        public const byte Empty = 0;
        public const byte Baseline = 1;
        public const byte Line = 2;

        /// <summary>Below the line (only with <c>fillArea</c>).</summary>
        public const byte Area = 3;

        // Dash and gap lengths per unit of line thickness: the dashed line is as thick as the
        // price line, so it stays visible after the texture is scaled down for display.
        private const int DashOn = 3;
        private const int DashOff = 3;

        /// <param name="values">
        /// Daily prices, oldest first, one column each. A non-finite value is a day with no price
        /// (a company not yet listed): nothing is drawn there, and the line starts at the first day
        /// with a price.
        /// </param>
        /// <param name="thickness">Line thickness in pixels (1 or more).</param>
        public static byte[] Render(
            float[] values,
            int width,
            int height,
            int thickness = 1,
            bool fillArea = false
        )
        {
            if (width <= 0 || height <= 0)
            {
                return new byte[0];
            }

            var mask = new byte[width * height];
            int n = values == null ? 0 : values.Length;
            var known = new List<int>();
            for (int i = 0; i < n; i++)
            {
                if (Finite(values[i]))
                {
                    known.Add(i);
                }
            }

            if (known.Count == 0)
            {
                return mask;
            }

            thickness = Math.Max(1, thickness);
            float lo = float.MaxValue,
                hi = float.MinValue;
            foreach (int i in known)
            {
                lo = Math.Min(lo, values[i]);
                hi = Math.Max(hi, values[i]);
            }

            int pad = (thickness - 1) / 2;
            int top = pad,
                bottom = height - 1 - (thickness - 1 - pad);
            Func<float, int> rowOf = value =>
                hi - lo < 1e-6f
                    ? (top + bottom) / 2
                    : bottom - (int)Math.Round((value - lo) / (hi - lo) * (bottom - top));
            Func<int, int> colOf = i => (int)ColumnOf(i, width, n);

            // Yesterday's price, dashed, across the days the stock has a price.
            if (known.Count >= 2 && Finite(values[n - 2]))
            {
                int yb = rowOf(values[n - 2]);
                int on = DashOn * thickness,
                    period = (DashOn + DashOff) * thickness;
                for (int x = colOf(known[0]); x < width; x++)
                {
                    if (x % period >= on)
                    {
                        continue;
                    }

                    // A vertical strip as thick as the line, centred like the line's pen.
                    for (int y = yb - pad; y <= yb + (thickness - 1 - pad); y++)
                    {
                        if (y >= 0 && y < height)
                        {
                            mask[y * width + x] = Baseline;
                        }
                    }
                }
            }

            int px = colOf(known[0]),
                py = rowOf(values[known[0]]);
            Plot(mask, width, height, px, py, thickness, Line);
            for (int k = 1; k < known.Count; k++)
            {
                int x = colOf(known[k]),
                    y = rowOf(values[known[k]]);
                DrawLine(mask, width, height, px, py, x, y, thickness);
                px = x;
                py = y;
            }

            if (fillArea)
            {
                FillBelow(mask, width, height);
            }

            return mask;
        }

        /// <summary>
        /// The days with no price before a stock existed, as a chart shows them: every value
        /// before <paramref name="firstDay"/> becomes NaN (drawn as nothing). Values are the last
        /// <c>values.Length</c> days, oldest first, the last one <paramref name="today"/>.
        /// </summary>
        public static float[] FromDay(float[] values, int today, int firstDay)
        {
            if (values == null)
            {
                return null;
            }

            var masked = (float[])values.Clone();
            int n = masked.Length;
            for (int i = 0; i < n; i++)
            {
                if (today - (n - 1 - i) < firstDay)
                {
                    masked[i] = float.NaN;
                }
            }

            return masked;
        }

        /// <summary>The first day in <paramref name="values"/> with a price, or -1.</summary>
        public static int FirstKnown(float[] values)
        {
            for (int i = 0; values != null && i < values.Length; i++)
            {
                if (Finite(values[i]))
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool Finite(float x)
        {
            return !float.IsNaN(x) && !float.IsInfinity(x);
        }

        /// <summary>
        /// Row (0 = top) a value lands on in a chart of this size over lo..hi — the same mapping
        /// Render uses, for grid lines and axis labels.
        /// </summary>
        public static int RowOf(float value, float lo, float hi, int height, int thickness = 1)
        {
            thickness = Math.Max(1, thickness);
            int pad = (thickness - 1) / 2;
            int top = pad,
                bottom = height - 1 - (thickness - 1 - pad);
            if (hi - lo < 1e-6f)
            {
                return (top + bottom) / 2;
            }

            return bottom - (int)Math.Round((value - lo) / (hi - lo) * (bottom - top));
        }

        /// <summary>
        /// The data point nearest column <paramref name="x"/> in a chart of this width over
        /// <paramref name="count"/> points — the inverse of the column mapping Render uses.
        /// </summary>
        public static int IndexAt(float x, int width, int count)
        {
            if (count <= 1 || width <= 1)
            {
                return Math.Max(0, count - 1);
            }

            int i = (int)Math.Round(x * (count - 1) / (double)(width - 1));
            return Math.Max(0, Math.Min(count - 1, i));
        }

        /// <summary>Column of data point <paramref name="index"/> (same mapping as Render).</summary>
        public static float ColumnOf(int index, int width, int count)
        {
            return count <= 1
                ? width - 1
                : (float)Math.Round(index * (width - 1) / (double)(count - 1));
        }

        private static void FillBelow(byte[] mask, int w, int h)
        {
            for (int x = 0; x < w; x++)
            {
                int y0 = -1;
                for (int y = 0; y < h; y++)
                {
                    if (mask[y * w + x] == Line)
                    {
                        y0 = y;
                        break;
                    }
                }

                if (y0 < 0)
                {
                    continue;
                }

                for (int y = y0 + 1; y < h; y++)
                {
                    // The dashed line stays on top of the shaded area.
                    if (mask[y * w + x] == Empty)
                    {
                        mask[y * w + x] = Area;
                    }
                }
            }
        }

        private static void DrawLine(
            byte[] mask,
            int w,
            int h,
            int x0,
            int y0,
            int x1,
            int y1,
            int thickness
        )
        {
            int dx = Math.Abs(x1 - x0),
                sx = x0 < x1 ? 1 : -1;
            int dy = -Math.Abs(y1 - y0),
                sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;
            while (true)
            {
                Plot(mask, w, h, x0, y0, thickness, Line);
                if (x0 == x1 && y0 == y1)
                {
                    return;
                }

                int e2 = 2 * err;
                if (e2 >= dy)
                {
                    err += dy;
                    x0 += sx;
                }

                if (e2 <= dx)
                {
                    err += dx;
                    y0 += sy;
                }
            }
        }

        private static void Plot(byte[] mask, int w, int h, int x, int y, int size, byte value)
        {
            int start = -(size - 1) / 2;
            for (int oy = start; oy < start + size; oy++)
            {
                for (int ox = start; ox < start + size; ox++)
                {
                    int px = x + ox,
                        py = y + oy;
                    if (px < 0 || py < 0 || px >= w || py >= h)
                    {
                        continue;
                    }

                    int i = py * w + px;
                    if (mask[i] < value)
                    {
                        mask[i] = value;
                    }
                }
            }
        }
    }
}

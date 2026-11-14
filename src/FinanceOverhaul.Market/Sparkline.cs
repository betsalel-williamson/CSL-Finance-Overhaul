using System;

namespace FinanceOverhaul.Market
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

        private const int DashOn = 3;
        private const int DashOff = 3;

        /// <param name="values">Daily prices, oldest first. Non-finite values are skipped.</param>
        /// <param name="thickness">Line thickness in pixels (1 or more).</param>
        public static byte[] Render(float[] values, int width, int height, int thickness = 1)
        {
            if (width <= 0 || height <= 0)
            {
                return new byte[0];
            }

            var mask = new byte[width * height];
            float[] v = FiniteOnly(values);
            if (v.Length == 0)
            {
                return mask;
            }

            thickness = Math.Max(1, thickness);
            float lo = float.MaxValue,
                hi = float.MinValue;
            foreach (float x in v)
            {
                lo = Math.Min(lo, x);
                hi = Math.Max(hi, x);
            }

            int pad = (thickness - 1) / 2;
            int top = pad,
                bottom = height - 1 - (thickness - 1 - pad);
            Func<float, int> rowOf = value =>
                hi - lo < 1e-6f
                    ? (top + bottom) / 2
                    : bottom - (int)Math.Round((value - lo) / (hi - lo) * (bottom - top));
            Func<int, int> colOf = i =>
                v.Length == 1
                    ? width - 1
                    : (int)Math.Round(i * (width - 1) / (double)(v.Length - 1));

            if (v.Length >= 2)
            {
                int yb = rowOf(v[v.Length - 2]);
                for (int x = 0; x < width; x++)
                {
                    if (x % (DashOn + DashOff) < DashOn)
                    {
                        Plot(mask, width, height, x, yb, 1, Baseline);
                    }
                }
            }

            int px = colOf(0),
                py = rowOf(v[0]);
            Plot(mask, width, height, px, py, thickness, Line);
            for (int i = 1; i < v.Length; i++)
            {
                int x = colOf(i),
                    y = rowOf(v[i]);
                DrawLine(mask, width, height, px, py, x, y, thickness);
                px = x;
                py = y;
            }

            return mask;
        }

        private static float[] FiniteOnly(float[] values)
        {
            if (values == null)
            {
                return new float[0];
            }

            int n = 0;
            foreach (float x in values)
            {
                if (!float.IsNaN(x) && !float.IsInfinity(x))
                {
                    n++;
                }
            }

            var v = new float[n];
            int j = 0;
            foreach (float x in values)
            {
                if (!float.IsNaN(x) && !float.IsInfinity(x))
                {
                    v[j++] = x;
                }
            }

            return v;
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

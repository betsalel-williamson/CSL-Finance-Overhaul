using System;

namespace FinanceOverhaul.Market
{
    /// <summary>
    /// Fits one line of text into a fixed-width label: kept at its base scale when it fits,
    /// scaled down (never below a minimum scale) when it nearly fits, and wrapped onto more
    /// lines at the base scale when even the minimum scale is too wide. The Host passes a
    /// measure of the text at a given scale; glyph widths round to whole pixels, so width is
    /// not exactly proportional to scale, so the largest fitting scale is found by bisection on
    /// real measurements.
    /// </summary>
    public static class LabelFit
    {
        private const int Steps = 10;

        public struct Result
        {
            /// <summary>Text scale to draw with.</summary>
            public float Scale;

            /// <summary>True when the text must wrap (drawn at the base scale).</summary>
            public bool Wrap;
        }

        /// <param name="baseScale">The label's normal text scale.</param>
        /// <param name="minScale">Smallest scale allowed before wrapping instead.</param>
        /// <param name="available">The label's width.</param>
        /// <param name="widthAt">Width of the text drawn at a scale.</param>
        public static Result SingleLine(
            float baseScale,
            float minScale,
            float available,
            Func<float, float> widthAt
        )
        {
            if (Fits(widthAt(baseScale), available))
            {
                return new Result { Scale = baseScale, Wrap = false };
            }

            float lo = Math.Min(minScale, baseScale);
            if (lo >= baseScale || !Fits(widthAt(lo), available))
            {
                return new Result { Scale = baseScale, Wrap = true };
            }

            // Largest scale that fits: lo always fits, hi never does.
            float hi = baseScale;
            for (int i = 0; i < Steps; i++)
            {
                float mid = (lo + hi) * 0.5f;
                if (Fits(widthAt(mid), available))
                {
                    lo = mid;
                }
                else
                {
                    hi = mid;
                }
            }

            return new Result { Scale = lo, Wrap = false };
        }

        private static bool Fits(float needed, float available)
        {
            return needed <= 0f || needed <= available;
        }
    }
}

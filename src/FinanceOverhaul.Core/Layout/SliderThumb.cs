using System;

namespace FinanceOverhaul.Core
{
    /// <summary>
    /// Where a horizontal slider's thumb belongs for a value, and where it actually is.
    /// Colossal's UISlider only redraws the thumb when its value changes, not when its
    /// range does, so a range change that keeps the value leaves the thumb drawn for the
    /// old range (Sell at the maximum showing the thumb mid-track). The Host redraws the
    /// thumb after every set; the game UI test uses <see cref="Matches"/> to prove it.
    /// </summary>
    public static class SliderThumb
    {
        /// <summary>Default tolerance: one percent of the track.</summary>
        public const float Tolerance = 0.01f;

        /// <summary>Fraction 0..1 of the track for <paramref name="value"/> in [min, max].
        /// An empty range (max ≤ min) puts the thumb at the end.</summary>
        public static float Fraction(float value, float min, float max)
        {
            if (!(max > min))
            {
                return 1f;
            }

            float f = (value - min) / (max - min);
            return f < 0f ? 0f
                : f > 1f ? 1f
                : f;
        }

        /// <summary>
        /// Fraction of the track the thumb is drawn at, from on-screen positions: the
        /// thumb's left edge relative to the track's left edge, its width, the slider's
        /// thumb offset, and the track width.
        /// </summary>
        public static float DrawnFraction(
            float thumbLeft,
            float thumbWidth,
            float thumbOffset,
            float trackWidth
        )
        {
            if (!(trackWidth > 0f))
            {
                return 0f;
            }

            return (thumbLeft + thumbWidth * 0.5f - thumbOffset) / trackWidth;
        }

        /// <summary>
        /// True when the thumb drawn at <paramref name="drawn"/> shows the value. An empty
        /// range always matches: vanilla disables that slider and its thumb has no place.
        /// </summary>
        public static bool Matches(float drawn, float value, float min, float max)
        {
            if (!(max > min))
            {
                return true;
            }

            return Math.Abs(drawn - Fraction(value, min, max)) <= Tolerance;
        }
    }
}

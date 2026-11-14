using System;

namespace FinanceOverhaul.Market
{
    /// <summary>
    /// Geometry and blink rule for the custom text caret. Colossal's UITextField
    /// draws its caret from padding.top (the top of the digits), so it can never
    /// reach above the text; the Host hides it and draws this bar instead, sized
    /// like a desktop text caret.
    /// </summary>
    public static class CaretLayout
    {
        /// <summary>How far the caret reaches above the digit tops (fraction of em).</summary>
        public const float AboveEm = 0.12f;

        /// <summary>How far the caret reaches below the baseline (fraction of em).</summary>
        public const float BelowEm = 0.18f;

        public struct Bar
        {
            public float Top;
            public float Height;
        }

        /// <param name="textTop">Where the text line starts (padding.top).</param>
        /// <param name="glyphTop">Distance from line top to the top of the digits.</param>
        /// <param name="glyphHeight">Digit height.</param>
        /// <param name="fontSize">Rendered font size (size × textScale).</param>
        public static Bar Measure(float textTop, float glyphTop, float glyphHeight, float fontSize)
        {
            float top = textTop + glyphTop - fontSize * AboveEm;
            float bottom = textTop + glyphTop + glyphHeight + fontSize * BelowEm;
            top = Math.Max(0f, top);
            return new Bar { Top = top, Height = bottom - top };
        }

        /// <summary>
        /// Solid for the first half of each blink period after the last edit or
        /// cursor move; <paramref name="blinkTime"/> ≤ 0 means always solid.
        /// </summary>
        public static bool IsVisible(float secondsSinceChange, float blinkTime)
        {
            if (blinkTime <= 0f || secondsSinceChange < 0f)
            {
                return true;
            }

            float phase = secondsSinceChange % (blinkTime * 2f);
            return phase < blinkTime;
        }
    }
}

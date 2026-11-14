using System;

namespace FinanceOverhaul.Market
{
    /// <summary>
    /// Centers single-line text inside a left-aligned text box by padding alone.
    /// Colossal's UITextField positions caret and selection correctly only for
    /// left alignment and draws from padding.top, so the Host measures the text and
    /// asks for paddings here instead of using Center/Middle alignment.
    /// </summary>
    public static class TextBoxLayout
    {
        public struct Padding
        {
            public int Left;
            public int Top;
            public int Bottom;
        }

        /// <param name="boxWidth">Text box width (UI units).</param>
        /// <param name="boxHeight">Text box height (UI units).</param>
        /// <param name="textWidth">Measured width of the current text.</param>
        /// <param name="glyphTop">Distance from the line top to the top of the digits.</param>
        /// <param name="glyphHeight">Height of the digits.</param>
        /// <param name="descent">
        /// Extra room below the digits for the caret and selection highlight, which
        /// Colossal draws from padding.top with height (box - vertical padding).
        /// </param>
        /// <param name="minSide">Smallest left padding allowed.</param>
        public static Padding Center(
            float boxWidth,
            float boxHeight,
            float textWidth,
            float glyphTop,
            float glyphHeight,
            float descent,
            int minSide
        )
        {
            int left = (int)
                Math.Round((boxWidth - textWidth) * 0.5f, MidpointRounding.AwayFromZero);
            int top = (int)
                Math.Round(
                    (boxHeight - glyphHeight) * 0.5f - glyphTop,
                    MidpointRounding.AwayFromZero
                );
            top = Math.Max(0, top);
            int bottom = (int)
                Math.Round(
                    boxHeight - top - glyphTop - glyphHeight - descent,
                    MidpointRounding.AwayFromZero
                );
            return new Padding
            {
                Left = Math.Max(minSide, left),
                Top = top,
                Bottom = Math.Max(0, bottom),
            };
        }
    }
}

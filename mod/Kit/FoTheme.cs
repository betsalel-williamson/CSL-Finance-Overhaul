using UnityEngine;

namespace FinanceOverhaul
{
    /// <summary>
    /// The colours the mod's UI is drawn in, one name per colour, matching the Figma library's
    /// variables where one exists. Surfaces take colours from here instead of declaring their
    /// own, so a colour is changed in one place.
    /// </summary>
    internal static class FoTheme
    {
        /// <summary>Selected tab, switches on, accents. Figma: color/bg/tab-active.</summary>
        public static readonly Color32 Accent = new Color32(101, 214, 248, 255);

        /// <summary>Title bars and text on light pills. Figma: color/bg/header.</summary>
        public static readonly Color32 Header = new Color32(9, 14, 17, 255);

        /// <summary>Background of the mod's own windows.</summary>
        public static readonly Color32 Window = new Color32(10, 13, 15, 255);

        /// <summary>Background of a typed field.</summary>
        public static readonly Color32 Field = new Color32(28, 28, 28, 255);

        /// <summary>Figma: color/text/primary.</summary>
        public static readonly Color32 TextPrimary = new Color32(250, 251, 255, 255);

        /// <summary>Figma: color/text/muted.</summary>
        public static readonly Color32 TextMuted = new Color32(155, 154, 159, 255);

        /// <summary>Hints and secondary lines inside the Economy panel.</summary>
        public static readonly Color32 Hint = new Color32(160, 172, 182, 255);

        /// <summary>Chart axis labels and secondary text in the Stock Monitor.</summary>
        public static readonly Color32 ChartLabel = new Color32(158, 163, 170, 255);

        /// <summary>A buy order, an estimate that is fine.</summary>
        public static readonly Color32 Buy = new Color32(78, 205, 96, 255);

        /// <summary>A sell order, a refused bond.</summary>
        public static readonly Color32 Sell = new Color32(233, 150, 66, 255);

        /// <summary>A falling price, an error. Figma: color/ticker/loss.</summary>
        public static readonly Color32 Loss = new Color32(233, 66, 79, 255);

        /// <summary>A rising price in the Stock Monitor and on the boards. Figma: color/ticker/gain.</summary>
        public static readonly Color32 TickerGain = new Color32(78, 244, 101, 255);

        /// <summary>Ticker symbols and the order pill. Figma: color/ticker/symbol.</summary>
        public static readonly Color32 TickerSymbol = new Color32(239, 176, 88, 255);

        /// <summary>Chart grid lines.</summary>
        public static readonly Color32 ChartGrid = new Color32(255, 255, 255, 40);

        /// <summary>The vertical line under the pointer on a chart.</summary>
        public static readonly Color32 HoverLine = new Color32(255, 255, 255, 150);

        /// <summary>Background of a chart hover readout.</summary>
        public static readonly Color32 Tip = new Color32(9, 14, 17, 235);

        /// <summary>Fully transparent.</summary>
        public static readonly Color32 Clear = new Color32(0, 0, 0, 0);
    }
}

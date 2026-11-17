using FinanceOverhaul.Core;
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
        // The game's own colours, read from the running game (Economy panel). Anything the mod
        // adds inside the game's panels uses these, so it reads as part of the game. Game UI
        // scenario 53 fails if the game's values ever differ from these.

        /// <summary>Money that went up: EconomyPanel.m_positiveNumberColor.</summary>
        public static readonly Color32 GamePositive = new Color32(151, 238, 0, 255);

        /// <summary>Money that went down: EconomyPanel.m_negativeNumberColor.</summary>
        public static readonly Color32 GameNegative = new Color32(254, 150, 89, 255);

        /// <summary>A money value, and a change of zero: EconomyPanel.m_neutralNumberColor.</summary>
        public static readonly Color32 GameValue = new Color32(185, 221, 254, 255);

        /// <summary>Text on the game's panels, tabs and buttons (Economy panel, Invest dialog).</summary>
        public static readonly Color32 GameText = new Color32(254, 254, 254, 255);

        /// <summary>Text of a game button that can't be pressed (Loans tab, "Take").</summary>
        public static readonly Color32 GameDisabledText = new Color32(91, 97, 106, 255);

        /// <summary>A heading or row label on the Loans tab: the game uses its gain colour.</summary>
        public static readonly Color32 GameHeading = GamePositive;

        /// <summary>The panel sprite of the game's loan cards (Economy → Loans).</summary>
        public const string GameLoanCardSprite = "UnlockingPanel2";

        /// <summary>A bond card's tint on that sprite: the blue of a loan the city has taken.</summary>
        public static readonly Color32 BondCard = new Color32(90, 150, 255, 255);

        /// <summary>
        /// Tint of a typed field inside a game panel: the tint of the Invest dialog's own
        /// share count, drawn on <see cref="GameFieldSprite"/>.
        /// </summary>
        public static readonly Color32 Field = new Color32(52, 52, 52, 255);

        /// <summary>The sprite behind the Invest dialog's share count.</summary>
        public const string GameFieldSprite = "GenericPanelLight";

        // The mod's own colours (windows and text the game has no equivalent for).

        /// <summary>Selected tab, switches on, accents. Figma: color/bg/tab-active.</summary>
        public static readonly Color32 Accent = new Color32(101, 214, 248, 255);

        /// <summary>Title bars and text on light pills. Figma: color/bg/header.</summary>
        public static readonly Color32 Header = new Color32(9, 14, 17, 255);

        /// <summary>Background of the mod's own windows. Figma: color/bg/header.</summary>
        public static readonly Color32 Window = new Color32(9, 14, 17, 255);

        /// <summary>Frame of a dialog. Figma: color/bg/dialog.</summary>
        public static readonly Color32 Dialog = new Color32(50, 55, 58, 255);

        /// <summary>A card inside a dialog. Figma: color/bg/panel-alt.</summary>
        public static readonly Color32 PanelAlt = new Color32(24, 29, 35, 255);

        /// <summary>Body text and unselected tabs. Figma: color/text/label.</summary>
        public static readonly Color32 TextLabel = new Color32(210, 213, 218, 255);

        /// <summary>A change of 0.00 and unknown values. Figma: color/ticker/flat.</summary>
        public static readonly Color32 TickerFlat = new Color32(159, 176, 192, 255);

        /// <summary>Figma: color/text/primary.</summary>
        public static readonly Color32 TextPrimary = new Color32(250, 251, 255, 255);

        /// <summary>Figma: color/text/muted.</summary>
        public static readonly Color32 TextMuted = new Color32(155, 154, 159, 255);

        /// <summary>Chart axis labels and secondary text in the Stock Monitor.</summary>
        public static readonly Color32 ChartLabel = new Color32(158, 163, 170, 255);

        /// <summary>A falling price, an error. Figma: color/ticker/loss.</summary>
        public static readonly Color32 Loss = new Color32(233, 66, 79, 255);

        /// <summary>A rising price in the Stock Monitor and on the boards. Figma: color/ticker/gain.</summary>
        public static readonly Color32 TickerGain = new Color32(78, 244, 101, 255);

        /// <summary>Ticker symbols and the order pill. Figma: color/ticker/symbol.</summary>
        public static readonly Color32 TickerSymbol = new Color32(239, 176, 88, 255);

        /// <summary>Chart grid lines, as the game's share chart draws them. Figma: color/chart/grid.</summary>
        public static readonly Color32 ChartGrid = new Color32(117, 121, 124, 255);

        /// <summary>The City bonds section's own panel (the game has no dark panel on the Loans tab).</summary>
        public static readonly Color32 BondsPanel = new Color32(24, 30, 36, 245);

        /// <summary>The vertical line under the pointer on a chart.</summary>
        public static readonly Color32 HoverLine = new Color32(255, 255, 255, 150);

        /// <summary>Background of a chart hover readout.</summary>
        public static readonly Color32 Tip = new Color32(9, 14, 17, 235);

        /// <summary>Fully transparent.</summary>
        public static readonly Color32 Clear = new Color32(0, 0, 0, 0);

        /// <summary>
        /// The colour of a price change in the Stock Monitor's style (pills, sparklines, share
        /// charts), from the library's verdict on the rounded rate: gain, loss or flat.
        /// </summary>
        public static Color32 Change(byte rate)
        {
            return rate == LedText.Gain ? TickerGain
                : rate == LedText.Loss ? Loss
                : TickerFlat;
        }
    }
}

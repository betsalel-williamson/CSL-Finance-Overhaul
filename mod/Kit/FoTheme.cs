using FinanceOverhaul.Core; // LedText
using UnityEngine; // Color32, Mathf

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

        /// <summary>Text on the chosen tab of a game tab strip (Economy: Taxes · … · Investments).</summary>
        public static readonly Color32 GameTabChosenText = new Color32(16, 16, 16, 255);

        /// <summary>
        /// A secondary tab group's idle tabs: the game's tab art dimmed, so a sub-choice reads
        /// quieter than the group above it.
        /// </summary>
        public static readonly Color32 SubTabIdle = new Color32(150, 150, 150, 255);

        /// <summary>Text on a disabled tab of a game tab strip.</summary>
        public static readonly Color32 GameTabDisabledText = new Color32(111, 111, 111, 255);

        /// <summary>A heading or row label on the Loans tab: the game uses its gain colour.</summary>
        public static readonly Color32 GameHeading = GamePositive;

        /// <summary>The tint of a loan the game offers (Economy → Loans): its money green.</summary>
        public static readonly Color32 GameLoanOffer = new Color32(79, 219, 0, 255);

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

        /// <summary>The tint of the game's stock cards (Investments tab).</summary>
        public static readonly Color32 GameStockCard = new Color32(107, 118, 135, 255);

        /// <summary>The background of the game's Share value chart.</summary>
        public static readonly Color32 GameChartBack = new Color32(30, 31, 33, 255);

        /// <summary>The game's own tint for a dialog's body sprite (the IPO dialog uses it).</summary>
        public static readonly Color32 GameDialogBody = new Color32(160, 160, 160, 255);

        /// <summary>An order card when the game's own card style can't be read.</summary>
        public static readonly Color32 GameCardFallback = new Color32(32, 38, 44, 255);

        /// <summary>A sprite drawn in its own colours: no tint.</summary>
        public static readonly Color32 Untinted = new Color32(255, 255, 255, 255);

        // The game's stock coins (drawn by CoinIcons): gold face, darker rim and side, a
        // lighter ring, a dark symbol.

        /// <summary>Coin face.</summary>
        public static readonly Color32 CoinFace = new Color32(246, 184, 60, 255);

        /// <summary>Coin rim.</summary>
        public static readonly Color32 CoinRim = new Color32(232, 152, 28, 255);

        /// <summary>Coin side.</summary>
        public static readonly Color32 CoinEdge = new Color32(186, 112, 16, 255);

        /// <summary>The lighter ring inside the rim.</summary>
        public static readonly Color32 CoinRing = new Color32(255, 214, 110, 255);

        /// <summary>The symbol stamped on the coin.</summary>
        public static readonly Color32 CoinSymbol = new Color32(122, 72, 6, 255);

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

        /// <summary>The dot on the line under the pointer on a chart.</summary>
        public static readonly Color32 HoverDot = new Color32(255, 255, 255, 255);

        /// <summary>Background of a chart hover readout.</summary>
        public static readonly Color32 Tip = new Color32(9, 14, 17, 235);

        /// <summary>Background of the Stock Monitor's settings popover: the window, see-through.</summary>
        public static readonly Color32 Popover = new Color32(9, 14, 17, 235);

        /// <summary>The track of a slider in the settings popover.</summary>
        public static readonly Color32 SliderTrack = new Color32(255, 255, 255, 64);

        /// <summary>A line between rows in the Stock Monitor.</summary>
        public static readonly Color32 Divider = new Color32(255, 255, 255, 26);

        /// <summary>The panel behind the Stock Monitor's detail chart (drawn at 35% opacity).</summary>
        public static readonly Color32 ChartBack = new Color32(0, 0, 0, 255);

        /// <summary>The shadow under every label the mod draws.</summary>
        public static readonly Color32 TextShadow = new Color32(0, 0, 0, 220);

        /// <summary>The arrow on a sort-direction button.</summary>
        public static readonly Color32 ButtonGlyph = new Color32(230, 236, 240, 255);

        /// <summary>Behind the gold symbol of a stock the city holds (Stock Monitor rows).</summary>
        public static readonly Color32 HeldBack = new Color32(70, 52, 24, 255);

        /// <summary>The track behind a progress bar (company cards, building panel).</summary>
        public static readonly Color32 BarTrack = new Color32(8, 12, 18, 255);

        // A company's building panel: the gold of an IPO offer.

        /// <summary>The Buy shares button while the company is offering shares.</summary>
        public static readonly Color32 OfferGold = new Color32(240, 170, 36, 255);

        /// <summary>That button under the pointer.</summary>
        public static readonly Color32 OfferGoldHover = new Color32(255, 200, 80, 255);

        /// <summary>Text on the gold button.</summary>
        public static readonly Color32 OfferGoldText = new Color32(58, 36, 4, 255);

        /// <summary>A section inside the building panel.</summary>
        public static readonly Color32 SectionFill = new Color32(30, 36, 44, 255);

        /// <summary>A section that opens something when clicked, under the mouse.</summary>
        public static readonly Color32 SectionFillHover = new Color32(40, 48, 58, 255);

        // The Stock Exchange's LED boards (dot colours: LedPalette in the library).

        /// <summary>The board face behind the dots. Figma: color/ticker/board.</summary>
        public static readonly Color32 LedBoard = new Color32(0x1E, 0x30, 0x3E, 0xFF);

        /// <summary>The frame around a board.</summary>
        public static readonly Color32 LedFrame = new Color32(0x0E, 0x14, 0x18, 0xFF);

        /// <summary>Fully transparent.</summary>
        public static readonly Color32 Clear = new Color32(0, 0, 0, 0);

        // The heatmap's tiles, as the Stitch design: grey for no change, then for a rise dark
        // green, green and the game's lime; for a fall dark red, red and the game's orange.

        /// <summary>A heatmap tile that did not move.</summary>
        public static readonly Color32 HeatFlat = new Color32(54, 60, 68, 255);

        /// <summary>A small rise.</summary>
        public static readonly Color32 HeatGainLow = new Color32(28, 96, 52, 255);

        /// <summary>A middling rise.</summary>
        public static readonly Color32 HeatGainMid = new Color32(40, 178, 92, 255);

        /// <summary>A rise at the top of the scale: the game's gain lime.</summary>
        public static readonly Color32 HeatGain = GamePositive;

        /// <summary>A small fall.</summary>
        public static readonly Color32 HeatLossLow = new Color32(116, 40, 42, 255);

        /// <summary>A middling fall.</summary>
        public static readonly Color32 HeatLossMid = new Color32(218, 78, 78, 255);

        /// <summary>A fall at the bottom of the scale: the game's loss orange.</summary>
        public static readonly Color32 HeatLoss = GameNegative;

        /// <summary>Text on a bright tile (lime, orange): the chosen tab's near-black.</summary>
        public static readonly Color32 HeatInkDark = GameTabChosenText;

        /// <summary>
        /// A tile's colour for a heat from −1 (biggest fall) to +1 (biggest rise): grey at 0, the
        /// low shade by 0.12 (so a small move already reads), the middle at 0.5, the end at 1.
        /// </summary>
        public static Color32 Heat(float heat)
        {
            float t = Mathf.Clamp01(Mathf.Abs(heat));
            bool up = heat >= 0f;
            Color32 low = up ? HeatGainLow : HeatLossLow;
            Color32 mid = up ? HeatGainMid : HeatLossMid;
            Color32 high = up ? HeatGain : HeatLoss;
            if (t <= 0.12f)
            {
                return Color32.Lerp(HeatFlat, low, t / 0.12f);
            }

            return t <= 0.5f
                ? Color32.Lerp(low, mid, (t - 0.12f) / 0.38f)
                : Color32.Lerp(mid, high, (t - 0.5f) / 0.5f);
        }

        /// <summary>The text colour that reads on a tile: near-black on the bright end, else white.</summary>
        public static Color32 HeatInk(Color32 tile)
        {
            float luminance = (0.299f * tile.r + 0.587f * tile.g + 0.114f * tile.b) / 255f;
            return luminance > 0.55f ? HeatInkDark : GameText;
        }

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

        /// <summary>
        /// The game's colours for a direction: its gain green when <paramref name="sign"/> is
        /// above 0, its loss orange below, else <paramref name="level"/> (each caller's neutral).
        /// </summary>
        public static Color32 Trend(int sign, Color32 level)
        {
            return sign > 0 ? GamePositive
                : sign < 0 ? GameNegative
                : level;
        }

        /// <summary>
        /// The parts of a chart with several (the holdings donut), in order: the accent, then
        /// the game's gain green, the offer gold, the bond blue and the loss orange.
        /// </summary>
        public static readonly Color32[] Series =
        {
            Accent,
            GamePositive,
            OfferGold,
            BondCard,
            GameNegative,
        };

        /// <summary>The "Other" part of such a chart.</summary>
        public static readonly Color32 SeriesOther = TickerFlat;
    }
}

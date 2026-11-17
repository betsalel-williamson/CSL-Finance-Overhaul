using ColossalFramework.UI;
using UnityEngine;

namespace FinanceOverhaul
{
    /// <summary>Text treatments the game's two font weights (Regular, Semibold) do not cover.</summary>
    internal static class FoText
    {
        /// <summary>
        /// A heavier weight than Semibold, for small text on a coloured pill. The game has no
        /// bold font, so each glyph is drawn twice, the second copy a fraction of a pixel to
        /// the right in the same colour, which thickens the strokes without blurring them (a
        /// full outline smears text this small).
        /// </summary>
        public static void Heavy(UILabel label)
        {
            label.useOutline = false;
            label.useDropShadow = true;
            label.dropShadowColor = label.textColor;
            label.dropShadowOffset = new Vector2(0.6f, 0f);
        }
    }
}

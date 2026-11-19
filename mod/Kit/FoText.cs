using ColossalFramework.UI; // UIComponent, UILabel, UIVerticalAlignment
using FinanceOverhaul.Core; // LabelFit
using UnityEngine; // Color32, Mathf, Vector2

namespace FinanceOverhaul
{
    /// <summary>Labels as the mod's windows draw them, and the text treatments they share.</summary>
    internal static class FoText
    {
        /// <summary>A label with the soft drop shadow that keeps text readable over the city.</summary>
        public static UILabel Label(
            UIComponent parent,
            string name,
            string text,
            float scale,
            Color32 colour
        )
        {
            UILabel l = parent.AddUIComponent<UILabel>();
            if (name != null)
            {
                l.name = name;
            }

            l.text = text;
            l.textScale = scale;
            l.textColor = colour;
            l.useDropShadow = true;
            l.dropShadowColor = new Color32(0, 0, 0, 220);
            l.dropShadowOffset = new Vector2(1f, -1f);
            return l;
        }

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

        /// <summary>
        /// Puts text in a label of a fixed width so it is never cut off: on one line at its
        /// normal scale, scaled down (not below <paramref name="minScale"/>) when it nearly
        /// fits, wrapped at the normal scale when it does not. Returns the label's height.
        /// The decision is <see cref="LabelFit"/>; this measures and applies it.
        /// </summary>
        public static float Fit(
            UILabel label,
            string text,
            float baseScale,
            float minScale,
            float width,
            float lineHeight
        )
        {
            label.autoHeight = false;
            label.wordWrap = false;
            label.text = text;
            LabelFit.Result fit = LabelFit.SingleLine(
                baseScale,
                minScale,
                width,
                scale =>
                {
                    label.autoSize = false;
                    label.textScale = scale;
                    label.autoSize = true;
                    return label.width;
                }
            );
            label.autoSize = false;
            label.textScale = fit.Scale;
            if (fit.Wrap)
            {
                label.wordWrap = true;
                label.width = width;
                label.autoHeight = true;
                return Mathf.Max(lineHeight, label.height);
            }

            label.size = new Vector2(width, lineHeight);
            // A scaled-down line stays centred on the row's normal line.
            label.verticalAlignment =
                fit.Scale < baseScale ? UIVerticalAlignment.Middle : UIVerticalAlignment.Top;
            return lineHeight;
        }
    }
}

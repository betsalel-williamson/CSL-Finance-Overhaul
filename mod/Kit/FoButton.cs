using ColossalFramework.UI; // UIButton, UIComponent
using UnityEngine; // Color32, FilterMode, Texture2D, TextureFormat, Vector2

namespace FinanceOverhaul
{
    /// <summary>
    /// A button in the game's own menu-button art, used for tabs, range chips, sort controls
    /// and actions in the mod's windows (Figma: FO/Button, FO/OrderTab, FO/SortDirection).
    /// </summary>
    internal static class FoButton
    {
        public static UIButton Add(
            UIComponent parent,
            string name,
            string text,
            Vector2 size,
            float textScale
        )
        {
            UIButton b = parent.AddUIComponent<UIButton>();
            b.name = name;
            b.text = text;
            b.textScale = textScale;
            b.size = size;
            b.normalBgSprite = "ButtonMenu";
            b.hoveredBgSprite = "ButtonMenuHovered";
            b.pressedBgSprite = "ButtonMenuPressed";
            b.disabledBgSprite = "ButtonMenuDisabled";
            return b;
        }

        /// <summary>A tab or chip that is the current choice looks pressed, with primary text.</summary>
        public static void SetSelected(UIButton b, bool on)
        {
            b.normalBgSprite = on ? "ButtonMenuPressed" : "ButtonMenu";
            b.focusedBgSprite = b.normalBgSprite;
            b.textColor = on ? FoTheme.TextPrimary : FoTheme.TextLabel;
        }

        /// <summary>
        /// A small filled triangle for a direction button: pointing down or up. The caller
        /// owns the texture and destroys it when it is replaced.
        /// </summary>
        public static Texture2D ArrowTexture(bool down)
        {
            const int w = 10,
                h = 6;
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            t.name = "FinanceOverhaulSortArrow";
            t.filterMode = FilterMode.Point;
            for (int y = 0; y < h; y++)
            {
                // row y counted from the texture bottom; the wide edge is at the top for "down".
                int fromWide = down ? h - 1 - y : y;
                int half = w / 2 - fromWide * w / (2 * h);
                for (int x = 0; x < w; x++)
                {
                    bool lit = x >= w / 2 - half && x < w / 2 + half;
                    t.SetPixel(x, y, lit ? new Color32(230, 236, 240, 255) : FoTheme.Clear);
                }
            }

            t.Apply();
            return t;
        }
    }
}

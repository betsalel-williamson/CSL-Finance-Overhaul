using ColossalFramework.UI;
using UnityEngine;

namespace FinanceOverhaul
{
    /// <summary>
    /// The title bar of one of the mod's own windows, built from the game's parts so it reads
    /// like a game panel: the caption font the game's info view panels use at its normal size,
    /// the game's close button and its round gear button, sized and placed like the City
    /// Vitals window players already know (40 px bar, 32 px close flush right, 24 px gear
    /// beside it).
    /// </summary>
    internal static class FoWindow
    {
        public const float TitleBarHeight = 40f;
        public const float CloseSize = 32f;
        public const float GearSize = 24f;

        /// <summary>Width the two buttons take from the right edge (the drag handle stops here).</summary>
        public const float ButtonsWidth = CloseSize + GearSize;

        /// <summary>A centred caption across the title bar.</summary>
        public static UILabel AddTitle(UIComponent window, string name, string text, float width)
        {
            UILabel title = window.AddUIComponent<UILabel>();
            title.name = name;
            UIFont font = CaptionFont();
            if (font != null)
            {
                title.font = font;
            }

            title.text = text;
            title.textColor = FoTheme.TextPrimary;
            title.autoSize = false;
            title.size = new Vector2(width, TitleBarHeight);
            title.textAlignment = UIHorizontalAlignment.Center;
            title.verticalAlignment = UIVerticalAlignment.Middle;
            title.relativePosition = Vector3.zero;
            title.isInteractive = false;
            return title;
        }

        /// <summary>The game's close button, flush with the right edge.</summary>
        public static UIButton AddClose(UIComponent window, string name, float width)
        {
            UIButton close = window.AddUIComponent<UIButton>();
            close.name = name;
            close.size = new Vector2(CloseSize, CloseSize);
            close.normalBgSprite = "buttonclose";
            close.hoveredBgSprite = "buttonclosehover";
            close.pressedBgSprite = "buttonclosepressed";
            close.relativePosition = new Vector3(width - CloseSize, 2f);
            return close;
        }

        /// <summary>The game's round options (gear) button, left of the close button.</summary>
        public static UIButton AddGear(UIComponent window, string name, float width)
        {
            UIButton gear = window.AddUIComponent<UIButton>();
            gear.name = name;
            gear.size = new Vector2(GearSize, GearSize);
            gear.normalBgSprite = "RoundBackBig";
            gear.hoveredBgSprite = "RoundBackBigHovered";
            gear.pressedBgSprite = "RoundBackBigPressed";
            gear.normalFgSprite = "Options";
            gear.relativePosition = new Vector3(width - CloseSize - GearSize, 7f);
            return gear;
        }

        /// <summary>
        /// Fades a whole window: its background, text, pills and charts together. A child
        /// keeps the opacity it was last drawn with until it is redrawn, so every part of the
        /// window is redrawn with the new value.
        /// </summary>
        public static void SetOpacity(UIComponent window, float opacity)
        {
            window.opacity = opacity;
            foreach (UIComponent part in window.GetComponentsInChildren<UIComponent>(true))
            {
                part.Invalidate();
            }
        }

        /// <summary>
        /// The font of the game's info view panel captions ("Electricity", "Water"…); null
        /// when that panel is not there, and the label then keeps the UI's default font.
        /// </summary>
        private static UIFont CaptionFont()
        {
            try
            {
                ElectricityInfoViewPanel panel =
                    Object.FindObjectOfType<ElectricityInfoViewPanel>();
                UILabel caption = panel == null ? null : panel.Find<UILabel>("Label");
                return caption == null ? null : caption.font;
            }
            catch
            {
                return null;
            }
        }
    }
}

using System; // Action
using ColossalFramework.UI; // UIComponent, UIHorizontalAlignment, UILabel, UIVerticalAlignment
using UnityEngine; // Color32, RectOffset, Vector2

namespace FinanceOverhaul
{
    /// <summary>
    /// Text on a rounded coloured background: the change pill and the order-count badge
    /// (Figma: the pills in FO/InvestmentRow and the Stock Monitor rows).
    /// </summary>
    internal static class FoPill
    {
        /// <summary>A fixed-size pill with heavy, right-aligned text (a number).</summary>
        public static UILabel AddValue(UIComponent parent, Vector2 size, float scale, Color32 text)
        {
            UILabel pill = FoText.Label(parent, null, "", scale, text);
            pill.autoSize = false;
            FoText.Heavy(pill); // dark text on the loss red needs the extra weight
            pill.size = size;
            pill.textAlignment = UIHorizontalAlignment.Right;
            pill.verticalAlignment = UIVerticalAlignment.Middle;
            pill.backgroundSprite = "GenericPanelWhite";
            pill.padding = new RectOffset(0, 6, 2, 0);
            return pill;
        }

        /// <summary>
        /// A value pill that does something when clicked (the change pills: flip every pill
        /// between % and ₡). The click is used up, so a row under the pill does not also take it.
        /// </summary>
        public static UILabel AddToggle(
            UIComponent parent,
            string name,
            Vector2 size,
            float scale,
            Color32 text,
            string tooltip,
            Action click
        )
        {
            UILabel pill = AddValue(parent, size, scale, text);
            pill.name = name;
            MakeToggle(pill, tooltip, click);
            return pill;
        }

        /// <summary>Makes an existing pill clickable as <see cref="AddToggle"/> does.</summary>
        public static void MakeToggle(UILabel pill, string tooltip, Action click)
        {
            pill.isInteractive = true;
            pill.tooltip = tooltip;
            pill.eventClick += (c, p) =>
            {
                p.Use();
                click();
            };
        }

        /// <summary>A small badge that hugs its text.</summary>
        public static UILabel AddBadge(UIComponent parent, float scale, Color32 text, Color32 fill)
        {
            UILabel badge = FoText.Label(parent, null, "", scale, text);
            badge.backgroundSprite = "GenericPanelWhite";
            badge.color = fill;
            badge.padding = new RectOffset(4, 4, 2, 1);
            return badge;
        }
    }
}

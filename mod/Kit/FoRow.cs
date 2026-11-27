using System; // Action, Func
using ColossalFramework.UI; // UIComponent, UIMouseEventParameter, UIPanel
using UnityEngine; // Vector2, Vector3

namespace FinanceOverhaul
{
    /// <summary>
    /// A row or section on the game's plain card: the section fill, lighter under the mouse
    /// when a click does something (Figma: the section fills of FO/InvestmentRow and the
    /// overview's rows).
    /// </summary>
    internal static class FoRow
    {
        /// <param name="click">What a click does; null for a row that only shows.</param>
        /// <param name="lights">
        /// Whether the row lights up under the mouse now (default: whenever it has a click).
        /// </param>
        public static UIPanel Add(
            UIComponent parent,
            string name,
            Vector2 size,
            Vector3 at,
            Action<UIMouseEventParameter> click,
            Func<bool> lights = null
        )
        {
            UIPanel row = parent.AddUIComponent<UIPanel>();
            row.name = name;
            row.backgroundSprite = "GenericPanelWhite"; // not shown: a game sprite name
            row.color = FoTheme.SectionFill;
            row.size = size;
            row.relativePosition = at;
            if (click != null)
            {
                row.eventClick += (c, p) => click(p);
            }

            if (click != null || lights != null)
            {
                row.eventMouseEnter += (c, p) =>
                {
                    if (lights == null || lights())
                    {
                        row.color = FoTheme.SectionFillHover;
                    }
                };
                row.eventMouseLeave += (c, p) => row.color = FoTheme.SectionFill;
            }

            return row;
        }
    }
}

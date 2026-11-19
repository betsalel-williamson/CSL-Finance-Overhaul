using System; // Action
using ColossalFramework.UI; // UIComponent, UISprite
using UnityEngine; // Vector2, Vector3

namespace FinanceOverhaul
{
    /// <summary>
    /// The game's own checkbox art (the box and tick used in Options and in City Vitals'
    /// settings), 16 px. The caller owns the row and the click; this draws the box and
    /// returns how to show it on or off.
    /// </summary>
    internal static class FoCheckbox
    {
        public const float Size = 16f;

        public static Action<bool> AddBox(UIComponent row, float x, float y)
        {
            UISprite box = row.AddUIComponent<UISprite>();
            box.spriteName = "check-unchecked";
            box.size = new Vector2(Size, Size);
            box.relativePosition = new Vector3(x, y);
            box.isInteractive = false;

            UISprite tick = box.AddUIComponent<UISprite>();
            tick.spriteName = "check-checked";
            tick.size = new Vector2(Size, Size);
            tick.relativePosition = Vector3.zero;
            tick.isInteractive = false;
            return on => tick.isVisible = on;
        }
    }
}

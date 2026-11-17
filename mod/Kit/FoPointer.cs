using ColossalFramework.UI;
using UnityEngine;

namespace FinanceOverhaul
{
    /// <summary>
    /// Where the mouse is, in the game's UI units. Game UI tests set <see cref="Test"/> to
    /// stand in for the mouse, so hover behaviour can be checked without moving the real one.
    /// </summary>
    internal static class FoPointer
    {
        /// <summary>A simulated position (dev builds' game UI tests); null for the real mouse.</summary>
        public static Vector2? Test;

        /// <summary>The simulated position when a test set one, else the real mouse.</summary>
        public static Vector2 Position
        {
            get { return Test ?? Mouse(); }
        }

        /// <summary>The real mouse: screen pixels (y up) to UI units (y down).</summary>
        public static Vector2 Mouse()
        {
            float scale = UIView.GetAView().GetScreenResolution().y / Mathf.Max(1f, Screen.height);
            Vector3 m = Input.mousePosition;
            return new Vector2(m.x * scale, (Screen.height - m.y) * scale);
        }
    }
}

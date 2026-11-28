// ColossalFramework.UI: UIComponent, UIFillDirection, UIOrientation, UIPivotPoint, UIScrollbar,
//   UISlicedSprite
using ColossalFramework.UI;
using UnityEngine; // Vector2, Vector3

namespace FinanceOverhaul
{
    /// <summary>
    /// A vertical scrollbar in the game's style (its track and thumb sprites) that hides itself
    /// while everything fits. Give it to a <c>UIScrollablePanel</c> as its
    /// <c>verticalScrollbar</c>; the panel sizes the thumb and moves with it.
    /// </summary>
    internal static class FoScrollbar
    {
        /// <summary>Its width; leave this much (and a little gap) beside the list.</summary>
        public const float Width = 12f;

        public static UIScrollbar Add(UIComponent parent, string name, Vector3 at, float height)
        {
            UIScrollbar bar = parent.AddUIComponent<UIScrollbar>();
            bar.name = name;
            bar.relativePosition = at;
            bar.size = new Vector2(Width, height);
            bar.orientation = UIOrientation.Vertical;
            bar.pivot = UIPivotPoint.TopLeft;
            bar.minValue = 0f;
            bar.value = 0f;
            bar.incrementAmount = 50f;
            bar.autoHide = true;
            UISlicedSprite track = bar.AddUIComponent<UISlicedSprite>();
            track.relativePosition = Vector3.zero;
            track.autoSize = true;
            track.size = bar.size;
            track.fillDirection = UIFillDirection.Vertical;
            track.spriteName = "ScrollbarTrack"; // not shown: a game sprite name
            bar.trackObject = track;
            UISlicedSprite thumb = track.AddUIComponent<UISlicedSprite>();
            thumb.relativePosition = new Vector3(2f, 0f); // centred in the track, as the game's
            thumb.fillDirection = UIFillDirection.Vertical;
            thumb.autoSize = true;
            thumb.width = Width - 4f;
            thumb.spriteName = "ScrollbarThumb"; // not shown: a game sprite name
            bar.thumbObject = thumb;
            return bar;
        }
    }
}

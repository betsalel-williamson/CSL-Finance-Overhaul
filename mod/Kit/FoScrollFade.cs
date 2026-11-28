using ColossalFramework.UI; // UIComponent, UIScrollablePanel
using FinanceOverhaul.Core; // ScrollFade

namespace FinanceOverhaul
{
    /// <summary>
    /// The scrolling-list rule's edge fades for a list whose rows fade themselves: the rows
    /// dissolve as they reach an edge with more past it (Figma band 12 option A), whatever is
    /// behind the list (the Stock Monitor is see-through). Call <see cref="Rows"/> when the list
    /// scrolls and after its rows change; <see cref="Attach"/> does the scrolling part.
    /// </summary>
    internal static class FoScrollFade
    {
        /// <summary>Fades the list's rows again on every scroll.</summary>
        public static void Attach(UIScrollablePanel list)
        {
            list.eventScrollPositionChanged += (c, p) => Rows(list);
        }

        /// <summary>Sets each shown row's opacity for where it sits in the view now.</summary>
        public static void Rows(UIScrollablePanel list)
        {
            if (list == null)
            {
                return;
            }

            float view = list.height;
            float top,
                bottom;
            ScrollFade.Alphas(
                list.scrollPosition.y,
                view,
                list.CalculateViewSize().y,
                ScrollFade.DefaultLength,
                out top,
                out bottom
            );
            foreach (UIComponent row in list.components)
            {
                if (!row.isVisible)
                {
                    continue;
                }

                // The row's place in the view already carries the scroll; the fade spans a
                // row, so the last row in view shows there is more.
                float opacity = ScrollFade.RowOpacity(
                    row.relativePosition.y,
                    row.height,
                    view,
                    top,
                    bottom,
                    UnityEngine.Mathf.Max(ScrollFade.DefaultLength, row.height)
                );
                if (UnityEngine.Mathf.Abs(row.opacity - opacity) > 0.001f)
                {
                    row.opacity = opacity;
                    // Texture sprites (sparklines) draw the new opacity only once told to.
                    foreach (UIComponent part in row.GetComponentsInChildren<UIComponent>())
                    {
                        part.Invalidate();
                    }
                }
            }
        }
    }
}

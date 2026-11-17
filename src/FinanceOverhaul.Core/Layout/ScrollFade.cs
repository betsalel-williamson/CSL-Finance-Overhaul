using System;

namespace FinanceOverhaul.Core
{
    /// <summary>
    /// Edge fades for scrolling lists (the scrolling-list rule, Figma band 12 option A): a fade
    /// shows on an edge only while there are more rows past it, and eases out over the last
    /// <c>fadeLength</c> of scrolling so it never pops. Alphas are 0..1. Proved in
    /// ScrollFadeTests.
    /// </summary>
    public static class ScrollFade
    {
        /// <summary>Fade height in UI units (also how far the fade takes to ease in or out).</summary>
        public const float DefaultLength = 22f;

        /// <summary>
        /// Less than this much left to scroll counts as the end (the game's scroll-to-bottom
        /// can stop a fraction of a unit short), so no sliver of fade lingers there.
        /// </summary>
        public const float DeadZone = 1f;

        /// <param name="scroll">Current scroll offset from the top (0 = top).</param>
        /// <param name="view">Visible height of the list.</param>
        /// <param name="content">Height of all rows.</param>
        public static void Alphas(
            float scroll,
            float view,
            float content,
            float fadeLength,
            out float top,
            out float bottom
        )
        {
            top = 0f;
            bottom = 0f;
            if (
                float.IsNaN(scroll)
                || float.IsNaN(view)
                || float.IsNaN(content)
                || view <= 0f
                || fadeLength <= 0f
            )
            {
                return;
            }

            float max = Math.Max(0f, content - view); // furthest the list can scroll
            if (max <= 0.5f)
            {
                return; // everything fits: no hints
            }

            float s = Math.Max(0f, Math.Min(max, scroll));
            top = Clamp01((s - DeadZone) / fadeLength);
            bottom = Clamp01((max - s - DeadZone) / fadeLength);
        }

        private static float Clamp01(float v)
        {
            return v < 0f ? 0f
                : v > 1f ? 1f
                : v;
        }
    }
}

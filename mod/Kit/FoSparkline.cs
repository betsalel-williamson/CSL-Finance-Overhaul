using FinanceOverhaul.Core;
using UnityEngine;

namespace FinanceOverhaul
{
    /// <summary>
    /// Paints a price history into a texture as a line with a dashed baseline at the first
    /// price (Figma: the sparkline in the Stock Monitor rows). The shape is
    /// <see cref="Sparkline"/>'s; this only colours it.
    /// </summary>
    internal static class FoSparkline
    {
        /// <param name="thickness">Line thickness in texture pixels.</param>
        public static void Draw(Texture2D tex, float[] history, Color32 colour, int thickness)
        {
            int w = tex.width,
                h = tex.height;
            byte[] mask = Sparkline.Render(history, w, h, thickness);
            var px = new Color32[w * h];
            var dash = new Color32(colour.r, colour.g, colour.b, 200);
            Color32 clear = FoTheme.Clear;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    byte m = mask[y * w + x];
                    px[(h - 1 - y) * w + x] =
                        m == Sparkline.Line ? colour
                        : m == Sparkline.Baseline ? dash
                        : clear;
                }
            }

            tex.SetPixels32(px);
            tex.Apply();
        }
    }
}

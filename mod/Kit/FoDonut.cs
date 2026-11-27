using System; // Math
using UnityEngine; // Color32, Texture2D

namespace FinanceOverhaul
{
    /// <summary>
    /// Paints a ring split into parts (Figma: the holdings donut) into a square texture: each
    /// part's share of the ring, clockwise from the top, in its colour. Knows no feature: the
    /// caller gives the shares (0 to 1, adding up to 1) and the colours.
    /// </summary>
    internal static class FoDonut
    {
        /// <param name="hole">The hole's radius as a part of the ring's (0 to 1).</param>
        public static void Draw(Texture2D tex, float[] shares, Color32[] colours, float hole)
        {
            int size = Math.Min(tex.width, tex.height);
            float centre = (size - 1) / 2f;
            float outer = size / 2f;
            float inner = outer * hole;
            var px = new Color32[tex.width * tex.height];
            Color32 clear = FoTheme.Clear;
            for (int y = 0; y < tex.height; y++)
            {
                for (int x = 0; x < tex.width; x++)
                {
                    float dx = x - centre,
                        dy = y - centre;
                    float r = (float)Math.Sqrt(dx * dx + dy * dy);
                    Color32 c = clear;
                    if (r <= outer && r >= inner && shares != null && shares.Length > 0)
                    {
                        // Clockwise from the top (the texture's rows count up from the bottom).
                        double turn = Math.Atan2(dx, dy) / (2 * Math.PI);
                        if (turn < 0)
                        {
                            turn += 1;
                        }

                        double edge = 0;
                        int part = shares.Length - 1;
                        for (int i = 0; i < shares.Length; i++)
                        {
                            edge += shares[i];
                            if (turn < edge)
                            {
                                part = i;
                                break;
                            }
                        }

                        c = colours[part % colours.Length];
                    }

                    px[y * tex.width + x] = c;
                }
            }

            tex.SetPixels32(px);
            tex.Apply();
        }
    }
}

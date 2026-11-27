using UnityEngine; // FilterMode, Texture2D, TextureFormat, TextureWrapMode

namespace FinanceOverhaul
{
    /// <summary>
    /// The texture every chart, sparkline, donut and icon the mod draws into: 32-bit colour
    /// with alpha, no mipmaps, smoothed when scaled, clamped at the edges (no bleed from the
    /// other side). The caller owns it and destroys it.
    /// </summary>
    internal static class FoTexture
    {
        public static Texture2D Canvas(int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.ARGB32, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            return texture;
        }
    }
}

using System; // Math

namespace FinanceOverhaul.Core
{
    /// <summary>
    /// The blank coin behind a ticker symbol: the icon of a stock the game has no art for (the
    /// Index, a local company), in the shape of the game's own stock coins: a round face with a
    /// rim, an inner ring, and a thick edge showing below. A pixel mask; the mod colours it.
    /// Row 0 is the top.
    /// </summary>
    public static class CoinIcon
    {
        public const byte Empty = 0;

        /// <summary>The coin's side, showing below the face.</summary>
        public const byte Edge = 1;
        public const byte Rim = 2;
        public const byte Face = 3;

        /// <summary>The thin ring inside the rim.</summary>
        public const byte Ring = 4;

        /// <summary>A square mask <paramref name="size"/> pixels a side.</summary>
        public static byte[] Render(int size)
        {
            if (size <= 0)
            {
                return new byte[0];
            }

            var mask = new byte[size * size];
            float radius = size * 0.44f;
            float cx = (size - 1) / 2f;
            float faceY = cx - size * 0.03f;
            float edgeY = cx + size * 0.045f;
            float rim = size * 0.055f;
            float ringOuter = radius - size * 0.105f;
            float ringInner = ringOuter - size * 0.022f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    double face = Math.Sqrt((x - cx) * (x - cx) + (y - faceY) * (y - faceY));
                    double edge = Math.Sqrt((x - cx) * (x - cx) + (y - edgeY) * (y - edgeY));
                    byte m = Empty;
                    if (face <= radius)
                    {
                        m =
                            face > radius - rim ? Rim
                            : face <= ringOuter && face > ringInner ? Ring
                            : Face;
                    }
                    else if (edge <= radius)
                    {
                        m = Edge;
                    }

                    mask[y * size + x] = m;
                }
            }

            return mask;
        }

        /// <summary>
        /// Text scale of the symbol on a coin drawn <paramref name="coinSize"/> pixels wide, so
        /// two to four letters fit inside the ring (the game's cards draw the coin at 64).
        /// </summary>
        public static float SymbolScale(string symbol, float coinSize)
        {
            int letters = string.IsNullOrEmpty(symbol) ? 1 : symbol.Length;
            float at64 =
                letters <= 2 ? 1.25f
                : letters == 3 ? 1.0f
                : letters == 4 ? 0.8f
                : 0.65f;
            return at64 * Math.Max(16f, coinSize) / 64f;
        }
    }
}

using System;

namespace FinanceOverhaul.Market
{
    /// <summary>How bright the Stock Exchange LED boards are (Options; visual only).</summary>
    public enum LedBrightness
    {
        /// <summary>The original Figma ticker tokens.</summary>
        Classic = 0,

        /// <summary>Lit dots at full strength; unlit dots darker (default).</summary>
        Bright = 1,

        /// <summary>Lit dots at full strength and lifted toward white; near-black unlit dots: for daylight.</summary>
        Brightest = 2,
    }

    /// <summary>An 8-bit RGB colour (the host turns it into a Unity Color32).</summary>
    public struct LedRgb
    {
        public byte R;
        public byte G;
        public byte B;

        public LedRgb(byte r, byte g, byte b)
        {
            R = r;
            G = g;
            B = b;
        }

        public int Max
        {
            get { return Math.Max(R, Math.Max(G, B)); }
        }

        /// <summary>Relative luminance ×255 (Rec. 709 weights).</summary>
        public double Luminance
        {
            get { return 0.2126 * R + 0.7152 * G + 0.0722 * B; }
        }
    }

    /// <summary>
    /// LED dot colours per <see cref="LedText"/> colour index and brightness. Brighter levels
    /// scale each lit colour so its strongest channel is full (255); Brightest then lifts every
    /// channel part of the way to white. Perceived brightness never drops, the dominant channel
    /// stays the same (green stays green, red stays red), and unlit dots get darker, so the
    /// contrast between lit and unlit dots only grows. Proved in LedPaletteTests.
    /// </summary>
    public static class LedPalette
    {
        // Figma tokens color/ticker/*: off (dim unlit dot), symbol, gain, loss, unknown, flat.
        private static readonly LedRgb[] Classic =
        {
            new LedRgb(0x29, 0x3A, 0x46),
            new LedRgb(0xEF, 0xB0, 0x58),
            new LedRgb(0x4E, 0xF4, 0x65),
            new LedRgb(0xE9, 0x42, 0x4F),
            new LedRgb(0x9F, 0xB0, 0xC0),
            new LedRgb(0x9F, 0xB0, 0xC0),
        };

        private static readonly LedRgb[] OffByLevel =
        {
            new LedRgb(0x29, 0x3A, 0x46),
            new LedRgb(0x1A, 0x26, 0x30),
            new LedRgb(0x10, 0x18, 0x1E),
        };

        // Share of the way each channel is lifted toward white after scaling to full strength.
        private static readonly double[] LiftByLevel = { 0.0, 0.0, 0.3 };

        /// <summary>The board between dots (Figma color/ticker/board).</summary>
        public static readonly LedRgb Board = new LedRgb(0x1E, 0x30, 0x3E);

        // How much of a lit dot's colour glows into the 1-texel gap around it (0 = dark gap).
        private static readonly double[] GlowByLevel = { 0.0, 0.3, 0.55 };

        public static int Count
        {
            get { return Classic.Length; }
        }

        /// <summary>
        /// The gap texels around a dot: the board for unlit dots and at Classic; at brighter
        /// levels a lit dot glows into its gap (board blended toward the dot's colour), so text
        /// reads brighter from a distance while the dots stay distinct up close.
        /// </summary>
        public static LedRgb Gap(byte cell, LedBrightness level)
        {
            int l = Math.Max(0, Math.Min(2, (int)level));
            if (cell == LedText.Off || cell >= Classic.Length || GlowByLevel[l] <= 0)
            {
                return Board;
            }

            LedRgb lit = Colour(cell, level);
            double g = GlowByLevel[l];
            Func<byte, byte, byte> mix = (b, c) => (byte)Math.Round(b + (c - b) * g);
            return new LedRgb(mix(Board.R, lit.R), mix(Board.G, lit.G), mix(Board.B, lit.B));
        }

        public static LedRgb Colour(byte cell, LedBrightness level)
        {
            int l = Math.Max(0, Math.Min(2, (int)level));
            if (cell == LedText.Off || cell >= Classic.Length)
            {
                return OffByLevel[l];
            }

            LedRgb c = Classic[cell];
            if (l == 0)
            {
                return c;
            }

            double scale = 255.0 / c.Max;
            double lift = LiftByLevel[l];
            Func<byte, byte> boost = ch =>
            {
                double full = ch * scale;
                double v = full + (255.0 - full) * lift;
                return (byte)Math.Max(0, Math.Min(255, Math.Round(v)));
            };
            return new LedRgb(boost(c.R), boost(c.G), boost(c.B));
        }
    }
}

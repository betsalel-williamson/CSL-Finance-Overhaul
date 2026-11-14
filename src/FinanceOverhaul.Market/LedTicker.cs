using System;
using System.Collections.Generic;

namespace FinanceOverhaul.Market
{
    /// <summary>One dot-matrix glyph: <see cref="Width"/> columns × <see cref="LedFont.Height"/> rows.</summary>
    public sealed class LedGlyph
    {
        private readonly bool[] lit;

        internal LedGlyph(string[] rows)
        {
            Width = 0;
            for (int i = 0; i < rows.Length; i++)
            {
                Width = Math.Max(Width, rows[i].Length);
            }

            lit = new bool[Width * LedFont.Height];
            for (int y = 0; y < rows.Length && y < LedFont.Height; y++)
            {
                for (int x = 0; x < rows[y].Length; x++)
                {
                    lit[y * Width + x] = rows[y][x] == '#';
                }
            }
        }

        public int Width { get; private set; }

        /// <summary>Row 0 is the top; out-of-range dots are unlit.</summary>
        public bool IsLit(int x, int y)
        {
            return x >= 0 && x < Width && y >= 0 && y < LedFont.Height && lit[y * Width + x];
        }
    }

    /// <summary>
    /// Proportional 5×7 LED font with one descender row (8 rows total) for the live
    /// Stock Exchange ticker. Covers everything the ticker prints: letters, digits,
    /// <c>+ - . ( )</c>, space and <c>?</c> (the fallback for anything else).
    /// </summary>
    public static class LedFont
    {
        public const int Height = 8;
        public const int MaxGlyphWidth = 5;

        /// <summary>Blank columns between glyphs.</summary>
        public const int Spacing = 1;

        private static readonly Dictionary<char, LedGlyph> Glyphs = Build();

        public static IEnumerable<char> Characters
        {
            get { return Glyphs.Keys; }
        }

        public static bool Has(char c)
        {
            return Glyphs.ContainsKey(c);
        }

        public static LedGlyph Get(char c)
        {
            LedGlyph g;
            return Glyphs.TryGetValue(c, out g) ? g : Glyphs['?'];
        }

        private static Dictionary<char, LedGlyph> Build()
        {
            var d = new Dictionary<char, LedGlyph>();
            Action<char, string[]> add = (c, rows) => d[c] = new LedGlyph(rows);

            add('A', new[] { ".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#" });
            add('B', new[] { "####.", "#...#", "#...#", "####.", "#...#", "#...#", "####." });
            add('C', new[] { ".###.", "#...#", "#....", "#....", "#....", "#...#", ".###." });
            add('D', new[] { "####.", "#...#", "#...#", "#...#", "#...#", "#...#", "####." });
            add('E', new[] { "#####", "#....", "#....", "####.", "#....", "#....", "#####" });
            add('F', new[] { "#####", "#....", "#....", "####.", "#....", "#....", "#...." });
            add('G', new[] { ".###.", "#...#", "#....", "#.###", "#...#", "#...#", ".####" });
            add('H', new[] { "#...#", "#...#", "#...#", "#####", "#...#", "#...#", "#...#" });
            add('I', new[] { "###", ".#.", ".#.", ".#.", ".#.", ".#.", "###" });
            add('J', new[] { "..###", "...#.", "...#.", "...#.", "...#.", "#..#.", ".##.." });
            add('K', new[] { "#...#", "#..#.", "#.#..", "##...", "#.#..", "#..#.", "#...#" });
            add('L', new[] { "#....", "#....", "#....", "#....", "#....", "#....", "#####" });
            add('M', new[] { "#...#", "##.##", "#.#.#", "#.#.#", "#...#", "#...#", "#...#" });
            add('N', new[] { "#...#", "#...#", "##..#", "#.#.#", "#..##", "#...#", "#...#" });
            add('O', new[] { ".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###." });
            add('P', new[] { "####.", "#...#", "#...#", "####.", "#....", "#....", "#...." });
            add('Q', new[] { ".###.", "#...#", "#...#", "#...#", "#.#.#", "#..#.", ".##.#" });
            add('R', new[] { "####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#" });
            add('S', new[] { ".####", "#....", "#....", ".###.", "....#", "....#", "####." });
            add('T', new[] { "#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#.." });
            add('U', new[] { "#...#", "#...#", "#...#", "#...#", "#...#", "#...#", ".###." });
            add('V', new[] { "#...#", "#...#", "#...#", "#...#", "#...#", ".#.#.", "..#.." });
            add('W', new[] { "#...#", "#...#", "#...#", "#.#.#", "#.#.#", "#.#.#", ".#.#." });
            add('X', new[] { "#...#", "#...#", ".#.#.", "..#..", ".#.#.", "#...#", "#...#" });
            add('Y', new[] { "#...#", "#...#", ".#.#.", "..#..", "..#..", "..#..", "..#.." });
            add('Z', new[] { "#####", "....#", "...#.", "..#..", ".#...", "#....", "#####" });

            add('a', new[] { ".....", ".....", ".###.", "....#", ".####", "#...#", ".####" });
            add('b', new[] { "#....", "#....", "#.##.", "##..#", "#...#", "#...#", "####." });
            add('c', new[] { "....", "....", ".###", "#...", "#...", "#...", ".###" });
            add('d', new[] { "....#", "....#", ".##.#", "#..##", "#...#", "#...#", ".####" });
            add('e', new[] { ".....", ".....", ".###.", "#...#", "#####", "#....", ".###." });
            add('f', new[] { "..##", ".#..", ".#..", "###.", ".#..", ".#..", ".#.." });
            add(
                'g',
                new[] { ".....", ".....", ".####", "#...#", "#...#", ".####", "....#", ".###." }
            );
            add('h', new[] { "#....", "#....", "#.##.", "##..#", "#...#", "#...#", "#...#" });
            add('i', new[] { ".#.", "...", "##.", ".#.", ".#.", ".#.", "###" });
            add('j', new[] { "...#", "....", "..##", "...#", "...#", "...#", "#..#", ".##." });
            add('k', new[] { "#...", "#...", "#..#", "#.#.", "##..", "#.#.", "#..#" });
            add('l', new[] { "##.", ".#.", ".#.", ".#.", ".#.", ".#.", "###" });
            add('m', new[] { ".....", ".....", "##.#.", "#.#.#", "#.#.#", "#.#.#", "#.#.#" });
            add('n', new[] { ".....", ".....", "#.##.", "##..#", "#...#", "#...#", "#...#" });
            add('o', new[] { ".....", ".....", ".###.", "#...#", "#...#", "#...#", ".###." });
            add(
                'p',
                new[] { ".....", ".....", "####.", "#...#", "#...#", "####.", "#....", "#...." }
            );
            add(
                'q',
                new[] { ".....", ".....", ".####", "#...#", "#...#", ".####", "....#", "....#" }
            );
            add('r', new[] { ".....", ".....", "#.##.", "##..#", "#....", "#....", "#...." });
            add('s', new[] { ".....", ".....", ".####", "#....", ".###.", "....#", "####." });
            add('t', new[] { ".#..", ".#..", "###.", ".#..", ".#..", ".#.#", "..#." });
            add('u', new[] { ".....", ".....", "#...#", "#...#", "#...#", "#..##", ".##.#" });
            add('v', new[] { ".....", ".....", "#...#", "#...#", "#...#", ".#.#.", "..#.." });
            add('w', new[] { ".....", ".....", "#...#", "#...#", "#.#.#", "#.#.#", ".#.#." });
            add('x', new[] { ".....", ".....", "#...#", ".#.#.", "..#..", ".#.#.", "#...#" });
            add(
                'y',
                new[] { ".....", ".....", "#...#", "#...#", "#...#", ".####", "....#", ".###." }
            );
            add('z', new[] { ".....", ".....", "#####", "...#.", "..#..", ".#...", "#####" });

            add('0', new[] { ".###.", "#...#", "#..##", "#.#.#", "##..#", "#...#", ".###." });
            add('1', new[] { ".#.", "##.", ".#.", ".#.", ".#.", ".#.", "###" });
            add('2', new[] { ".###.", "#...#", "....#", "...#.", "..#..", ".#...", "#####" });
            add('3', new[] { "#####", "...#.", "..#..", "...#.", "....#", "#...#", ".###." });
            add('4', new[] { "...#.", "..##.", ".#.#.", "#..#.", "#####", "...#.", "...#." });
            add('5', new[] { "#####", "#....", "####.", "....#", "....#", "#...#", ".###." });
            add('6', new[] { "..##.", ".#...", "#....", "####.", "#...#", "#...#", ".###." });
            add('7', new[] { "#####", "....#", "...#.", "..#..", ".#...", ".#...", ".#..." });
            add('8', new[] { ".###.", "#...#", "#...#", ".###.", "#...#", "#...#", ".###." });
            add('9', new[] { ".###.", "#...#", "#...#", ".####", "....#", "...#.", ".##.." });

            add('+', new[] { ".....", "..#..", "..#..", "#####", "..#..", "..#..", "....." });
            add('-', new[] { "....", "....", "....", "####", "....", "....", "...." });
            add('.', new[] { "..", "..", "..", "..", "..", "##", "##" });
            add('(', new[] { "..#", ".#.", "#..", "#..", "#..", ".#.", "..#" });
            add(')', new[] { "#..", ".#.", "..#", "..#", "..#", ".#.", "#.." });
            add(' ', new[] { "..", "..", "..", "..", "..", "..", ".." });
            add('?', new[] { ".###.", "#...#", "....#", "...#.", "..#..", ".....", "..#.." });
            return d;
        }
    }

    /// <summary>A rendered dot-matrix row: one colour index per dot, row-major, row 0 on top.</summary>
    public sealed class LedStrip
    {
        public LedStrip(int width, int height)
        {
            Width = width;
            Height = height;
            Cells = new byte[width * height];
        }

        public int Width { get; private set; }

        public int Height { get; private set; }

        public byte[] Cells { get; private set; }

        public byte Get(int x, int y)
        {
            return Cells[y * Width + x];
        }

        internal void Set(int x, int y, byte value)
        {
            Cells[y * Width + x] = value;
        }
    }

    /// <summary>
    /// Lays ticker entries (<c>SYM ( +2.67 )</c>) out as LED dots with colour indices
    /// from the ticker tokens (<see cref="TickerLine"/> hex constants). Every entry ends
    /// with <see cref="EntryGap"/> dark columns, so a strip can repeat seamlessly while
    /// it scrolls.
    /// </summary>
    public static class LedText
    {
        public const byte Off = 0;
        public const byte Symbol = 1;
        public const byte Gain = 2;
        public const byte Loss = 3;
        public const byte Unknown = 4;

        /// <summary>Dark columns after each entry.</summary>
        public const int EntryGap = 10;

        public static int MeasureEntry(string symbol, float rate)
        {
            return MeasureText(symbol + RatePart(rate)) + EntryGap;
        }

        public static LedStrip RenderRow(
            IList<string> symbols,
            IList<float> changeRates,
            int start,
            int count
        )
        {
            if (symbols == null || changeRates == null)
            {
                return new LedStrip(0, LedFont.Height);
            }

            int end = Math.Min(start + count, Math.Min(symbols.Count, changeRates.Count));
            int width = 0;
            for (int i = start; i < end; i++)
            {
                width += MeasureEntry(symbols[i], changeRates[i]);
            }

            var strip = new LedStrip(width, LedFont.Height);
            int x = 0;
            for (int i = start; i < end; i++)
            {
                float rate = changeRates[i];
                byte rateColour =
                    float.IsNaN(rate) || float.IsInfinity(rate) ? Unknown
                    : rate < 0f ? Loss
                    : Gain;
                x = Draw(strip, x, symbols[i], Symbol);
                x += LedFont.Spacing;
                x = Draw(strip, x, RatePart(rate), rateColour);
                x += EntryGap;
            }

            return strip;
        }

        /// <summary>Plain one-colour message (e.g. "Waiting for market data"), ending with the entry gap.</summary>
        public static LedStrip RenderText(string text, byte colour)
        {
            int width = MeasureText(text);
            if (width == 0)
            {
                return new LedStrip(0, LedFont.Height);
            }

            var strip = new LedStrip(width + EntryGap, LedFont.Height);
            Draw(strip, 0, text, colour);
            return strip;
        }

        /// <summary>Splits entries over rows exactly like <see cref="TickerLine.FormatRichRows"/>.</summary>
        public static LedStrip[] RenderRows(
            IList<string> symbols,
            IList<float> changeRates,
            int rows
        )
        {
            rows = Math.Max(1, rows);
            var result = new LedStrip[rows];
            int n =
                symbols == null || changeRates == null
                    ? 0
                    : Math.Min(symbols.Count, changeRates.Count);
            int perRow = (n + rows - 1) / rows;
            for (int r = 0; r < rows; r++)
            {
                int start = Math.Min(n, r * perRow);
                result[r] = RenderRow(symbols, changeRates, start, Math.Min(perRow, n - start));
            }

            return result;
        }

        private static string RatePart(float rate)
        {
            return " ( " + TickerLine.FormatRate(rate) + " )";
        }

        private static int MeasureText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            int w = 0;
            for (int i = 0; i < text.Length; i++)
            {
                w += LedFont.Get(text[i]).Width;
            }

            return w + (text.Length - 1) * LedFont.Spacing;
        }

        /// <summary>Draws text at column x; returns the column just after its last glyph.</summary>
        private static int Draw(LedStrip strip, int x, string text, byte colour)
        {
            if (string.IsNullOrEmpty(text))
            {
                return x;
            }

            for (int i = 0; i < text.Length; i++)
            {
                if (i > 0)
                {
                    x += LedFont.Spacing;
                }

                LedGlyph g = LedFont.Get(text[i]);
                for (int gx = 0; gx < g.Width; gx++)
                {
                    for (int gy = 0; gy < LedFont.Height; gy++)
                    {
                        if (g.IsLit(gx, gy))
                        {
                            strip.Set(x + gx, gy, colour);
                        }
                    }
                }

                x += g.Width;
            }

            return x;
        }
    }
}

using System; // Action, Math, MidpointRounding
using System.Collections.Generic; // Dictionary, IEnumerable, IList

namespace FinanceOverhaul.Core
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
            add('%', new[] { "##...", "##..#", "...#.", "..#..", ".#...", "#..##", "...##" });
            add(',', new[] { "..", "..", "..", "..", "..", ".#", "#." });
            // ₡ (colón): a C crossed by a stroke; "−" (minus sign) draws like "-".
            add('₡', new[] { ".####", "#.#..", "#.#..", "#.#..", "#.#..", "#.#..", ".####" });
            add('−', new[] { "....", "....", "....", "####", "....", "....", "...." });
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
        public const byte Flat = 5;

        /// <summary>Dark columns after each entry.</summary>
        public const int EntryGap = 10;

        /// <summary>
        /// Colour for a change rate, decided on the printed (rounded) value like real
        /// tickers: up green, down red, unchanged (<c>0.00</c>) and unknown neutral.
        /// </summary>
        public static byte RateColour(float rate)
        {
            if (float.IsNaN(rate) || float.IsInfinity(rate))
            {
                return Unknown;
            }

            double rounded = Math.Round(rate, 2, MidpointRounding.AwayFromZero);
            return rounded == 0d ? Flat
                : rounded < 0d ? Loss
                : Gain;
        }

        /// <summary>Row r's entry range when n entries are split over rows (earlier rows fuller).</summary>
        public static void RowRange(int n, int rows, int r, out int start, out int count)
        {
            rows = Math.Max(1, rows);
            int perRow = (Math.Max(0, n) + rows - 1) / rows;
            start = Math.Min(n, r * perRow);
            count = Math.Max(0, Math.Min(perRow, n - start));
        }

        public static int MeasureEntry(string symbol, float rate)
        {
            return MeasureEntry(symbol, float.NaN, rate, TickerDisplay.ChangePercent);
        }

        public static int MeasureEntry(
            string symbol,
            float price,
            float rate,
            TickerDisplay display
        )
        {
            return MeasureText(symbol + ChangePart(price, rate, display)) + EntryGap;
        }

        /// <summary>
        /// What follows the symbol (with its leading space): <c>" +3.22%"</c> in
        /// <see cref="TickerDisplay.ChangePercent"/>, <c>" ₡951 +₡30"</c> in
        /// <see cref="TickerDisplay.PriceAndChange"/> (the same change rate in ₡, the Stock
        /// Monitor's rule), <c>" --"</c> when unknown. Signs are + and −, never arrows.
        /// </summary>
        public static string ChangePart(float price, float rate, TickerDisplay display)
        {
            if (display == TickerDisplay.PriceAndChange)
            {
                string priceText = StockMonitor.PriceText(price);
                if (priceText == "--")
                {
                    return " --";
                }

                return " " + priceText + " " + StockMonitor.ChangeAmountFromRate(price, rate);
            }

            return " " + StockMonitor.ChangeText(rate);
        }

        public static LedStrip RenderRow(
            IList<string> symbols,
            IList<float> changeRates,
            int start,
            int count
        )
        {
            return RenderRow(symbols, null, changeRates, start, count, TickerDisplay.ChangePercent);
        }

        public static LedStrip RenderRow(
            IList<string> symbols,
            IList<float> prices,
            IList<float> changeRates,
            int start,
            int count,
            TickerDisplay display
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
                width += MeasureEntry(symbols[i], PriceAt(prices, i), changeRates[i], display);
            }

            var strip = new LedStrip(width, LedFont.Height);
            int x = 0;
            for (int i = start; i < end; i++)
            {
                float rate = changeRates[i];
                byte rateColour = RateColour(rate);
                x = Draw(strip, x, symbols[i], Symbol);
                x += LedFont.Spacing;
                x = Draw(strip, x, ChangePart(PriceAt(prices, i), rate, display), rateColour);
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

        private static float PriceAt(IList<float> prices, int i)
        {
            return prices != null && i < prices.Count ? prices[i] : float.NaN;
        }

        internal static int MeasureText(string text)
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
        internal static int Draw(LedStrip strip, int x, string text, byte colour)
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
                        if (g.IsLit(gx, gy) && x + gx < strip.Width)
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

namespace FinanceOverhaul.Core
{
    /// <summary>One day's ticker entries, published as a single object so readers never see a half update.</summary>
    public sealed class TickerSnapshot
    {
        public TickerSnapshot(string[] symbols, float[] rates)
            : this(symbols, rates, null) { }

        public TickerSnapshot(string[] symbols, float[] rates, float[] prices)
        {
            Symbols = symbols ?? new string[0];
            Rates = rates ?? new float[0];
            Prices = prices ?? new float[0];
        }

        public string[] Symbols { get; private set; }

        public float[] Rates { get; private set; }

        /// <summary>Daily prices (whole ₡ values), same order; may be shorter (then unknown).</summary>
        public float[] Prices { get; private set; }

        public int Count
        {
            get { return Math.Min(Symbols.Length, Rates.Length); }
        }
    }

    /// <summary>
    /// A traditional LED marquee for one ticker row. The strip is a ring buffer of
    /// <see cref="RingDots"/> columns; the visible window starts at <see cref="Offset"/>
    /// (absolute, only ever increases) and is <see cref="WindowDots"/> wide. Entries are
    /// written one after another at <see cref="Head"/>, always beyond the window's right
    /// edge, using the newest content at the moment they are written. Nothing inside the
    /// window is ever redrawn, so values on screen never change and the scroll never jumps.
    /// </summary>
    public sealed class LedMarquee
    {
        /// <summary>Dark columns written when there is nothing to show.</summary>
        public const int IdleGap = 20;

        /// <summary>Widest entry a slot of the ring reserves room for; wider entries are clipped.</summary>
        public static readonly int MaxEntryDots =
            Math.Max(
                LedText.MeasureText("WWWW +999.99%"),
                LedText.MeasureText("WWWW ₡9,999,999 +₡9,999,999")
            ) + LedText.EntryGap;

        /// <summary>How entries written from now on show the change (Options → Ticker shows).</summary>
        public TickerDisplay Display { get; set; }

        private readonly string idleText;
        private TickerSnapshot content;
        private int start;
        private int count;
        private int cursor;

        public LedMarquee(int windowDots, string idleText)
        {
            WindowDots = Math.Max(1, windowDots);
            this.idleText = idleText;
            int idle = string.IsNullOrEmpty(idleText)
                ? 0
                : LedText.MeasureText(idleText) + LedText.EntryGap;
            RingDots = WindowDots + Lookahead + Math.Max(MaxEntryDots, idle);
            Strip = new LedStrip(RingDots, LedFont.Height);
            Fill();
        }

        /// <summary>Columns kept written beyond the window's right edge.</summary>
        public const int Lookahead = 2;

        public int WindowDots { get; private set; }

        public int RingDots { get; private set; }

        public LedStrip Strip { get; private set; }

        /// <summary>Absolute column at the window's left edge; only ever increases.</summary>
        public long Offset { get; private set; }

        /// <summary>Absolute column where the next entry will be written.</summary>
        public long Head { get; private set; }

        /// <summary>Ring column of the window's left edge (for the texture offset).</summary>
        public int RingOffset
        {
            get { return (int)(Offset % RingDots); }
        }

        /// <summary>Uses these entries (row slice [start, start + count)) for entries written from now on.</summary>
        public void SetContent(TickerSnapshot snapshot, int start, int count)
        {
            content = snapshot;
            this.start = start;
            this.count =
                snapshot == null ? 0 : Math.Max(0, Math.Min(count, snapshot.Count - start));
            if (this.count > 0)
            {
                cursor %= this.count;
            }
            else
            {
                cursor = 0;
            }
        }

        /// <summary>
        /// Scrolls left by whole dots and writes new entries ahead of the window. Returns
        /// the absolute column range written (<paramref name="fromX"/> inclusive,
        /// <paramref name="toX"/> exclusive); empty when fromX == toX.
        /// </summary>
        public void Advance(int dots, out long fromX, out long toX)
        {
            Offset += Math.Max(0, dots);
            fromX = Head;
            Fill();
            toX = Head;
        }

        /// <summary>
        /// Only while nobody can see the row: rewrites the whole window from the current
        /// content (e.g. when the camera comes back after values went stale).
        /// </summary>
        public void Restart()
        {
            Head = Offset;
            Fill();
        }

        private void Fill()
        {
            while (Head < Offset + WindowDots + Lookahead)
            {
                LedStrip entry = NextEntry();
                int width = Math.Max(1, Math.Min(entry.Width, RingDots - WindowDots - Lookahead));
                for (int x = 0; x < width; x++)
                {
                    int ring = (int)((Head + x) % RingDots);
                    for (int y = 0; y < LedFont.Height; y++)
                    {
                        Strip.Set(ring, y, x < entry.Width ? entry.Get(x, y) : LedText.Off);
                    }
                }

                Head += width;
            }
        }

        private LedStrip NextEntry()
        {
            if (count == 0)
            {
                LedStrip idle = string.IsNullOrEmpty(idleText)
                    ? null
                    : LedText.RenderText(idleText, LedText.Unknown);
                return idle != null && idle.Width > 0
                    ? idle
                    : new LedStrip(IdleGap, LedFont.Height);
            }

            int i = start + cursor;
            cursor = (cursor + 1) % count;
            LedStrip entry = LedText.RenderRow(
                content.Symbols,
                content.Prices,
                content.Rates,
                i,
                1,
                Display
            );
            if (entry.Width > MaxEntryDots)
            {
                // Clip absurd rates, keeping the trailing gap.
                var clipped = new LedStrip(MaxEntryDots, LedFont.Height);
                for (int x = 0; x < MaxEntryDots - LedText.EntryGap; x++)
                {
                    for (int y = 0; y < LedFont.Height; y++)
                    {
                        clipped.Set(x, y, entry.Get(x, y));
                    }
                }

                return clipped;
            }

            return entry;
        }
    }
}

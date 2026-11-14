using FinanceOverhaul.Market;
using Xunit;

namespace FinanceOverhaul.Market.Tests
{
    public class LedFontTests
    {
        [Fact]
        public void EveryGlyphIsWellFormed()
        {
            foreach (char c in LedFont.Characters)
            {
                LedGlyph g = LedFont.Get(c);
                Assert.True(g.Width >= 1 && g.Width <= LedFont.MaxGlyphWidth, "width of " + c);
                for (int x = 0; x < g.Width; x++)
                {
                    for (int y = 0; y < LedFont.Height; y++)
                    {
                        // Must not throw; lit or not.
                        g.IsLit(x, y);
                    }
                }
            }
        }

        [Fact]
        public void CoversTickerCharacters()
        {
            const string needed =
                "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+-.() ?";
            foreach (char c in needed)
            {
                Assert.True(LedFont.Has(c), "missing glyph " + c);
            }
        }

        [Fact]
        public void UnknownCharacterFallsBackToQuestionMark()
        {
            Assert.Same(LedFont.Get('?'), LedFont.Get('€'));
        }

        [Fact]
        public void LetterAHasExpectedShape()
        {
            LedGlyph a = LedFont.Get('A');
            Assert.Equal(5, a.Width);
            Assert.False(a.IsLit(0, 0));
            Assert.True(a.IsLit(1, 0));
            Assert.True(a.IsLit(0, 3) && a.IsLit(4, 3) && a.IsLit(2, 3));
            Assert.False(a.IsLit(2, 4));
            Assert.False(a.IsLit(0, LedFont.Height - 1)); // descender row empty
        }

        [Fact]
        public void DescenderLettersUseTheBottomRow()
        {
            Assert.True(LedFont.Get('y').IsLit(1, LedFont.Height - 1));
            Assert.True(LedFont.Get('p').IsLit(0, LedFont.Height - 1));
        }

        [Fact]
        public void NarrowPunctuation()
        {
            Assert.True(LedFont.Get('.').Width < 5);
            Assert.True(LedFont.Get('(').Width < 5);
        }
    }

    public class LedStripTests
    {
        [Fact]
        public void MeasureMatchesRenderedWidth()
        {
            LedStrip strip = LedText.RenderRow(new[] { "TJL" }, new[] { 1.5f }, 0, 1);
            Assert.Equal(LedText.MeasureEntry("TJL", 1.5f), strip.Width);
            Assert.Equal(LedFont.Height, strip.Height);
        }

        [Fact]
        public void EntryEndsWithGapSoWrapIsSeamless()
        {
            LedStrip strip = LedText.RenderRow(new[] { "TJL" }, new[] { 1.5f }, 0, 1);
            for (int x = strip.Width - LedText.EntryGap; x < strip.Width; x++)
            {
                for (int y = 0; y < strip.Height; y++)
                {
                    Assert.Equal(LedText.Off, strip.Get(x, y));
                }
            }
        }

        [Fact]
        public void SymbolAndRateUseTokenColours()
        {
            LedStrip gain = LedText.RenderRow(new[] { "T" }, new[] { 2f }, 0, 1);
            LedStrip loss = LedText.RenderRow(new[] { "T" }, new[] { -2f }, 0, 1);
            LedStrip nan = LedText.RenderRow(new[] { "T" }, new[] { float.NaN }, 0, 1);

            // 'T' top bar is the symbol colour.
            Assert.Equal(LedText.Symbol, gain.Get(0, 0));
            Assert.Contains(LedText.Gain, gain.Cells);
            Assert.DoesNotContain(LedText.Loss, gain.Cells);
            Assert.Contains(LedText.Loss, loss.Cells);
            Assert.DoesNotContain(LedText.Gain, loss.Cells);
            Assert.Contains(LedText.Unknown, nan.Cells);
        }

        [Theory]
        [InlineData(0f)]
        [InlineData(0.004f)]
        [InlineData(-0.004f)]
        public void RatesThatPrintAsZeroAreFlat(float rate)
        {
            Assert.Equal("0.00", TickerLine.FormatRate(rate));
            Assert.Equal(LedText.Flat, LedText.RateColour(rate));
        }

        [Fact]
        public void SmallestPrintedMovesKeepTheirColour()
        {
            Assert.Equal(LedText.Gain, LedText.RateColour(0.006f)); // +0.01
            Assert.Equal(LedText.Loss, LedText.RateColour(-0.006f)); // -0.01
            Assert.Equal(LedText.Unknown, LedText.RateColour(float.NaN));
        }

        [Fact]
        public void RowIsEntriesSideBySide()
        {
            LedStrip both = LedText.RenderRow(new[] { "A", "B" }, new[] { 1f, -1f }, 0, 2);
            Assert.Equal(
                LedText.MeasureEntry("A", 1f) + LedText.MeasureEntry("B", -1f),
                both.Width
            );

            // Second entry starts with 'B' (symbol colour) at column of first width.
            int bx = LedText.MeasureEntry("A", 1f);
            Assert.Equal(LedText.Symbol, both.Get(bx, 0));
        }

        [Fact]
        public void EmptyInputGivesEmptyStrip()
        {
            Assert.Equal(0, LedText.RenderRow(null, null, 0, 0).Width);
            Assert.Equal(0, LedText.RenderRow(new string[0], new float[0], 0, 0).Width);
        }

        [Fact]
        public void PlainTextUsesOneColourAndEndsWithGap()
        {
            LedStrip strip = LedText.RenderText("Wait", LedText.Unknown);
            Assert.Contains(LedText.Unknown, strip.Cells);
            Assert.DoesNotContain(LedText.Symbol, strip.Cells);
            Assert.Equal(LedText.Off, strip.Get(strip.Width - 1, 0));
            Assert.Equal(0, LedText.RenderText(string.Empty, LedText.Unknown).Width);
        }

        [Fact]
        public void RowsSplitLikeTheTextBoards()
        {
            var symbols = new[] { "A", "B", "C", "D", "E", "F", "G", "H" };
            var rates = new[] { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f };
            LedStrip[] rows = LedText.RenderRows(symbols, rates, 3);
            Assert.Equal(3, rows.Length);
            // 3 + 3 + 2 entries, as FormatRichRows splits them.
            Assert.Equal(LedText.MeasureEntry("A", 1f) * 3, rows[0].Width);
            Assert.Equal(LedText.MeasureEntry("A", 1f) * 2, rows[2].Width);
        }
    }
}

namespace FinanceOverhaul.Market.Tests
{
    public class LedMarqueeTests
    {
        private static TickerSnapshot Snap(params float[] rates)
        {
            var symbols = new string[rates.Length];
            for (int i = 0; i < rates.Length; i++)
            {
                symbols[i] = ((char)('A' + i)).ToString();
            }

            return new TickerSnapshot(symbols, rates);
        }

        private static byte[] Window(LedMarquee m)
        {
            var cells = new byte[m.WindowDots * LedFont.Height];
            for (int x = 0; x < m.WindowDots; x++)
            {
                int ring = (int)((m.Offset + x) % m.RingDots);
                for (int y = 0; y < LedFont.Height; y++)
                {
                    cells[y * m.WindowDots + x] = m.Strip.Get(ring, y);
                }
            }

            return cells;
        }

        [Fact]
        public void StartsWithAFullWindowAndLookahead()
        {
            var m = new LedMarquee(100, "Waiting");
            Assert.True(m.Head >= m.Offset + m.WindowDots + LedMarquee.Lookahead);
            Assert.Contains(LedText.Unknown, m.Strip.Cells);
        }

        [Fact]
        public void OffsetOnlyMovesForwardByWholeDots()
        {
            var m = new LedMarquee(100, null);
            m.SetContent(Snap(1f, -2f, 3f), 0, 3);
            long previous = m.Offset;
            long from,
                to;
            for (int i = 0; i < 1000; i++)
            {
                m.Advance(1, out from, out to);
                Assert.Equal(previous + 1, m.Offset);
                previous = m.Offset;
            }
        }

        [Fact]
        public void VisibleColumnsNeverChangeWhenContentChanges()
        {
            var m = new LedMarquee(100, null);
            m.SetContent(Snap(1f, -2f, 3f), 0, 3);
            long from,
                to;
            for (int step = 0; step < 2000; step++)
            {
                byte[] before = Window(m);
                if (step % 37 == 0)
                {
                    // New day: values and even the entry count change.
                    m.SetContent(Snap(step % 2 == 0 ? -5f : 7f, 1f), 0, 2);
                }

                m.Advance(1, out from, out to);

                // The window moved by one dot: what was visible shifted left, unchanged.
                byte[] after = Window(m);
                for (int x = 0; x < m.WindowDots - 1; x++)
                {
                    for (int y = 0; y < LedFont.Height; y++)
                    {
                        Assert.Equal(before[y * m.WindowDots + x + 1], after[y * m.WindowDots + x]);
                    }
                }
            }
        }

        [Fact]
        public void WritesHappenOnlyBeyondTheWindow()
        {
            var m = new LedMarquee(100, null);
            m.SetContent(Snap(1f, 2f), 0, 2);
            long from,
                to;
            for (int i = 0; i < 500; i++)
            {
                m.Advance(1, out from, out to);
                if (to > from)
                {
                    Assert.True(from >= m.Offset + m.WindowDots);
                    Assert.True(to - m.Offset <= m.RingDots);
                }
            }
        }

        [Fact]
        public void NewValuesAppearInTheNextWrittenEntry()
        {
            var m = new LedMarquee(60, null);
            m.SetContent(Snap(1f), 0, 1);
            m.SetContent(Snap(-1f), 0, 1);
            Assert.DoesNotContain(LedText.Loss, m.Strip.Cells); // already written stays

            long from,
                to;
            int guard = 0;
            do
            {
                m.Advance(1, out from, out to);
            } while (to == from && guard++ < 1000);

            Assert.Contains(LedText.Loss, m.Strip.Cells);
        }

        [Fact]
        public void HugeRateIsClipped()
        {
            var m = new LedMarquee(100, null);
            m.SetContent(new TickerSnapshot(new[] { "TVLC" }, new[] { 1234567f }), 0, 1);
            long from,
                to;
            for (int i = 0; i < 500; i++)
            {
                m.Advance(1, out from, out to);
                Assert.True(to - from <= LedMarquee.MaxEntryDots * 2);
            }
        }

        [Fact]
        public void RowSliceUsesOnlyItsEntries()
        {
            var m = new LedMarquee(100, null);
            m.SetContent(new TickerSnapshot(new[] { "A", "B" }, new[] { 1f, -1f }), 1, 1);
            m.Restart();
            Assert.Contains(LedText.Loss, m.Strip.Cells);
            Assert.DoesNotContain(LedText.Gain, m.Strip.Cells);
        }

        [Fact]
        public void RowRangeSplitsLikeTheBoards()
        {
            int s,
                c;
            LedText.RowRange(8, 3, 2, out s, out c);
            Assert.Equal(6, s);
            Assert.Equal(2, c);
            LedText.RowRange(1, 3, 2, out s, out c);
            Assert.Equal(0, c);
        }
    }
}

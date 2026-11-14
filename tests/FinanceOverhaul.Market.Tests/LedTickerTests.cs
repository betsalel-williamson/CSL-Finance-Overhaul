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

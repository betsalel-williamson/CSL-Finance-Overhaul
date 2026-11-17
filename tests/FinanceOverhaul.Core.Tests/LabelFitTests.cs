using System;
using FinanceOverhaul.Core;
using Xunit;

namespace FinanceOverhaul.Core.Tests
{
    public class LabelFitTests
    {
        /// <summary>Text 'baseWidth' wide at scale 0.75, proportional to scale.</summary>
        private static Func<float, float> Linear(float baseWidth)
        {
            return s => baseWidth * s / 0.75f;
        }

        /// <summary>Like the game: each of 50 glyphs rounds up to a whole pixel.</summary>
        private static Func<float, float> Rounded(float glyphAtBase, float baseScale)
        {
            return s => 50f * (float)Math.Ceiling(glyphAtBase * s / baseScale);
        }

        [Fact]
        public void SingleLine_KeepsBaseScaleWhenTextFits()
        {
            var r = LabelFit.SingleLine(0.75f, 0.6f, 360f, Linear(300f));
            Assert.Equal(0.75f, r.Scale);
            Assert.False(r.Wrap);
        }

        [Fact]
        public void SingleLine_ExactFitIsNotScaled()
        {
            var r = LabelFit.SingleLine(0.75f, 0.6f, 360f, Linear(360f));
            Assert.Equal(0.75f, r.Scale);
            Assert.False(r.Wrap);
        }

        [Fact]
        public void SingleLine_ScalesDownSoTheTextJustFits()
        {
            // 400 wide at 0.75 into 360: 0.75 * 360 / 400 = 0.675.
            var r = LabelFit.SingleLine(0.75f, 0.6f, 360f, Linear(400f));
            Assert.False(r.Wrap);
            Assert.Equal(0.675f, r.Scale, 2);
            Assert.True(Linear(400f)(r.Scale) <= 360f);
        }

        [Fact]
        public void SingleLine_ReMeasuresWhenGlyphRoundingMakesTheGuessTooWide()
        {
            // 7.8 px glyphs draw 8 px wide: 400 at the base scale, still 400 at the
            // proportional guess; only below 7 px a glyph (scale ~0.757) does it fit.
            Func<float, float> width = Rounded(7.8f, 0.84375f);
            var r = LabelFit.SingleLine(0.84375f, 0.6f, 388f, width);
            Assert.False(r.Wrap);
            Assert.True(r.Scale < 0.84375f);
            Assert.True(width(r.Scale) <= 388f);
            Assert.True(r.Scale > 0.74f);
        }

        [Fact]
        public void SingleLine_MinimumScaleItselfStillFitsOnOneLine()
        {
            // 0.75 * 360 / 450 = 0.6 exactly.
            var r = LabelFit.SingleLine(0.75f, 0.6f, 360f, Linear(450f));
            Assert.False(r.Wrap);
            Assert.Equal(0.6f, r.Scale, 2);
        }

        [Fact]
        public void SingleLine_WrapsAtBaseScaleWhenTooWideEvenAtTheMinimum()
        {
            var r = LabelFit.SingleLine(0.75f, 0.6f, 360f, Linear(600f));
            Assert.True(r.Wrap);
            Assert.Equal(0.75f, r.Scale);
        }

        [Fact]
        public void SingleLine_NoRoomWraps()
        {
            var r = LabelFit.SingleLine(0.75f, 0.6f, 0f, Linear(100f));
            Assert.True(r.Wrap);
        }

        [Fact]
        public void SingleLine_EmptyTextKeepsBaseScale()
        {
            var r = LabelFit.SingleLine(0.75f, 0.6f, 360f, Linear(0f));
            Assert.Equal(0.75f, r.Scale);
            Assert.False(r.Wrap);
        }

        [Fact]
        public void SingleLine_BaseBelowMinimumNeverScalesUp()
        {
            // A label already drawn smaller than the minimum wraps rather than growing.
            var r = LabelFit.SingleLine(0.5f, 0.6f, 360f, Linear(600f));
            Assert.True(r.Wrap);
            Assert.Equal(0.5f, r.Scale);
        }
    }
}

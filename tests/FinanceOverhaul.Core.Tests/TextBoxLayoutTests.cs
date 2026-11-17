using FinanceOverhaul.Core;
using Xunit;

namespace FinanceOverhaul.Core.Tests
{
    public class TextBoxLayoutTests
    {
        [Fact]
        public void Center_PutsTextInTheMiddleHorizontally()
        {
            var p = TextBoxLayout.Center(64f, 22f, 24f, 0f, 10f, 0f, 4);
            Assert.Equal(20, p.Left);
        }

        [Fact]
        public void Center_NeverGoesBelowMinimumSidePadding()
        {
            var p = TextBoxLayout.Center(64f, 22f, 80f, 0f, 10f, 0f, 4);
            Assert.Equal(4, p.Left);
        }

        [Fact]
        public void Center_UsesGlyphNotLineBoxVertically()
        {
            // Digits start 3 units below the line top and are 10 tall in a 22-tall box:
            // glyph should sit at (22 - 10) / 2 = 6 → padding top = 6 - 3 = 3, and with no
            // descent the bottom padding ends right under the digits: 22 - 16 = 6.
            var p = TextBoxLayout.Center(64f, 22f, 24f, 3f, 10f, 0f, 4);
            Assert.Equal(3, p.Top);
            Assert.Equal(6, p.Bottom);
        }

        [Fact]
        public void Center_DescentLetsCaretReachBelowDigits()
        {
            // In-game share field: box 27, digits 12.18 tall from the line top, 3.5 descent.
            var p = TextBoxLayout.Center(67f, 27f, 27f, 0f, 12.18f, 3.5f, 4);
            Assert.Equal(7, p.Top);
            Assert.Equal(4, p.Bottom);
            // Caret/selection height = box - top - bottom = 16 (> 12.18 digit height).
            Assert.Equal(16, 27 - p.Top - p.Bottom);
        }

        [Fact]
        public void Center_ClampsVerticalPaddingAtZero()
        {
            var p = TextBoxLayout.Center(64f, 10f, 24f, 8f, 10f, 0f, 4);
            Assert.Equal(0, p.Top);
            Assert.Equal(0, p.Bottom);
        }

        [Fact]
        public void Center_EmptyTextCentersCaret()
        {
            var p = TextBoxLayout.Center(64f, 22f, 0f, 0f, 10f, 0f, 4);
            Assert.Equal(32, p.Left);
        }
    }
}

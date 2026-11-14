using FinanceOverhaul.Market;
using Xunit;

namespace FinanceOverhaul.Market.Tests
{
    public class TextBoxLayoutTests
    {
        [Fact]
        public void Center_PutsTextInTheMiddleHorizontally()
        {
            var p = TextBoxLayout.Center(64f, 22f, 24f, 0f, 10f, 4);
            Assert.Equal(20, p.Left);
        }

        [Fact]
        public void Center_NeverGoesBelowMinimumSidePadding()
        {
            var p = TextBoxLayout.Center(64f, 22f, 80f, 0f, 10f, 4);
            Assert.Equal(4, p.Left);
        }

        [Fact]
        public void Center_UsesGlyphNotLineBoxVertically()
        {
            // Digits start 3 units below the line top and are 10 tall in a 22-tall box:
            // glyph should sit at (22 - 10) / 2 = 6 → padding top = 6 - 3 = 3.
            var p = TextBoxLayout.Center(64f, 22f, 24f, 3f, 10f, 4);
            Assert.Equal(3, p.Top);
            Assert.Equal(3, p.Bottom);
        }

        [Fact]
        public void Center_ClampsVerticalPaddingAtZero()
        {
            var p = TextBoxLayout.Center(64f, 10f, 24f, 8f, 10f, 4);
            Assert.Equal(0, p.Top);
            Assert.Equal(0, p.Bottom);
        }

        [Fact]
        public void Center_EmptyTextCentersCaret()
        {
            var p = TextBoxLayout.Center(64f, 22f, 0f, 0f, 10f, 4);
            Assert.Equal(32, p.Left);
        }
    }
}

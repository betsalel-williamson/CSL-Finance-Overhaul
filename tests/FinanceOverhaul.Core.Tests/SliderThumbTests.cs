using FinanceOverhaul.Core; // SliderThumb
using Xunit; // Assert, FactAttribute, InlineDataAttribute, TheoryAttribute

namespace FinanceOverhaul.Core.Tests
{
    public class SliderThumbTests
    {
        [Theory]
        [InlineData(0f, 0f, 200f, 0f)]
        [InlineData(100f, 0f, 200f, 0.5f)]
        [InlineData(200f, 0f, 200f, 1f)]
        [InlineData(1f, 1f, 100f, 0f)]
        [InlineData(100f, 1f, 100f, 1f)]
        [InlineData(500f, 1f, 100f, 1f)]
        [InlineData(-5f, 1f, 100f, 0f)]
        public void Fraction_IsPositionOnTrack(float value, float min, float max, float want)
        {
            Assert.Equal(want, SliderThumb.Fraction(value, min, max), 4);
        }

        [Fact]
        public void Fraction_EmptyRange_IsAtTheEnd()
        {
            Assert.Equal(1f, SliderThumb.Fraction(1f, 1f, 1f));
            Assert.Equal(1f, SliderThumb.Fraction(0f, 0f, 0f));
        }

        [Fact]
        public void DrawnFraction_UsesThumbCentreAndOffset()
        {
            // 300-wide track, 20-wide thumb centred on 150 (+2 offset) → half way.
            Assert.Equal(0.5f, SliderThumb.DrawnFraction(142f, 20f, 2f, 300f), 4);
            Assert.Equal(1f, SliderThumb.DrawnFraction(290f, 20f, 0f, 300f), 4);
            Assert.Equal(0f, SliderThumb.DrawnFraction(0f, 20f, 0f, 0f));
        }

        [Fact]
        public void Matches_SellAtMax_ThumbLeftFromBuyRange_IsStale()
        {
            // Buy drew 100 of 0..200 (mid-track); Sell keeps 100 but the range is 1..100.
            float drawn = SliderThumb.Fraction(100f, 0f, 200f);
            Assert.False(SliderThumb.Matches(drawn, 100f, 1f, 100f));
            Assert.True(SliderThumb.Matches(SliderThumb.Fraction(100f, 1f, 100f), 100f, 1f, 100f));
        }

        [Fact]
        public void Matches_EmptyRange_AlwaysMatches()
        {
            Assert.True(SliderThumb.Matches(float.NaN, 0f, 0f, 0f));
        }

        [Fact]
        public void Matches_AllowsOnePercent()
        {
            Assert.True(SliderThumb.Matches(0.995f, 100f, 0f, 100f));
            Assert.False(SliderThumb.Matches(0.97f, 100f, 0f, 100f));
        }
    }
}

using FinanceOverhaul.Market;
using Xunit;

namespace FinanceOverhaul.Market.Tests
{
    public class CaretLayoutTests
    {
        [Fact]
        public void Bar_ReachesAboveDigitsAndBelowBaseline()
        {
            // Share field: digits drawn from padding.top 7, 12.18 tall, font 14.
            CaretLayout.Bar bar = CaretLayout.Measure(7f, 0f, 12.18f, 14f);
            Assert.True(bar.Top < 7f, "caret should start above the digits");
            Assert.True(bar.Top + bar.Height > 7f + 12.18f, "caret should end below the digits");
            Assert.Equal(7f - 14f * CaretLayout.AboveEm, bar.Top, 3);
            Assert.Equal(12.18f + 14f * (CaretLayout.AboveEm + CaretLayout.BelowEm), bar.Height, 3);
        }

        [Fact]
        public void Bar_NeverStartsAboveTheBox()
        {
            CaretLayout.Bar bar = CaretLayout.Measure(0f, 0f, 12f, 14f);
            Assert.Equal(0f, bar.Top, 3);
        }

        [Theory]
        [InlineData(0.0f, true)]
        [InlineData(0.49f, true)]
        [InlineData(0.51f, false)]
        [InlineData(0.99f, false)]
        [InlineData(1.2f, true)]
        public void Blink_SolidFirstHalfOfEachPeriod(float sinceChange, bool visible)
        {
            Assert.Equal(visible, CaretLayout.IsVisible(sinceChange, 0.5f));
        }

        [Fact]
        public void Blink_ZeroRateStaysSolid()
        {
            Assert.True(CaretLayout.IsVisible(3.7f, 0f));
        }
    }
}

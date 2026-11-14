using FinanceOverhaul.Market;
using Xunit;

namespace FinanceOverhaul.Market.Tests
{
    public class ShareAmountInputTests
    {
        [Theory]
        [InlineData("250", 1000u, 250u)]
        [InlineData(" 42 ", 1000u, 42u)]
        [InlineData("1,500", 2000u, 1500u)]
        [InlineData("0", 1000u, 0u)]
        public void TryParse_AcceptsPlainCounts(string text, uint max, uint expected)
        {
            Assert.True(ShareAmountInput.TryParse(text, 0u, max, out uint value));
            Assert.Equal(expected, value);
        }

        [Fact]
        public void TryParse_ClampsAboveMaximum()
        {
            Assert.True(ShareAmountInput.TryParse("99999", 0u, 800u, out uint value));
            Assert.Equal(800u, value);
        }

        [Fact]
        public void TryParse_ClampsBelowMinimum()
        {
            Assert.True(ShareAmountInput.TryParse("0", 1u, 800u, out uint value));
            Assert.Equal(1u, value);
        }

        [Fact]
        public void TryParse_HugeNumberClampsToMaximum()
        {
            Assert.True(
                ShareAmountInput.TryParse(
                    "123456789012345678901234567890",
                    0u,
                    800u,
                    out uint value
                )
            );
            Assert.Equal(800u, value);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("-5")]
        [InlineData("12.5")]
        [InlineData("abc")]
        [InlineData("1e3")]
        public void TryParse_RejectsNonCounts(string text)
        {
            Assert.False(ShareAmountInput.TryParse(text, 0u, 800u, out uint value));
            Assert.Equal(0u, value);
        }

        [Fact]
        public void TryParse_MaxBelowMinUsesMinimum()
        {
            Assert.True(ShareAmountInput.TryParse("5", 1u, 0u, out uint value));
            Assert.Equal(1u, value);
        }
    }
}

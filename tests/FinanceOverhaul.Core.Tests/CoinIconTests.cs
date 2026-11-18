using FinanceOverhaul.Core;
using Xunit;

namespace FinanceOverhaul.Core.Tests
{
    public class CoinIconTests
    {
        [Fact]
        public void TheCoinIsARoundFaceWithARimARingAndAnEdgeBelow()
        {
            const int size = 64;
            byte[] mask = CoinIcon.Render(size);
            Assert.Equal(size * size, mask.Length);
            Assert.Equal(CoinIcon.Empty, mask[0]); // corners are clear
            Assert.Equal(CoinIcon.Empty, mask[size * size - 1]);
            Assert.Equal(CoinIcon.Face, mask[30 * size + 32]); // the middle, where the symbol goes
            Assert.Equal(CoinIcon.Rim, mask[30 * size + 5]); // the left side of the face
            Assert.Contains(CoinIcon.Ring, mask);
            // The edge shows under the face, never above it.
            Assert.Equal(CoinIcon.Edge, mask[61 * size + 32]);
            for (int y = 0; y < size / 2; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Assert.NotEqual(CoinIcon.Edge, mask[y * size + x]);
                }
            }
        }

        [Fact]
        public void LongerSymbolsAreDrawnSmallerAndASmallerCoinScalesThemDown()
        {
            Assert.True(CoinIcon.SymbolScale("GI", 64f) > CoinIcon.SymbolScale("IDX", 64f));
            Assert.True(CoinIcon.SymbolScale("IDX", 64f) > CoinIcon.SymbolScale("TVLC", 64f));
            Assert.Equal(CoinIcon.SymbolScale("IDX", 64f) / 2f, CoinIcon.SymbolScale("IDX", 32f));
            Assert.Empty(CoinIcon.Render(0));
        }
    }
}

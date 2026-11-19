using FinanceOverhaul.Core; // CityIndexBasket
using Xunit; // Assert, FactAttribute

namespace FinanceOverhaul.Core.Tests
{
    public class CityIndexChainTests
    {
        private static bool Chain(
            float previous,
            float[] last,
            float[] prev,
            bool[] eligible,
            out float mark,
            string[] names = null
        )
        {
            names = names ?? new string[last.Length];
            for (int i = 0; i < names.Length; i++)
            {
                names[i] = names[i] ?? "S" + i;
            }

            return CityIndexBasket.TryComputeChainedMark(
                previous,
                names,
                last,
                prev,
                eligible,
                out mark
            );
        }

        [Fact]
        public void EqualWeightsReturnsNotPrices()
        {
            // Cheap stock ×1.1, expensive stock ÷1.1 → index flat (price average would drop ~9%).
            Assert.True(
                Chain(
                    1000f,
                    new[] { 110f, 2000f / 1.1f },
                    new[] { 100f, 2000f },
                    new[] { true, true },
                    out float mark
                )
            );
            Assert.Equal(1000f, mark, 1);
        }

        [Fact]
        public void IndexMovesByAverageReturn()
        {
            Assert.True(
                Chain(
                    1000f,
                    new[] { 102f, 101f, 103f },
                    new[] { 100f, 100f, 100f },
                    new[] { true, true, true },
                    out float mark
                )
            );
            // Geometric mean of the moves: (1.02 × 1.01 × 1.03)^(1/3) ≈ 1.019967.
            Assert.Equal(1019.967f, mark, 1);
        }

        [Fact]
        public void IndexIsCalmerThanItsMostVolatileMember()
        {
            float[] prev = { 100f, 100f, 100f, 100f };
            float[] last = { 112f, 99f, 101f, 100f }; // one member +12%
            Assert.True(Chain(1000f, last, prev, new[] { true, true, true, true }, out float mark));
            float indexMove = mark / 1000f - 1f;
            Assert.True(indexMove < 0.12f / 2f);
            // exp(mean ln(1.12, 0.99, 1.01, 1.00)) − 1 ≈ 2.95% (the old mean of returns: 3%).
            Assert.Equal(0.0287f, indexMove, 3);
        }

        [Fact]
        public void SkipsIneligibleIndexAndBadHistory()
        {
            Assert.True(
                Chain(
                    1000f,
                    new[] { 110f, 500f, 50f, float.NaN, 120f },
                    new[] { 100f, 100f, 0f, 100f, 100f },
                    new[] { true, false, true, true, true },
                    out float mark,
                    new[] { "A", "Locked", "NoPrev", "Broken", CityIndexBasket.IndexName }
                )
            );
            Assert.Equal(1100f, mark, 2);
        }

        [Fact]
        public void ClampsAbsurdSingleTickMoves()
        {
            // A 10x jump (e.g. a stock unlocking from ₡1) counts as at most +100%.
            Assert.True(
                Chain(1000f, new[] { 1000f }, new[] { 100f }, new[] { true }, out float mark)
            );
            Assert.Equal(2000f, mark, 2);
        }

        [Fact]
        public void NoUsableMembersOrNoPreviousLevelFailsSoft()
        {
            Assert.False(Chain(1000f, new[] { 1f }, new[] { 1f }, new[] { false }, out _));
            Assert.False(Chain(0f, new[] { 110f }, new[] { 100f }, new[] { true }, out _));
            Assert.False(Chain(float.NaN, new[] { 110f }, new[] { 100f }, new[] { true }, out _));
        }
    }
}

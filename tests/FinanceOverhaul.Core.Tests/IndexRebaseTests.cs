using Xunit;

namespace FinanceOverhaul.Core.Tests
{
    public class IndexRebaseTests
    {
        [Fact]
        public void InflatedLevelIsScaledToTheMembersAverage()
        {
            IndexRebase.Plan p = IndexRebase.Decide(98000f, 1960f, 0, 0);
            Assert.Equal(IndexRebase.Decision.Rescale, p.Decision);
            Assert.Equal(0.02, p.Factor, 6);
        }

        [Fact]
        public void LevelsWithinTwoTimesAreLeftAlone()
        {
            Assert.Equal(
                IndexRebase.Decision.Skip,
                IndexRebase.Decide(3000f, 1960f, 0, 0).Decision
            );
            Assert.Equal(
                IndexRebase.Decision.Skip,
                IndexRebase.Decide(1000f, 1960f, 0, 0).Decision
            );
            Assert.Equal(
                IndexRebase.Decision.Skip,
                IndexRebase.Decide(float.NaN, 1960f, 0, 0).Decision
            );
            Assert.Equal(IndexRebase.Decision.Skip, IndexRebase.Decide(98000f, 0f, 0, 0).Decision);
        }

        [Fact]
        public void WaitsWhileTheCityHoldsIndexOrHasIndexOrders()
        {
            Assert.Equal(
                IndexRebase.Decision.Wait,
                IndexRebase.Decide(98000f, 1960f, 280, 0).Decision
            );
            Assert.Equal(
                IndexRebase.Decision.Wait,
                IndexRebase.Decide(98000f, 1960f, 0, 1).Decision
            );
        }

        [Fact]
        public void ScalingKeepsTheChartShape()
        {
            float[] h = { 81950f, 83968f, 98860f };
            float[] s = IndexRebase.Scale(h, 0.02);
            Assert.Equal(1639f, s[0], 0);
            for (int i = 1; i < h.Length; i++)
            {
                Assert.Equal(h[i] / h[i - 1], s[i] / s[i - 1], 4); // every day's move unchanged
            }

            Assert.True(float.IsNaN(IndexRebase.Scale(new[] { float.NaN }, 0.5)[0]));
            Assert.Empty(IndexRebase.Scale(null, 0.5));
        }
    }
}

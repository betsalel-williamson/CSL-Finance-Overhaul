using System; // Math
using FinanceOverhaul.Core; // FacadeFraming
using Xunit; // Assert, FactAttribute, InlineDataAttribute, TheoryAttribute

namespace FinanceOverhaul.Core.Tests
{
    public class FacadeFramingTests
    {
        [Fact]
        public void FrontOffsetMatchesVanillaFrontDirection()
        {
            // Vanilla front direction is (−sin a, 0, cos a).
            foreach (float a in new[] { 0f, 1.546f, 3f, -2f })
            {
                float x,
                    z;
                FacadeFraming.LocalToWorld(a, 0f, 1f, out x, out z);
                Assert.Equal(-Math.Sin(a), x, 4);
                Assert.Equal(Math.Cos(a), z, 4);
            }
        }

        [Theory]
        [InlineData(0f, 180f)] // front is +Z, so look along −Z
        [InlineData((float)(Math.PI / 2), 90f)] // front is −X, look along +X
        [InlineData((float)Math.PI, 0f)]
        public void YawLooksIntoTheFacade(float angle, float expected)
        {
            float yaw = FacadeFraming.FacadeYawDegrees(angle);
            double diff = Math.Abs(((yaw - expected) % 360.0 + 540.0) % 360.0 - 180.0);
            Assert.True(diff < 0.01, "yaw " + yaw + " vs " + expected);

            // Unity yaw → horizontal forward is (sin yaw, cos yaw); it must equal −front.
            double r = yaw * Math.PI / 180.0;
            Assert.Equal(Math.Sin(angle), Math.Sin(r), 4);
            Assert.Equal(-Math.Cos(angle), Math.Cos(r), 4);
        }

        [Theory]
        [InlineData(null, 1, false)]
        [InlineData("", 1, false)]
        [InlineData("capture", 1, true)]
        [InlineData("2", 2, false)]
        [InlineData("3 capture\n", 3, true)]
        [InlineData("0 junk", 1, false)]
        public void ParsesRequest(string text, int index, bool capture)
        {
            int i;
            bool c;
            FacadeFraming.ParseRequest(text, out i, out c);
            Assert.Equal(index, i);
            Assert.Equal(capture, c);
        }
    }
}

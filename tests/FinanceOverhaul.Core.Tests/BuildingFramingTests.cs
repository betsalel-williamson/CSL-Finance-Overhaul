using FinanceOverhaul.Core; // BuildingFraming
using Xunit; // Assert, FactAttribute

namespace FinanceOverhaul.Core.Tests
{
    public class BuildingFramingTests
    {
        [Fact]
        public void ASmallShopIsSeenWithItsNeighbours()
        {
            Assert.Equal(BuildingFraming.Nearest, BuildingFraming.Distance(16f, 16f, 10f));
        }

        [Fact]
        public void ATowerIsFramedByItsHeight()
        {
            Assert.Equal(320f, BuildingFraming.Distance(32f, 32f, 160f));
            Assert.True(BuildingFraming.Pitch(160f) < BuildingFraming.Pitch(20f));
        }

        [Fact]
        public void AWideLotIsFramedByItsLargerSide()
        {
            Assert.Equal(288f, BuildingFraming.Distance(32f, 96f, 12f));
        }

        [Fact]
        public void TheCameraNeverLeavesTheBuildingBehind()
        {
            Assert.Equal(BuildingFraming.Farthest, BuildingFraming.Distance(32f, 32f, 5000f));
            Assert.Equal(
                BuildingFraming.Nearest,
                BuildingFraming.Distance(float.NaN, -3f, float.NaN)
            );
        }
    }
}

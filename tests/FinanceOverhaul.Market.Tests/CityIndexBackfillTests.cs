using FinanceOverhaul.Market;
using Xunit;

namespace FinanceOverhaul.Market.Tests
{
    public class CityIndexBackfillTests
    {
        // Histories are newest-first (index 0 = latest, like Queue.GetFromTop).

        [Fact]
        public void RebuildsOlderLevelsFromMemberReturns()
        {
            // Two members; each day both rose 10%, so going back each older level is /1.1.
            var histories = new[] { new[] { 121f, 110f, 100f }, new[] { 242f, 220f, 200f } };
            float[] older = CityIndexBasket.BackfillOlder(
                1210f,
                1,
                new[] { "A", "B" },
                histories,
                new[] { true, true },
                10
            );
            Assert.Equal(2, older.Length);
            Assert.Equal(1100f, older[0], 1); // 1 day before the oldest own point
            Assert.Equal(1000f, older[1], 1); // 2 days before
        }

        [Fact]
        public void StartsAfterIndexsOwnHistory()
        {
            // Index already has 2 own points (steps 0 and 1); only step 2 is missing.
            var histories = new[] { new[] { 121f, 110f, 100f } };
            float[] older = CityIndexBasket.BackfillOlder(
                1100f,
                2,
                new[] { "A" },
                histories,
                new[] { true },
                10
            );
            Assert.Single(older);
            Assert.Equal(1000f, older[0], 1);
        }

        [Fact]
        public void EqualWeightsReturnsAcrossCheapAndExpensiveNames()
        {
            // Cheap ×1.1 and expensive ÷1.1 on the same day → index flat that day.
            var histories = new[] { new[] { 110f, 100f }, new[] { 2000f / 1.1f, 2000f } };
            float[] older = CityIndexBasket.BackfillOlder(
                500f,
                1,
                new[] { "Cheap", "Pricey" },
                histories,
                new[] { true, true },
                10
            );
            Assert.Equal(500f, older[0], 1);
        }

        [Fact]
        public void SkipsIndexIneligibleAndShortHistories()
        {
            var histories = new[]
            {
                new[] { 110f, 100f, 100f }, // A: +10% then flat
                new[] { 999f, 1f, 1f }, // Locked: ignored
                new[] { 50f }, // Young: no returns available
                new[] { 1f, 2f, 3f }, // Index itself: ignored
            };
            float[] older = CityIndexBasket.BackfillOlder(
                1100f,
                1,
                new[] { "A", "Locked", "Young", CityIndexBasket.IndexName },
                histories,
                new[] { true, false, true, true },
                10
            );
            Assert.Equal(2, older.Length);
            Assert.Equal(1000f, older[0], 1);
            Assert.Equal(1000f, older[1], 1);
        }

        [Fact]
        public void HoldsLevelOnDaysWithNoUsableMember()
        {
            var histories = new[] { new[] { 110f, float.NaN, 100f } };
            float[] older = CityIndexBasket.BackfillOlder(
                1000f,
                1,
                new[] { "A" },
                histories,
                new[] { true },
                10
            );
            Assert.Equal(new[] { 1000f, 1000f }, older);
        }

        [Fact]
        public void RespectsCapacityIncludingOwnPoints()
        {
            var histories = new[] { new[] { 6f, 5f, 4f, 3f, 2f, 1f } };
            float[] older = CityIndexBasket.BackfillOlder(
                100f,
                2,
                new[] { "A" },
                histories,
                new[] { true },
                4
            );
            Assert.Equal(2, older.Length); // 2 own + 2 backfilled = capacity 4
        }

        [Fact]
        public void NothingToDoReturnsEmpty()
        {
            Assert.Empty(
                CityIndexBasket.BackfillOlder(
                    0f,
                    1,
                    new[] { "A" },
                    new[] { new[] { 2f, 1f } },
                    new[] { true },
                    10
                )
            );
            Assert.Empty(
                CityIndexBasket.BackfillOlder(
                    100f,
                    3,
                    new[] { "A" },
                    new[] { new[] { 2f, 1f } },
                    new[] { true },
                    10
                )
            );
            Assert.Empty(CityIndexBasket.BackfillOlder(100f, 1, null, null, null, 10));
        }
    }
}

using System.Collections.Generic; // Dictionary, List
using System.Linq; // Sum()
using Xunit; // Assert, FactAttribute

namespace FinanceOverhaul.Core.Tests
{
    public class IpoShortlistTests
    {
        private static BuildingKey K(ushort id) => new BuildingKey(id, 1);

        [Fact]
        public void TheShortlistIsTheLargestFirstTiesToTheOlderBuilding()
        {
            var workers = new Dictionary<BuildingKey, int>
            {
                { K(1), 20 },
                { K(2), 186 },
                { K(3), 45 },
                { K(4), 186 },
                { K(5), 90 },
            };
            var pool = new List<BuildingKey> { K(1), K(2), K(3), K(4), K(5) };
            List<BuildingKey> top = IpoShortlist.Rank(pool, b => workers[b], 3);
            Assert.Equal(new[] { K(2), K(4), K(5) }, top);
            Assert.Equal(5, IpoShortlist.Rank(pool, b => workers[b], 10).Count);
            Assert.Single(IpoShortlist.Rank(pool, b => workers[b], 0)); // at least one
            Assert.Empty(IpoShortlist.Rank(new List<BuildingKey>(), b => 0, 10));
            Assert.Equal(new[] { K(1), K(2), K(3), K(4), K(5) }, pool); // the pool is untouched
        }

        [Fact]
        public void ABiggerCandidateIsLikelierByAsMuchAsTheOptionSays()
        {
            var sizes = new List<int> { 300, 100 };
            double[] full = IpoShortlist.Chances(sizes, 100);
            Assert.Equal(0.75, full[0], 6);
            Assert.Equal(0.25, full[1], 6);
            double[] none = IpoShortlist.Chances(sizes, 0);
            Assert.Equal(0.5, none[0], 6);
            double[] half = IpoShortlist.Chances(sizes, 50);
            Assert.Equal(0.625, half[0], 6);
            Assert.Equal(1.0, IpoShortlist.Chances(new List<int> { 7, 0, 186 }, 70).Sum(), 6);
            // Nobody read yet: everyone still has a chance.
            Assert.Equal(0.5, IpoShortlist.Chances(new List<int> { 0, 0 }, 100)[0], 6);
            Assert.Empty(IpoShortlist.Chances(new List<int>(), 100));
        }

        [Fact]
        public void TheRollLandsOnTheCandidateWhoseShareItFallsIn()
        {
            var sizes = new List<int> { 300, 100 };
            Assert.Equal(0, IpoShortlist.Pick(sizes, 100, 0.0));
            Assert.Equal(0, IpoShortlist.Pick(sizes, 100, 0.7499));
            Assert.Equal(1, IpoShortlist.Pick(sizes, 100, 0.75));
            Assert.Equal(1, IpoShortlist.Pick(sizes, 100, 0.9999999));
            Assert.Equal(-1, IpoShortlist.Pick(new List<int>(), 100, 0.5));
        }

        [Fact]
        public void TheBookKeepsWorkersOnlyForWhatItCounts()
        {
            var rules = new IpoRules();
            var book = new CandidateBook();
            book.Restore(K(1), rules.HealthyDays, BusinessKind.Office);
            book.SetWorkers(K(1), 120);
            book.SetWorkers(K(2), 99); // not counted: not kept
            Assert.Equal(120, book.WorkersOf(K(1)));
            Assert.Equal(0, book.WorkersOf(K(2)));
            book.EndDay(); // not seen today: forgotten, workers too
            Assert.Equal(0, book.WorkersOf(K(1)));
        }
    }
}

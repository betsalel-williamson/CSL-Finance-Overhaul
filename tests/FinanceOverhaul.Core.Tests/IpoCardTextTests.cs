using System.Collections.Generic; // List
// FinanceOverhaul.Core: BoardView, BusinessKind, CompanyRegistry, IpoCardText, IpoRules, KindFacts,
//   KindStatus, LocalCompany
using FinanceOverhaul.Core;
using Xunit; // Assert, FactAttribute

namespace FinanceOverhaul.Core.Tests
{
    public class IpoCardTextTests
    {
        private static KindFacts Kind(BusinessKind kind, int squares, int top, int candidates)
        {
            return new KindFacts
            {
                Kind = kind,
                Squares = squares,
                TopLevelBuildings = top,
                Candidates = candidates,
            };
        }

        private static CompanyRegistry With(params BusinessKind[] listed)
        {
            var registry = new CompanyRegistry();
            foreach (BusinessKind kind in listed)
            {
                registry.Companies.Add(new LocalCompany { Id = registry.NextId++, Kind = kind });
            }

            return registry;
        }

        [Fact]
        public void OneCardPerKindTheCityHas()
        {
            var kinds = new List<KindFacts>
            {
                Kind(BusinessKind.Office, 132, 9, 2),
                Kind(BusinessKind.ItCluster, 400, 6, 14),
                Kind(BusinessKind.Ore, 0, 0, 0), // none in the city: no card
            };
            BoardView board = IpoCardText.View(
                kinds,
                new CompanyRegistry(),
                3,
                1000,
                new IpoRules()
            );
            Assert.Equal(2, board.Kinds.Count);
            Assert.Equal(6, board.Cap);
            Assert.Equal(0, board.Listed);
            Assert.True(board.HasRoom);
        }

        [Fact]
        public void ALockedKindShowsItsRequirementAndTheBarTowardsIt()
        {
            var rules = new IpoRules();
            BoardView board = IpoCardText.View(
                new List<KindFacts> { Kind(BusinessKind.Office, 132, 9, 2) },
                new CompanyRegistry(),
                3,
                1000,
                rules
            );
            KindStatus office = board.Kinds[0];
            Assert.Equal("Office companies", IpoCardText.Title(office.Kind));
            Assert.Equal(
                "Unlock requirement: 200 squares of these businesses in your city.",
                IpoCardText.Line(office)
            );
            Assert.Equal(string.Empty, IpoCardText.Note(office, board, "01/01/2112"));
            Assert.Equal("To unlock", IpoCardText.BarCaption(office));
            Assert.Equal("132 / 200", IpoCardText.BarText(office));
            Assert.Equal(0.66f, IpoCardText.BarFraction(office), 3);
        }

        [Fact]
        public void AnOpenSlotSaysAListingIsLikelyNeverWhen()
        {
            BoardView board = IpoCardText.View(
                new List<KindFacts> { Kind(BusinessKind.ItCluster, 400, 6, 14) },
                new CompanyRegistry(),
                3,
                1000,
                new IpoRules()
            );
            KindStatus it = board.Kinds[0];
            Assert.Equal("1 slot open · 14 candidates", IpoCardText.Line(it));
            Assert.Equal(
                "A listing is likely within one to two years.",
                IpoCardText.Note(it, board, "01/01/2112")
            );
            Assert.Equal("Next slot", IpoCardText.BarCaption(it));
            Assert.Equal("6 / 20", IpoCardText.BarText(it));
        }

        [Fact]
        public void WaitingFullAndHeldBackEachSaySo()
        {
            var rules = new IpoRules();
            BoardView waiting = IpoCardText.View(
                new List<KindFacts> { Kind(BusinessKind.Leisure, 400, 45, 0) },
                new CompanyRegistry(),
                3,
                1000,
                rules
            );
            Assert.Equal("3 slots open · 0 candidates", IpoCardText.Line(waiting.Kinds[0]));
            Assert.Equal(
                "No business has been healthy long enough yet.",
                IpoCardText.Note(waiting.Kinds[0], waiting, "x")
            );

            BoardView full = IpoCardText.View(
                new List<KindFacts> { Kind(BusinessKind.Commercial, 900, 30, 8) },
                With(BusinessKind.Commercial, BusinessKind.Commercial),
                3,
                1000,
                rules
            );
            Assert.Equal("2 of 2 listed", IpoCardText.Line(full.Kinds[0]));
            Assert.Equal("30 / 40", IpoCardText.BarText(full.Kinds[0]));

            // Two IPOs already this year: an open slot is held back until the older is a year old.
            CompanyRegistry paced = With();
            paced.IpoDays.Add(900);
            paced.IpoDays.Add(950);
            BoardView held = IpoCardText.View(
                new List<KindFacts> { Kind(BusinessKind.Industry, 900, 9, 6) },
                paced,
                3,
                1000,
                rules
            );
            Assert.False(held.PacingAllows);
            Assert.Equal(1265, held.NextAllowedDay);
            Assert.Equal("1 slot open · 6 candidates", IpoCardText.Line(held.Kinds[0]));
            Assert.Equal(
                "Next listing possible from 01/01/2112.",
                IpoCardText.Note(held.Kinds[0], held, "01/01/2112")
            );

            // One tile holds two listings: the board itself is full.
            BoardView boardFull = IpoCardText.View(
                new List<KindFacts> { Kind(BusinessKind.Industry, 900, 9, 6) },
                With(BusinessKind.Office, BusinessKind.Office),
                1,
                1000,
                rules
            );
            Assert.Equal(
                "The local board is full (2 of 2).",
                IpoCardText.Note(boardFull.Kinds[0], boardFull, "x")
            );
        }

        [Fact]
        public void TheFooterSumsUpTheBoard()
        {
            BoardView board = IpoCardText.View(
                new List<KindFacts> { Kind(BusinessKind.Office, 900, 9, 6) },
                With(BusinessKind.Office, BusinessKind.Oil, BusinessKind.Oil),
                3,
                1000,
                new IpoRules()
            );
            Assert.Equal(
                "Local listings 3 of 6 · 2 per map tile (3 owned) · at most 2 IPOs a year",
                IpoCardText.Footer(board, new IpoRules())
            );
        }

        [Fact]
        public void AKindWithAListingButNothingBuiltAnyMoreKeepsItsCard()
        {
            BoardView board = IpoCardText.View(
                new List<KindFacts> { Kind(BusinessKind.Oil, 0, 0, 0) },
                With(BusinessKind.Oil),
                3,
                1000,
                new IpoRules()
            );
            Assert.Single(board.Kinds);
        }

        [Fact]
        public void EveryKindHasACardTitle()
        {
            foreach (BusinessKind kind in System.Enum.GetValues(typeof(BusinessKind)))
            {
                Assert.DoesNotContain("ipo.card", IpoCardText.Title(kind));
            }
        }
    }
}

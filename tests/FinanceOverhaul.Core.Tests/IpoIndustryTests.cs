using System.Collections.Generic; // List
// FinanceOverhaul.Core: AreaFacts, BoardView, BuildingKey, BusinessKind, BusinessSite,
//   CandidateBook, CompanyRegistry, FactoryFacts, IpoBoard, IpoCardText, IpoIndustry, IpoRules,
//   KindFacts, KindState, KindStatus
using FinanceOverhaul.Core;
using Xunit; // Assert, FactAttribute, InlineDataAttribute, TheoryAttribute

namespace FinanceOverhaul.Core.Tests
{
    public class IpoIndustryTests
    {
        // The test city's oil area (2026-11-19): exports ₡15,840, bought ₡1,920, upkeep ₡4,960 a week.
        private static AreaFacts OilArea(int level = 5)
        {
            return new AreaFacts
            {
                Kind = BusinessKind.Oil,
                Level = level,
                MainBuildingActive = true,
                WeeklySalesCents = 1_584_000,
                WeeklyBoughtCents = 192_000,
                WeeklyUpkeepCents = 496_000,
            };
        }

        private static FactoryFacts Bakery()
        {
            return new FactoryFacts
            {
                Active = true,
                ProductionPercent = 100,
                WeeklySalesCents = 384_000,
                WeeklyBoughtCents = 100_000,
                WeeklyUpkeepCents = 80_000,
            };
        }

        [Fact]
        public void AnAreaEarnsItsWeeksProfitTimesFiftyTwo()
        {
            // ₡8,960 a week: ₡465,920 a year.
            Assert.Equal(896_000L, IpoIndustry.WeeklyProfitCents(1_584_000, 192_000, 496_000));
            Assert.Equal(896_000L * 52, IpoIndustry.AnnualCents(OilArea()));
        }

        [Theory]
        [InlineData(2, false)]
        [InlineData(3, true)]
        [InlineData(5, true)]
        public void AnAreaQualifiesFromLevelThree(int level, bool healthy)
        {
            Assert.Equal(healthy, IpoIndustry.AreaHealthy(OilArea(level), new IpoRules()));
        }

        [Fact]
        public void AnAreaIsNotHealthySwitchedOffOrAtALoss()
        {
            var rules = new IpoRules();
            AreaFacts off = OilArea();
            off.MainBuildingActive = false;
            Assert.False(IpoIndustry.AreaHealthy(off, rules));

            AreaFacts loss = OilArea();
            loss.WeeklyUpkeepCents = 2_000_000;
            Assert.False(IpoIndustry.AreaHealthy(loss, rules));
            Assert.Equal(0L, IpoIndustry.AnnualCents(loss)); // a loss is no earnings, not negative

            AreaFacts zone = OilArea();
            zone.Kind = BusinessKind.Office; // only farming, forestry, oil and ore have areas
            Assert.False(IpoIndustry.AreaHealthy(zone, rules));
        }

        [Fact]
        public void AFactoryIsHealthyOnlyOnFullProductionWithNoProblemsAndAProfit()
        {
            var rules = new IpoRules();
            Assert.True(IpoIndustry.FactoryHealthy(Bakery(), rules));
            Assert.Equal(204_000L * 52, IpoIndustry.AnnualCents(Bakery()));

            FactoryFacts slow = Bakery();
            slow.ProductionPercent = 90;
            Assert.False(IpoIndustry.FactoryHealthy(slow, rules));

            FactoryFacts problems = Bakery();
            problems.HasProblems = true;
            Assert.False(IpoIndustry.FactoryHealthy(problems, rules));

            FactoryFacts off = Bakery();
            off.Active = false;
            Assert.False(IpoIndustry.FactoryHealthy(off, rules));

            FactoryFacts loss = Bakery();
            loss.WeeklyBoughtCents = 400_000;
            Assert.False(IpoIndustry.FactoryHealthy(loss, rules));
        }

        [Fact]
        public void EachAreaAtLevelThreeGivesItsKindASlot()
        {
            var rules = new IpoRules();
            Assert.Equal(2, IpoIndustry.AreaSlots(new[] { 5, 3, 2, 1 }, rules));
            Assert.Equal(0, IpoIndustry.AreaSlots(null, rules));

            // Oil with no zoned oil to speak of: open through its areas alone.
            var oil = new KindFacts
            {
                Kind = BusinessKind.Oil,
                Squares = 40,
                Candidates = 1,
                ExtraSlots = 2,
            };
            Assert.True(IpoBoard.GateOpen(oil, rules));
            Assert.Equal(2, IpoBoard.Slots(oil, rules));
            KindStatus status = IpoBoard.Status(oil, true, rules);
            Assert.Equal(KindState.Open, status.State);
            Assert.Equal(40, status.BarHave); // zoned oil can still earn a slot of its own
            Assert.Equal(rules.GateSquares, status.BarNeed);

            // With enough zoned oil too, the area slots come on top.
            oil.Squares = 300;
            Assert.Equal(3, IpoBoard.Slots(oil, rules));
        }

        [Fact]
        public void UniqueFactoriesHaveOneSlotEachAndNoBar()
        {
            var rules = new IpoRules();
            var factories = new KindFacts
            {
                Kind = BusinessKind.UniqueFactory,
                Squares = 900, // factories have no zoned gate, whatever their squares
                ExtraSlots = 3,
                Candidates = 1,
                Listed = 1,
            };
            Assert.Equal(3, IpoBoard.Slots(factories, rules));
            Assert.Equal(2, IpoBoard.OpenSlots(factories, rules));
            KindStatus status = IpoBoard.Status(factories, true, rules);
            Assert.Equal(KindState.Open, status.State);
            Assert.Equal(0, status.BarNeed);

            factories.ExtraSlots = 0;
            factories.Listed = 0;
            Assert.Equal(KindState.Locked, IpoBoard.Status(factories, true, rules).State);
        }

        [Fact]
        public void TheCardsShowAKindThatHasOnlyAreasOrFactories()
        {
            var kinds = new List<KindFacts>
            {
                new KindFacts { Kind = BusinessKind.UniqueFactory, ExtraSlots = 2 },
            };
            BoardView board = IpoCardText.View(kinds, new CompanyRegistry(), 3, 0, new IpoRules());
            KindStatus card = Assert.Single(board.Kinds);
            Assert.Equal("Unique factories", IpoCardText.Title(card.Kind));
        }

        [Fact]
        public void TheBookKeepsWhichCandidatesAreAreasAndFactories()
        {
            var rules = new IpoRules();
            var book = new CandidateBook();
            var area = new BuildingKey(40, 4);
            var factory = new BuildingKey(41, 5);
            var shop = new BuildingKey(42, 6);
            book.Observe(area, BusinessKind.Oil, true, rules, BusinessSite.Area, "area 7");
            book.Observe(
                factory,
                BusinessKind.UniqueFactory,
                true,
                rules,
                BusinessSite.Factory,
                "Bakery"
            );
            book.Observe(shop, BusinessKind.Oil, true, rules);
            book.EndDay();

            Assert.Equal(BusinessSite.Area, book.SiteOf(area));
            Assert.Equal("area 7", book.SiteRefOf(area));
            Assert.Equal(BusinessSite.Factory, book.SiteOf(factory));
            Assert.Equal(BusinessSite.Zoned, book.SiteOf(shop));
            Assert.Null(book.SiteRefOf(shop));
            Assert.Equal(2, book.Candidates(rules, BusinessKind.Oil).Count);

            // An unhealthy factory loses days, not its site; forgotten, it loses both.
            book.Observe(
                factory,
                BusinessKind.UniqueFactory,
                false,
                rules,
                BusinessSite.Factory,
                "Bakery"
            );
            book.EndDay();
            Assert.Equal(BusinessSite.Factory, book.SiteOf(factory));
            book.EndDay();
            Assert.Equal(BusinessSite.Zoned, book.SiteOf(factory));
            Assert.Equal(0, book.HealthyDaysOf(factory));
        }
    }
}

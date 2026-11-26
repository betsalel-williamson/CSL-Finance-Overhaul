using System.Collections.Generic; // List
// FinanceOverhaul.Core: AreaFacts, BoardView, BuildingKey, BusinessKind, BusinessSite,
//   CandidateBook, CompaniesCodec, CompanyRegistry, FactoryFacts, IpoBoard, IpoCardText,
//   IpoDialogText, IpoDice, IpoIndustry, IpoRules, KindFacts, KindState, KindStatus, LocalCompany,
//   SiteProfits
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

        [Fact]
        public void AreasAndFactoriesAlreadyInACityAreCreditedWithThePastYearOnce()
        {
            var rules = new IpoRules(); // profit averaged over 4 weeks: the credit stays open 28 days
            var book = new CandidateBook { LookedBehind = true }; // a city scanned before 0.2.0
            var area = new BuildingKey(40, 4);
            var factory = new BuildingKey(41, 5);
            var shop = new BuildingKey(42, 6);
            book.StartSiteDay(1000, rules);
            Assert.Equal(1028, book.SiteCreditUntilDay);
            book.Observe(
                factory,
                BusinessKind.UniqueFactory,
                true,
                rules,
                BusinessSite.Factory,
                "factory:Bakery"
            );
            book.Observe(area, BusinessKind.Oil, false, rules, BusinessSite.Area, "area:7"); // a bad week
            book.Observe(shop, BusinessKind.Office, true, rules); // a zoned business earns its days
            book.EndDay();
            Assert.True(book.IsCandidate(factory, rules));
            Assert.False(book.IsCandidate(area, rules));
            Assert.Equal(1, book.HealthyDaysOf(shop));

            // Still within the weeks: the area, healthy on its average now, gets the credit too.
            book.StartSiteDay(1010, rules);
            book.Observe(
                factory,
                BusinessKind.UniqueFactory,
                true,
                rules,
                BusinessSite.Factory,
                "factory:Bakery"
            );
            book.Observe(area, BusinessKind.Oil, true, rules, BusinessSite.Area, "area:7");
            book.EndDay();
            Assert.True(book.IsCandidate(area, rules));
            Assert.False(book.LookedBehindSites);

            // Over: one built afterwards counts its days one at a time.
            book.StartSiteDay(1028, rules);
            Assert.True(book.LookedBehindSites);
            var later = new BuildingKey(44, 8);
            book.Observe(
                later,
                BusinessKind.UniqueFactory,
                true,
                rules,
                BusinessSite.Factory,
                "factory:Toys"
            );
            book.EndDay();
            Assert.Equal(1, book.HealthyDaysOf(later));
        }

        [Fact]
        public void TheCreditAndTheWeeksAreRememberedInTheSave()
        {
            var rules = new IpoRules();
            var book = new CandidateBook { LookedBehind = true };
            book.StartSiteDay(500, rules);
            var area = new BuildingKey(40, 4);
            book.Profits.Record(area, 896_000, rules, 500);
            book.Profits.Record(area, -150_000, rules, 501);
            string saved = CompaniesCodec.Format(book, new IpoDice(1UL), new CompanyRegistry());
            Assert.Contains("sitecredit=528", saved);
            Assert.Contains("profit|40|4|896000,-150000", saved);

            CandidateBook back = CompaniesCodec.Parse(
                saved,
                new IpoDice(1UL),
                new CompanyRegistry()
            );
            Assert.Equal(528, back.SiteCreditUntilDay);
            Assert.Equal(373_000L, back.Profits.Average(area));
            Assert.Equal(
                saved,
                CompaniesCodec.Format(back, new IpoDice(1UL), new CompanyRegistry())
            );

            book.StartSiteDay(528, rules);
            Assert.Contains(
                "lookbehind-sites=done",
                CompaniesCodec.Format(book, new IpoDice(1UL), new CompanyRegistry())
            );

            // A save from before areas and factories were read: they get the credit.
            CandidateBook old = CompaniesCodec.Parse(
                "v1\nlookbehind=done\n",
                new IpoDice(1UL),
                new CompanyRegistry()
            );
            Assert.False(old.LookedBehindSites);
            Assert.Equal(-1, old.SiteCreditUntilDay);
        }

        [Fact]
        public void OneBadWeekDoesNotSinkAStrongArea()
        {
            var rules = new IpoRules();
            var profits = new SiteProfits();
            var area = new BuildingKey(40, 4);
            long average = 0;
            // Three weeks at ₡8,960, then a week at a loss of ₡3,273 (the test city's oil area).
            for (int day = 0; day < 21; day++)
            {
                average = profits.Record(area, 896_000, rules, day);
            }

            for (int day = 21; day < 28; day++)
            {
                average = profits.Record(area, -327_300, rules, day);
            }

            // A second reading the same day replaces the first.
            Assert.Equal(average, profits.Record(area, -327_300, rules, 27));

            Assert.Equal(28, profits.Count(area));
            Assert.True(average > 0);
            Assert.True(IpoIndustry.AreaHealthy(OilArea(), average, rules));
            Assert.Equal(average * 52, IpoIndustry.AnnualCents(average));

            // A whole month at a loss is not healthy, and the oldest readings drop out.
            for (int day = 28; day < 56; day++)
            {
                average = profits.Record(area, -327_300, rules, day);
            }

            Assert.Equal(28, profits.Count(area));
            Assert.Equal(-327_300L, average);
            Assert.False(IpoIndustry.AreaHealthy(OilArea(), average, rules));
            Assert.Equal(0L, IpoIndustry.AnnualCents(average));

            var one = new IpoRules { ProfitWeeks = 1 };
            profits.Record(area, 100, one, 56);
            Assert.Equal(7, profits.Count(area));

            profits.KeepOnly(new List<BuildingKey>());
            Assert.Equal(0, profits.Count(area));
        }

        [Fact]
        public void TheCardsSayWhatAnAreaOrAFactoryNeeds()
        {
            var rules = new IpoRules();
            var kinds = new List<KindFacts>
            {
                new KindFacts { Kind = BusinessKind.UniqueFactory, ExtraSlots = 3 },
                new KindFacts
                {
                    Kind = BusinessKind.Oil,
                    Squares = 552,
                    TopLevelBuildings = 135,
                    ExtraSlots = 1,
                },
                new KindFacts
                {
                    Kind = BusinessKind.Farming,
                    Squares = 552,
                    TopLevelBuildings = 98,
                },
            };
            BoardView board = IpoCardText.View(kinds, new CompanyRegistry(), 9, 0, rules);
            Assert.Equal(
                "No factory has been healthy for a year yet.",
                IpoCardText.Note(board.Kinds[0], board, "-")
            );
            Assert.False(IpoCardText.HasBar(board.Kinds[0]));
            Assert.Equal(
                "No industry area or large business has been steady for a year yet.",
                IpoCardText.Note(board.Kinds[1], board, "-")
            );
            Assert.True(IpoCardText.HasBar(board.Kinds[1]));
            Assert.Equal(
                "A business needs 50 workers and a steady year to list.",
                IpoCardText.Note(board.Kinds[2], board, "-", rules)
            );
        }

        [Fact]
        public void TheDialogSaysAnAreaOrAFactoryIsListing()
        {
            var area = new LocalCompany
            {
                Name = "Park Oil Group",
                Kind = BusinessKind.Oil,
                Site = BusinessSite.Area,
            };
            var factory = new LocalCompany
            {
                Name = "Bakery",
                Kind = BusinessKind.UniqueFactory,
                Site = BusinessSite.Factory,
            };
            var shop = new LocalCompany { Name = "Shop", Kind = BusinessKind.Commercial };
            Assert.Equal(
                "Oil industry area · in Westdale",
                IpoDialogText.Where(area, null, null, "Westdale")
            );
            Assert.Equal(
                "Unique factory · on Main Street",
                IpoDialogText.Where(factory, "Main Street", null, null)
            );
            Assert.Equal(
                "An industry area your city grew is listing on the Stock Exchange.",
                IpoDialogText.LeadFor(area)
            );
            Assert.Equal(
                "A unique factory your city built is listing on the Stock Exchange.",
                IpoDialogText.LeadFor(factory)
            );
            Assert.Equal(IpoDialogText.Lead, IpoDialogText.LeadFor(shop));
            Assert.DoesNotContain("Historical", IpoDialogText.SmallPrintFor(area));
            Assert.Contains("Historical", IpoDialogText.SmallPrintFor(shop));
        }

        [Fact]
        public void ForgettingSitesKeepsZonedBusinessesAndOpensTheCreditAgain()
        {
            var rules = new IpoRules();
            var book = new CandidateBook { LookedBehind = true };
            var area = new BuildingKey(40, 4);
            var shop = new BuildingKey(42, 6);
            book.StartSiteDay(10, rules);
            book.Observe(area, BusinessKind.Oil, true, rules, BusinessSite.Area, "area:7");
            book.Observe(shop, BusinessKind.Office, true, rules);
            book.Profits.Record(area, 5, rules, 10);
            book.EndDay();
            book.StartSiteDay(40, rules);
            Assert.True(book.LookedBehindSites);

            book.ForgetSites();
            Assert.Equal(0, book.HealthyDaysOf(area));
            Assert.Equal(0, book.Profits.Count(area));
            Assert.Equal(1, book.HealthyDaysOf(shop));
            Assert.False(book.LookedBehindSites);
            Assert.Equal(-1, book.SiteCreditUntilDay);
        }
    }
}

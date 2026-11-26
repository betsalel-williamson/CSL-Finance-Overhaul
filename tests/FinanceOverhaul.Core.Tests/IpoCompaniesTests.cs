using System.Collections.Generic; // KeyValuePair, List
// FinanceOverhaul.Core: BuildingKey, BusinessKind, BusinessKinds, BusinessSite, CandidateBook,
//   CompaniesCodec, CompanyRegistry, CompanyStatus, IpoDice, IpoRules, LocalCompany,
//   LocalCompanyNames
using FinanceOverhaul.Core;
using Xunit; // Assert, FactAttribute, InlineDataAttribute, TheoryAttribute

namespace FinanceOverhaul.Core.Tests
{
    public class IpoCompaniesTests
    {
        [Theory]
        [InlineData("Commercial", "CommercialLow", BusinessKind.Commercial)]
        [InlineData("Commercial", "CommercialHigh", BusinessKind.Commercial)]
        [InlineData("Commercial", "CommercialWallToWall", BusinessKind.Commercial)]
        [InlineData("Commercial", "CommercialLeisure", BusinessKind.Leisure)]
        [InlineData("Commercial", "CommercialTourist", BusinessKind.Tourism)]
        [InlineData("Commercial", "CommercialEco", BusinessKind.Organic)]
        [InlineData("Office", "OfficeGeneric", BusinessKind.Office)]
        [InlineData("Office", "OfficeFinancial", BusinessKind.Office)]
        [InlineData("Office", "OfficeWallToWall", BusinessKind.Office)]
        [InlineData("Office", "OfficeHightech", BusinessKind.ItCluster)]
        [InlineData("Industrial", "IndustrialGeneric", BusinessKind.Industry)]
        [InlineData("Industrial", "IndustrialFarming", BusinessKind.Farming)]
        [InlineData("Industrial", "IndustrialForestry", BusinessKind.Forestry)]
        [InlineData("Industrial", "IndustrialOil", BusinessKind.Oil)]
        [InlineData("Industrial", "IndustrialOre", BusinessKind.Ore)]
        public void TheGamesZonesMapToKinds(string service, string subService, BusinessKind kind)
        {
            BusinessKind got;
            Assert.True(BusinessKinds.TryFromGame(service, subService, out got));
            Assert.Equal(kind, got);
        }

        [Theory]
        [InlineData("Residential", "ResidentialHigh")]
        [InlineData("PlayerIndustry", "PlayerIndustryOil")]
        [InlineData("Monument", "None")]
        [InlineData(null, null)]
        public void EverythingElseIsNotABusiness(string service, string subService)
        {
            BusinessKind got;
            Assert.False(BusinessKinds.TryFromGame(service, subService, out got));
        }

        [Fact]
        public void TheZonesTopOutAtLevelThreeASpecialisationHasOneLevel()
        {
            Assert.False(BusinessKinds.IsTopLevel(BusinessKind.Office, 1));
            Assert.True(BusinessKinds.IsTopLevel(BusinessKind.Office, 2));
            Assert.False(BusinessKinds.IsTopLevel(BusinessKind.Commercial, 0));
            Assert.True(BusinessKinds.IsTopLevel(BusinessKind.Industry, 2));
            Assert.True(BusinessKinds.IsTopLevel(BusinessKind.Oil, 0));
            Assert.True(BusinessKinds.IsTopLevel(BusinessKind.ItCluster, 0));
            Assert.True(BusinessKinds.IsTopLevel(BusinessKind.Leisure, 0));
        }

        [Fact]
        public void AZonedFarmForestOilOrOrePlotNeedsManyWorkersToList()
        {
            var rules = new IpoRules();
            var sheds = new List<int> { 1, 1, 2, 3, 5, 8 };
            // A shed with a handful of workers is not a company, even when it is the largest.
            Assert.Equal(50, BusinessKinds.WorkersFloor(BusinessKind.Farming, sheds, rules));
            Assert.Equal(50, BusinessKinds.WorkersFloor(BusinessKind.Oil, sheds, rules));
            // Large operations: the larger employers among them, as for any kind.
            var large = new List<int> { 40, 60, 120 };
            Assert.Equal(120, BusinessKinds.WorkersFloor(BusinessKind.Forestry, large, rules));
            // Shops, offices and generic industry: the larger employers, but never a handful
            // of workers (45 at least), so a corner shop is never queued.
            Assert.Equal(45, BusinessKinds.WorkersFloor(BusinessKind.Commercial, sheds, rules));
            Assert.Equal(45, BusinessKinds.WorkersFloor(BusinessKind.Industry, sheds, rules));
            Assert.Equal(
                45,
                BusinessKinds.WorkersFloor(
                    BusinessKind.Commercial,
                    new List<int> { 1, 1, 1 },
                    rules
                )
            );
            Assert.Equal(
                164,
                BusinessKinds.WorkersFloor(
                    BusinessKind.Office,
                    new List<int> { 6, 130, 164 },
                    rules
                )
            );
            Assert.False(BusinessKinds.IsSpecialisedIndustry(BusinessKind.Office));
        }

        [Fact]
        public void TheWorkersFloorIsTheTopThirdOfTheKindInAnyCity()
        {
            // Nine oil buildings, all small: the three largest (and any tied with them) count.
            var oil = new List<int> { 3, 4, 4, 5, 6, 7, 8, 9, 9 };
            Assert.Equal(8, BusinessKinds.WorkersFloor(oil, 33));
            // The same rule in a city of big offices.
            var offices = new List<int> { 40, 80, 120, 200, 260, 300 };
            Assert.Equal(260, BusinessKinds.WorkersFloor(offices, 33));
            // Ties with the last one in count too; one business is its own top; none gives 0.
            Assert.Equal(5, BusinessKinds.WorkersFloor(new List<int> { 5, 5, 5, 5 }, 33));
            Assert.Equal(12, BusinessKinds.WorkersFloor(new List<int> { 12 }, 33));
            Assert.Equal(0, BusinessKinds.WorkersFloor(new List<int>(), 33));
            Assert.Equal(3, BusinessKinds.WorkersFloor(oil, 100)); // everyone
            // A larger share never raises the floor.
            int last = int.MaxValue;
            for (int percent = 1; percent <= 100; percent++)
            {
                int floor = BusinessKinds.WorkersFloor(oil, percent);
                Assert.True(floor <= last);
                last = floor;
            }
        }

        [Fact]
        public void LandValueIsAskedOfShopsAndOfficesOnly()
        {
            Assert.True(BusinessKinds.LandValueMatters(BusinessKind.Commercial));
            Assert.True(BusinessKinds.LandValueMatters(BusinessKind.Office));
            Assert.True(BusinessKinds.LandValueMatters(BusinessKind.ItCluster));
            Assert.True(BusinessKinds.LandValueMatters(BusinessKind.Tourism));
            Assert.False(BusinessKinds.LandValueMatters(BusinessKind.Industry));
            Assert.False(BusinessKinds.LandValueMatters(BusinessKind.Oil));
            Assert.False(BusinessKinds.LandValueMatters(BusinessKind.Farming));
        }

        [Fact]
        public void HealthyDaysAndTheDiceSurviveTheSave()
        {
            var book = new CandidateBook();
            book.Restore(new BuildingKey(700, 12345), 400, BusinessKind.Office);
            book.Restore(new BuildingKey(12, 99), 3, BusinessKind.Oil);
            var dice = new IpoDice(987654321012345678UL);
            dice.Next();
            var registry = new CompanyRegistry();
            string saved = CompaniesCodec.Format(book, dice, registry);

            var loadedDice = new IpoDice(1UL);
            var loaded = new CompanyRegistry();
            CandidateBook back = CompaniesCodec.Parse(saved, loadedDice, loaded);
            Assert.Equal(dice.State, loadedDice.State);
            Assert.Equal(400, back.HealthyDaysOf(new BuildingKey(700, 12345)));
            Assert.Equal(BusinessKind.Office, back.KindOf(new BuildingKey(700, 12345)));
            Assert.Equal(3, back.HealthyDaysOf(new BuildingKey(12, 99)));
            Assert.Equal(BusinessKind.Oil, back.KindOf(new BuildingKey(12, 99)));
            Assert.Equal(saved, CompaniesCodec.Format(back, loadedDice, loaded)); // stable, in id order
            Assert.Equal(dice.Next(), loadedDice.Next()); // and the dice carry on the same
        }

        [Fact]
        public void CompaniesAndWhatIsScheduledSurviveTheSave()
        {
            var registry = new CompanyRegistry { NextId = 8, NextDrawDay = 4321 };
            registry.Companies.Add(
                new LocalCompany
                {
                    Id = 3,
                    Name = "Westdale Software | Tech Park",
                    Symbol = "WSO",
                    Kind = BusinessKind.ItCluster,
                    Home = new BuildingKey(700, 12345),
                    AnnouncedDay = 4000,
                    FirstTradingDay = 4007,
                    OfferPriceCents = 5_200,
                    PriceCents = 6_100,
                    Status = CompanyStatus.Trading,
                    HeldShares = 250,
                    HeldSpent = 1_300_000,
                    History = new[] { 61f, 59.5f, 52f },
                    WasHistorical = true,
                    HomeLostDay = 4310,
                }
            );
            registry.Companies.Add(
                new LocalCompany
                {
                    Id = 7,
                    Name = "Green Street Mall",
                    Symbol = "GSM",
                    Kind = BusinessKind.Commercial,
                    Home = new BuildingKey(41, 9),
                    AnnouncedDay = 4300,
                    FirstTradingDay = 4307,
                    OfferPriceCents = 900,
                    PriceCents = 900,
                    Status = CompanyStatus.Offering,
                }
            );
            registry.IpoDays.Add(4000);
            registry.IpoDays.Add(4300);
            registry.BuyoutScheduled = true;
            registry.BuyoutCompanyId = 3;
            registry.BuyoutDay = 4330;
            registry.IpoScheduled = true;
            registry.IpoBusiness = new BuildingKey(15, 77);
            registry.IpoKind = BusinessKind.Oil;
            registry.IpoDay = 4340;

            var dice = new IpoDice(5UL);
            string saved = CompaniesCodec.Format(new CandidateBook(), dice, registry);
            var back = new CompanyRegistry();
            CompaniesCodec.Parse(saved, new IpoDice(1UL), back);

            Assert.Equal(8, back.NextId);
            Assert.Equal(4321, back.NextDrawDay);
            Assert.Equal(new List<int> { 4000, 4300 }, back.IpoDays);
            Assert.Equal(2, back.Companies.Count);
            LocalCompany wso = back.Find(3);
            Assert.Equal(4310, wso.HomeLostDay); // in its grace period
            Assert.Equal(-1, back.Find(7).HomeLostDay); // has its headquarters
            Assert.Equal("Westdale Software | Tech Park", wso.Name); // a name may hold the separator
            Assert.Equal("WSO", wso.Symbol);
            // Whether the building was Historical before the listing is kept per company.
            Assert.True(wso.WasHistorical);
            Assert.False(back.Find(7).WasHistorical);
            Assert.Equal(BusinessKind.ItCluster, wso.Kind);
            Assert.Equal(new BuildingKey(700, 12345), wso.Home);
            Assert.Equal(4007, wso.FirstTradingDay);
            Assert.Equal(5_200, wso.OfferPriceCents);
            Assert.Equal(6_100, wso.PriceCents);
            Assert.Equal(CompanyStatus.Trading, wso.Status);
            Assert.Equal(250u, wso.HeldShares); // the city's holding comes back with the company
            Assert.Equal(1_300_000, wso.HeldSpent);
            Assert.Equal(new[] { 61f, 59.5f, 52f }, wso.History);
            Assert.Equal(0u, back.Find(7).HeldShares);
            Assert.Empty(back.Find(7).History);
            Assert.Equal(CompanyStatus.Offering, back.Find(7).Status);
            Assert.True(back.BuyoutScheduled);
            Assert.Equal(3, back.BuyoutCompanyId);
            Assert.Equal(4330, back.BuyoutDay);
            Assert.True(back.IpoScheduled);
            Assert.Equal(new BuildingKey(15, 77), back.IpoBusiness);
            Assert.Equal(BusinessKind.Oil, back.IpoKind);
            Assert.Equal(4340, back.IpoDay);
            Assert.Equal(saved, CompaniesCodec.Format(new CandidateBook(), dice, back));
        }

        [Fact]
        public void AreasAndFactoriesSurviveTheSave()
        {
            var rules = new IpoRules();
            var book = new CandidateBook();
            book.Restore(
                new BuildingKey(40, 4),
                300,
                BusinessKind.Oil,
                BusinessSite.Area,
                "park|7"
            );
            book.Restore(
                new BuildingKey(41, 5),
                200,
                BusinessKind.UniqueFactory,
                BusinessSite.Factory,
                "Bakery"
            );
            book.Restore(new BuildingKey(42, 6), 100, BusinessKind.Oil);
            var registry = new CompanyRegistry { NextId = 3 };
            registry.Companies.Add(
                new LocalCompany
                {
                    Id = 2,
                    Name = "Park Oil Group",
                    Symbol = "POG",
                    Kind = BusinessKind.Oil,
                    Home = new BuildingKey(40, 4),
                    Site = BusinessSite.Area,
                    SiteRef = "park 7",
                    Status = CompanyStatus.Trading,
                }
            );
            var dice = new IpoDice(9UL);
            string saved = CompaniesCodec.Format(book, dice, registry);

            var loaded = new CompanyRegistry();
            CandidateBook back = CompaniesCodec.Parse(saved, new IpoDice(1UL), loaded);
            Assert.Equal(BusinessSite.Area, back.SiteOf(new BuildingKey(40, 4)));
            Assert.Equal("park7", back.SiteRefOf(new BuildingKey(40, 4))); // no bars in a field
            Assert.Equal(BusinessSite.Factory, back.SiteOf(new BuildingKey(41, 5)));
            Assert.Equal(BusinessKind.UniqueFactory, back.KindOf(new BuildingKey(41, 5)));
            Assert.Equal(BusinessSite.Zoned, back.SiteOf(new BuildingKey(42, 6)));
            LocalCompany c = Assert.Single(loaded.Companies);
            Assert.Equal(BusinessSite.Area, c.Site);
            Assert.Equal("park 7", c.SiteRef);
            Assert.Equal(saved, CompaniesCodec.Format(back, new IpoDice(9UL), loaded));
        }

        [Fact]
        public void ACompanysStockTypeIsNamedByItsId()
        {
            Assert.Equal("FinanceOverhaul.Company.7", LocalCompanyNames.PrefabName(7));
            int id;
            Assert.True(LocalCompanyNames.TryId("FinanceOverhaul.Company.7", out id));
            Assert.Equal(7, id);
            Assert.False(LocalCompanyNames.TryId("Oil Industry Investment", out id));
            Assert.False(LocalCompanyNames.TryId("FinanceOverhaul.Company.x", out id));
            Assert.False(LocalCompanyNames.TryId("FinanceOverhaul.Company.0", out id));
            Assert.False(LocalCompanyNames.TryId(null, out id));
        }

        [Fact]
        public void ASaveFromBeforeCompaniesStillReads()
        {
            // The first build wrote only the dice and the healthy days.
            var registry = new CompanyRegistry();
            var dice = new IpoDice(1UL);
            CandidateBook book = CompaniesCodec.Parse(
                "v1\ndice=42\nhealthy|9|100|12|Office\n",
                dice,
                registry
            );
            Assert.Equal(42UL, dice.State);
            Assert.Equal(12, book.HealthyDaysOf(new BuildingKey(9, 100)));
            Assert.Empty(registry.Companies);
            Assert.Equal(1, registry.NextId);
            Assert.False(registry.IpoScheduled);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("v9\nhealthy|1|2|3|Office\n")] // a newer format
        [InlineData("v1\nhealthy|1|2|0|Office\n")] // no days
        [InlineData("v1\nhealthy|1|2|3|Casino\n")] // a kind this build does not know
        [InlineData("v1\nhealthy|x|2|3|Office\nrubbish\nhealthy|1|2\n")]
        public void UnknownOrDamagedSavesAreSkippedNotFatal(string text)
        {
            var dice = new IpoDice(5UL);
            var registry = new CompanyRegistry();
            CandidateBook book = CompaniesCodec.Parse(text, dice, registry);
            Assert.Empty(registry.Companies);
            var counted = new List<KeyValuePair<BuildingKey, int>>(book.Counted);
            Assert.Empty(counted);
            Assert.Equal(5UL, dice.State);
        }
    }
}

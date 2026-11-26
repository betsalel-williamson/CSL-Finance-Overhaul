using FinanceOverhaul.Core; // BusinessKind, BusinessSite, CompanyPanelText, IpoRules, LocalCompany
using Xunit; // Assert, FactAttribute

namespace FinanceOverhaul.Core.Tests
{
    public class CompanyPanelTextTests
    {
        private static LocalCompany Goldwoman()
        {
            return new LocalCompany
            {
                Id = 12,
                Name = "Goldwoman Investments",
                Symbol = "GI",
                Kind = BusinessKind.Office,
                AnnouncedDay = 100,
                FirstTradingDay = 107,
                OfferPriceCents = 43200,
                PriceCents = 43200,
            };
        }

        [Fact]
        public void InTheOfferingWindowThePanelSaysWhatTheCityCanBuy()
        {
            LocalCompany c = Goldwoman();
            Assert.True(CompanyPanelText.Offering(c, 102));
            Assert.Equal("OFFERING", CompanyPanelText.Badge(c, 102));
            Assert.Equal(
                "Office · First trading day 21/08/2116",
                CompanyPanelText.Line(c, 102, "21/08/2116", "14/08/2116")
            );
            Assert.Equal("₡432", CompanyPanelText.Price(c));
            Assert.Equal("Offering price · 5 days left", CompanyPanelText.OfferingNote(c, 102));
            Assert.Equal("Offering price · 1 day left", CompanyPanelText.OfferingNote(c, 106));
            Assert.Equal(
                "The city can buy up to 1,000 shares (a 10% stake, ₡432,000)",
                CompanyPanelText.Stake(c, new IpoRules(), 102, 0, 0)
            );
            Assert.Null(CompanyPanelText.Gain(0, 0, 0));
            Assert.Equal("Buy at the offering price", CompanyPanelText.Buy(c, 102));
        }

        [Fact]
        public void AnIndustryAreaAndAUniqueFactorySayWhatTheyAre()
        {
            LocalCompany area = Goldwoman();
            area.Kind = BusinessKind.Oil;
            area.Site = BusinessSite.Area;
            Assert.Equal(
                "Oil industry area · First trading day 21/08/2116",
                CompanyPanelText.Line(area, 102, "21/08/2116", "14/08/2116")
            );
            LocalCompany factory = Goldwoman();
            factory.Kind = BusinessKind.UniqueFactory;
            factory.Site = BusinessSite.Factory;
            Assert.Equal(
                "Unique factory · Listed 14/08/2116",
                CompanyPanelText.Line(factory, 107, "21/08/2116", "14/08/2116")
            );
            Assert.Equal(
                "First trading day 21/08/2116",
                CompanyPanelText.ShortLine(area, 102, "21/08/2116", "14/08/2116")
            );
            Assert.Equal(
                "Listed 14/08/2116",
                CompanyPanelText.ShortLine(factory, 107, "21/08/2116", "14/08/2116")
            );
        }

        [Fact]
        public void OnceItTradesThePanelShowsTheListingAndTheStake()
        {
            LocalCompany c = Goldwoman();
            c.PriceCents = 43600;
            Assert.False(CompanyPanelText.Offering(c, 107));
            Assert.Equal("TRADING", CompanyPanelText.Badge(c, 107));
            Assert.Equal(
                "Office · Listed 14/08/2116",
                CompanyPanelText.Line(c, 107, "21/08/2116", "14/08/2116")
            );
            Assert.Null(CompanyPanelText.OfferingNote(c, 107));
            Assert.Equal(
                "City holds 250 of 1,000 shares · avg ₡432.00",
                CompanyPanelText.Stake(c, new IpoRules(), 107, 250, 108000)
            );
            Assert.Equal("+₡1,000", CompanyPanelText.Gain(250, 108000, 109000));
            Assert.Equal(1, CompanyPanelText.GainSign(108000, 109000));
            Assert.Equal("−₡500", CompanyPanelText.Gain(250, 108000, 107500));
            Assert.Equal(
                "City holds 0 of 1,000 shares",
                CompanyPanelText.Stake(c, new IpoRules(), 107, 0, 0)
            );
            Assert.Equal("Buy", CompanyPanelText.Buy(c, 107));
        }

        [Fact]
        public void TheStakeBarFillsTowardsTheCap()
        {
            Assert.Equal(0f, CompanyPanelText.StakeFraction(0));
            Assert.Equal(0.25f, CompanyPanelText.StakeFraction(250));
            Assert.Equal(1f, CompanyPanelText.StakeFraction(5000));
        }
    }
}

// FinanceOverhaul.Core: BusinessKind, IpoDialogText, IpoRules, LocalCompany, ModSettings, Notice,
//   NoticeKind, NoticeText, SettingsFile
using FinanceOverhaul.Core;
using Xunit; // Assert, FactAttribute, InlineDataAttribute, TheoryAttribute

namespace FinanceOverhaul.Core.Tests
{
    public class IpoTextTests
    {
        private static LocalCompany Wso()
        {
            return new LocalCompany
            {
                Id = 3,
                Name = "Westdale Software",
                Symbol = "WSO",
                Kind = BusinessKind.ItCluster,
                AnnouncedDay = 4000,
                FirstTradingDay = 4007,
                OfferPriceCents = 5_200,
                PriceCents = 5_200,
            };
        }

        [Fact]
        public void TheAnnouncementChirp()
        {
            Notice n = NoticeText.IpoAnnounced("Westdale Software", 5_200, 7);
            Assert.Equal(NoticeKind.IpoAnnounced, n.Kind);
            Assert.Equal("City Brokerage", n.Sender);
            Assert.Equal(
                "Westdale Software is going public at ₡52 a share. The city can buy at the offering price for 7 days. #ipo",
                n.Text
            );
            Assert.False(n.IsOrder); // never folded into the daily order summary
        }

        [Theory]
        [InlineData(
            5_200L,
            6_000L,
            "Westdale Software opened at ₡60 on its first trading day, 15% above the offering price. #ipo"
        )]
        [InlineData(
            5_200L,
            4_200L,
            "Westdale Software opened at ₡42 on its first trading day, 19% below the offering price. #ipo"
        )]
        [InlineData(
            5_200L,
            5_200L,
            "Westdale Software opened at ₡52 on its first trading day, level with the offering price. #ipo"
        )]
        public void TheFirstDayChirpSaysUpDownOrLevel(long offer, long first, string text)
        {
            Assert.Equal(text, NoticeText.IpoFirstDay("Westdale Software", offer, first).Text);
        }

        [Fact]
        public void TheBuyOutChirpSaysWhatTheCityReceived()
        {
            Assert.Equal(
                "Westdale Software was bought out at ₡73 a share, 40% above its price. The city received ₡73,000 for its 1,000 shares. #ipo",
                NoticeText.IpoBoughtOut("Westdale Software", 7_300, 0.4, 1000, 7_300_000).Text
            );
            Assert.Equal(
                "Westdale Software was bought out at ₡73 a share, 40% above its price. The city received ₡73 for its 1 share. #ipo",
                NoticeText.IpoBoughtOut("Westdale Software", 7_300, 0.4, 1, 7_300).Text
            );
            Assert.Equal(
                "Westdale Software was bought out at ₡73 a share, 40% above its price. The city held no shares. #ipo",
                NoticeText.IpoBoughtOut("Westdale Software", 7_300, 0.4, 0, 0).Text
            );
        }

        [Fact]
        public void TheDialogStatesOnlyWhatIsFixedThatDay()
        {
            LocalCompany c = Wso();
            var rules = new IpoRules();
            Assert.Equal("Westdale Software is going public", IpoDialogText.Title(c));
            Assert.Equal(
                "IT cluster · on Green Avenue at Pearl Street, Tech Park",
                IpoDialogText.Where(c, "Green Avenue", "Pearl Street", "Tech Park")
            );
            Assert.Equal(
                "IT cluster · on Green Avenue at Pearl Street",
                IpoDialogText.Where(c, "Green Avenue", "Pearl Street", null)
            );
            Assert.Equal(
                "IT cluster · on Green Avenue, Tech Park",
                IpoDialogText.Where(c, "Green Avenue", null, "Tech Park")
            );
            // A "cross street" with the street's own name is no cross street.
            Assert.Equal(
                "IT cluster · on Green Avenue",
                IpoDialogText.Where(c, "Green Avenue", "Green Avenue", "")
            );
            Assert.Equal(
                "IT cluster · in Tech Park",
                IpoDialogText.Where(c, null, "x", "Tech Park")
            );
            Assert.Equal("IT cluster", IpoDialogText.Where(c, null, null, null));
            // The company's name appears once in the dialog: in the title only.
            Assert.DoesNotContain(
                c.Name,
                IpoDialogText.Where(c, "Green Avenue", "Pearl Street", "Tech Park")
            );
            Assert.Equal("₡52 a share", IpoDialogText.Price(c));
            Assert.Equal("7 days · until 16/06/2111", IpoDialogText.Window(c, "16/06/2111"));
            Assert.Equal(
                "up to 1,000 shares (a 10% stake, ₡52,000)",
                IpoDialogText.CanBuy(c, rules)
            );
            Assert.Equal(
                "17/06/2111 · the price then moves freely",
                IpoDialogText.FirstDay("17/06/2111")
            );
            Assert.Equal("Buy at the offering price", IpoDialogText.Buy);
            Assert.Equal("Don't show this for future IPOs", IpoDialogText.DontShow);
        }

        [Fact]
        public void EveryKindHasAName()
        {
            foreach (BusinessKind kind in System.Enum.GetValues(typeof(BusinessKind)))
            {
                string name = IpoDialogText.KindName(kind);
                Assert.False(string.IsNullOrEmpty(name));
                Assert.DoesNotContain("company.kind", name);
            }
        }

        [Fact]
        public void TheDialogPreferenceIsOnByDefaultAndSurvivesTheSettingsFile()
        {
            var settings = new ModSettings();
            Assert.True(settings.IpoDialogEnabled);
            settings.IpoDialogEnabled = false;
            settings.LocalIposEnabled = true;
            ModSettings back = SettingsFile.Parse(SettingsFile.Format(settings));
            Assert.False(back.IpoDialogEnabled);
            Assert.True(back.LocalIposEnabled);
        }
    }
}

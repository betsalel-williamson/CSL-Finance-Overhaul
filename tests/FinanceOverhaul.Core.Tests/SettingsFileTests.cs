using FinanceOverhaul.Core;
using Xunit;

namespace FinanceOverhaul.Core.Tests
{
    public class SettingsFileTests
    {
        [Fact]
        public void RoundTrip_KeepsToggles()
        {
            var original = new ModSettings { IndexStockEnabled = true };
            ModSettings parsed = SettingsFile.Parse(SettingsFile.Format(original));
            Assert.True(parsed.IndexStockEnabled);
        }

        [Fact]
        public void RoundTrip_LiveTickerOff()
        {
            var original = new ModSettings { LiveTickerEnabled = false };
            Assert.False(SettingsFile.Parse(SettingsFile.Format(original)).LiveTickerEnabled);
            Assert.True(SettingsFile.Parse("").LiveTickerEnabled);
        }

        [Fact]
        public void Parse_EmptyOrNullGivesSaveSafeDefaults()
        {
            foreach (string text in new[] { null, "", "   \n" })
            {
                ModSettings s = SettingsFile.Parse(text);
                Assert.False(s.IndexStockEnabled);
            }
        }

        [Fact]
        public void Parse_IgnoresUnknownKeysCommentsAndGarbage()
        {
            ModSettings s = SettingsFile.Parse(
                "# Finance Overhaul\nfutureThing=1\nnot a pair\nindexStockEnabled = TRUE\r\n"
            );
            Assert.True(s.IndexStockEnabled);
        }

        [Fact]
        public void Parse_LegacyProbeBiasKeyIsIgnored()
        {
            // Settings written by versions that had the MVP probe bias option.
            ModSettings s = SettingsFile.Parse(
                "probeBiasEnabled=true\nindexStockEnabled=true\nliveTickerEnabled=false\n"
            );
            Assert.True(s.IndexStockEnabled);
            Assert.False(s.LiveTickerEnabled);
            Assert.DoesNotContain("probeBias", SettingsFile.Format(s));
        }

        [Fact]
        public void DevTestStock_IsOffByDefaultAndRoundTrips()
        {
            Assert.False(SettingsFile.Parse("").DevTestStockEnabled);
            var on = new ModSettings { DevTestStockEnabled = true };
            Assert.True(SettingsFile.Parse(SettingsFile.Format(on)).DevTestStockEnabled);
        }

        [Fact]
        public void Parse_InvalidBooleanKeepsDefault()
        {
            ModSettings s = SettingsFile.Parse("indexStockEnabled=maybe");
            Assert.False(s.IndexStockEnabled);
        }
    }
}

namespace FinanceOverhaul.Core.Tests
{
    public class OrderFlagSettingsTests
    {
        [Fact]
        public void EveryOrderTypeIsOnByDefault()
        {
            ModSettings s = SettingsFile.Parse("");
            foreach (OrderKind kind in ModSettings.OrderKinds)
            {
                Assert.True(s.IsEnabled(kind));
            }
        }

        [Fact]
        public void EachFlagRoundTripsOnItsOwn()
        {
            foreach (OrderKind off in ModSettings.OrderKinds)
            {
                var s = new ModSettings();
                s.SetEnabled(off, false);
                ModSettings back = SettingsFile.Parse(SettingsFile.Format(s));
                foreach (OrderKind kind in ModSettings.OrderKinds)
                {
                    Assert.Equal(kind != off, back.IsEnabled(kind));
                }
            }
        }

        [Fact]
        public void KeysAreReadable()
        {
            Assert.Equal("ordersLimitBuyEnabled", SettingsFile.OrderKey(OrderKind.LimitBuy));
            Assert.Equal("ordersStopLossEnabled", SettingsFile.OrderKey(OrderKind.StopLossSell));
            Assert.Equal(
                "ordersTakeProfitEnabled",
                SettingsFile.OrderKey(OrderKind.TakeProfitSell)
            );
            Assert.Equal(
                "ordersTrailingStopEnabled",
                SettingsFile.OrderKey(OrderKind.TrailingStopSell)
            );
            Assert.False(SettingsFile.Parse("ordersTakeProfitEnabled=false\n").TakeProfitEnabled);
        }

        [Fact]
        public void AnUnknownKindIsNeverEnabled()
        {
            Assert.False(new ModSettings().IsEnabled((OrderKind)99));
        }

        [Fact]
        public void PausedCardsSaySo()
        {
            Assert.Equal(
                "Open order · SELL (stop-loss) · paused in Options",
                OrderText.CardSide(OrderKind.StopLossSell, true)
            );
            Assert.Equal("Open order · BUY", OrderText.CardSide(OrderKind.LimitBuy, false));
            Assert.Equal(
                "Orders: Trailing stop sells",
                OrderText.OptionLabel(OrderKind.TrailingStopSell)
            );
        }
    }
}

namespace FinanceOverhaul.Core.Tests
{
    public class CityBondsFlagTests
    {
        [Fact]
        public void OffByDefaultAndRoundTrips()
        {
            Assert.False(SettingsFile.Parse("").CityBondsEnabled);
            var on = new ModSettings { CityBondsEnabled = true };
            Assert.True(SettingsFile.Parse(SettingsFile.Format(on)).CityBondsEnabled);
            Assert.True(SettingsFile.Parse("cityBondsEnabled=true\n").CityBondsEnabled);
        }
    }

    public class ShareChartsFlagTests
    {
        [Fact]
        public void OnByDefaultAndRoundTrips()
        {
            Assert.True(SettingsFile.Parse("").ShareChartsEnabled);
            var off = new ModSettings { ShareChartsEnabled = false };
            Assert.False(SettingsFile.Parse(SettingsFile.Format(off)).ShareChartsEnabled);
            Assert.False(SettingsFile.Parse("shareChartsEnabled=false\n").ShareChartsEnabled);
        }
    }
}

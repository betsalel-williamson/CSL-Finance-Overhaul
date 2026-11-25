using System.Collections.Generic; // HashSet
using System.Reflection; // BindingFlags, FieldInfo
// FinanceOverhaul.Core: BuildingKey, BusinessKind, CompanyRegistry, IBusinessSource, IpoBuyout,
//   IpoDice, IpoEngine, IpoPricing, IpoRules, IpoTuning, LocalCompany, ModSettings, SettingsFile,
//   Strings, TuningAudience, TuningGroup
using FinanceOverhaul.Core;
using Xunit; // Assert, FactAttribute, InlineDataAttribute, TheoryAttribute

namespace FinanceOverhaul.Core.Tests
{
    public class IpoTuningTests
    {
        [Fact]
        public void EveryIpoRuleValueIsAnOption()
        {
            // A new value in IpoRules must be tunable: in the player's Options or the dev group.
            var tuned = new HashSet<string>();
            foreach (IpoTuning t in IpoTuning.All)
            {
                Assert.True(tuned.Add(t.Key), "two options with the key " + t.Key);
            }

            foreach (
                FieldInfo f in typeof(IpoRules).GetFields(
                    BindingFlags.Public | BindingFlags.Instance
                )
            )
            {
                string key =
                    IpoTuning.KeyPrefix + char.ToLowerInvariant(f.Name[0]) + f.Name.Substring(1);
                Assert.True(tuned.Contains(key), f.Name + " has no option (" + key + ")");
            }

            Assert.Equal(
                typeof(IpoRules).GetFields(BindingFlags.Public | BindingFlags.Instance).Length,
                IpoTuning.All.Count
            );
        }

        [Fact]
        public void EachOptionHasWordingAndItsDefaultIsInRange()
        {
            var rules = new IpoRules();
            foreach (IpoTuning t in IpoTuning.All)
            {
                string label = t.Label(rules);
                Assert.DoesNotContain("options.ipo.", label);
                Assert.Contains(t.Shown(t.Default).ToString("#,0"), label);
                Assert.InRange(t.Default, t.Min, t.Max);
                Assert.Equal(t.Default, t.Stored(t.Shown(t.Default))); // the default is a step
            }

            foreach (
                TuningGroup g in new[]
                {
                    TuningGroup.Listing,
                    TuningGroup.Candidates,
                    TuningGroup.Prices,
                    TuningGroup.BuyoutsAndClosing,
                    TuningGroup.Market,
                }
            )
            {
                Assert.DoesNotContain("options.ipo.group", Strings.Get("options.ipo.group." + g));
            }
        }

        [Fact]
        public void TheFinerWorkingsAreDevOnlyAndThePlayerGetsTheRest()
        {
            foreach (IpoTuning t in IpoTuning.All)
            {
                Assert.Equal(t.Group == TuningGroup.Market, t.Audience == TuningAudience.Dev);
            }

            Assert.Equal(TuningAudience.Player, IpoTuning.Find("ipo.exchangeLevel").Audience);
            Assert.Equal(TuningAudience.Player, IpoTuning.Find("ipo.areaMinLevel").Audience);
        }

        [Fact]
        public void TheSecondSlotOptionShowsTwiceTheDoublingBase()
        {
            IpoTuning slot = IpoTuning.Find("ipo.slotDoublingBase");
            Assert.Equal(20, slot.Shown(10));
            Assert.Equal(15, slot.Stored(30));
            Assert.Equal(
                "Top-level buildings for a kind's second listing slot: 20",
                slot.Label(new IpoRules())
            );
        }

        [Fact]
        public void APickedValueSnapsToItsStepAndStaysInRange()
        {
            IpoTuning gate = IpoTuning.Find("ipo.gateSquares"); // 50 to 2,000 in 50s
            Assert.Equal(200, gate.Stored(210));
            Assert.Equal(250, gate.Stored(230));
            Assert.Equal(50, gate.Stored(-5));
            Assert.Equal(2000, gate.Stored(99999));
        }

        [Fact]
        public void OnlyChangedValuesAreSavedAndTheyReadBack()
        {
            var settings = new ModSettings();
            Assert.DoesNotContain("ipo.", SettingsFile.Format(settings)); // defaults: nothing written
            settings.Ipo.ExchangeLevel = 4;
            settings.Ipo.AreaMinLevel = 2;
            settings.Ipo.BadDayCost = 9;
            string text = SettingsFile.Format(settings);
            Assert.Contains("ipo.exchangeLevel=4", text);
            Assert.DoesNotContain("ipo.gateSquares", text);

            ModSettings back = SettingsFile.Parse(text);
            Assert.Equal(4, back.Ipo.ExchangeLevel);
            Assert.Equal(2, back.Ipo.AreaMinLevel);
            Assert.Equal(9, back.Ipo.BadDayCost);
            Assert.Equal(200, back.Ipo.GateSquares);
        }

        [Fact]
        public void AHandEditedValueIsKeptInRangeAndNonsenseIsIgnored()
        {
            ModSettings s = SettingsFile.Parse(
                "ipo.exchangeLevel=9\nipo.healthyDays=abc\nipo.unknown=3\n"
            );
            Assert.Equal(5, s.Ipo.ExchangeLevel);
            Assert.Equal(365, s.Ipo.HealthyDays);
        }

        [Fact]
        public void ResetPutsOnlyThatAudienceBack()
        {
            var rules = new IpoRules { ExchangeLevel = 5, BadDayCost = 30 };
            IpoTuning.Reset(rules, TuningAudience.Player);
            Assert.Equal(2, rules.ExchangeLevel);
            Assert.Equal(30, rules.BadDayCost);
            IpoTuning.Reset(rules, TuningAudience.Dev);
            Assert.Equal(4, rules.BadDayCost);
        }

        [Fact]
        public void TheOfferingWindowAndTheBoundsFollowTheRules()
        {
            var rules = new IpoRules
            {
                OfferDays = 12,
                PopMinPercent = 0,
                PopMaxPercent = 20,
                BuyoutPremiumMinPercent = 50,
                BuyoutPremiumMaxPercent = 60,
            };
            var registry = new CompanyRegistry();
            LocalCompany c = IpoEngine
                .List(registry, new BuildingKey(1, 1), BusinessKind.Office, 100, rules, new Names())
                .Company;
            Assert.Equal(112, c.FirstTradingDay);

            var dice = new IpoDice(4UL);
            for (int i = 0; i < 5000; i++)
            {
                Assert.InRange(IpoPricing.FirstDayPop(rules, dice), 0d, 0.2d);
                Assert.InRange(IpoBuyout.Premium(rules, dice), 0.5d, 0.6d);
            }
        }

        [Fact]
        public void EachOptionSaysItsMinAndMaxAndShowsItsValueInTheField()
        {
            IpoTuning slot = IpoTuning.Find("ipo.slotDoublingBase");
            Assert.Equal("Min 4", slot.MinLabel()); // shown as the player sees it: base × 2
            Assert.Equal("Max 200", slot.MaxLabel());
            Assert.Equal("20", slot.FieldText(new IpoRules()));
            IpoTuning fill = IpoTuning.Find("ipo.slotFillDays");
            Assert.Equal("Max 3,650", fill.MaxLabel());
            Assert.Equal("548", fill.FieldText(new IpoRules()));
            Assert.Equal("Min -50", IpoTuning.Find("ipo.popAveragePercent").MinLabel());
        }

        [Theory]
        [InlineData("548", true, 548)]
        [InlineData(" 2,000 ", true, 2000)]
        [InlineData("-50", true, -50)]
        [InlineData("15%", true, 15)]
        [InlineData("", false, 0)]
        [InlineData("abc", false, 0)]
        [InlineData("5-0", false, 0)]
        [InlineData("1.5", false, 0)]
        public void ATypedNumberIsReadWithSeparatorsAndASign(string text, bool ok, int value)
        {
            int shown;
            Assert.Equal(ok, IpoTuning.TryParseTyped(text, out shown));
            if (ok)
            {
                Assert.Equal(value, shown);
            }
        }

        private sealed class Names : IBusinessSource
        {
            public string NameOf(BuildingKey building)
            {
                return "B";
            }

            public string SymbolFor(string name)
            {
                return "B";
            }

            public long AnnualEarningsCents(BuildingKey building)
            {
                return 100_000;
            }

            public bool HomeStands(BuildingKey building)
            {
                return true;
            }

            public bool TrySuccessor(LocalCompany company, out BuildingKey home)
            {
                home = default(BuildingKey);
                return false;
            }
        }
    }
}

using System.Collections.Generic; // HashSet, List
using System.Reflection; // BindingFlags, FieldInfo
// FinanceOverhaul.Core: BuildingKey, BusinessKind, CompanyRegistry, IBusinessSource, IpoBuyout,
//   IpoDice, IpoEngine, IpoPresets, IpoPricing, IpoRules, IpoTuning, IpoUserPreset, LocalCompany,
//   ModSettings, SettingsFile, Strings, TuningAudience, TuningGroup, TuningPreset
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

        [Fact]
        public void EveryPresetValueIsInRangeOnItsStepAndLeansTheRightWay()
        {
            foreach (IpoTuning t in IpoTuning.All)
            {
                foreach (
                    TuningPreset p in new[] { TuningPreset.LessLikely, TuningPreset.MoreLikely }
                )
                {
                    int v = t.PresetValue(p);
                    Assert.InRange(v, t.Min, t.Max);
                    Assert.Equal(v, t.Stored(t.Shown(v)));
                }

                if (t.Audience == TuningAudience.Dev)
                {
                    Assert.Null(t.Less); // the finer workings have only their defaults
                    Assert.Null(t.More);
                }
            }

            Assert.True(IpoTuning.HasPresets(TuningGroup.Listing));
            Assert.False(IpoTuning.HasPresets(TuningGroup.Market));
        }

        [Fact]
        public void MoreLikelyListsMoreAndSoonerThanLessLikely()
        {
            var less = new IpoRules();
            var more = new IpoRules();
            IpoTuning.Apply(less, TuningGroup.Listing, TuningPreset.LessLikely);
            IpoTuning.Apply(more, TuningGroup.Listing, TuningPreset.MoreLikely);
            var def = new IpoRules();
            Assert.True(
                more.MaxIposPerYear > def.MaxIposPerYear && def.MaxIposPerYear > less.MaxIposPerYear
            );
            Assert.True(
                more.SlotFillDays < def.SlotFillDays && def.SlotFillDays < less.SlotFillDays
            );
            Assert.True(more.ExchangeLevel < less.ExchangeLevel);

            IpoTuning.Apply(less, TuningGroup.Candidates, TuningPreset.LessLikely);
            IpoTuning.Apply(more, TuningGroup.Candidates, TuningPreset.MoreLikely);
            Assert.True(more.HealthyDays < less.HealthyDays);
            Assert.True(more.AreaMinLevel < less.AreaMinLevel);

            IpoTuning.Apply(more, TuningGroup.BuyoutsAndClosing, TuningPreset.MoreLikely);
            Assert.True(more.BuyoutPercentPerYear > def.BuyoutPercentPerYear);
        }

        [Fact]
        public void APresetChangesOnlyItsSectionAndDefaultsPutItBack()
        {
            var rules = new IpoRules { BadDayCost = 9, HealthyDays = 100 };
            int changed = IpoTuning.Apply(rules, TuningGroup.Listing, TuningPreset.MoreLikely);
            Assert.Equal(7, changed);
            Assert.True(IpoTuning.Matches(rules, TuningGroup.Listing, TuningPreset.MoreLikely));
            Assert.Equal(100, rules.HealthyDays); // another section
            Assert.Equal(9, rules.BadDayCost); // the dev section

            IpoTuning.Apply(rules, TuningGroup.Listing, TuningPreset.Default);
            Assert.True(IpoTuning.Matches(rules, TuningGroup.Listing, TuningPreset.Default));
            Assert.Equal(2, rules.ExchangeLevel);

            IpoTuning.Apply(rules, TuningGroup.Market, TuningPreset.MoreLikely); // only defaults there
            Assert.Equal(4, rules.BadDayCost);
        }

        [Fact]
        public void ThePresetInUseIsWorkedOutFromTheValues()
        {
            var rules = new IpoRules();
            var saved = new List<IpoUserPreset>();
            Assert.Equal("Default", IpoPresets.Identify(rules, saved));
            Assert.Equal(
                new List<string> { "Less likely", "Default", "More likely" },
                IpoPresets.Names(rules, saved)
            );

            IpoPresets.Apply(rules, TuningPreset.MoreLikely);
            Assert.Equal("More likely", IpoPresets.Identify(rules, saved));

            rules.HealthyDays = 200; // any edit: a New Preset
            Assert.Equal("New Preset", IpoPresets.Identify(rules, saved));
            Assert.Equal("New Preset", IpoPresets.Names(rules, saved)[3]);

            Assert.True(IpoPresets.Save(saved, rules, "  Busy | city  "));
            Assert.Equal("Busy  city", saved[0].Name);
            Assert.Equal("Busy  city", IpoPresets.Identify(rules, saved));
            Assert.Equal(4, IpoPresets.Names(rules, saved).Count); // no New Preset now

            Assert.True(IpoPresets.ApplyNamed(rules, saved, "Default"));
            Assert.Equal(365, rules.HealthyDays);
            Assert.True(IpoPresets.ApplyNamed(rules, saved, "Busy  city"));
            Assert.Equal(200, rules.HealthyDays);
            Assert.False(IpoPresets.ApplyNamed(rules, saved, "New Preset"));
        }

        [Fact]
        public void BuiltInNamesAreNeverOverwrittenAndSavingReplacesByName()
        {
            var rules = new IpoRules();
            var saved = new List<IpoUserPreset>();
            Assert.False(IpoPresets.Save(saved, rules, "more likely"));
            Assert.False(IpoPresets.Save(saved, rules, "New Preset"));
            Assert.False(IpoPresets.Save(saved, rules, "   "));
            Assert.True(IpoPresets.Save(saved, rules, "Mine"));
            rules.MaxIposPerYear = 6;
            Assert.True(IpoPresets.Save(saved, rules, "mine"));
            IpoUserPreset only = Assert.Single(saved);
            Assert.Equal(6, only.Values["ipo.maxIposPerYear"]);
            Assert.Equal("My preset", IpoPresets.SuggestName(new List<IpoUserPreset>()));
            Assert.Equal(
                "My preset 2",
                IpoPresets.SuggestName(
                    new List<IpoUserPreset> { new IpoUserPreset { Name = "My preset" } }
                )
            );
            Assert.True(IpoPresets.Delete(saved, "MINE"));
            Assert.Empty(saved);
        }

        [Fact]
        public void SavedPresetsSurviveTheSettingsFileAndLeaveTheFinerWorkingsAlone()
        {
            var settings = new ModSettings();
            settings.Ipo.MaxIposPerYear = 5;
            settings.Ipo.BadDayCost = 9;
            Assert.True(IpoPresets.Save(settings.IpoUserPresets, settings.Ipo, "Five a year"));
            Assert.False(
                IpoPresets.Snapshot(settings.Ipo, "x").Values.ContainsKey("ipo.badDayCost")
            );
            string text = SettingsFile.Format(settings);
            Assert.Contains("ipoPreset=Five a year|", text);

            ModSettings back = SettingsFile.Parse(text);
            IpoUserPreset five = Assert.Single(back.IpoUserPresets);
            Assert.Equal(5, five.Values["ipo.maxIposPerYear"]);
            back.Ipo = new IpoRules { BadDayCost = 30 };
            Assert.True(IpoPresets.ApplyNamed(back.Ipo, back.IpoUserPresets, "Five a year"));
            Assert.Equal(5, back.Ipo.MaxIposPerYear);
            Assert.Equal(30, back.Ipo.BadDayCost);

            Assert.Null(IpoPresets.Parse("Default|ipo.maxIposPerYear=9")); // a reserved name
            Assert.Empty(SettingsFile.Parse("ipoPreset=|x").IpoUserPresets);
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

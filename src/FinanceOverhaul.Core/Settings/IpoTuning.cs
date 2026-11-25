using System; // Action, ArgumentException, Func, Math, StringComparison
using System.Collections.Generic; // IEnumerable, List
using System.Globalization; // CultureInfo, NumberStyles

namespace FinanceOverhaul.Core
{
    /// <summary>Who sees a Local IPOs value in Options.</summary>
    public enum TuningAudience
    {
        /// <summary>Every player, in the Local IPOs groups (while Local IPOs is on).</summary>
        Player,

        /// <summary>Dev builds only, in their own group: the finer workings of the market.</summary>
        Dev,
    }

    /// <summary>A quick setting for a whole section of Local IPOs options.</summary>
    public enum TuningPreset
    {
        /// <summary>Rarer: fewer IPOs, stricter candidates, calmer first days, fewer buy-outs.</summary>
        LessLikely,

        /// <summary>Every value of the section back to its default.</summary>
        Default,

        /// <summary>Livelier: more IPOs, easier candidates, bigger first days, more buy-outs.</summary>
        MoreLikely,
    }

    /// <summary>Which Local IPOs group a value sits in, in Options.</summary>
    public enum TuningGroup
    {
        Listing,
        Candidates,
        Prices,
        BuyoutsAndClosing,
        Market,
    }

    /// <summary>
    /// One Local IPOs value as an option: its settings key, its wording, its range and step,
    /// and where it lives in <see cref="IpoRules"/>. Shown as a slider; the label is
    /// <c>Strings.Format(LabelId, value)</c>. <see cref="Factor"/> scales what the player sees
    /// (the second slot comes at twice the doubling base, so that option shows the base × 2).
    /// </summary>
    public sealed class IpoTuning
    {
        public string Key;
        public string LabelId;
        public TuningAudience Audience;
        public TuningGroup Group;
        public int Min;
        public int Max;
        public int Step = 1;
        public int Factor = 1;
        public Func<IpoRules, int> Get;
        public Action<IpoRules, int> Set;

        /// <summary>Its value (stored) in the section's "less likely" preset; null: the default.</summary>
        public int? Less;

        /// <summary>Its value (stored) in the section's "more likely" preset; null: the default.</summary>
        public int? More;

        /// <summary>Its value (stored) in a preset.</summary>
        public int PresetValue(TuningPreset preset)
        {
            int? v =
                preset == TuningPreset.LessLikely ? Less
                : preset == TuningPreset.MoreLikely ? More
                : null;
            return v ?? Default;
        }

        /// <summary>
        /// Sets every value of a section to a preset (the dev section has only its defaults).
        /// Returns how many values changed.
        /// </summary>
        public static int Apply(IpoRules rules, TuningGroup group, TuningPreset preset)
        {
            int changed = 0;
            foreach (IpoTuning t in All)
            {
                if (t.Group != group)
                {
                    continue;
                }

                int v = t.PresetValue(preset);
                if (t.Get(rules) != v)
                {
                    t.Set(rules, v);
                    changed++;
                }
            }

            return changed;
        }

        /// <summary>The section's values are exactly a preset's (to show which one is in use).</summary>
        public static bool Matches(IpoRules rules, TuningGroup group, TuningPreset preset)
        {
            foreach (IpoTuning t in All)
            {
                if (t.Group == group && t.Get(rules) != t.PresetValue(preset))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>A section with less and more likely presets (the dev section has only defaults).</summary>
        public static bool HasPresets(TuningGroup group)
        {
            return group != TuningGroup.Market;
        }

        /// <summary>The default, as stored (from a fresh <see cref="IpoRules"/>).</summary>
        public int Default
        {
            get { return Get(new IpoRules()); }
        }

        /// <summary>A stored value as the player sees it.</summary>
        public int Shown(int stored)
        {
            return stored * Factor;
        }

        /// <summary>
        /// A value the player picked, as stored: snapped to the step and kept in range. A value
        /// outside the range (a hand-edited file) is brought inside it.
        /// </summary>
        public int Stored(int shown)
        {
            int value = Factor <= 1 ? shown : (int)Math.Round((double)shown / Factor);
            if (Step > 1)
            {
                value = Min + (int)Math.Round((double)(value - Min) / Step) * Step;
            }

            return Math.Max(Min, Math.Min(Max, value));
        }

        /// <summary>The option's line: its sentence with the value in it.</summary>
        public string Label(IpoRules rules)
        {
            return Strings.Format(
                LabelId,
                Shown(Get(rules)).ToString("#,0", CultureInfo.InvariantCulture)
            );
        }

        /// <summary>"Min 1": the lowest value, as the player sees it, under the slider's left end.</summary>
        public string MinLabel()
        {
            return Strings.Format("options.ipo.min", Number(Shown(Min)));
        }

        /// <summary>"Max 5": the highest value, under the slider's right end.</summary>
        public string MaxLabel()
        {
            return Strings.Format("options.ipo.max", Number(Shown(Max)));
        }

        /// <summary>The value as the number field shows it ("548", "-50", "2,000").</summary>
        public string FieldText(IpoRules rules)
        {
            return Number(Shown(Get(rules)));
        }

        /// <summary>
        /// A number typed into the field, as the player sees values (separators, spaces and a
        /// trailing % are ignored; a leading minus is kept). False for anything else; the
        /// caller then shows the value again.
        /// </summary>
        public static bool TryParseTyped(string text, out int shown)
        {
            shown = 0;
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            var digits = new System.Text.StringBuilder();
            string t = text.Trim();
            for (int i = 0; i < t.Length; i++)
            {
                char c = t[i];
                if (c >= '0' && c <= '9')
                {
                    digits.Append(c);
                }
                else if ((c == '-' || c == '−') && digits.Length == 0 && i == 0)
                {
                    digits.Append('-');
                }
                else if (c != ',' && c != ' ' && c != '%' && c != '\u00a0')
                {
                    return false;
                }
            }

            return int.TryParse(
                digits.ToString(),
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out shown
            );
        }

        private static string Number(int value)
        {
            return value.ToString("#,0", CultureInfo.InvariantCulture);
        }

        /// <summary>Settings keys start with this; the rest is the value's name.</summary>
        public const string KeyPrefix = "ipo.";

        /// <summary>Every Local IPOs value, in the order Options shows them.</summary>
        public static readonly List<IpoTuning> All = Build();

        /// <summary>Reads one settings line into the rules; false if the key is not a Local IPOs value.</summary>
        public static bool TryRead(IpoRules rules, string key, string value)
        {
            IpoTuning t = Find(key);
            int number;
            if (t == null)
            {
                return false;
            }

            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
            {
                t.Set(rules, Math.Max(t.Min, Math.Min(t.Max, number)));
            }

            return true; // ours, even if unreadable: the default stays
        }

        /// <summary>The settings lines for every value that differs from its default.</summary>
        public static IEnumerable<string> Lines(IpoRules rules)
        {
            foreach (IpoTuning t in All)
            {
                int v = t.Get(rules);
                if (v != t.Default)
                {
                    yield return t.Key + "=" + v.ToString(CultureInfo.InvariantCulture);
                }
            }
        }

        /// <summary>Every value back to its default.</summary>
        public static void Reset(IpoRules rules, TuningAudience audience)
        {
            var fresh = new IpoRules();
            foreach (IpoTuning t in All)
            {
                if (t.Audience == audience)
                {
                    t.Set(rules, t.Get(fresh));
                }
            }
        }

        public static IpoTuning Find(string key)
        {
            foreach (IpoTuning t in All)
            {
                if (string.Equals(t.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    return t;
                }
            }

            return null;
        }

        private static List<IpoTuning> Build()
        {
            var list = new List<IpoTuning>();
            const TuningAudience P = TuningAudience.Player;
            const TuningAudience D = TuningAudience.Dev;

            // When companies list.
            Add(
                list,
                "exchangeLevel",
                P,
                TuningGroup.Listing,
                1,
                5,
                1,
                r => r.ExchangeLevel,
                (r, v) => r.ExchangeLevel = v
            );
            Add(
                list,
                "gateSquares",
                P,
                TuningGroup.Listing,
                50,
                2000,
                50,
                r => r.GateSquares,
                (r, v) => r.GateSquares = v
            );
            Add(
                list,
                "slotDoublingBase",
                P,
                TuningGroup.Listing,
                2,
                100,
                1,
                r => r.SlotDoublingBase,
                (r, v) => r.SlotDoublingBase = v
            ).Factor = 2;
            Add(
                list,
                "listingsPerTile",
                P,
                TuningGroup.Listing,
                1,
                10,
                1,
                r => r.ListingsPerTile,
                (r, v) => r.ListingsPerTile = v
            );
            Add(
                list,
                "listingCap",
                P,
                TuningGroup.Listing,
                1,
                100,
                1,
                r => r.ListingCap,
                (r, v) => r.ListingCap = v
            );
            Add(
                list,
                "maxIposPerYear",
                P,
                TuningGroup.Listing,
                1,
                24,
                1,
                r => r.MaxIposPerYear,
                (r, v) => r.MaxIposPerYear = v
            );
            Add(
                list,
                "slotFillDays",
                P,
                TuningGroup.Listing,
                30,
                3650,
                1,
                r => r.SlotFillDays,
                (r, v) => r.SlotFillDays = v
            );
            Add(
                list,
                "drawEveryDays",
                P,
                TuningGroup.Listing,
                1,
                365,
                1,
                r => r.DrawEveryDays,
                (r, v) => r.DrawEveryDays = v
            );

            // Which businesses can.
            Add(
                list,
                "healthyDays",
                P,
                TuningGroup.Candidates,
                1,
                1825,
                1,
                r => r.HealthyDays,
                (r, v) => r.HealthyDays = v
            );
            Add(
                list,
                "topEmployersPercent",
                P,
                TuningGroup.Candidates,
                1,
                100,
                1,
                r => r.TopEmployersPercent,
                (r, v) => r.TopEmployersPercent = v
            );
            Add(
                list,
                "minJobs",
                P,
                TuningGroup.Candidates,
                1,
                500,
                1,
                r => r.MinJobs,
                (r, v) => r.MinJobs = v
            );
            Add(
                list,
                "specialisedMinJobs",
                P,
                TuningGroup.Candidates,
                1,
                500,
                1,
                r => r.SpecialisedMinJobs,
                (r, v) => r.SpecialisedMinJobs = v
            );
            Add(
                list,
                "maxVacancyPercent",
                P,
                TuningGroup.Candidates,
                0,
                100,
                1,
                r => r.MaxVacancyPercent,
                (r, v) => r.MaxVacancyPercent = v
            );
            Add(
                list,
                "landValueFloor",
                P,
                TuningGroup.Candidates,
                0,
                100,
                1,
                r => r.LandValueFloor,
                (r, v) => r.LandValueFloor = v
            );
            Add(
                list,
                "areaMinLevel",
                P,
                TuningGroup.Candidates,
                1,
                5,
                1,
                r => r.AreaMinLevel,
                (r, v) => r.AreaMinLevel = v
            );
            Add(
                list,
                "factoryMinProductionPercent",
                P,
                TuningGroup.Candidates,
                1,
                150,
                1,
                r => r.FactoryMinProductionPercent,
                (r, v) => r.FactoryMinProductionPercent = v
            );
            Add(
                list,
                "profitWeeks",
                P,
                TuningGroup.Candidates,
                1,
                12,
                1,
                r => r.ProfitWeeks,
                (r, v) => r.ProfitWeeks = v
            );

            // Prices.
            Add(
                list,
                "offerDays",
                P,
                TuningGroup.Prices,
                1,
                60,
                1,
                r => r.OfferDays,
                (r, v) => r.OfferDays = v
            );
            Add(
                list,
                "priceToEarnings",
                P,
                TuningGroup.Prices,
                1,
                100,
                1,
                r => r.PriceToEarnings,
                (r, v) => r.PriceToEarnings = v
            );
            Add(
                list,
                "cityStakePercent",
                P,
                TuningGroup.Prices,
                1,
                100,
                1,
                r => r.CityStakePercent,
                (r, v) => r.CityStakePercent = v
            );
            Add(
                list,
                "popAveragePercent",
                P,
                TuningGroup.Prices,
                -50,
                200,
                1,
                r => r.PopAveragePercent,
                (r, v) => r.PopAveragePercent = v
            );
            Add(
                list,
                "popSpreadPercent",
                P,
                TuningGroup.Prices,
                0,
                200,
                1,
                r => r.PopSpreadPercent,
                (r, v) => r.PopSpreadPercent = v
            );

            // Buy-outs and closing.
            Add(
                list,
                "buyoutPercentPerYear",
                P,
                TuningGroup.BuyoutsAndClosing,
                0,
                100,
                1,
                r => r.BuyoutPercentPerYear,
                (r, v) => r.BuyoutPercentPerYear = v
            );
            Add(
                list,
                "buyoutMinDaysListed",
                P,
                TuningGroup.BuyoutsAndClosing,
                0,
                3650,
                1,
                r => r.BuyoutMinDaysListed,
                (r, v) => r.BuyoutMinDaysListed = v
            );
            Add(
                list,
                "buyoutPremiumAveragePercent",
                P,
                TuningGroup.BuyoutsAndClosing,
                0,
                300,
                1,
                r => r.BuyoutPremiumAveragePercent,
                (r, v) => r.BuyoutPremiumAveragePercent = v
            );
            Add(
                list,
                "buyoutPremiumSpreadPercent",
                P,
                TuningGroup.BuyoutsAndClosing,
                0,
                200,
                1,
                r => r.BuyoutPremiumSpreadPercent,
                (r, v) => r.BuyoutPremiumSpreadPercent = v
            );
            Add(
                list,
                "homeGraceDays",
                P,
                TuningGroup.BuyoutsAndClosing,
                1,
                730,
                1,
                r => r.HomeGraceDays,
                (r, v) => r.HomeGraceDays = v
            );

            // The finer workings (dev builds).
            Add(
                list,
                "steadyPercent",
                D,
                TuningGroup.Market,
                1,
                100,
                1,
                r => r.SteadyPercent,
                (r, v) => r.SteadyPercent = v
            );
            Add(
                list,
                "badDayCost",
                D,
                TuningGroup.Market,
                1,
                365,
                1,
                r => r.BadDayCost,
                (r, v) => r.BadDayCost = v
            );
            Add(
                list,
                "dailySwingPercent",
                D,
                TuningGroup.Market,
                0,
                50,
                1,
                r => r.DailySwingPercent,
                (r, v) => r.DailySwingPercent = v
            );
            Add(
                list,
                "fairPullPercent",
                D,
                TuningGroup.Market,
                0,
                100,
                1,
                r => r.FairPullPercent,
                (r, v) => r.FairPullPercent = v
            );
            Add(
                list,
                "maxDailyMovePercent",
                D,
                TuningGroup.Market,
                1,
                100,
                1,
                r => r.MaxDailyMovePercent,
                (r, v) => r.MaxDailyMovePercent = v
            );
            Add(
                list,
                "homelessFallPercent",
                D,
                TuningGroup.Market,
                0,
                50,
                1,
                r => r.HomelessFallPercent,
                (r, v) => r.HomelessFallPercent = v
            );
            Add(
                list,
                "popMinPercent",
                D,
                TuningGroup.Market,
                -99,
                0,
                1,
                r => r.PopMinPercent,
                (r, v) => r.PopMinPercent = v
            );
            Add(
                list,
                "popMaxPercent",
                D,
                TuningGroup.Market,
                0,
                2000,
                10,
                r => r.PopMaxPercent,
                (r, v) => r.PopMaxPercent = v
            );
            Add(
                list,
                "buyoutPremiumMinPercent",
                D,
                TuningGroup.Market,
                0,
                300,
                1,
                r => r.BuyoutPremiumMinPercent,
                (r, v) => r.BuyoutPremiumMinPercent = v
            );
            Add(
                list,
                "buyoutPremiumMaxPercent",
                D,
                TuningGroup.Market,
                0,
                1000,
                10,
                r => r.BuyoutPremiumMaxPercent,
                (r, v) => r.BuyoutPremiumMaxPercent = v
            );
            // Quick presets for each player section (stored values; others keep their default).
            // When companies list: rarer or more often.
            Preset(list, "exchangeLevel", 3, 1);
            Preset(list, "gateSquares", 400, 100);
            Preset(list, "slotDoublingBase", 20, 5);
            Preset(list, "listingsPerTile", 1, 3);
            Preset(list, "listingCap", 8, 20);
            Preset(list, "maxIposPerYear", 1, 4);
            Preset(list, "slotFillDays", 1095, 274);
            // Which businesses can: stricter or easier.
            Preset(list, "healthyDays", 730, 180);
            Preset(list, "topEmployersPercent", 20, 50);
            Preset(list, "minJobs", 20, 5);
            Preset(list, "specialisedMinJobs", 50, 20);
            Preset(list, "maxVacancyPercent", 5, 20);
            Preset(list, "landValueFloor", 50, 30);
            Preset(list, "areaMinLevel", 4, 2);
            Preset(list, "factoryMinProductionPercent", 110, 90);
            // Prices: a calmer or a bigger first day, a shorter or a longer offering window.
            Preset(list, "offerDays", 3, 14);
            Preset(list, "popAveragePercent", 8, 25);
            Preset(list, "popSpreadPercent", 12, 40);
            // Buy-outs and closing: rarer or more often, smaller or larger premiums; a company
            // without a headquarters gets a shorter or a longer grace period.
            Preset(list, "buyoutPercentPerYear", 2, 8);
            Preset(list, "buyoutMinDaysListed", 730, 180);
            Preset(list, "buyoutPremiumAveragePercent", 25, 60);
            Preset(list, "buyoutPremiumSpreadPercent", 10, 20);
            Preset(list, "homeGraceDays", 45, 180);
            return list;
        }

        private static void Preset(List<IpoTuning> list, string name, int less, int more)
        {
            foreach (IpoTuning t in list)
            {
                if (t.Key == KeyPrefix + name)
                {
                    t.Less = less;
                    t.More = more;
                    return;
                }
            }

            throw new ArgumentException("no Local IPOs option " + name); // not shown: a coding error
        }

        private static IpoTuning Add(
            List<IpoTuning> list,
            string name,
            TuningAudience audience,
            TuningGroup group,
            int min,
            int max,
            int step,
            Func<IpoRules, int> get,
            Action<IpoRules, int> set
        )
        {
            var t = new IpoTuning
            {
                Key = KeyPrefix + name,
                LabelId = "options.ipo." + name,
                Audience = audience,
                Group = group,
                Min = min,
                Max = max,
                Step = step,
                Get = get,
                Set = set,
            };
            list.Add(t);
            return t;
        }
    }
}

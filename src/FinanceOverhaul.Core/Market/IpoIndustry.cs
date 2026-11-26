using System; // Math, MidpointRounding
using System.Collections.Generic; // Dictionary, ICollection, IEnumerable, IList, KeyValuePair, List

namespace FinanceOverhaul.Core
{
    /// <summary>
    /// What the mod reads from the game about one industry area on one day (Industries DLC).
    /// Money is a week's, in cents, as the area's own panel works it out.
    /// </summary>
    public struct AreaFacts
    {
        /// <summary>Farming, forestry, oil or ore.</summary>
        public BusinessKind Kind;

        /// <summary>The area's level, the game's 1 to 5.</summary>
        public int Level;

        /// <summary>The main building stands and is switched on.</summary>
        public bool MainBuildingActive;

        /// <summary>What the area sold outside the city this week.</summary>
        public long WeeklySalesCents;

        /// <summary>What it bought in this week.</summary>
        public long WeeklyBoughtCents;

        /// <summary>The upkeep of its buildings this week.</summary>
        public long WeeklyUpkeepCents;

        /// <summary>The people working in its buildings today (not its unique factories).</summary>
        public int Workers;
    }

    /// <summary>What the mod reads from the game about one unique factory on one day.</summary>
    public struct FactoryFacts
    {
        /// <summary>The factory stands and is switched on.</summary>
        public bool Active;

        /// <summary>Any problem icon on the building.</summary>
        public bool HasProblems;

        /// <summary>Its production slider, in percent of its budget (100 = the default).</summary>
        public int ProductionPercent;

        /// <summary>What it sold this week.</summary>
        public long WeeklySalesCents;

        /// <summary>What its inputs cost this week.</summary>
        public long WeeklyBoughtCents;

        /// <summary>Its upkeep this week.</summary>
        public long WeeklyUpkeepCents;

        /// <summary>The people working in it today.</summary>
        public int Workers;
    }

    /// <summary>
    /// Industry areas and unique factories as companies (docs/features/local-ipos.md, "Industry
    /// areas and unique factories"). An area is a company of its own kind (farming, forestry,
    /// oil, ore), headquartered in its main building; a unique factory is a company of the kind
    /// <see cref="BusinessKind.UniqueFactory"/>. Both count healthy days in the
    /// <see cref="CandidateBook"/> like any business, and both earn what their panel shows,
    /// over a year.
    /// </summary>
    public static class IpoIndustry
    {
        /// <summary>
        /// Healthy today: at <see cref="IpoRules.AreaMinLevel"/> or higher, its main building
        /// switched on, at least <see cref="IpoRules.MinWorkers"/> people working in it, and
        /// profitable this week.
        /// </summary>
        public static bool AreaHealthy(AreaFacts facts, IpoRules rules)
        {
            return BusinessKinds.IsSpecialisedIndustry(facts.Kind)
                && facts.Level >= rules.AreaMinLevel
                && facts.MainBuildingActive
                && facts.Workers >= rules.MinWorkers
                && WeeklyProfitCents(
                    facts.WeeklySalesCents,
                    facts.WeeklyBoughtCents,
                    facts.WeeklyUpkeepCents
                ) > 0;
        }

        /// <summary>
        /// Healthy today: switched on, no problem icons, its production slider at
        /// <see cref="IpoRules.FactoryMinProductionPercent"/> or higher, at least
        /// <see cref="IpoRules.MinWorkers"/> people working in it, and profitable this week.
        /// </summary>
        public static bool FactoryHealthy(FactoryFacts facts, IpoRules rules)
        {
            return facts.Active
                && !facts.HasProblems
                && facts.ProductionPercent >= rules.FactoryMinProductionPercent
                && facts.Workers >= rules.MinWorkers
                && WeeklyProfitCents(
                    facts.WeeklySalesCents,
                    facts.WeeklyBoughtCents,
                    facts.WeeklyUpkeepCents
                ) > 0;
        }

        /// <summary>What is left of a week's sales after what was bought in and the upkeep.</summary>
        public static long WeeklyProfitCents(long sales, long bought, long upkeep)
        {
            return Math.Max(0, sales) - Math.Max(0, bought) - Math.Max(0, upkeep);
        }

        /// <summary>A year's earnings: the week's profit × 52; nothing for a week at a loss.</summary>
        public static long AnnualCents(AreaFacts facts)
        {
            return Annual(
                WeeklyProfitCents(
                    facts.WeeklySalesCents,
                    facts.WeeklyBoughtCents,
                    facts.WeeklyUpkeepCents
                )
            );
        }

        /// <summary>A year's earnings: the week's profit × 52; nothing for a week at a loss.</summary>
        public static long AnnualCents(FactoryFacts facts)
        {
            return Annual(
                WeeklyProfitCents(
                    facts.WeeklySalesCents,
                    facts.WeeklyBoughtCents,
                    facts.WeeklyUpkeepCents
                )
            );
        }

        /// <summary>
        /// Listing slots industry areas give their kind: one for each area at
        /// <see cref="IpoRules.AreaMinLevel"/> or higher (whether healthy today or not).
        /// </summary>
        public static int AreaSlots(int[] areaLevels, IpoRules rules)
        {
            int slots = 0;
            foreach (int level in areaLevels ?? new int[0])
            {
                if (level >= rules.AreaMinLevel)
                {
                    slots++;
                }
            }

            return slots;
        }

        /// <summary>
        /// Healthy today, judged on <paramref name="averageWeeklyProfitCents"/> (the last
        /// <see cref="IpoRules.ProfitWeeks"/> weeks, <see cref="SiteProfits"/>) rather than this
        /// week alone: at the level areas need, its main building switched on, and profitable on
        /// average.
        /// </summary>
        public static bool AreaHealthy(
            AreaFacts facts,
            long averageWeeklyProfitCents,
            IpoRules rules
        )
        {
            return BusinessKinds.IsSpecialisedIndustry(facts.Kind)
                && facts.Level >= rules.AreaMinLevel
                && facts.MainBuildingActive
                && facts.Workers >= rules.MinWorkers
                && averageWeeklyProfitCents > 0;
        }

        /// <summary>Healthy today, judged on its average weekly profit (see the area's overload).</summary>
        public static bool FactoryHealthy(
            FactoryFacts facts,
            long averageWeeklyProfitCents,
            IpoRules rules
        )
        {
            return facts.Active
                && !facts.HasProblems
                && facts.ProductionPercent >= rules.FactoryMinProductionPercent
                && facts.Workers >= rules.MinWorkers
                && averageWeeklyProfitCents > 0;
        }

        /// <summary>A year's earnings from an average week's profit: × 52, nothing for a loss.</summary>
        public static long AnnualCents(long averageWeeklyProfitCents)
        {
            return Annual(averageWeeklyProfitCents);
        }

        private static long Annual(long weekly)
        {
            if (weekly <= 0)
            {
                return 0;
            }

            return weekly > long.MaxValue / IpoEarnings.WeeksPerYear
                ? long.MaxValue
                : weekly * IpoEarnings.WeeksPerYear;
        }
    }

    /// <summary>
    /// The weekly profit of each industry area and unique factory, as read each day over the
    /// last <see cref="IpoRules.ProfitWeeks"/> weeks (the game keeps only last week's). Its
    /// average is what the area is judged and priced on. Kept in the save.
    /// </summary>
    public sealed class SiteProfits
    {
        private readonly Dictionary<BuildingKey, List<long>> days =
            new Dictionary<BuildingKey, List<long>>();

        // The game day of each site's newest reading (not saved: at worst one day is counted
        // twice after a reload).
        private readonly Dictionary<BuildingKey, int> lastDay = new Dictionary<BuildingKey, int>();

        /// <summary>
        /// Records a site's weekly profit as read on <paramref name="today"/> (a second reading
        /// the same day replaces the first); returns the average of its readings.
        /// </summary>
        public long Record(BuildingKey site, long weeklyProfitCents, IpoRules rules, int today)
        {
            List<long> list;
            if (!days.TryGetValue(site, out list))
            {
                list = new List<long>();
                days[site] = list;
            }

            int seen;
            if (lastDay.TryGetValue(site, out seen) && seen == today && list.Count > 0)
            {
                list[list.Count - 1] = weeklyProfitCents;
                return Average(site);
            }

            lastDay[site] = today;
            list.Add(weeklyProfitCents);
            int keep = Math.Max(1, rules.ProfitWeeks) * 7;
            if (list.Count > keep)
            {
                list.RemoveRange(0, list.Count - keep);
            }

            return Average(site);
        }

        /// <summary>The average weekly profit over the readings kept (0 with none).</summary>
        public long Average(BuildingKey site)
        {
            List<long> list;
            if (!days.TryGetValue(site, out list) || list.Count == 0)
            {
                return 0;
            }

            decimal sum = 0;
            foreach (long v in list)
            {
                sum += v;
            }

            return (long)Math.Round(sum / list.Count, MidpointRounding.AwayFromZero);
        }

        /// <summary>Readings kept for a site.</summary>
        public int Count(BuildingKey site)
        {
            List<long> list;
            return days.TryGetValue(site, out list) ? list.Count : 0;
        }

        /// <summary>Forgets every site not in <paramref name="standing"/> (demolished areas and factories).</summary>
        public void KeepOnly(ICollection<BuildingKey> standing)
        {
            var gone = new List<BuildingKey>();
            foreach (BuildingKey k in days.Keys)
            {
                if (!standing.Contains(k))
                {
                    gone.Add(k);
                }
            }

            foreach (BuildingKey k in gone)
            {
                days.Remove(k);
                lastDay.Remove(k);
            }
        }

        /// <summary>Every site's readings, oldest first (for the save).</summary>
        public IEnumerable<KeyValuePair<BuildingKey, List<long>>> All
        {
            get { return days; }
        }

        /// <summary>Sets a site's readings read from the save.</summary>
        public void Restore(BuildingKey site, IList<long> readings)
        {
            days[site] = new List<long>(readings);
        }
    }
}

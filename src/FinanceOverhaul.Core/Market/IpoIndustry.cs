using System; // Math

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
        /// switched on, and profitable this week.
        /// </summary>
        public static bool AreaHealthy(AreaFacts facts, IpoRules rules)
        {
            return BusinessKinds.IsSpecialisedIndustry(facts.Kind)
                && facts.Level >= rules.AreaMinLevel
                && facts.MainBuildingActive
                && WeeklyProfitCents(
                    facts.WeeklySalesCents,
                    facts.WeeklyBoughtCents,
                    facts.WeeklyUpkeepCents
                ) > 0;
        }

        /// <summary>
        /// Healthy today: switched on, no problem icons, its production slider at
        /// <see cref="IpoRules.FactoryMinProductionPercent"/> or higher, and profitable this week.
        /// </summary>
        public static bool FactoryHealthy(FactoryFacts facts, IpoRules rules)
        {
            return facts.Active
                && !facts.HasProblems
                && facts.ProductionPercent >= rules.FactoryMinProductionPercent
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
}

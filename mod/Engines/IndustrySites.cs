using System; // Math
using System.Collections.Generic; // Dictionary, List
// FinanceOverhaul.Core: AreaFacts, BuildingKey, BusinessKind, BusinessSite, FactoryFacts,
//   IpoIndustry, IpoRules, LocalCompany, SiteProfits
using FinanceOverhaul.Core;

// Game classes used without a using (global namespace, Assembly-CSharp): Building, BuildingInfo,
//   BuildingManager, DistrictAreaResourceData, DistrictManager, DistrictPark, EconomyManager,
//   FastList, IndustryBuildingAI, InstanceID, ItemClass, MainIndustryBuildingAI, Notification,
//   TransferManager, UniqueFactoryAI

namespace FinanceOverhaul
{
    /// <summary>One industry area or unique factory as the daily scan read it.</summary>
    internal struct SiteReading
    {
        /// <summary>Its headquarters: the area's main building, or the factory.</summary>
        public BuildingKey Home;
        public BusinessKind Kind;
        public BusinessSite Site;

        /// <summary>Which area or factory (see <see cref="IndustrySites"/>): kept with its company.</summary>
        public string Ref;

        /// <summary>The company's name: the area's, or the factory's.</summary>
        public string Name;

        /// <summary>An area's level (1 to 5); 0 for a factory.</summary>
        public int Level;

        public bool Healthy;
        public long AnnualCents;

        /// <summary>Its average weekly profit over the recent weeks it was read, in cents.</summary>
        public long WeeklyProfitCents;
    }

    /// <summary>
    /// Reads the Industries DLC's industry areas and unique factories for Local IPOs
    /// (docs/features/local-ipos.md, "Industry areas and unique factories"). Money is a week's,
    /// as the game's own panels work it out: an area's exports and what it bought in (each
    /// resource at the game's price), a factory's sales and inputs, and the upkeep of the
    /// buildings; the rules are the library's <see cref="IpoIndustry"/>. Simulation thread.
    /// </summary>
    internal static class IndustrySites
    {
        private const string AreaRef = "area:"; // not shown: a save note
        private const string FactoryRef = "factory:"; // not shown: a save note

        // A week's upkeep in ₡ is the game's rate × 0.0016 (LocaleFormatter.FormatUpkeep), so in
        // cents the rate × 16 / 100. The game reports the rate as a negative number (a cost).
        private const long UpkeepCentsPerRate100 = 16;

        /// <summary>Every industry area and unique factory in the city today.</summary>
        /// <remarks>
        /// Each one's week is recorded in <paramref name="profits"/>, and it is judged and priced
        /// on its average over <see cref="IpoRules.ProfitWeeks"/> weeks; sites no longer there
        /// are forgotten.
        /// </remarks>
        public static List<SiteReading> Read(IpoRules rules, SiteProfits profits, int today)
        {
            var list = new List<SiteReading>();
            ReadAreas(rules, profits, today, list);
            ReadFactories(rules, profits, today, list);
            var standing = new List<BuildingKey>();
            foreach (SiteReading s in list)
            {
                standing.Add(s.Home);
            }

            profits.KeepOnly(standing);
            return list;
        }

        /// <summary>
        /// The area's main building or the factory as it stands now, for a company whose own is
        /// lost: the area's main building if it was rebuilt, or a factory of the same type.
        /// </summary>
        public static bool TrySuccessor(LocalCompany company, out BuildingKey home)
        {
            home = default(BuildingKey);
            string note = company == null ? null : company.SiteRef;
            if (string.IsNullOrEmpty(note))
            {
                return false;
            }

            Building[] buffer = BuildingManager.instance.m_buildings.m_buffer;
            if (note.StartsWith(AreaRef))
            {
                int park;
                if (!int.TryParse(note.Substring(AreaRef.Length), out park))
                {
                    return false;
                }

                DistrictManager districts = DistrictManager.instance;
                if (park <= 0 || park >= districts.m_parks.m_size)
                {
                    return false;
                }

                DistrictPark p = districts.m_parks.m_buffer[park];
                ushort gate = MainBuilding((byte)park);
                if ((p.m_flags & DistrictPark.Flags.Created) == 0 || !p.IsIndustry || gate == 0)
                {
                    return false;
                }

                home = new BuildingKey(gate, buffer[gate].m_buildIndex);
                return true;
            }

            if (note.StartsWith(FactoryRef))
            {
                string prefab = note.Substring(FactoryRef.Length);
                for (int i = 1; i < buffer.Length; i++)
                {
                    if (
                        (buffer[i].m_flags & Building.Flags.Created) != 0
                        && buffer[i].Info != null
                        && buffer[i].Info.name == prefab
                        && buffer[i].Info.m_buildingAI is UniqueFactoryAI
                    )
                    {
                        home = new BuildingKey((ushort)i, buffer[i].m_buildIndex);
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// The industry area's main building, or 0. The game's own note of it (the park's main
        /// gate) can be 0 while the main building stands (seen in a level 5 farming area), so
        /// the area's buildings are searched for it then.
        /// </summary>
        public static ushort MainBuilding(byte park)
        {
            DistrictManager districts = DistrictManager.instance;
            Building[] buffer = BuildingManager.instance.m_buildings.m_buffer;
            ushort gate = districts.m_parks.m_buffer[park].m_mainGate;
            if (park == 0 || IsMainBuilding(gate))
            {
                return park == 0 ? (ushort)0 : gate;
            }

            FastList<ushort> service = BuildingManager.instance.GetServiceBuildings(
                ItemClass.Service.PlayerIndustry
            );
            ushort found = 0;
            for (int i = 0; i < service.m_size; i++)
            {
                ushort id = service.m_buffer[i];
                if (
                    IsMainBuilding(id)
                    && districts.GetPark(buffer[id].m_position) == park
                    && (found == 0 || id < found)
                )
                {
                    found = id;
                }
            }

            return found;
        }

        private static bool IsMainBuilding(ushort id)
        {
            if (id == 0)
            {
                return false;
            }

            Building b = BuildingManager.instance.m_buildings.m_buffer[id];
            return (b.m_flags & Building.Flags.Created) != 0
                && b.Info != null
                && b.Info.m_buildingAI is MainIndustryBuildingAI;
        }

        /// <summary>
        /// Game UI tests: the weekly profit every area and factory reports, in cents, until the
        /// scenario ends (null: the game's). Not recorded in the city's history.
        /// </summary>
        internal static long? TestWeeklyProfitCents;

        private static long Average(
            SiteProfits profits,
            BuildingKey home,
            long weeklyProfitCents,
            IpoRules rules,
            int today
        )
        {
            return TestWeeklyProfitCents.HasValue
                ? TestWeeklyProfitCents.Value
                : profits.Record(home, weeklyProfitCents, rules, today);
        }

        /// <summary>The player has the building switched on (the game's On/Off is its production rate).</summary>
        public static bool SwitchedOn(ushort building)
        {
            return BuildingManager.instance.m_buildings.m_buffer[building].m_productionRate != 0;
        }

        private static void ReadAreas(
            IpoRules rules,
            SiteProfits profits,
            int today,
            List<SiteReading> list
        )
        {
            DistrictManager districts = DistrictManager.instance;
            Building[] buffer = BuildingManager.instance.m_buildings.m_buffer;

            // Upkeep of every industry building, by the area it stands in.
            var upkeepRate = new Dictionary<byte, long>();
            FastList<ushort> service = BuildingManager.instance.GetServiceBuildings(
                ItemClass.Service.PlayerIndustry
            );
            for (int i = 0; i < service.m_size; i++)
            {
                ushort id = service.m_buffer[i];
                BuildingInfo info = buffer[id].Info;
                if (info == null || info.m_buildingAI is UniqueFactoryAI)
                {
                    continue; // a unique factory is a company of its own
                }

                byte park = districts.GetPark(buffer[id].m_position);
                long rate;
                upkeepRate.TryGetValue(park, out rate);
                upkeepRate[park] =
                    rate
                    + Math.Abs(
                        info.m_buildingAI.GetResourceRate(
                            id,
                            ref buffer[id],
                            EconomyManager.Resource.Maintenance
                        )
                    );
            }

            for (int p = 1; p < districts.m_parks.m_size; p++)
            {
                DistrictPark park = districts.m_parks.m_buffer[p];
                ushort gate = MainBuilding((byte)p);
                BusinessKind kind;
                if (
                    (park.m_flags & DistrictPark.Flags.Created) == 0
                    || !park.IsIndustry
                    || gate == 0
                    || !TryKind(park.m_parkType, out kind)
                )
                {
                    continue;
                }

                long sales,
                    bought;
                Week(park, out sales, out bought);
                long upkeep;
                upkeepRate.TryGetValue((byte)p, out upkeep);
                var facts = new AreaFacts
                {
                    Kind = kind,
                    Level = (int)park.m_parkLevel,
                    MainBuildingActive = Stands(gate) && SwitchedOn(gate),
                    WeeklySalesCents = sales,
                    WeeklyBoughtCents = bought,
                    WeeklyUpkeepCents = Math.Abs(upkeep) * UpkeepCentsPerRate100 / 100,
                };
                var home = new BuildingKey(gate, buffer[gate].m_buildIndex);
                long average = Average(
                    profits,
                    home,
                    IpoIndustry.WeeklyProfitCents(
                        facts.WeeklySalesCents,
                        facts.WeeklyBoughtCents,
                        facts.WeeklyUpkeepCents
                    ),
                    rules,
                    today
                );
                list.Add(
                    new SiteReading
                    {
                        Home = home,
                        Kind = kind,
                        Site = BusinessSite.Area,
                        Ref = AreaRef + p,
                        Name = districts.GetParkName((byte)p),
                        Level = facts.Level,
                        Healthy = IpoIndustry.AreaHealthy(facts, average, rules),
                        AnnualCents = IpoIndustry.AnnualCents(average),
                        WeeklyProfitCents = average,
                    }
                );
            }
        }

        private static void ReadFactories(
            IpoRules rules,
            SiteProfits profits,
            int today,
            List<SiteReading> list
        )
        {
            Building[] buffer = BuildingManager.instance.m_buildings.m_buffer;
            FastList<ushort> service = BuildingManager.instance.GetServiceBuildings(
                ItemClass.Service.PlayerIndustry
            );
            for (int i = 0; i < service.m_size; i++)
            {
                ushort id = service.m_buffer[i];
                BuildingInfo info = buffer[id].Info;
                UniqueFactoryAI ai = info == null ? null : info.m_buildingAI as UniqueFactoryAI;
                if (ai == null || (buffer[id].m_flags & Building.Flags.Created) == 0)
                {
                    continue;
                }

                // As the factory's panel works it out (UniqueFactoryWorldInfoPanel).
                long health = buffer[id].m_health;
                long bought =
                    Input(health, ai.m_inputRate1, ai.m_inputResource1)
                    + Input(health, ai.m_inputRate2, ai.m_inputResource2)
                    + Input(health, ai.m_inputRate3, ai.m_inputResource3)
                    + Input(health, ai.m_inputRate4, ai.m_inputResource4);
                long made = (long)buffer[id].m_education3 * ai.m_outputRate * 16 / 100;
                long sales = made * IndustryBuildingAI.GetResourcePrice(ai.m_outputResource) / 100;
                long upkeep = ai.GetResourceRate(
                    id,
                    ref buffer[id],
                    EconomyManager.Resource.Maintenance
                );
                var facts = new FactoryFacts
                {
                    Active = Stands(id) && SwitchedOn(id),
                    HasProblems =
                        buffer[id].m_problems.m_Problems1 != Notification.Problem1.None
                        || buffer[id].m_problems.m_Problems2 != Notification.Problem2.None,
                    ProductionPercent = buffer[id].m_productionRate,
                    WeeklySalesCents = sales,
                    WeeklyBoughtCents = bought,
                    WeeklyUpkeepCents = Math.Abs(upkeep) * UpkeepCentsPerRate100 / 100,
                };
                var home = new BuildingKey(id, buffer[id].m_buildIndex);
                long average = Average(
                    profits,
                    home,
                    IpoIndustry.WeeklyProfitCents(
                        facts.WeeklySalesCents,
                        facts.WeeklyBoughtCents,
                        facts.WeeklyUpkeepCents
                    ),
                    rules,
                    today
                );
                list.Add(
                    new SiteReading
                    {
                        Home = home,
                        Kind = BusinessKind.UniqueFactory,
                        Site = BusinessSite.Factory,
                        Ref = FactoryRef + info.name,
                        Name = BuildingManager.instance.GetBuildingName(
                            id,
                            new InstanceID { Building = id }
                        ),
                        Healthy = IpoIndustry.FactoryHealthy(facts, average, rules),
                        AnnualCents = IpoIndustry.AnnualCents(average),
                        WeeklyProfitCents = average,
                    }
                );
            }
        }

        /// <summary>Not demolished, abandoned, collapsed or burned down.</summary>
        private static bool Stands(ushort building)
        {
            const Building.Flags Closed =
                Building.Flags.Abandoned
                | Building.Flags.Collapsed
                | Building.Flags.BurnedDown
                | Building.Flags.Deleted;
            Building.Flags flags = BuildingManager.instance.m_buildings.m_buffer[building].m_flags;
            return (flags & Building.Flags.Created) != 0 && (flags & Closed) == 0;
        }

        /// <summary>A week of one input as the factory's panel shows it, in cents.</summary>
        private static long Input(long health, int rate, TransferManager.TransferReason resource)
        {
            if (resource == TransferManager.TransferReason.None)
            {
                return 0;
            }

            long amount = health * rate * 16 / 100;
            return amount * IndustryBuildingAI.GetResourcePrice(resource) / 100;
        }

        /// <summary>An area's week as its panel shows it, in cents: exports, and raw resources bought in.</summary>
        private static void Week(DistrictPark park, out long sales, out long bought)
        {
            DistrictAreaResourceData raw,
                a,
                b;
            TransferManager.TransferReason rr,
                ra,
                rb;
            switch (park.m_parkType)
            {
                case DistrictPark.ParkType.Farming:
                    raw = park.m_grainData;
                    a = park.m_animalProductsData;
                    b = park.m_floursData;
                    rr = TransferManager.TransferReason.Grain;
                    ra = TransferManager.TransferReason.AnimalProducts;
                    rb = TransferManager.TransferReason.Flours;
                    break;
                case DistrictPark.ParkType.Forestry:
                    raw = park.m_logsData;
                    a = park.m_planedTimberData;
                    b = park.m_paperData;
                    rr = TransferManager.TransferReason.Logs;
                    ra = TransferManager.TransferReason.PlanedTimber;
                    rb = TransferManager.TransferReason.Paper;
                    break;
                case DistrictPark.ParkType.Oil:
                    raw = park.m_oilData;
                    a = park.m_petroleumData;
                    b = park.m_plasticsData;
                    rr = TransferManager.TransferReason.Oil;
                    ra = TransferManager.TransferReason.Petroleum;
                    rb = TransferManager.TransferReason.Plastics;
                    break;
                case DistrictPark.ParkType.Ore:
                    raw = park.m_oreData;
                    a = park.m_glassData;
                    b = park.m_metalsData;
                    rr = TransferManager.TransferReason.Ore;
                    ra = TransferManager.TransferReason.Glass;
                    rb = TransferManager.TransferReason.Metals;
                    break;
                default:
                    sales = bought = 0;
                    return;
            }

            sales =
                Money(raw.m_finalExport, rr)
                + Money(a.m_finalExport, ra)
                + Money(b.m_finalExport, rb);
            bought = Money(raw.m_finalImport, rr);
        }

        private static long Money(uint amount, TransferManager.TransferReason reason)
        {
            return (long)amount * IndustryBuildingAI.GetResourcePrice(reason) / 100;
        }

        private static bool TryKind(DistrictPark.ParkType type, out BusinessKind kind)
        {
            switch (type)
            {
                case DistrictPark.ParkType.Farming:
                    kind = BusinessKind.Farming;
                    return true;
                case DistrictPark.ParkType.Forestry:
                    kind = BusinessKind.Forestry;
                    return true;
                case DistrictPark.ParkType.Oil:
                    kind = BusinessKind.Oil;
                    return true;
                case DistrictPark.ParkType.Ore:
                    kind = BusinessKind.Ore;
                    return true;
                default:
                    kind = BusinessKind.Commercial;
                    return false;
            }
        }
    }
}

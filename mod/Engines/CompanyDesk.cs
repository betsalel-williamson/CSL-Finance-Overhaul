using System; // Enum, Exception, Math
using System.Collections.Generic; // Dictionary, HashSet, KeyValuePair, List
using System.Diagnostics; // Stopwatch
using System.Text; // Encoding, StringBuilder
using ColossalFramework.Math; // Randomizer
// FinanceOverhaul.Core: BoardView, BondText, BuildingKey, BusinessFacts, BusinessKind,
//   BusinessKinds, BusinessSite, CandidateBook, CompaniesCodec, CompanyRegistry, IBusinessSource,
//   IpoCardText, IpoDice, IpoEarnings, IpoEngine, IpoEvent, IpoEventKind, IpoRules, KindFacts,
//   LocalCompany, ModSettings, Notice, NoticeText
using FinanceOverhaul.Core;

// Game classes used without a using (global namespace, Assembly-CSharp): Building, BuildingInfo,
//   BuildingManager, EconomyManager, GameAreaManager, ImmaterialResourceManager, InstanceID,
//   ItemClass, LoadingManager, Notification, PrivateBuildingAI, SimulationManager, StockExchangeAI

namespace FinanceOverhaul
{
    /// <summary>
    /// Local IPOs in the running game (docs/features/local-ipos.md): once per in-game day, right
    /// after the vanilla investment update, every zoned business is read from the game and
    /// handed to the library, which counts how long each has stayed healthy and what the city
    /// has of each kind (the gates and slots); then the library's daily step runs and this class
    /// carries out what it decided (list a company, open its first trading day, pay a buy-out).
    /// Everything lives in the mod's own save data (<see cref="CompaniesCodec.DataId"/>),
    /// written when the city saves. Only runs while Options → Local IPOs is on.
    /// </summary>
    public static class CompanyDesk
    {
        private static readonly object Sync = new object();
        private static readonly IpoRules DefaultRules = new IpoRules();
        private static CandidateBook book;
        private static IpoDice dice;
        private static CompanyRegistry registry;
        private static KindFacts[] kinds = new KindFacts[0];

        // Industry areas and unique factories as the last scan read them, by headquarters.
        private static Dictionary<BuildingKey, SiteReading> sites =
            new Dictionary<BuildingKey, SiteReading>();
        private static int lastScanDay = -1;

        /// <summary>Game UI tests: rules for the scenario (null: the player's Options).</summary>
        public static IpoRules Rules;

        /// <summary>The rules in force: a scenario's, else the player's Options, else the defaults.</summary>
        public static IpoRules Current
        {
            get
            {
                if (Rules != null)
                {
                    return Rules;
                }

                ModSettings settings = Mod.Settings;
                return settings != null && settings.Ipo != null ? settings.Ipo : DefaultRules;
            }
        }

        /// <summary>Game UI tests: the Stock Exchange level IPOs see (-1: the city's own).</summary>
        public static int TestExchangeLevel = -1;

        /// <summary>
        /// The city's Stock Exchange level: the game's 1 to 5, 0 without one (or if it cannot
        /// be read). IPOs wait below <see cref="IpoRules.ExchangeLevel"/>.
        /// </summary>
        public static int ExchangeLevel()
        {
            if (TestExchangeLevel >= 0)
            {
                return TestExchangeLevel;
            }

            try
            {
                return (int)StockExchangeAI.GetLevel();
            }
            catch
            {
                return 0; // fail soft: no IPOs rather than a broken day
            }
        }

        /// <summary>The industry areas and unique factories the last scan read, for the log and tests.</summary>
        public static string LastSites { get; private set; }

        /// <summary>An industry area or unique factory the last scan read, by its headquarters.</summary>
        internal static bool TrySite(BuildingKey home, out SiteReading site)
        {
            lock (Sync)
            {
                return sites.TryGetValue(home, out site);
            }
        }

        /// <summary>How the last scan went, for the log and tests.</summary>
        public static int LastBusinesses { get; private set; }
        public static double LastScanMilliseconds { get; private set; }

        /// <summary>
        /// Per kind, how many businesses failed each health condition in the last scan (a
        /// business can fail several): not top level, a problem icon, understaffed, not among
        /// the larger employers of its kind (with that kind's jobs floor), land value too low;
        /// then how many passed all five. For tuning the rules.
        /// </summary>
        public static string LastHealthBreakdown { get; private set; }

        /// <summary>Per kind, how many jobs its businesses have (dev readout of the last scan).</summary>
        public static string LastJobSizes { get; private set; }

        /// <summary>Dev: each kind's shortlist at the last measured scan (workers, largest first).</summary>
        public static string LastShortlists { get; private set; }

        /// <summary>Dev: the next scans also fill <see cref="LastJobSizes"/>.</summary>
        public static bool MeasureJobSizes;

        public static bool Enabled
        {
            get { return Mod.Settings != null && Mod.Settings.LocalIposEnabled; }
        }

        /// <summary>What the city has of each kind, as of the last scan.</summary>
        public static KindFacts[] Kinds()
        {
            lock (Sync)
            {
                return (KindFacts[])kinds.Clone();
            }
        }

        /// <summary>Healthy days counted for a building so far.</summary>
        public static int HealthyDaysOf(ushort building, uint buildIndex)
        {
            lock (Sync)
            {
                return Book().HealthyDaysOf(new BuildingKey(building, buildIndex));
            }
        }

        /// <summary>Postfix target: EconomyManager.SimulationStepInvestments (simulation thread).</summary>
        /// <remarks>
        /// With Local IPOs switched off no IPO is drawn, but the companies already listed (they
        /// stay listed whatever the flag says) keep trading: their prices move every day.
        /// </remarks>
        public static void OnInvestmentsStepped()
        {
            bool newListings = Enabled;
            if (!newListings && !AnyListed())
            {
                return;
            }

            int today = OrderDesk.Today;
            if (today == lastScanDay)
            {
                return;
            }

            lastScanDay = today;
            Scan();
            Step(today, newListings);
        }

        private static bool AnyListed()
        {
            lock (Sync)
            {
                Book();
                return registry.Companies.Count > 0;
            }
        }

        /// <summary>The whole local board as of the last scan, for Economy → Investments.</summary>
        public static BoardView Board()
        {
            lock (Sync)
            {
                Book();
                return IpoCardText.View(
                    kinds,
                    registry,
                    GameAreaManager.instance.m_areaCount,
                    OrderDesk.Today,
                    Current,
                    ExchangeLevel()
                );
            }
        }

        /// <summary>The company's price now, in cents (0 if unknown).</summary>
        public static long PriceCentsOf(int companyId)
        {
            lock (Sync)
            {
                LocalCompany c = registry == null ? null : registry.Find(companyId);
                return c == null ? 0 : c.PriceCents;
            }
        }

        /// <summary>The company with this id, or null.</summary>
        public static LocalCompany CompanyOf(int companyId)
        {
            lock (Sync)
            {
                return registry == null ? null : registry.Find(companyId);
            }
        }

        /// <summary>The companies listed now.</summary>
        public static List<LocalCompany> Companies()
        {
            lock (Sync)
            {
                Book();
                return new List<LocalCompany>(registry.Companies);
            }
        }

        /// <summary>
        /// Main thread (level loaded, hot reload): lists every company the save holds again.
        /// The game dropped their stock types on load; the holdings come back from the registry.
        /// </summary>
        public static void EnsureListed()
        {
            try
            {
                // Whatever the flag says: a company already listed keeps its stock (the flag
                // gates new IPOs), and a company stock the save does not know is cleared.
                if (!LoadingManager.exists || !LoadingManager.instance.m_loadingComplete)
                {
                    return;
                }

                List<LocalCompany> companies = Companies();
                var known = new List<int>();
                foreach (LocalCompany c in companies)
                {
                    known.Add(c.Id);
                    CompanyListing.List(c);
                }

                SimulationManager.instance.AddAction(() => CompanyListing.SweepOrphans(known));
            }
            catch (Exception ex)
            {
                ModLog.Warn("ipo: listing the city's companies failed: " + ex.Message);
            }
        }

        /// <summary>Simulation thread: one day of the library's engine, then its events.</summary>
        private static void Step(int today, bool newListings = true)
        {
            List<IpoEvent> events;
            lock (Sync)
            {
                Book();
                events = IpoEngine.Step(
                    registry,
                    book,
                    kinds,
                    GameAreaManager.instance.m_areaCount,
                    today,
                    Current,
                    dice,
                    Source,
                    ExchangeLevel(),
                    newListings
                );
            }

            foreach (IpoEvent e in events)
            {
                Carry(e);
            }
        }

        /// <summary>Simulation thread: does in the game what the engine decided.</summary>
        private static void Carry(IpoEvent e)
        {
            LocalCompany c = e.Company;
            switch (e.Kind)
            {
                case IpoEventKind.Announced:
                    // Remembered, so a buy-out can put the building back as it was.
                    c.WasHistorical = IsHistorical(c.Home);
                    if (e.StoodIn)
                    {
                        ModLog.Info(
                            "ipo: the business picked at the draw no longer qualified; "
                                + c.Name
                                + " takes its place"
                        );
                    }

                    MarkHistorical(c.Home);
                    ModLog.Info(
                        "ipo: "
                            + c.Name
                            + " is going public at "
                            + BondText.WholeMoney(c.OfferPriceCents)
                            + " a share; first trading day "
                            + c.FirstTradingDay
                            + "; headquarters building "
                            + c.Home.Id
                    );
                    CompanyListing.OnMainThread(() => CompanyListing.List(c));
                    Chirp(
                        NoticeText.IpoAnnounced(
                            c.Name,
                            c.OfferPriceCents,
                            c.FirstTradingDay - c.AnnouncedDay
                        )
                    );
                    IpoDialog.Announce(c.Id);
                    IpoFireworks.Over(c.Home.Id, c.Home.BuildIndex);
                    break;
                case IpoEventKind.Cancelled:
                    ModLog.Info(
                        "ipo: the IPO planned for today fell through (no longer qualifies, and no other candidate of its kind)"
                    );
                    break;
                case IpoEventKind.FirstTradingDay:
                    ModLog.Info(
                        "ipo: "
                            + c.Name
                            + " opened at "
                            + BondText.WholeMoney(c.PriceCents)
                            + " ("
                            + (e.Fraction * 100).ToString("+0.0;-0.0")
                            + "% on the offering price)"
                    );
                    Chirp(NoticeText.IpoFirstDay(c.Name, c.OfferPriceCents, c.PriceCents));
                    IpoFireworks.Over(c.Home.Id, c.Home.BuildIndex);
                    break;
                case IpoEventKind.BoughtOut:
                    uint sold;
                    // The game's sale answers in whole ₡ (measured: 10 shares at ₡81 cost 810).
                    long paid = CompanyListing.Delist(c.Id, e.PricePerShareCents, out sold) * 100;
                    Chirp(
                        NoticeText.IpoBoughtOut(
                            c.Name,
                            e.PricePerShareCents,
                            e.Fraction,
                            sold,
                            paid
                        )
                    );
                    ModLog.Info(
                        "ipo: "
                            + c.Name
                            + " was bought out at "
                            + BondText.WholeMoney(e.PricePerShareCents)
                            + " a share; the city received "
                            + BondText.WholeMoney(paid)
                            + " for "
                            + sold
                            + " share(s)"
                    );
                    // Listed no more: the building goes back to how it was.
                    if (!c.WasHistorical)
                    {
                        ClearHistorical(c.Home);
                    }

                    break;
                case IpoEventKind.Moved:
                    // The building it left goes back to how it was (an abandoned one can then
                    // be replaced by the game); the new one becomes its Historical headquarters.
                    if (!c.WasHistorical)
                    {
                        ClearHistorical(e.From);
                    }

                    c.WasHistorical = IsHistorical(c.Home);
                    MarkHistorical(c.Home);
                    string newHome = Source.NameOf(c.Home);
                    ModLog.Info(
                        "ipo: "
                            + c.Name
                            + " lost its headquarters (building "
                            + e.From.Id
                            + ") and moved to "
                            + newHome
                            + " (building "
                            + c.Home.Id
                            + ")"
                    );
                    Chirp(NoticeText.IpoMoved(c.Name, newHome));
                    break;
                case IpoEventKind.HomeLost:
                {
                    int grace = Current.HomeGraceDays;
                    ModLog.Info(
                        "ipo: "
                            + c.Name
                            + " lost its headquarters (building "
                            + c.Home.Id
                            + ") and no business of its kind can take it; it closes in "
                            + grace
                            + " days unless one can"
                    );
                    Chirp(NoticeText.IpoHomeLost(c.Name, grace));
                    break;
                }
                case IpoEventKind.HomeRestored:
                {
                    ModLog.Info(
                        "ipo: "
                            + c.Name
                            + " has its headquarters back (building "
                            + c.Home.Id
                            + "); its grace period is over"
                    );
                    Chirp(NoticeText.IpoHomeRestored(c.Name));
                    break;
                }
                case IpoEventKind.Defunct:
                {
                    uint shares;
                    // Paid at the last price, as the game sells (whole ₡).
                    long payout =
                        CompanyListing.Delist(c.Id, e.PricePerShareCents, out shares) * 100;
                    Chirp(NoticeText.IpoDefunct(c.Name, e.PricePerShareCents, shares, payout));
                    ModLog.Info(
                        "ipo: "
                            + c.Name
                            + " closed with no headquarters at "
                            + BondText.WholeMoney(e.PricePerShareCents)
                            + " a share; the city received "
                            + BondText.WholeMoney(payout)
                            + " for "
                            + shares
                            + " share(s)"
                    );
                    if (!c.WasHistorical)
                    {
                        ClearHistorical(c.Home);
                    }

                    break;
                }
            }
        }

        /// <summary>
        /// One chirp from City Brokerage. Follows the "Order and bond notices in Chirper"
        /// preference like every other notice of the mod.
        /// </summary>
        private static void Chirp(Notice notice)
        {
            ChirpNotices.PostDay(new List<Notice> { notice });
        }

        /// <summary>
        /// A company's headquarters becomes a Historical building (if it is still that building).
        /// Only a zoned building: an industry area's main building or a unique factory is a
        /// service building, which the game never lets be Historical.
        /// </summary>
        private static void MarkHistorical(BuildingKey home)
        {
            Building[] buffer = BuildingManager.instance.m_buildings.m_buffer;
            if (
                home.Id < buffer.Length
                && (buffer[home.Id].m_flags & Building.Flags.Created) != 0
                && buffer[home.Id].m_buildIndex == home.BuildIndex
                && buffer[home.Id].Info != null
                && buffer[home.Id].Info.m_buildingAI is PrivateBuildingAI
            )
            {
                buffer[home.Id].m_flags |= Building.Flags.Historical;
            }
        }

        private static bool SameBuilding(BuildingKey home)
        {
            Building[] buffer = BuildingManager.instance.m_buildings.m_buffer;
            return home.Id < buffer.Length
                && (buffer[home.Id].m_flags & Building.Flags.Created) != 0
                && buffer[home.Id].m_buildIndex == home.BuildIndex;
        }

        private static bool IsHistorical(BuildingKey home)
        {
            return SameBuilding(home)
                && (
                    BuildingManager.instance.m_buildings.m_buffer[home.Id].m_flags
                    & Building.Flags.Historical
                ) != 0;
        }

        private static void ClearHistorical(BuildingKey home)
        {
            if (SameBuilding(home))
            {
                BuildingManager.instance.m_buildings.m_buffer[home.Id].m_flags &= ~Building
                    .Flags
                    .Historical;
            }
        }

        /// <summary>
        /// Any thread: a listed company's headquarters stays a Historical building, whatever
        /// was unticked before the tick was locked.
        /// </summary>
        public static void KeepHistorical(LocalCompany company)
        {
            if (company != null && !IsHistorical(company.Home))
            {
                BuildingKey home = company.Home;
                SimulationManager.instance.AddAction(() => MarkHistorical(home));
            }
        }

        private static readonly IBusinessSource Source = new GameBusinesses();

        /// <summary>What the engine asks the game about a business that is about to list.</summary>
        private sealed class GameBusinesses : IBusinessSource
        {
            public bool TrySuccessor(LocalCompany company, out BuildingKey home)
            {
                return IndustrySites.TrySuccessor(company, out home);
            }

            public string NameOf(BuildingKey building)
            {
                SiteReading site;
                if (TrySite(building, out site) && !string.IsNullOrEmpty(site.Name))
                {
                    return site.Name; // an area is named after the area, not its main building
                }

                return BuildingManager.instance.GetBuildingName(
                    building.Id,
                    new InstanceID { Building = building.Id }
                );
            }

            public string SymbolFor(string name)
            {
                // Symbols come from the one map built over every stock type; it is read once
                // the company's own type exists.
                return string.Empty;
            }

            public long AnnualEarningsCents(BuildingKey building)
            {
                // A building that is gone (or whose id another building has now) earns
                // nothing the engine can go by: unknown, not zero.
                if (!SameBuilding(building))
                {
                    return 0;
                }

                SiteReading site;
                return TrySite(building, out site)
                    ? site.AnnualCents
                    : CompanyDesk.AnnualEarningsCents(building.Id);
            }

            public bool HomeStands(BuildingKey building)
            {
                if (!SameBuilding(building))
                {
                    return false; // demolished, or its id now belongs to another building
                }

                lock (Sync)
                {
                    if (
                        testLost.Contains(building)
                        || (testLostKind.HasValue && Book().KindOf(building) == testLostKind.Value)
                    )
                    {
                        return false;
                    }
                }

                const Building.Flags Closed =
                    Building.Flags.Abandoned | Building.Flags.Collapsed | Building.Flags.BurnedDown;
                if (
                    (BuildingManager.instance.m_buildings.m_buffer[building.Id].m_flags & Closed)
                    != 0
                )
                {
                    return false;
                }

                // An area's main building or a factory switched off by the player counts as
                // lost too (decided 2026-11-20); switched back on in time, it is back.
                SiteReading site;
                return !TrySite(building, out site) || IndustrySites.SwitchedOn(building.Id);
            }
        }

        // Game UI tests: buildings a scenario treats as lost (the city's own are untouched).
        private static readonly HashSet<BuildingKey> testLost = new HashSet<BuildingKey>();

        // Game UI tests: every business of this kind counts as lost, today's and later ones.
        private static BusinessKind? testLostKind;

        /// <summary>
        /// Game UI tests: a company's headquarters counts as lost from the next daily step;
        /// with <paramref name="wholeKind"/>, every candidate of its kind too, so it has nowhere
        /// to move. Returns how many buildings that is.
        /// </summary>
        public static int TestLoseHome(int companyId, bool wholeKind)
        {
            lock (Sync)
            {
                LocalCompany c = registry.Find(companyId);
                if (c == null)
                {
                    return 0;
                }

                testLost.Add(c.Home);
                if (wholeKind)
                {
                    testLostKind = c.Kind;
                    foreach (BuildingKey b in Book().Candidates(Current, c.Kind))
                    {
                        testLost.Add(b);
                    }
                }

                return testLost.Count;
            }
        }

        /// <summary>Game UI tests: every building counts as standing again.</summary>
        public static void TestFindHomes()
        {
            lock (Sync)
            {
                testLost.Clear();
                testLostKind = null;
            }
        }

        /// <summary>Game UI tests: runs today's step now (simulation paused).</summary>
        public static void TestStepToday()
        {
            Step(OrderDesk.Today);
        }

        /// <summary>
        /// Game UI tests (simulation paused): lists the first candidate now, as the scheduled
        /// path would. Null if the city has no candidate.
        /// </summary>
        public static LocalCompany TestListFirstCandidate(
            out bool wasHistorical,
            BusinessSite site = BusinessSite.Zoned
        )
        {
            wasHistorical = false;
            IpoEvent e;
            lock (Sync)
            {
                CandidateBook b = Book();
                IpoRules rules = Current;
                List<BuildingKey> pool = b.Candidates(rules);
                pool.RemoveAll(registry.IsHome);
                pool.RemoveAll(k => b.SiteOf(k) != site);
                if (pool.Count == 0)
                {
                    return null;
                }

                wasHistorical =
                    (
                        BuildingManager.instance.m_buildings.m_buffer[pool[0].Id].m_flags
                        & Building.Flags.Historical
                    ) != 0;
                e = IpoEngine.List(
                    registry,
                    pool[0],
                    b.KindOf(pool[0]),
                    OrderDesk.Today,
                    rules,
                    Source,
                    b.SiteOf(pool[0]),
                    b.SiteRefOf(pool[0])
                );
            }

            Carry(e);
            return e.Company;
        }

        /// <summary>
        /// Dev: removes every local company as if none had ever listed (no payout), clears what
        /// the dice had scheduled, and rewrites the city's mod data so the next save does not
        /// carry them. Returns how many were removed.
        /// </summary>
        public static int TestRemoveAll()
        {
            List<LocalCompany> all = Companies();
            foreach (LocalCompany c in all)
            {
                TestRemove(c.Id, c.AnnouncedDay);
            }

            lock (Sync)
            {
                registry.IpoScheduled = false;
                registry.BuyoutScheduled = false;
                registry.IpoDays.Clear();
            }

            Save();
            return all.Count;
        }

        /// <summary>Game UI tests: the company's offering window closes today.</summary>
        public static void TestOpenFirstDay(int companyId)
        {
            lock (Sync)
            {
                LocalCompany c = registry.Find(companyId);
                if (c != null)
                {
                    c.FirstTradingDay = OrderDesk.Today;
                }
            }

            Step(OrderDesk.Today);
        }

        /// <summary>Game UI tests: the company is bought out today.</summary>
        public static void TestBuyOut(int companyId)
        {
            lock (Sync)
            {
                registry.BuyoutScheduled = true;
                registry.BuyoutCompanyId = companyId;
                registry.BuyoutDay = OrderDesk.Today;
            }

            Step(OrderDesk.Today);
        }

        /// <summary>
        /// Game UI tests: removes a company as if it had never listed (no payout), frees its
        /// pacing day and takes the Historical flag off its building again.
        /// </summary>
        public static void TestRemove(int companyId, int announcedDay)
        {
            LocalCompany c;
            lock (Sync)
            {
                Book();
                // Whatever became of the company: a test listing never uses up one of the
                // year's IPOs (one a scenario had bought out is already gone from the records,
                // and its day used to stay behind and hold up the city's real IPOs).
                registry.IpoDays.Remove(announcedDay);
                c = registry.Find(companyId);
                if (c == null)
                {
                    return;
                }

                registry.Companies.Remove(c);
            }

            uint none;
            CompanyListing.Delist(companyId, 0, out none);
            Building[] buffer = BuildingManager.instance.m_buildings.m_buffer;
            // Its headquarters now (it may have moved); the one it listed on is the runner's.
            if (!c.WasHistorical && buffer[c.Home.Id].m_buildIndex == c.Home.BuildIndex)
            {
                buffer[c.Home.Id].m_flags &= ~Building.Flags.Historical;
            }
        }

        /// <summary>
        /// Dev: industry areas and unique factories are forgotten (healthy days, weekly profits)
        /// and their one-time credit starts again from the next scan, as for a city updating to
        /// 0.2.0.
        /// </summary>
        public static void TestCreditSitesAgain()
        {
            lock (Sync)
            {
                Book().ForgetSites();
            }
        }

        /// <summary>
        /// Dev: the next scan looks behind again, as on the first scan of a city (for a test
        /// city whose first look behind was spent under an older rule).
        /// </summary>
        public static void TestLookBehindAgain()
        {
            lock (Sync)
            {
                Book().LookedBehind = false;
                Book().LookedBehindSites = false;
            }
        }

        /// <summary>
        /// Dev clean-up: forgets the days counted towards "at most N IPOs a year", so the next
        /// monthly draw is free to list. Returns how many were forgotten.
        /// </summary>
        public static int TestForgetPacing()
        {
            lock (Sync)
            {
                Book();
                int n = registry.IpoDays.Count;
                registry.IpoDays.Clear();
                return n;
            }
        }

        /// <summary>Reads every zoned business now (simulation thread, or with it paused).</summary>
        public static void Scan()
        {
            lock (Sync)
            {
                Stopwatch watch = Stopwatch.StartNew();
                CandidateBook b = Book();
                IpoRules rules = Current;
                var totals = new Dictionary<BusinessKind, KindFacts>();
                foreach (BusinessKind kind in Enum.GetValues(typeof(BusinessKind)))
                {
                    totals[kind] = new KindFacts { Kind = kind };
                }

                var failed = new Dictionary<BusinessKind, int[]>();
                foreach (BusinessKind kind in Enum.GetValues(typeof(BusinessKind)))
                {
                    failed[kind] = new int[6];
                }

                // First pass: read every business. The jobs floor of a kind (its larger
                // employers) is only known once all of that kind are read.
                Building[] buffer = BuildingManager.instance.m_buildings.m_buffer;
                ImmaterialResourceManager land = ImmaterialResourceManager.instance;
                var seen = new List<KeyValuePair<BuildingKey, BusinessFacts>>();
                var jobsOfKind = new Dictionary<BusinessKind, List<int>>();
                foreach (BusinessKind kind in Enum.GetValues(typeof(BusinessKind)))
                {
                    jobsOfKind[kind] = new List<int>();
                }

                // The people working in each business today: the rules judge size by them.
                var workersOfKind = new Dictionary<BusinessKind, List<int>>();
                foreach (BusinessKind kind in Enum.GetValues(typeof(BusinessKind)))
                {
                    workersOfKind[kind] = new List<int>();
                }

                const Building.Flags Gone =
                    Building.Flags.Abandoned
                    | Building.Flags.Collapsed
                    | Building.Flags.BurnedDown
                    | Building.Flags.Deleted;
                for (int i = 1; i < buffer.Length; i++)
                {
                    if ((buffer[i].m_flags & Building.Flags.Created) == 0)
                    {
                        continue;
                    }

                    BuildingInfo info = buffer[i].Info;
                    PrivateBuildingAI ai =
                        info == null ? null : info.m_buildingAI as PrivateBuildingAI;
                    BusinessKind kind;
                    if (
                        ai == null
                        || info.m_class == null
                        || !BusinessKinds.TryFromGame(
                            info.m_class.m_service.ToString(),
                            info.m_class.m_subService.ToString(),
                            out kind
                        )
                    )
                    {
                        continue;
                    }

                    int width = buffer[i].Width,
                        length = buffer[i].Length;
                    int level = buffer[i].m_level;
                    bool top = BusinessKinds.IsTopLevel(kind, level);
                    KindFacts total = totals[kind];
                    total.Squares += width * length;
                    if (top)
                    {
                        total.TopLevelBuildings++;
                    }

                    totals[kind] = total;
                    int jobs = BuildingStaff.Jobs((ushort)i);
                    jobsOfKind[kind].Add(jobs);
                    int workers = BuildingStaff.Workers((ushort)i);
                    workersOfKind[kind].Add(workers);

                    int landValue;
                    land.CheckLocalResource(
                        ImmaterialResourceManager.Resource.LandValue,
                        buffer[i].m_position,
                        out landValue
                    );
                    seen.Add(
                        new KeyValuePair<BuildingKey, BusinessFacts>(
                            new BuildingKey((ushort)i, buffer[i].m_buildIndex),
                            new BusinessFacts
                            {
                                Kind = kind,
                                TopLevel = top,
                                HasProblems =
                                    (buffer[i].m_flags & Gone) != 0
                                    || buffer[i].m_problems.m_Problems1
                                        != Notification.Problem1.None
                                    || buffer[i].m_problems.m_Problems2
                                        != Notification.Problem2.None,
                                FullyStaffed = buffer[i].m_allUnemployed <= rules.MaxVacancyPercent,
                                Jobs = jobs,
                                Workers = workers,
                                LandValue = landValue,
                            }
                        )
                    );
                }

                // Second pass: judge each business against its kind's floor.
                var floors = new Dictionary<BusinessKind, int>();
                foreach (BusinessKind kind in Enum.GetValues(typeof(BusinessKind)))
                {
                    floors[kind] = BusinessKinds.WorkersFloor(kind, workersOfKind[kind], rules);
                }

                // How large the businesses of each kind are (for choosing the jobs rules).
                // Only when a dev readout asked for it: it sorts every kind's list.
                var sizes = new StringBuilder();
                foreach (BusinessKind kind in Enum.GetValues(typeof(BusinessKind)))
                {
                    var jobs = new List<int>(jobsOfKind[kind]);
                    if (!MeasureJobSizes || jobs.Count == 0)
                    {
                        continue;
                    }

                    jobs.Sort();
                    int single = 0;
                    foreach (int j in jobs)
                    {
                        single += j <= 1 ? 1 : 0;
                    }

                    sizes
                        .Append(' ')
                        .Append(kind)
                        .Append(" n=")
                        .Append(jobs.Count)
                        .Append(" oneJob=")
                        .Append(single)
                        .Append(" min=")
                        .Append(jobs[0])
                        .Append(" median=")
                        .Append(jobs[jobs.Count / 2])
                        .Append(" p90=")
                        .Append(jobs[jobs.Count * 9 / 10])
                        .Append(" max=")
                        .Append(jobs[jobs.Count - 1])
                        .Append(" floor=")
                        .Append(floors[kind])
                        .Append(" jobs=");
                    // Every business's jobs, for plotting the spread.
                    for (int j = 0; j < jobs.Count; j++)
                    {
                        sizes.Append(j == 0 ? string.Empty : ",").Append(jobs[j]);
                    }

                    var workers = new List<int>(workersOfKind[kind]);
                    workers.Sort();
                    sizes.Append(" workers=");
                    for (int j = 0; j < workers.Count; j++)
                    {
                        sizes.Append(j == 0 ? string.Empty : ",").Append(workers[j]);
                    }

                    sizes.Append(';');
                }

                LastJobSizes = sizes.ToString();

                int businesses = seen.Count;
                foreach (KeyValuePair<BuildingKey, BusinessFacts> one in seen)
                {
                    BusinessFacts facts = one.Value;
                    facts.WorkersFloor = floors[facts.Kind];
                    int[] why = failed[facts.Kind];
                    why[0] += facts.TopLevel ? 0 : 1;
                    why[1] += facts.HasProblems ? 1 : 0;
                    why[2] += facts.FullyStaffed ? 0 : 1;
                    why[3] += facts.Workers >= facts.WorkersFloor ? 0 : 1;
                    why[4] +=
                        !BusinessKinds.LandValueMatters(facts.Kind)
                        || facts.LandValue >= rules.LandValueFloor
                            ? 0
                            : 1;
                    why[5] += CandidateBook.Healthy(facts, rules) ? 1 : 0;
                    b.Observe(one.Key, facts, rules);
                }

                // Industry areas and unique factories: companies of their own, judged by the
                // library's IpoIndustry. Each area at the level areas need, and each factory,
                // gives its kind a slot.
                var read = new Dictionary<BuildingKey, SiteReading>();
                var siteLog = new StringBuilder();
                bool creditWasOpen = !b.LookedBehindSites;
                b.StartSiteDay(OrderDesk.Today, rules);
                foreach (SiteReading s in IndustrySites.Read(rules, b.Profits, OrderDesk.Today))
                {
                    read[s.Home] = s;
                    b.Observe(s.Home, s.Kind, s.Healthy, rules, s.Site, s.Ref);
                    b.SetWorkers(s.Home, s.Workers);
                    KindFacts total = totals[s.Kind];
                    if (s.Site == BusinessSite.Factory || s.Level >= rules.AreaMinLevel)
                    {
                        total.ExtraSlots++;
                    }

                    totals[s.Kind] = total;
                    siteLog
                        .Append(' ')
                        .Append(s.Site)
                        .Append(" '")
                        .Append(s.Name)
                        .Append("' ")
                        .Append(s.Kind)
                        .Append(s.Site == BusinessSite.Area ? " level " + s.Level : string.Empty)
                        .Append(s.Healthy ? " healthy " : " not healthy ") // not shown: the log
                        .Append(BondText.WholeMoney(s.AnnualCents))
                        .Append("/yr, ") // not shown: the log
                        .Append(s.Workers)
                        .Append(" workers (") // not shown: the log
                        .Append(b.Profits.Count(s.Home))
                        .Append(" day(s) read);");
                }

                sites = read;
                LastSites = siteLog.ToString();
                bool lookingBehind = !b.LookedBehind;
                bool lookingBehindSites = !b.LookedBehindSites;
                b.EndDay();
                if (creditWasOpen && b.LookedBehindSites)
                {
                    ModLog.Info(
                        "ipo: the one-time credit for industry areas and factories is over"
                    );
                }

                if (lookingBehind)
                {
                    ModLog.Info(
                        "ipo: first scan of this city: "
                            + b.Candidates(rules).Count
                            + " business(es) healthy today count as healthy for the past year"
                    );
                }
                else if (lookingBehindSites)
                {
                    int credited = 0;
                    foreach (SiteReading s in read.Values)
                    {
                        credited += b.IsCandidate(s.Home, rules) ? 1 : 0;
                    }

                    ModLog.Info(
                        "ipo: industry areas and factories within their one-time credit (until day "
                            + b.SiteCreditUntilDay
                            + "): "
                            + credited
                            + " healthy on their average count as healthy for the past year"
                    );
                }

                var list = new List<KindFacts>();
                var shortlists = new StringBuilder();
                foreach (BusinessKind kind in Enum.GetValues(typeof(BusinessKind)))
                {
                    KindFacts total = totals[kind];
                    List<BuildingKey> pool = b.Candidates(rules, kind);
                    total.Candidates = pool.Count;
                    list.Add(total);
                    if (MeasureJobSizes && pool.Count > 0)
                    {
                        shortlists.Append(' ').Append(kind).Append(" of ").Append(pool.Count);
                        foreach (BuildingKey k in b.Shortlist(pool, rules))
                        {
                            shortlists.Append(' ').Append(b.WorkersOf(k));
                        }

                        shortlists.Append(';');
                    }
                }

                if (MeasureJobSizes)
                {
                    LastShortlists = shortlists.ToString();
                }

                var breakdown = new StringBuilder();
                foreach (BusinessKind kind in Enum.GetValues(typeof(BusinessKind)))
                {
                    int[] why = failed[kind];
                    if (totals[kind].Squares > 0)
                    {
                        breakdown
                            .Append(' ')
                            .Append(kind)
                            .Append(" notTop=")
                            .Append(why[0])
                            .Append(" problems=")
                            .Append(why[1])
                            .Append(" understaffed=")
                            .Append(why[2])
                            .Append(" smallEmployer=")
                            .Append(why[3])
                            .Append(" (floor ")
                            .Append(floors[kind])
                            .Append(" workers)")
                            .Append(" lowLand=")
                            .Append(why[4])
                            .Append(" healthy=")
                            .Append(why[5])
                            .Append(';');
                    }
                }

                LastHealthBreakdown = breakdown.ToString();
                kinds = list.ToArray();
                LastBusinesses = businesses;
                LastScanMilliseconds = watch.Elapsed.TotalMilliseconds;
            }
        }

        /// <summary>
        /// The building's jobs by the game's formula. The game works them out from the building
        /// itself: the width and length passed in do not change the answer (checked in game).
        /// </summary>
        private static int Workplaces(
            PrivateBuildingAI ai,
            int level,
            int seed,
            int width,
            int length
        )
        {
            int level0,
                level1,
                level2,
                level3;
            ai.CalculateWorkplaceCount(
                (ItemClass.Level)level,
                new Randomizer(seed),
                width,
                length,
                out level0,
                out level1,
                out level2,
                out level3
            );
            return level0 + level1 + level2 + level3;
        }

        /// <summary>
        /// What the game's own formula gives a business as income for one of its simulation
        /// steps at full production (the figure the game taxes): 0 for anything that is not a
        /// zoned business. An estimate: it does not see how busy the building is today.
        /// </summary>
        public static int IncomePerStep(ushort building)
        {
            Building[] buffer = BuildingManager.instance.m_buildings.m_buffer;
            BuildingInfo info = buffer[building].Info;
            PrivateBuildingAI ai = info == null ? null : info.m_buildingAI as PrivateBuildingAI;
            if (ai == null)
            {
                return 0;
            }

            int electricity,
                water,
                sewage,
                garbage,
                income,
                mail;
            ai.GetConsumptionRates(
                (ItemClass.Level)buffer[building].m_level,
                new Randomizer(building),
                100,
                out electricity,
                out water,
                out sewage,
                out garbage,
                out income,
                out mail
            );
            return income;
        }

        /// <summary>
        /// A business's weight when its zone's income is shared out: the game formula's income
        /// times its jobs, so a larger business takes a larger share (the formula alone hardly
        /// differs between businesses of one zone).
        /// </summary>
        public static long EarningsWeight(ushort building)
        {
            Building[] buffer = BuildingManager.instance.m_buildings.m_buffer;
            BuildingInfo info = buffer[building].Info;
            PrivateBuildingAI ai = info == null ? null : info.m_buildingAI as PrivateBuildingAI;
            if (ai == null)
            {
                return 0;
            }

            int jobs = Workplaces(
                ai,
                buffer[building].m_level,
                building,
                buffer[building].Width,
                buffer[building].Length
            );
            return (long)IncomePerStep(building) * Math.Max(1, jobs);
        }

        /// <summary>The weights of every zoned business of a service, summed.</summary>
        public static long ZoneEarningsWeight(ItemClass.Service service)
        {
            Building[] buffer = BuildingManager.instance.m_buildings.m_buffer;
            long total = 0;
            for (int i = 1; i < buffer.Length; i++)
            {
                BuildingInfo info = buffer[i].Info;
                if (
                    (buffer[i].m_flags & Building.Flags.Created) != 0
                    && info != null
                    && info.m_class != null
                    && info.m_class.m_service == service
                    && info.m_buildingAI is PrivateBuildingAI
                )
                {
                    total += EarningsWeight((ushort)i);
                }
            }

            return total;
        }

        private static readonly Dictionary<ItemClass.Service, long> ZoneWeights =
            new Dictionary<ItemClass.Service, long>();
        private static int zoneWeightsDay = int.MinValue;

        /// <summary>
        /// <see cref="ZoneEarningsWeight"/>, worked out once a day for each zone: every listed
        /// company asks for its zone's every day, and each answer reads the whole city.
        /// </summary>
        private static long ZoneWeightToday(ItemClass.Service service)
        {
            lock (ZoneWeights)
            {
                if (zoneWeightsDay != OrderDesk.Today)
                {
                    ZoneWeights.Clear();
                    zoneWeightsDay = OrderDesk.Today;
                }

                long weight;
                if (!ZoneWeights.TryGetValue(service, out weight))
                {
                    weight = ZoneEarningsWeight(service);
                    ZoneWeights[service] = weight;
                }

                return weight;
            }
        }

        /// <summary>
        /// What a business earns in a game year, in cents: its share (by the game's formula and
        /// its jobs) of the weekly income the game reports for its zone, as the income that tax was levied
        /// on. 0 when the zone brings in nothing (untaxed, or no income yet).
        /// </summary>
        public static long AnnualEarningsCents(ushort building)
        {
            BuildingInfo info = BuildingManager.instance.m_buildings.m_buffer[building].Info;
            if (info == null || info.m_class == null)
            {
                return 0;
            }

            EconomyManager em = EconomyManager.instance;
            long weeklyIncome,
                weeklyExpenses;
            em.GetIncomeAndExpenses(
                info.m_class.m_service,
                ItemClass.SubService.None,
                ItemClass.Level.None,
                out weeklyIncome,
                out weeklyExpenses
            );
            return IpoEarnings.AnnualCents(
                EarningsWeight(building),
                ZoneWeightToday(info.m_class.m_service),
                weeklyIncome,
                em.GetTaxRate(info.m_class)
            );
        }

        /// <summary>City save: writes the counts (a city never scanned writes nothing).</summary>
        public static void Save()
        {
            lock (Sync)
            {
                if (book == null)
                {
                    return;
                }

                try
                {
                    CompanyListing.CopyHoldings(registry.Companies);
                    SimulationManager.instance.m_SerializableDataWrapper.SaveData(
                        CompaniesCodec.DataId,
                        Encoding.UTF8.GetBytes(CompaniesCodec.Format(book, dice, registry))
                    );
                }
                catch (Exception ex)
                {
                    ModLog.Warn("ipo: save failed: " + ex.Message);
                }
            }
        }

        /// <summary>Level load / unload: the next city reads its own counts.</summary>
        public static void Reset()
        {
            lock (Sync)
            {
                book = null;
                shelved = null;
                dice = null;
                registry = null;
                kinds = new KindFacts[0];
                lastScanDay = -1;
                Rules = null;
                IpoDialog.Reset();
            }
        }

        /// <summary>Game UI tests: forgets the counts in memory (the save is untouched until the city saves).</summary>
        public static void ForgetCounts()
        {
            lock (Sync)
            {
                // The city's own counts are set aside, not lost: RestoreCounts puts them back
                // when the scenario ends (scenario 64 used to wipe a test city's candidates).
                if (shelved == null)
                {
                    shelved = Book();
                }

                // As a city already being watched: the tests count days one at a time.
                book = new CandidateBook { LookedBehind = true, LookedBehindSites = true };
            }
        }

        private static CandidateBook shelved;

        /// <summary>Game UI tests, at the end of a scenario: the city's own counts are back.</summary>
        public static void RestoreCounts()
        {
            lock (Sync)
            {
                if (shelved != null)
                {
                    book = shelved;
                    shelved = null;
                }
            }
        }

        private static CandidateBook Book()
        {
            if (book != null)
            {
                return book;
            }

            // A fresh city starts its dice from its own build index, so two cities differ.
            dice = new IpoDice(
                0x9E3779B97F4A7C15UL ^ SimulationManager.instance.m_currentBuildIndex
            );
            book = new CandidateBook();
            registry = new CompanyRegistry();
            try
            {
                byte[] data = SimulationManager.instance.m_SerializableDataWrapper.LoadData(
                    CompaniesCodec.DataId
                );
                if (data != null && data.Length > 0)
                {
                    book = CompaniesCodec.Parse(Encoding.UTF8.GetString(data), dice, registry);
                    int counted = 0;
                    foreach (KeyValuePair<BuildingKey, int> kv in book.Counted)
                    {
                        counted++;
                    }

                    ModLog.Info("ipo: loaded healthy-day counts for " + counted + " business(es)");
                }
            }
            catch (Exception ex)
            {
                ModLog.Warn("ipo: load failed, starting empty: " + ex.Message);
            }

            return book;
        }
    }
}

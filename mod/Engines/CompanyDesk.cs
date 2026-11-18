using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using ColossalFramework.Math;
using FinanceOverhaul.Core;

namespace FinanceOverhaul
{
    /// <summary>
    /// Local IPOs in the running game (docs/features/local-ipos.md), first part: once per in-game
    /// day, right after the vanilla investment update, every zoned business is read from the game
    /// and handed to the library, which counts how long each has stayed healthy and what the
    /// city has of each kind (the gates and slots). Nothing lists yet. The counts live in the
    /// mod's own save data (<see cref="CompaniesCodec.DataId"/>), written when the city saves.
    /// Only runs while Options → Local IPOs is on.
    /// </summary>
    public static class CompanyDesk
    {
        private static readonly object Sync = new object();
        private static readonly IpoRules DefaultRules = new IpoRules();
        private static CandidateBook book;
        private static IpoDice dice;
        private static KindFacts[] kinds = new KindFacts[0];
        private static int lastScanDay = -1;

        /// <summary>The rules in force (game UI tests may swap them; null puts the defaults back).</summary>
        public static IpoRules Rules = DefaultRules;

        /// <summary>How the last scan went, for the log and tests.</summary>
        public static int LastBusinesses { get; private set; }
        public static double LastScanMilliseconds { get; private set; }

        /// <summary>
        /// Per kind, how many businesses failed each health condition in the last scan (a
        /// business can fail several): not top level, a problem icon, understaffed, too few
        /// jobs, land value too low; then how many passed all five. For tuning the rules.
        /// </summary>
        public static string LastHealthBreakdown { get; private set; }

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
        public static void OnInvestmentsStepped()
        {
            if (!Enabled)
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
        }

        /// <summary>Reads every zoned business now (simulation thread, or with it paused).</summary>
        public static void Scan()
        {
            lock (Sync)
            {
                Stopwatch watch = Stopwatch.StartNew();
                CandidateBook b = Book();
                IpoRules rules = Rules ?? DefaultRules;
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

                Building[] buffer = BuildingManager.instance.m_buildings.m_buffer;
                ImmaterialResourceManager land = ImmaterialResourceManager.instance;
                int businesses = 0;
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

                    businesses++;
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

                    int level0,
                        level1,
                        level2,
                        level3;
                    ai.CalculateWorkplaceCount(
                        (ItemClass.Level)level,
                        new Randomizer(i),
                        width,
                        length,
                        out level0,
                        out level1,
                        out level2,
                        out level3
                    );
                    int landValue;
                    land.CheckLocalResource(
                        ImmaterialResourceManager.Resource.LandValue,
                        buffer[i].m_position,
                        out landValue
                    );
                    const Building.Flags Gone =
                        Building.Flags.Abandoned
                        | Building.Flags.Collapsed
                        | Building.Flags.BurnedDown
                        | Building.Flags.Deleted;
                    var facts = new BusinessFacts
                    {
                        Kind = kind,
                        TopLevel = top,
                        HasProblems =
                            (buffer[i].m_flags & Gone) != 0
                            || buffer[i].m_problems.m_Problems1 != Notification.Problem1.None
                            || buffer[i].m_problems.m_Problems2 != Notification.Problem2.None,
                        FullyStaffed = buffer[i].m_allUnemployed <= rules.MaxVacancyPercent,
                        Jobs = level0 + level1 + level2 + level3,
                        LandValue = landValue,
                    };
                    int[] why = failed[kind];
                    why[0] += facts.TopLevel ? 0 : 1;
                    why[1] += facts.HasProblems ? 1 : 0;
                    why[2] += facts.FullyStaffed ? 0 : 1;
                    why[3] += facts.Jobs >= rules.MinJobs ? 0 : 1;
                    why[4] += facts.LandValue >= rules.LandValueFloor ? 0 : 1;
                    why[5] += CandidateBook.Healthy(facts, rules) ? 1 : 0;
                    b.Observe(new BuildingKey((ushort)i, buffer[i].m_buildIndex), facts, rules);
                }

                b.EndDay();
                var list = new List<KindFacts>();
                foreach (BusinessKind kind in Enum.GetValues(typeof(BusinessKind)))
                {
                    KindFacts total = totals[kind];
                    total.Candidates = b.Candidates(rules, kind).Count;
                    list.Add(total);
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
                            .Append(" fewJobs=")
                            .Append(why[3])
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
                    SimulationManager.instance.m_SerializableDataWrapper.SaveData(
                        CompaniesCodec.DataId,
                        Encoding.UTF8.GetBytes(CompaniesCodec.Format(book, dice))
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
                dice = null;
                kinds = new KindFacts[0];
                lastScanDay = -1;
                Rules = DefaultRules;
            }
        }

        /// <summary>Game UI tests: forgets the counts in memory (the save is untouched until the city saves).</summary>
        public static void ForgetCounts()
        {
            lock (Sync)
            {
                book = new CandidateBook();
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
            try
            {
                byte[] data = SimulationManager.instance.m_SerializableDataWrapper.LoadData(
                    CompaniesCodec.DataId
                );
                if (data != null && data.Length > 0)
                {
                    book = CompaniesCodec.Parse(Encoding.UTF8.GetString(data), dice);
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

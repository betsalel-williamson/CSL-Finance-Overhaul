using System;
using System.Collections.Generic;
using System.Diagnostics;
using FinanceOverhaul.Core;
using UnityEngine;

namespace FinanceOverhaul
{
    /// <summary>
    /// Dev-only stress listings for finding how many stocks the game handles (game UI scenario
    /// 51, docs/developer/testing.md). Adds copies of a share stock ("Stress 001 (dev)" …) with
    /// their own investment slots, measures the daily investment update, and removes them again.
    /// Only in --dev builds with the dev test stock option on (test saves); never touches the
    /// player's holdings. The game gives every investment type a slot up to 255 (slot 0 unused).
    /// </summary>
    public static class StressStocks
    {
        private const string CollectionName = "FinanceOverhaul.Stress";

        private static GameObject holder;
        private static readonly List<InvestmentInfo> Infos = new List<InvestmentInfo>();
        private static readonly Stopwatch StepWatch = new Stopwatch();
        private static long stepCalls;
        private static double stepTotalMs;
        private static double stepMaxMs;

        public static bool Allowed
        {
            get { return TestStockListing.Enabled; }
        }

        public static int Registered
        {
            get { return Infos.Count; }
        }

        public static bool IsStress(InvestmentAI ai)
        {
            return ai != null && ai.m_info != null && StressStock.IsStressName(ai.m_info.m_name);
        }

        /// <summary>
        /// Simulation thread. Game bug: loading a city rebuilds the investment table with every
        /// slot on the free list, including the slots the save uses. The game never adds
        /// investments after loading, so it never notices; anything that does (the Index, the
        /// test stock, local listings) would get a used slot once the top ones run out and
        /// overwrite a held investment (found by scenario 51: 13 vanilla stocks overwritten).
        /// This rebuilds the free list from the slots actually in use. Idempotent.
        /// </summary>
        public static void RepairFreeSlots()
        {
            Array8<Investment> table = EconomyManager.instance.m_investments;
            table.ClearUnused();
            int free = 0;
            for (int i = (int)table.m_size - 1; i >= 1; i--)
            {
                if ((table.m_buffer[i].m_flags & Investment.Flags.Created) == 0)
                {
                    table.ReleaseItem((byte)i);
                    free++;
                }
            }

            ModLog.Info("investments: free-slot list rebuilt (" + free + " free)");
        }

        /// <summary>
        /// Investments in use (all, not only stress ones). Slot 0 is the game's empty
        /// placeholder (flagged, no type, never saved), so it is not counted.
        /// </summary>
        public static int SlotsUsed()
        {
            int n = 0;
            Investment[] buffer = EconomyManager.instance.m_investments.m_buffer;
            for (int i = 1; i < buffer.Length; i++)
            {
                if ((buffer[i].m_flags & Investment.Flags.Created) != 0)
                {
                    n++;
                }
            }

            return n;
        }

        /// <summary>
        /// Main thread: registers <paramref name="count"/> more stress prefabs, then (simulation
        /// thread) asks the game for a slot for each. Results are logged as
        /// "stress: added …".
        /// </summary>
        public static void Add(int count)
        {
            InvestmentInfo source = IndexListing.FindLoadedShareSource();
            if (source == null)
            {
                ModLog.Warn("stress: no share stock to copy");
                return;
            }

            if (holder == null)
            {
                holder = new GameObject("FinanceOverhaulStressHolder");
                holder.SetActive(false);
                UnityEngine.Object.DontDestroyOnLoad(holder);
            }

            var created = new List<InvestmentInfo>();
            for (int i = 0; i < count; i++)
            {
                string name = StressStock.PrefabName(Infos.Count + created.Count + 1);
                GameObject clone = (GameObject)
                    UnityEngine.Object.Instantiate(source.gameObject, holder.transform);
                clone.name = name;
                InvestmentInfo info = clone.GetComponent<InvestmentInfo>();
                info.m_name = name;
                info.m_investmentAI = clone.GetComponent<InvestmentAI>();
                info.m_unlockingMilestone = null;
                if (info.m_investmentAI != null)
                {
                    info.m_investmentAI.m_info = info;
                }

                created.Add(info);
            }

            Stopwatch sw = Stopwatch.StartNew();
            PrefabCollection<InvestmentInfo>.InitializePrefabs(
                CollectionName,
                created.ToArray(),
                null
            );
            foreach (InvestmentInfo info in created)
            {
                if (info.m_investmentAI != null)
                {
                    info.m_investmentAI.m_info = info; // InitializePrefab can reset the link
                }
            }

            PrefabCollection<InvestmentInfo>.BindPrefabs();
            double registerMs = sw.Elapsed.TotalMilliseconds;
            Infos.AddRange(created);
            InvestmentInfo[] toList = created.ToArray();
            int prefabs = PrefabCollection<InvestmentInfo>.LoadedCount();
            SimulationManager.instance.AddAction(() =>
            {
                RepairFreeSlots();
                CreateSlots(toList, registerMs, prefabs);
            });
        }

        private static void CreateSlots(InvestmentInfo[] infos, double registerMs, int prefabs)
        {
            int listed = 0,
                failed = 0,
                errors = 0;
            string firstError = null;

            // Guard: never take a slot that already holds an investment (the game's own stocks,
            // the Index, the player's holdings). Stop at the first sign of it.
            Investment[] buffer = EconomyManager.instance.m_investments.m_buffer;
            var before = (Investment[])buffer.Clone();
            var used = new bool[buffer.Length];
            for (int i = 0; i < buffer.Length; i++)
            {
                used[i] = (buffer[i].m_flags & Investment.Flags.Created) != 0;
            }

            foreach (InvestmentInfo info in infos)
            {
                try
                {
                    byte id;
                    bool ok = EconomyManager.instance.CreateInvestment(
                        out id,
                        ref SimulationManager.instance.m_randomizer,
                        info
                    );
                    if (ok && used[id])
                    {
                        buffer[id] = before[id]; // put the holding back (shares, money spent)
                        ModLog.Warn(
                            "stress: STOPPED: the game handed out slot "
                                + id
                                + ", which was already in use; that slot was put back"
                        );
                        errors++;
                        break;
                    }

                    if (ok)
                    {
                        used[id] = true;
                        listed++;
                    }
                    else
                    {
                        failed++;
                    }
                }
                catch (Exception ex)
                {
                    errors++;
                    if (firstError == null)
                    {
                        firstError = ex.GetType().Name + ": " + ex.Message;
                    }
                }
            }

            ModLog.Info(
                "stress: added "
                    + infos.Length
                    + " (listed "
                    + listed
                    + ", no slot "
                    + failed
                    + ", errors "
                    + errors
                    + (firstError == null ? "" : " first: " + firstError)
                    + "); slots used "
                    + SlotsUsed()
                    + "/"
                    + StressStock.GameSlotLimit
                    + ", investment types "
                    + prefabs
                    + ", register "
                    + registerMs.ToString("0.0")
                    + " ms"
            );
        }

        /// <summary>Main thread: frees the stress slots (simulation thread) and then the prefabs.</summary>
        public static void ClearSlots()
        {
            SimulationManager.instance.AddAction(() =>
            {
                int freed = 0;
                Investment[] buffer = EconomyManager.instance.m_investments.m_buffer;
                for (int i = 1; i < buffer.Length; i++)
                {
                    if (
                        (buffer[i].m_flags & Investment.Flags.Created) != 0
                        && buffer[i].Info != null
                        && StressStock.IsStressName(buffer[i].Info.m_name)
                    )
                    {
                        buffer[i].m_flags = Investment.Flags.None;
                        EconomyManager.instance.m_investments.ReleaseItem((byte)i);
                        freed++;
                    }
                }

                ModLog.Info("stress: freed " + freed + " slot(s); slots used " + SlotsUsed());
            });
        }

        /// <summary>Main thread, after <see cref="ClearSlots"/> ran: removes the prefabs.</summary>
        public static void DestroyPrefabs()
        {
            if (Infos.Count > 0)
            {
                try
                {
                    PrefabCollection<InvestmentInfo>.DestroyPrefabs(
                        CollectionName,
                        Infos.ToArray(),
                        null
                    );
                }
                catch (Exception ex)
                {
                    ModLog.Warn("stress: destroy prefabs failed: " + ex.Message);
                }
            }

            Infos.Clear();
            if (holder != null)
            {
                UnityEngine.Object.Destroy(holder);
                holder = null;
            }

            ModLog.Info("stress: removed the stress listings");
        }

        /// <summary>
        /// Main thread: sends the game's "investments updated" signal, which makes the Economy
        /// panel rebuild its lists (the game only sends it while a city loads).
        /// </summary>
        public static void NotifyInvestmentsChanged()
        {
            try
            {
                HarmonyLib
                    .AccessTools.Method(typeof(EconomyManager), "InvestmentsUpdated")
                    .Invoke(EconomyManager.instance, null);
            }
            catch (Exception ex)
            {
                ModLog.Warn("stress: investments-updated signal failed: " + ex.Message);
            }
        }

        /// <summary>Simulation thread: every slot in use, by number (for checking slot 0).</summary>
        public static void LogSlots()
        {
            Investment[] buffer = EconomyManager.instance.m_investments.m_buffer;
            var sb = new System.Text.StringBuilder();
            int stress = 0;
            for (int i = 1; i < buffer.Length; i++)
            {
                if ((buffer[i].m_flags & Investment.Flags.Created) == 0)
                {
                    continue;
                }

                InvestmentInfo info = buffer[i].Info;
                string name = info == null ? "(no type)" : info.m_name;
                if (StressStock.IsStressName(name))
                {
                    stress++;
                    continue;
                }

                sb.Append(" #")
                    .Append(i)
                    .Append('=')
                    .Append(name)
                    .Append(" shares=")
                    .Append(buffer[i].m_playerOwnedShareCount);
            }

            ModLog.Info("slots:" + sb + " (+" + stress + " stress)");
        }

        // ---------- measuring ----------

        public static void ResetStepTiming()
        {
            stepCalls = 0;
            stepTotalMs = 0;
            stepMaxMs = 0;
        }

        /// <summary>Prefix of EconomyManager.SimulationStepInvestments (simulation thread).</summary>
        public static volatile bool Measuring;

        public static void StepBegin()
        {
            if (Measuring)
            {
                StepWatch.Reset();
                StepWatch.Start();
            }
        }

        /// <summary>Postfix of EconomyManager.SimulationStepInvestments (simulation thread).</summary>
        public static void StepEnd()
        {
            if (!StepWatch.IsRunning)
            {
                return;
            }

            StepWatch.Stop();
            double ms = StepWatch.Elapsed.TotalMilliseconds;
            stepCalls++;
            stepTotalMs += ms;
            if (ms > stepMaxMs)
            {
                stepMaxMs = ms;
            }
        }

        public static string StepSummary()
        {
            return "investment step "
                + stepCalls
                + " call(s), avg "
                + (stepCalls == 0 ? 0 : stepTotalMs / stepCalls).ToString("0.000")
                + " ms, max "
                + stepMaxMs.ToString("0.000")
                + " ms";
        }
    }
}

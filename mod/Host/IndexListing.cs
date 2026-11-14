#if HAS_CITIES
using System;
using System.Collections.Generic;
using FinanceOverhaul.Market;
using UnityEngine;

namespace FinanceOverhaul
{
    /// <summary>
    /// Registers the <c>Index</c> investment prefab and drives its mark from the
    /// sector basket. Vanilla <c>EconomyManager.LateUpdateData</c> creates one
    /// investment per loaded <see cref="InvestmentInfo"/>, so registering the
    /// prefab while the investment collection loads is enough to list it, save it
    /// and trade it. If the mod is removed, vanilla drops the orphaned listing.
    /// Basket rules live in <see cref="CityIndexBasket"/>.
    /// </summary>
    public static class IndexListing
    {
        private const string CollectionName = "FinanceOverhaul.Index";

        private static GameObject holder;
        private static InvestmentInfo indexInfo;

        private static readonly List<string> Names = new List<string>(16);
        private static readonly List<float> Last = new List<float>(16);
        private static readonly List<float> Prev = new List<float>(16);
        private static readonly List<bool> Eligible = new List<bool>(16);

        private static bool markFailureLogged;

        public static bool IsRegistered
        {
            get { return indexInfo != null; }
        }

        /// <summary>Postfix target: InvestmentCollection.Awake (city loading).</summary>
        public static void Register(InvestmentCollection collection)
        {
            if (collection == null || collection.m_prefabs == null)
            {
                return;
            }

            if (Mod.Settings == null || !Mod.Settings.IndexStockEnabled)
            {
                ModLog.Info("index: experimental Index stock disabled in Options; not registering");
                return;
            }

            if (indexInfo != null || TryAdoptExisting())
            {
                return;
            }

            InvestmentInfo source = FindSectorSource(collection.m_prefabs);
            if (source == null)
            {
                ModLog.Info(
                    "index: collection '"
                        + collection.name
                        + "' has no sector stock to clone; skipping"
                );
                return;
            }

            RegisterFrom(source, false);
        }

        /// <summary>
        /// Lists Index in an already-loaded city: called when the toggle is turned
        /// on, after a hot reload (PatchAll), and after a level loads. Main thread.
        /// Registers (or adopts) the prefab, binds it, then queues investment
        /// creation on the simulation thread exactly like EconomyManager.LateUpdateData.
        /// </summary>
        public static void EnsureInLoadedCity()
        {
            try
            {
                if (Mod.Settings == null || !Mod.Settings.IndexStockEnabled)
                {
                    return;
                }

                if (!LoadingManager.exists || !LoadingManager.instance.m_loadingComplete)
                {
                    return;
                }

                if (indexInfo == null && !TryAdoptExisting())
                {
                    InvestmentInfo source = FindLoadedSectorSource();
                    if (source == null)
                    {
                        ModLog.Info("index: no sector stock loaded (Financial Districts missing?)");
                        return;
                    }

                    if (!RegisterFrom(source, true))
                    {
                        return;
                    }
                }

                InvestmentInfo info = indexInfo;
                SimulationManager.instance.AddAction(() => CreateInvestmentIfMissing(info));
            }
            catch (Exception ex)
            {
                ModLog.Warn(
                    "index: mid-game listing failed, vanilla stocks untouched: " + ex.Message
                );
            }
        }

        private static bool TryAdoptExisting()
        {
            // Hot reload: an older copy of this mod may already have registered Index.
            if (!PrefabCollection<InvestmentInfo>.LoadedExists(CityIndexBasket.IndexName))
            {
                return false;
            }

            indexInfo = PrefabCollection<InvestmentInfo>.FindLoaded(CityIndexBasket.IndexName);
            markFailureLogged = false;
            ModLog.Info("index: adopted already-registered Index prefab");
            // Repairs an Index registered by an older build whose AI lost its back-link.
            RepairLinks(indexInfo);
            return indexInfo != null;
        }

        private static bool RegisterFrom(InvestmentInfo source, bool bindNow)
        {
            try
            {
                holder = new GameObject("FinanceOverhaulIndexHolder");
                holder.SetActive(false);
                UnityEngine.Object.DontDestroyOnLoad(holder);

                GameObject clone = (GameObject)
                    UnityEngine.Object.Instantiate(source.gameObject, holder.transform);
                clone.name = CityIndexBasket.IndexName;
                InvestmentInfo info = clone.GetComponent<InvestmentInfo>();
                if (info == null)
                {
                    ModLog.Warn("index: cloned prefab lost InvestmentInfo; skipping");
                    DestroyHolder();
                    return false;
                }

                info.m_name = CityIndexBasket.IndexName;
                info.m_investmentAI = clone.GetComponent<InvestmentAI>();
                // Index is always purchasable; it does not inherit the source's unlock.
                info.m_unlockingMilestone = null;

                PrefabCollection<InvestmentInfo>.InitializePrefabs(
                    CollectionName,
                    new[] { info },
                    null
                );
                RepairLinks(info);
                if (bindNow)
                {
                    // Mid-game: give the new prefab a simulation index now (BindPrefabs
                    // appends unbound scene prefabs) so Investment.Info resolves.
                    PrefabCollection<InvestmentInfo>.BindPrefabs();
                }

                indexInfo = info;
                markFailureLogged = false;
                ModLog.Info(
                    "index: registered prefab cloned from '"
                        + source.name
                        + "' index="
                        + info.m_prefabDataIndex
                );
                return true;
            }
            catch (Exception ex)
            {
                ModLog.Warn("index: register failed, vanilla stocks untouched: " + ex.Message);
                indexInfo = null;
                DestroyHolder();
                return false;
            }
        }

        /// <summary>
        /// Instantiate does not carry the AI's back-link (InvestmentAI.m_info), and the
        /// clone keeps the source's "already initialized" flag, so vanilla
        /// InvestmentInfo.InitializePrefab never sets it. A null m_info throws in
        /// InvestmentAI.IsAvailable on every simulation tick, so set it explicitly.
        /// </summary>
        private static void RepairLinks(InvestmentInfo info)
        {
            if (info == null)
            {
                return;
            }

            if (info.m_investmentAI == null)
            {
                info.m_investmentAI = info.GetComponent<InvestmentAI>();
            }

            if (info.m_investmentAI == null)
            {
                ModLog.Warn("index: Index prefab has no InvestmentAI component");
                return;
            }

            bool wasBroken = info.m_investmentAI.m_info != info;
            info.m_investmentAI.m_info = info;
            ModLog.Info(
                "index: AI linked to Index info (was "
                    + (wasBroken ? "broken, repaired" : "ok")
                    + ", ai="
                    + info.m_investmentAI.GetType().Name
                    + ")"
            );
        }

        /// <summary>Simulation thread.</summary>
        private static void CreateInvestmentIfMissing(InvestmentInfo info)
        {
            try
            {
                if (info == null)
                {
                    return;
                }

                int existing = FindIndexInvestment();
                if (existing >= 0)
                {
                    // Already listed (e.g. loaded from a save): fill any missing older days.
                    BackfillHistory(ref EconomyManager.instance.m_investments.m_buffer[existing]);
                    return;
                }

                byte id;
                if (
                    !EconomyManager.instance.CreateInvestment(
                        out id,
                        ref SimulationManager.instance.m_randomizer,
                        info
                    )
                )
                {
                    ModLog.Warn("index: no free investment slot; Index not listed");
                    return;
                }

                // Idempotent with the CreateInvestment postfix (no-op once history is full).
                BackfillHistory(ref EconomyManager.instance.m_investments.m_buffer[id]);
                InvestmentInfo bound = EconomyManager.instance.m_investments.m_buffer[id].Info;
                ModLog.Info(
                    "index: created investment id="
                        + id
                        + " info="
                        + (bound == null ? "null" : bound.m_name)
                        + " value="
                        + EconomyManager.instance.m_investments.m_buffer[id].value
                        + " (reopen Economy to see it)"
                );
            }
            catch (Exception ex)
            {
                ModLog.Warn("index: create investment failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Postfix target: InvestmentAI.CreateInvestment, and called for an Index that
        /// already exists when a city is listed. Rebuilds the days before Index's own
        /// oldest point from the members' vanilla histories, so a new or re-created
        /// Index shows the same window of history as every other stock instead of a
        /// flat line. Real Index points are kept as they are.
        /// </summary>
        public static void BackfillHistory(ref Investment data)
        {
            try
            {
                Investment.Queue<float> own = data.allValues;
                int ownCount = own == null ? 0 : own.Count;
                if (ownCount == 0)
                {
                    return;
                }

                var names = new List<string>(16);
                var histories = new List<float[]>(16);
                var eligible = new List<bool>(16);
                ReadBasketHistories(names, histories, eligible);

                float[] older = CityIndexBasket.BackfillOlder(
                    own.GetFromTop(ownCount - 1),
                    ownCount,
                    names,
                    histories,
                    eligible,
                    Investment.kPastValueCount
                );
                if (older.Length == 0)
                {
                    return;
                }

                var ownNewestFirst = new float[ownCount];
                for (int j = 0; j < ownCount; j++)
                {
                    ownNewestFirst[j] = own.GetFromTop(j);
                }

                // Queue.Add appends newest, so add oldest first.
                data.ClearValues();
                for (int j = older.Length - 1; j >= 0; j--)
                {
                    data.AddValue(older[j]);
                }

                for (int j = ownCount - 1; j >= 0; j--)
                {
                    data.AddValue(ownNewestFirst[j]);
                }

                ModLog.Info(
                    "index: backfilled "
                        + older.Length
                        + " older day(s) before "
                        + ownCount
                        + " real point(s); oldest="
                        + older[older.Length - 1].ToString("0.##")
                        + " newest="
                        + ownNewestFirst[0].ToString("0.##")
                );
            }
            catch (Exception ex)
            {
                ModLog.Warn(
                    "index: history backfill failed, keeping real points only: " + ex.Message
                );
            }
        }

        private static void ReadBasketHistories(
            List<string> names,
            List<float[]> histories,
            List<bool> eligible
        )
        {
            Investment[] buffer = EconomyManager.instance.m_investments.m_buffer;
            for (int i = 0; i < buffer.Length; i++)
            {
                if ((buffer[i].m_flags & Investment.Flags.Created) == 0)
                {
                    continue;
                }

                InvestmentInfo info = buffer[i].Info;
                if (info == null || info.m_investmentAI == null)
                {
                    continue;
                }

                Investment.Queue<float> history = buffer[i].allValues;
                int count = history == null ? 0 : history.Count;
                var newestFirst = new float[count];
                for (int j = 0; j < count; j++)
                {
                    newestFirst[j] = history.GetFromTop(j);
                }

                names.Add(info.m_name);
                histories.Add(newestFirst);
                eligible.Add(
                    IsSectorStock(info) && info.m_investmentAI.IsAvailable((byte)i, ref buffer[i])
                );
            }
        }

        private static int FindIndexInvestment()
        {
            Investment[] buffer = EconomyManager.instance.m_investments.m_buffer;
            for (int i = 0; i < buffer.Length; i++)
            {
                if ((buffer[i].m_flags & Investment.Flags.Created) == 0)
                {
                    continue;
                }

                InvestmentInfo info = buffer[i].Info;
                if (info != null && CityIndexBasket.IsIndexName(info.m_name))
                {
                    return i;
                }
            }

            return -1;
        }

        private static InvestmentInfo FindLoadedSectorSource()
        {
            int count = PrefabCollection<InvestmentInfo>.LoadedCount();
            var loaded = new InvestmentInfo[count];
            for (int i = 0; i < count; i++)
            {
                loaded[i] = PrefabCollection<InvestmentInfo>.GetLoaded((uint)i);
            }

            return FindSectorSource(loaded);
        }

        public static void Unregister()
        {
            if (indexInfo == null)
            {
                return;
            }

            try
            {
                PrefabCollection<InvestmentInfo>.DestroyPrefabs(
                    CollectionName,
                    new[] { indexInfo },
                    null
                );
            }
            catch (Exception ex)
            {
                ModLog.Warn("index: unregister failed: " + ex.Message);
            }

            if (holder == null && indexInfo != null)
            {
                // Adopted from an older assembly: destroy its holder too.
                Transform parent = indexInfo.transform.parent;
                UnityEngine.Object.Destroy(
                    parent != null ? parent.gameObject : indexInfo.gameObject
                );
            }

            indexInfo = null;
            DestroyHolder();
            ModLog.Info("index: unregistered prefab");
        }

        public static bool IsIndex(InvestmentAI ai)
        {
            return ai != null && ai.m_info != null && CityIndexBasket.IsIndexName(ai.m_info.m_name);
        }

        /// <summary>
        /// Postfix target: InvestmentAI.SafeCalculateValue (after vanilla random
        /// variance, so Index carries none). Index level is chain-linked:
        /// previous level × (1 + mean latest return of unlocked share stocks).
        /// Seeds from the price average when Index has no history yet.
        /// </summary>
        public static void ApplyMark(InvestmentAI ai, ref Investment data, ref float value)
        {
            if (!IsIndex(ai))
            {
                return;
            }

            try
            {
                float previous =
                    data.allValues != null && data.allValues.Count > 0 ? data.value : 0f;
                ReadBasket();
                float mark;
                string mode;
                if (
                    CityIndexBasket.TryComputeChainedMark(
                        previous,
                        Names,
                        Last,
                        Prev,
                        Eligible,
                        out mark
                    )
                )
                {
                    mode = "chain";
                }
                else if (
                    !(previous > 0f)
                    && CityIndexBasket.TryComputeMark(Names, Last, Eligible, out mark)
                )
                {
                    mode = "seed";
                }
                else if (previous > 0f)
                {
                    mark = previous;
                    mode = "hold";
                }
                else
                {
                    if (!markFailureLogged)
                    {
                        markFailureLogged = true;
                        ModLog.Warn(
                            "index: no unlocked share prices readable; keeping vanilla mark"
                        );
                    }

                    return;
                }

                value = mark;
                LogUpdate(mode, previous, mark);
            }
            catch (Exception ex)
            {
                if (!markFailureLogged)
                {
                    markFailureLogged = true;
                    ModLog.Warn("index: mark failed, keeping vanilla mark: " + ex.Message);
                }
            }
        }

        private static int updateCount;

        /// <summary>First 20 updates after (re)load, then every 30th, to keep the log small.</summary>
        private static void LogUpdate(string mode, float previous, float mark)
        {
            updateCount++;
            if (updateCount > 20 && updateCount % 30 != 0)
            {
                return;
            }

            float change = previous > 0f ? (mark / previous - 1f) * 100f : 0f;
            ModLog.Info(
                "index: update #"
                    + updateCount
                    + " mode="
                    + mode
                    + " "
                    + previous.ToString("0.##")
                    + " -> "
                    + mark.ToString("0.##")
                    + " ("
                    + change.ToString("+0.00;-0.00")
                    + "%) members="
                    + CountEligible()
            );
        }

        /// <summary>One-line readout for the log after a level loads.</summary>
        public static void LogStatus()
        {
            try
            {
                if (indexInfo == null)
                {
                    ModLog.Info("index: not registered (Financial Districts missing?)");
                    return;
                }

                bool listed = false;
                float current = float.NaN;
                Investment[] buffer = EconomyManager.instance.m_investments.m_buffer;
                for (int i = 0; i < buffer.Length; i++)
                {
                    if ((buffer[i].m_flags & Investment.Flags.Created) == 0)
                    {
                        continue;
                    }

                    InvestmentInfo info = buffer[i].Info;
                    if (info != null && CityIndexBasket.IsIndexName(info.m_name))
                    {
                        listed = true;
                        current = buffer[i].value;
                        break;
                    }
                }

                TryReadBasketMark(out float basket);
                ModLog.Info(
                    "index: listed="
                        + listed
                        + " value="
                        + current
                        + " basket="
                        + basket
                        + " sectors="
                        + CountEligible()
                );
            }
            catch (Exception ex)
            {
                ModLog.Warn("index: status failed: " + ex.Message);
            }
        }

        private static void ReadBasket()
        {
            Names.Clear();
            Last.Clear();
            Prev.Clear();
            Eligible.Clear();

            Investment[] buffer = EconomyManager.instance.m_investments.m_buffer;
            for (int i = 0; i < buffer.Length; i++)
            {
                if ((buffer[i].m_flags & Investment.Flags.Created) == 0)
                {
                    continue;
                }

                InvestmentInfo info = buffer[i].Info;
                if (info == null || info.m_investmentAI == null)
                {
                    continue;
                }

                Investment.Queue<float> history = buffer[i].allValues;
                int count = history == null ? 0 : history.Count;
                Names.Add(info.m_name);
                // GetFromTop(0) is the newest value, GetFromTop(1) the one before it.
                Last.Add(count > 0 ? history.GetFromTop(0) : float.NaN);
                Prev.Add(count > 1 ? history.GetFromTop(1) : float.NaN);
                Eligible.Add(
                    IsSectorStock(info) && info.m_investmentAI.IsAvailable((byte)i, ref buffer[i])
                );
            }
        }

        private static bool TryReadBasketMark(out float mark)
        {
            ReadBasket();
            return CityIndexBasket.TryComputeMark(Names, Last, Eligible, out mark);
        }

        private static int CountEligible()
        {
            int n = 0;
            for (int i = 0; i < Eligible.Count; i++)
            {
                if (Eligible[i] && !CityIndexBasket.IsIndexName(Names[i]))
                {
                    n++;
                }
            }

            return n;
        }

        /// <summary>
        /// Basket member: any share-priced investment (not coin-priced ones such as
        /// crypto). Index itself is excluded by name in CityIndexBasket.
        /// </summary>
        private static bool IsSectorStock(InvestmentInfo info)
        {
            return info.m_investmentAI != null
                && info.m_investmentAI.m_unit == InvestmentAI.Unit.Share;
        }

        /// <summary>
        /// Picks a share-priced investment prefab to clone, preferring one with no
        /// unlock milestone. Logs every candidate so a miss is diagnosable.
        /// </summary>
        private static InvestmentInfo FindSectorSource(InvestmentInfo[] prefabs)
        {
            InvestmentInfo fallback = null;
            for (int i = 0; i < prefabs.Length; i++)
            {
                InvestmentInfo p = prefabs[i];
                if (p == null)
                {
                    continue;
                }

                InvestmentAI ai = p.GetComponent<InvestmentAI>();
                ModLog.Info(
                    "index: candidate '"
                        + p.name
                        + "' ai="
                        + (ai == null ? "null" : ai.GetType().Name)
                        + " unit="
                        + (ai == null ? "?" : ai.m_unit.ToString())
                        + " milestone="
                        + (p.m_unlockingMilestone == null ? "none" : p.m_unlockingMilestone.name)
                );
                if (CityIndexBasket.IsIndexName(p.name))
                {
                    continue;
                }

                if (ai == null || ai.m_unit != InvestmentAI.Unit.Share)
                {
                    continue;
                }

                if (p.m_unlockingMilestone == null)
                {
                    return p;
                }

                if (fallback == null)
                {
                    fallback = p;
                }
            }

            return fallback;
        }

        private static void DestroyHolder()
        {
            if (holder != null)
            {
                try
                {
                    UnityEngine.Object.Destroy(holder);
                }
                catch
                {
                    // fail soft
                }
            }

            holder = null;
        }
    }
}
#endif

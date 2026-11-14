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
        private static readonly List<float> Values = new List<float>(16);
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
                if (info == null || FindIndexInvestment() >= 0)
                {
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

        /// <summary>Overrides the Index mark with the equal-weight sector basket.</summary>
        public static void ApplyMark(InvestmentAI ai, ref float value)
        {
            if (!IsIndex(ai))
            {
                return;
            }

            try
            {
                if (TryReadBasketMark(out float mark))
                {
                    value = mark;
                }
                else if (!markFailureLogged)
                {
                    markFailureLogged = true;
                    ModLog.Warn("index: no sector prices readable; keeping vanilla mark");
                }
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

        private static bool TryReadBasketMark(out float mark)
        {
            Names.Clear();
            Values.Clear();
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

                Names.Add(info.m_name);
                Values.Add(buffer[i].value);
                Eligible.Add(IsSectorStock(info));
            }

            return CityIndexBasket.TryComputeMark(Names, Values, Eligible, out mark);
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

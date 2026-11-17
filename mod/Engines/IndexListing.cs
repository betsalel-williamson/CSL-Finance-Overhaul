using System;
using System.Collections.Generic;
using FinanceOverhaul.Core;
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
    public static partial class IndexListing
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

        /// <summary>Shares restored from the copy since this city loaded (0 if none), for the Welcome back dialog.</summary>
        public static long RestoredShares { get; private set; }

        private static volatile bool restoredNoticeDue;

        public static void Unregister()
        {
            if (indexInfo == null)
            {
                return;
            }

            SaveCopy(); // remember the holding before the slot loses its prefab

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

        private static int updateCount;

        // null = not read yet for this city; true = done (marker in the city's mod data).
        private static bool? rebaseDone;
        private static int lastRebaseWaitDay = -1;

        /// <summary>
        /// Basket member: any share-priced investment (not coin-priced ones such as
        /// crypto). Index itself is excluded by name in CityIndexBasket.
        /// </summary>
        private static bool IsSectorStock(InvestmentInfo info)
        {
            return info.m_investmentAI != null
                && info.m_investmentAI.m_unit == InvestmentAI.Unit.Share
                && IndexDay.IsMemberName(info.m_name);
        }

        /// <summary>A share-priced prefab to clone (also used by the dev test listing).</summary>
        internal static InvestmentInfo FindShareSource(InvestmentInfo[] prefabs)
        {
            return FindSectorSource(prefabs);
        }

        internal static InvestmentInfo FindLoadedShareSource()
        {
            return FindLoadedSectorSource();
        }

        /// <summary>
        /// Picks a share-priced investment prefab to clone, preferring one with no
        /// unlock milestone. Logs every candidate so a miss is diagnosable.
        /// </summary>
        private static InvestmentInfo FindSectorSource(InvestmentInfo[] prefabs)
        {
            var candidates = new List<IndexSource>(prefabs.Length);
            for (int i = 0; i < prefabs.Length; i++)
            {
                InvestmentInfo p = prefabs[i];
                InvestmentAI ai = p == null ? null : p.GetComponent<InvestmentAI>();
                candidates.Add(
                    new IndexSource
                    {
                        Name = p == null ? null : p.name,
                        SharePriced = ai != null && ai.m_unit == InvestmentAI.Unit.Share,
                        HasMilestone = p != null && p.m_unlockingMilestone != null,
                    }
                );
                if (p != null)
                {
                    ModLog.Info(
                        "index: candidate '"
                            + p.name
                            + "' ai="
                            + (ai == null ? "null" : ai.GetType().Name)
                            + " unit="
                            + (ai == null ? "?" : ai.m_unit.ToString())
                            + " milestone="
                            + (
                                p.m_unlockingMilestone == null
                                    ? "none"
                                    : p.m_unlockingMilestone.name
                            )
                    );
                }
            }

            int pick = IndexDay.PickSource(candidates);
            return pick < 0 ? null : prefabs[pick];
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

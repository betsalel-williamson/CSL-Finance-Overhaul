using System;
using FinanceOverhaul.Market;
using UnityEngine;

namespace FinanceOverhaul
{
    /// <summary>
    /// Dev-only "Test Stock (dev)" (TST) for game UI scenarios: a cloned share listing
    /// (same mechanism as <see cref="IndexListing"/>) whose price never moves unless a
    /// test pins one with <see cref="SetPrice"/> (applied at the next daily update).
    /// Listed only in --dev builds with the dev Options toggle on; it adds an investment
    /// to the save, so use a test save. Never part of the Index basket.
    /// </summary>
    public static class TestStockListing
    {
        private const string CollectionName = "FinanceOverhaul.TestStock";

        private static GameObject holder;
        private static InvestmentInfo info;
        private static float pinned;

        public static bool Enabled
        {
            get
            {
                return BuildInfo.ShowDevBuildIdentity
                    && Mod.Settings != null
                    && Mod.Settings.DevTestStockEnabled;
            }
        }

        public static bool IsTest(InvestmentAI ai)
        {
            return ai != null && ai.m_info != null && TestStock.IsTestName(ai.m_info.m_name);
        }

        /// <summary>Test step: the price from the next daily update on (0 = keep flat).</summary>
        public static void SetPrice(float price)
        {
            pinned = price;
            ModLog.Info("test stock: next daily price pinned to " + price);
        }

        /// <summary>Postfix on SafeCalculateValue: no random variance, only pinned moves.</summary>
        public static void ApplyMark(InvestmentAI ai, ref Investment data, ref float value)
        {
            if (!IsTest(ai))
            {
                return;
            }

            float current = data.allValues != null && data.allValues.Count > 0 ? data.value : 0f;
            value = TestStock.NextMark(pinned, current);
        }

        /// <summary>Postfix target: InvestmentCollection.Awake (city loading).</summary>
        public static void Register(InvestmentCollection collection)
        {
            if (!Enabled || collection == null || collection.m_prefabs == null)
            {
                return;
            }

            if (info != null || TryAdoptExisting())
            {
                return;
            }

            InvestmentInfo source = IndexListing.FindShareSource(collection.m_prefabs);
            if (source != null)
            {
                RegisterFrom(source, false);
            }
        }

        /// <summary>Main thread: toggle on, hot reload, level loaded.</summary>
        public static void EnsureInLoadedCity()
        {
            try
            {
                if (
                    !Enabled
                    || !LoadingManager.exists
                    || !LoadingManager.instance.m_loadingComplete
                )
                {
                    return;
                }

                if (info == null && !TryAdoptExisting())
                {
                    InvestmentInfo source = IndexListing.FindLoadedShareSource();
                    if (source == null || !RegisterFrom(source, true))
                    {
                        return;
                    }
                }

                InvestmentInfo listed = info;
                SimulationManager.instance.AddAction(() => CreateIfMissing(listed));
            }
            catch (Exception ex)
            {
                ModLog.Warn("test stock: listing failed: " + ex.Message);
            }
        }

        public static void Unregister()
        {
            if (info == null)
            {
                return;
            }

            try
            {
                PrefabCollection<InvestmentInfo>.DestroyPrefabs(
                    CollectionName,
                    new[] { info },
                    null
                );
            }
            catch (Exception ex)
            {
                ModLog.Warn("test stock: unregister failed: " + ex.Message);
            }

            if (holder == null)
            {
                Transform parent = info.transform.parent;
                UnityEngine.Object.Destroy(parent != null ? parent.gameObject : info.gameObject);
            }

            info = null;
            DestroyHolder();
        }

        private static bool TryAdoptExisting()
        {
            if (!PrefabCollection<InvestmentInfo>.LoadedExists(TestStock.PrefabName))
            {
                return false;
            }

            info = PrefabCollection<InvestmentInfo>.FindLoaded(TestStock.PrefabName);
            Link(info);
            return info != null;
        }

        private static bool RegisterFrom(InvestmentInfo source, bool bindNow)
        {
            try
            {
                holder = new GameObject("FinanceOverhaulTestStockHolder");
                holder.SetActive(false);
                UnityEngine.Object.DontDestroyOnLoad(holder);
                GameObject clone = (GameObject)
                    UnityEngine.Object.Instantiate(source.gameObject, holder.transform);
                clone.name = TestStock.PrefabName;
                InvestmentInfo created = clone.GetComponent<InvestmentInfo>();
                if (created == null)
                {
                    DestroyHolder();
                    return false;
                }

                created.m_name = TestStock.PrefabName;
                created.m_investmentAI = clone.GetComponent<InvestmentAI>();
                created.m_unlockingMilestone = null;
                PrefabCollection<InvestmentInfo>.InitializePrefabs(
                    CollectionName,
                    new[] { created },
                    null
                );
                Link(created);
                if (bindNow)
                {
                    PrefabCollection<InvestmentInfo>.BindPrefabs();
                }

                info = created;
                ModLog.Info("test stock: registered (cloned from '" + source.name + "')");
                return true;
            }
            catch (Exception ex)
            {
                ModLog.Warn("test stock: register failed: " + ex.Message);
                info = null;
                DestroyHolder();
                return false;
            }
        }

        private static void Link(InvestmentInfo target)
        {
            if (target == null)
            {
                return;
            }

            if (target.m_investmentAI == null)
            {
                target.m_investmentAI = target.GetComponent<InvestmentAI>();
            }

            if (target.m_investmentAI != null)
            {
                target.m_investmentAI.m_info = target; // Instantiate drops this back-link
            }
        }

        /// <summary>Simulation thread.</summary>
        private static void CreateIfMissing(InvestmentInfo listed)
        {
            try
            {
                Investment[] buffer = EconomyManager.instance.m_investments.m_buffer;
                for (int i = 0; i < buffer.Length; i++)
                {
                    if (
                        (buffer[i].m_flags & Investment.Flags.Created) != 0
                        && buffer[i].Info != null
                        && TestStock.IsTestName(buffer[i].Info.m_name)
                    )
                    {
                        return;
                    }
                }

                byte id;
                if (
                    EconomyManager.instance.CreateInvestment(
                        out id,
                        ref SimulationManager.instance.m_randomizer,
                        listed
                    )
                )
                {
                    ModLog.Info("test stock: listed TST id=" + id + " value=" + buffer[id].value);
                }
                else
                {
                    ModLog.Warn("test stock: no free investment slot");
                }
            }
            catch (Exception ex)
            {
                ModLog.Warn("test stock: create failed: " + ex.Message);
            }
        }

        private static void DestroyHolder()
        {
            if (holder != null)
            {
                UnityEngine.Object.Destroy(holder);
            }

            holder = null;
        }
    }
}

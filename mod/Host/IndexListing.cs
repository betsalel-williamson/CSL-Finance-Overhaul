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

        public static void Register(InvestmentCollection collection)
        {
            if (indexInfo != null || collection == null || collection.m_prefabs == null)
            {
                return;
            }

            if (Mod.Settings == null || !Mod.Settings.IndexStockEnabled)
            {
                ModLog.Info("index: experimental Index stock disabled in Options; not registering");
                return;
            }

            try
            {
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
                    return;
                }

                info.m_name = CityIndexBasket.IndexName;
                info.m_investmentAI = clone.GetComponent<InvestmentAI>();

                PrefabCollection<InvestmentInfo>.InitializePrefabs(
                    CollectionName,
                    new[] { info },
                    null
                );
                indexInfo = info;
                markFailureLogged = false;
                ModLog.Info("index: registered prefab cloned from '" + source.name + "'");
            }
            catch (Exception ex)
            {
                ModLog.Warn("index: register failed, vanilla stocks untouched: " + ex.Message);
                indexInfo = null;
                DestroyHolder();
            }
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

        private static bool IsSectorStock(InvestmentInfo info)
        {
            return info.m_investmentAI is RandomRiskInvestmentAI
                && info.m_investmentAI.m_unit == InvestmentAI.Unit.Share;
        }

        private static InvestmentInfo FindSectorSource(InvestmentInfo[] prefabs)
        {
            for (int i = 0; i < prefabs.Length; i++)
            {
                InvestmentInfo p = prefabs[i];
                if (p != null && CityIndexBasket.IsIndexName(p.name))
                {
                    ModLog.Warn("index: a prefab named Index already exists; not adding another");
                    return null;
                }
            }

            for (int i = 0; i < prefabs.Length; i++)
            {
                InvestmentInfo p = prefabs[i];
                if (p == null)
                {
                    continue;
                }

                InvestmentAI ai = p.GetComponent<InvestmentAI>();
                if (ai is RandomRiskInvestmentAI && ai.m_unit == InvestmentAI.Unit.Share)
                {
                    return p;
                }
            }

            return null;
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

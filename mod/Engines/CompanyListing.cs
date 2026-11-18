using System;
using System.Collections.Generic;
using ColossalFramework.Threading;
using FinanceOverhaul.Core;
using UnityEngine;

namespace FinanceOverhaul
{
    /// <summary>
    /// A local company as a stock in the game: a copy of a share stock type named after the
    /// company's id, with its own investment slot (the mechanism of the Index and the test
    /// stock). The price is the company's price in the registry; the name is the company's.
    /// The game drops a stock type it does not know when a city loads, and a company's type can
    /// only be registered once the mod has read its save data, so every company is listed again
    /// after loading and its holding and price history are put back from the registry.
    /// </summary>
    public static class CompanyListing
    {
        private const string CollectionName = "FinanceOverhaul.Companies";

        private static GameObject holder;
        private static readonly Dictionary<int, InvestmentInfo> Infos =
            new Dictionary<int, InvestmentInfo>();

        /// <summary>
        /// Runs something on the main thread: now if this is the main thread (the game's
        /// dispatcher refuses a call from its own thread), else queued.
        /// </summary>
        public static void OnMainThread(Action action)
        {
            if (Dispatcher.currentSafe == ThreadHelper.dispatcher)
            {
                action();
            }
            else
            {
                ThreadHelper.dispatcher.Dispatch(action);
            }
        }

        public static int Listed
        {
            get { return Infos.Count; }
        }

        public static bool IsCompany(InvestmentAI ai, out int companyId)
        {
            companyId = 0;
            return ai != null
                && ai.m_info != null
                && LocalCompanyNames.TryId(ai.m_info.m_name, out companyId);
        }

        /// <summary>Postfix on SafeCalculateValue: a company's price is the registry's, in ₡.</summary>
        public static void ApplyMark(InvestmentAI ai, ref float value)
        {
            int id;
            if (!IsCompany(ai, out id))
            {
                return;
            }

            long cents = CompanyDesk.PriceCentsOf(id);
            if (cents > 0)
            {
                value = cents / 100f;
            }
        }

        /// <summary>The stock type of a listed company, or null.</summary>
        public static InvestmentInfo InfoOf(int companyId)
        {
            InvestmentInfo info;
            return Infos.TryGetValue(companyId, out info) ? info : null;
        }

        /// <summary>
        /// Main thread: registers the company's stock type, then (simulation thread) gives it a
        /// slot and puts back the holding and history the registry kept.
        /// </summary>
        public static bool List(LocalCompany company)
        {
            if (Infos.ContainsKey(company.Id))
            {
                return true;
            }

            // After a hot reload of the mod the stock type (and its slot) is still in the game:
            // take it over instead of registering it twice.
            string existing = LocalCompanyNames.PrefabName(company.Id);
            if (PrefabCollection<InvestmentInfo>.LoadedExists(existing))
            {
                InvestmentInfo loaded = PrefabCollection<InvestmentInfo>.FindLoaded(existing);
                if (loaded != null)
                {
                    Infos[company.Id] = loaded;
                    return true;
                }
            }

            InvestmentInfo source = IndexListing.FindLoadedShareSource();
            if (source == null)
            {
                ModLog.Warn("ipo: no share stock to copy; " + company.Name + " not listed");
                return false;
            }

            if (holder == null)
            {
                holder = new GameObject("FinanceOverhaulCompaniesHolder");
                holder.SetActive(false);
                UnityEngine.Object.DontDestroyOnLoad(holder);
            }

            string name = LocalCompanyNames.PrefabName(company.Id);
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

            PrefabCollection<InvestmentInfo>.InitializePrefabs(
                CollectionName + "." + company.Id,
                new[] { info },
                null
            );
            if (info.m_investmentAI != null)
            {
                info.m_investmentAI.m_info = info; // InitializePrefab can reset the link
            }

            PrefabCollection<InvestmentInfo>.BindPrefabs();
            Infos[company.Id] = info;
            StockSymbols.Reset(); // the symbol map is built from the stock types
            uint shares = company.HeldShares;
            int spent = company.HeldSpent;
            float[] history = company.History ?? new float[0];
            int id = company.Id;
            string display = company.Name;
            SimulationManager.instance.AddAction(() =>
            {
                StressStocks.RepairFreeSlots();
                CreateSlot(info, id, display, shares, spent, history);
            });
            return true;
        }

        private static void CreateSlot(
            InvestmentInfo info,
            int companyId,
            string display,
            uint shares,
            int spent,
            float[] history
        )
        {
            Investment[] buffer = EconomyManager.instance.m_investments.m_buffer;
            var before = (Investment[])buffer.Clone();
            try
            {
                byte slot;
                if (
                    !EconomyManager.instance.CreateInvestment(
                        out slot,
                        ref SimulationManager.instance.m_randomizer,
                        info
                    )
                )
                {
                    ModLog.Warn("ipo: the game had no slot for " + display);
                    return;
                }

                if ((before[slot].m_flags & Investment.Flags.Created) != 0)
                {
                    buffer[slot] = before[slot]; // never overwrite a holding: put it back
                    ModLog.Warn(
                        "ipo: the game handed out slot "
                            + slot
                            + ", already in use; "
                            + display
                            + " not listed"
                    );
                    return;
                }

                if (history.Length > 0)
                {
                    buffer[slot].ClearValues();
                    for (int i = history.Length - 1; i >= 0; i--)
                    {
                        buffer[slot].AddValue(history[i]); // oldest first: the newest ends on top
                    }
                }

                buffer[slot].m_playerOwnedShareCount = shares;
                buffer[slot].m_playerSpentMoney = spent;
                ModLog.Info(
                    "ipo: listed "
                        + display
                        + " (company "
                        + companyId
                        + ") in slot "
                        + slot
                        + " at ₡"
                        + buffer[slot].value.ToString("0.##")
                        + (shares > 0 ? ", holding of " + shares + " share(s) put back" : "")
                );
            }
            catch (Exception ex)
            {
                ModLog.Warn("ipo: listing " + display + " failed: " + ex.Message);
            }

            OnMainThread(StressStocks.NotifyInvestmentsChanged);
        }

        /// <summary>
        /// Simulation thread: frees every company slot whose company the registry does not know
        /// (a hot reload of the mod loses what was listed since the last save). No payout.
        /// </summary>
        public static void SweepOrphans(ICollection<int> knownCompanyIds)
        {
            Investment[] buffer = EconomyManager.instance.m_investments.m_buffer;
            for (int i = 1; i < buffer.Length; i++)
            {
                int id;
                if (
                    (buffer[i].m_flags & Investment.Flags.Created) != 0
                    && buffer[i].Info != null
                    && LocalCompanyNames.TryId(buffer[i].Info.m_name, out id)
                    && !knownCompanyIds.Contains(id)
                )
                {
                    buffer[i].m_flags = Investment.Flags.None;
                    EconomyManager.instance.m_investments.ReleaseItem((byte)i);
                    ModLog.Info("ipo: freed the slot of company " + id + ", unknown to the save");
                }
            }
        }

        /// <summary>The slot of a company's stock, or −1 (simulation thread).</summary>
        public static int SlotOf(int companyId)
        {
            Investment[] buffer = EconomyManager.instance.m_investments.m_buffer;
            string name = LocalCompanyNames.PrefabName(companyId);
            for (int i = 1; i < buffer.Length; i++)
            {
                if (
                    (buffer[i].m_flags & Investment.Flags.Created) != 0
                    && buffer[i].Info != null
                    && buffer[i].Info.m_name == name
                )
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Simulation thread: the company leaves the exchange. With a buyer's price (cents a
        /// share, above 0) the city's shares are sold at it first, through the game's own sale
        /// so the money and the profit are booked as for any stock. Then the slot is freed and
        /// (main thread) the stock type removed.
        /// </summary>
        public static long Delist(int companyId, long buyerPriceCents, out uint sharesSold)
        {
            long paid = 0;
            sharesSold = 0;
            int slot = SlotOf(companyId);
            if (slot > 0)
            {
                Investment[] buffer = EconomyManager.instance.m_investments.m_buffer;
                InvestmentInfo info = buffer[slot].Info;
                uint shares = buffer[slot].m_playerOwnedShareCount;
                if (
                    buyerPriceCents > 0
                    && shares > 0
                    && info != null
                    && info.m_investmentAI != null
                )
                {
                    buffer[slot].AddValue(buyerPriceCents / 100f);
                    paid = info.m_investmentAI.SellShares((byte)slot, ref buffer[slot], shares);
                    sharesSold = shares;
                }

                buffer[slot].m_flags = Investment.Flags.None;
                EconomyManager.instance.m_investments.ReleaseItem((byte)slot);
            }

            OnMainThread(() => DestroyType(companyId));
            return paid;
        }

        private static void DestroyType(int companyId)
        {
            InvestmentInfo info;
            if (!Infos.TryGetValue(companyId, out info))
            {
                return;
            }

            Infos.Remove(companyId);
            try
            {
                PrefabCollection<InvestmentInfo>.DestroyPrefabs(
                    CollectionName + "." + companyId,
                    new[] { info },
                    null
                );
                UnityEngine.Object.Destroy(info.gameObject);
            }
            catch (Exception ex)
            {
                ModLog.Warn("ipo: removing a stock type failed: " + ex.Message);
            }

            StockSymbols.Reset();
            StressStocks.NotifyInvestmentsChanged();
        }

        /// <summary>
        /// Copies each company's holding and price history from its slot into the registry
        /// entry, so the save has them (the game will drop the slot on the next load).
        /// </summary>
        public static void CopyHoldings(IEnumerable<LocalCompany> companies)
        {
            Investment[] buffer = EconomyManager.instance.m_investments.m_buffer;
            foreach (LocalCompany c in companies)
            {
                int slot = SlotOf(c.Id);
                if (slot <= 0)
                {
                    continue;
                }

                c.HeldShares = buffer[slot].m_playerOwnedShareCount;
                c.HeldSpent = buffer[slot].m_playerSpentMoney;
                Investment.Queue<float> values = buffer[slot].allValues;
                var history = new List<float>();
                for (int k = 0; values != null && k < values.Count; k++)
                {
                    history.Add(values.GetFromTop(k)); // newest first
                }

                c.History = history.ToArray();
            }
        }

        /// <summary>Level unload / hot reload (main thread): removes every company stock type.</summary>
        public static void RemoveAll()
        {
            foreach (
                KeyValuePair<int, InvestmentInfo> kv in new Dictionary<int, InvestmentInfo>(Infos)
            )
            {
                try
                {
                    PrefabCollection<InvestmentInfo>.DestroyPrefabs(
                        CollectionName + "." + kv.Key,
                        new[] { kv.Value },
                        null
                    );
                }
                catch (Exception ex)
                {
                    ModLog.Warn("ipo: removing a stock type failed: " + ex.Message);
                }
            }

            Infos.Clear();
            if (holder != null)
            {
                UnityEngine.Object.Destroy(holder);
                holder = null;
            }
        }
    }
}

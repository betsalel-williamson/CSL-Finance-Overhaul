using System; // Exception, Math
using System.Collections.Generic; // List
// FinanceOverhaul.Core: CityIndexBasket, IndexCopyCodec, IndexDay, IndexHolding, Notice,
//   NoticeText
using FinanceOverhaul.Core;

// Game classes used without a using (global namespace, Assembly-CSharp): EconomyManager,
//   Investment, InvestmentInfo, LoadingManager, PrefabCollection, SimulationManager

namespace FinanceOverhaul
{
    /// <summary>Index listing: the investment itself in a save: creating it, its price history, and restoring shares lost while the mod was off.</summary>
    public static partial class IndexListing
    {
        public static void ResetRestored()
        {
            RestoredShares = 0;
            restoredNoticeDue = false;
        }

        /// <summary>
        /// Save time (and any time the holding should be remembered): copies the city's Index
        /// holding into mod data. Leaves the copy alone when the Index is not listed (for example
        /// the city was loaded with the Index flag off), so the copy outlives a dropped slot.
        /// </summary>
        public static void SaveCopy()
        {
            try
            {
                int id = FindIndexInvestment();
                if (id < 0)
                {
                    return;
                }

                Investment slot = EconomyManager.instance.m_investments.m_buffer[id];
                var h = new IndexHolding
                {
                    Shares = slot.m_playerOwnedShareCount,
                    SpentCents = slot.m_playerSpentMoney,
                };
                SimulationManager.instance.m_SerializableDataWrapper.SaveData(
                    IndexCopyCodec.DataId,
                    System.Text.Encoding.UTF8.GetBytes(IndexCopyCodec.Format(h))
                );
            }
            catch (Exception ex)
            {
                ModLog.Warn("index: copy save failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Postfix of InvestmentAI.CreateInvestment for the Index (after the game zeroes the
        /// holding): a new Index slot means there was none, so the game dropped it (city loaded
        /// without the mod or with the Index flag off) or it never existed. The copy always equals
        /// the holding at the last save, quit or hot reload while the Index was listed, so putting
        /// it back is exact; with no copy (older saves, new cities) nothing changes.
        /// </summary>
        public static void RestoreInto(ref Investment data)
        {
            byte[] raw = SimulationManager.instance.m_SerializableDataWrapper.LoadData(
                IndexCopyCodec.DataId
            );
            IndexHolding? copy =
                raw == null ? null : IndexCopyCodec.Parse(System.Text.Encoding.UTF8.GetString(raw));
            IndexHolding? back = IndexCopyCodec.ToRestore(data.m_playerOwnedShareCount == 0, copy);
            if (back == null)
            {
                return;
            }

            // The game's own field types: shares uint, money spent int (cents).
            data.m_playerOwnedShareCount = (uint)Math.Min(back.Value.Shares, uint.MaxValue);
            data.m_playerSpentMoney = (int)Math.Min(back.Value.SpentCents, int.MaxValue);
            RestoredShares = back.Value.Shares;
            ModLog.Info(
                "index: restored "
                    + back.Value.Shares
                    + " share(s) from the copy (the Index slot was created again)"
            );
            if (LoadingManager.exists && LoadingManager.instance.m_loadingComplete)
            {
                PostRestoredNotice(); // mid-session (hot reload, Index flag turned on)
            }
            else
            {
                restoredNoticeDue = true; // the city is loading: chirp once it has loaded
            }
        }

        /// <summary>Level loaded: posts the "shares are back" chirp held during loading.</summary>
        public static void PostRestoredNoticeIfDue()
        {
            if (restoredNoticeDue)
            {
                restoredNoticeDue = false;
                PostRestoredNotice();
            }
        }

        private static void PostRestoredNotice()
        {
            ChirpNotices.PostDay(new List<Notice> { NoticeText.IndexRestored(RestoredShares) });
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

                // Queue.Add appends newest, so the history goes back in oldest first.
                data.ClearValues();
                foreach (float v in IndexDay.WithOlderDays(ownNewestFirst, older))
                {
                    data.AddValue(v);
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
    }
}

using ICities;

namespace FinanceOverhaul
{
    /// <summary>
    /// Save time: keeps the Index copy current (save compatibility) and writes the Local IPO
    /// counts. Bonds, orders and the "last seen" bookmark are written as they change, so they
    /// need nothing here.
    /// </summary>
    public class SaveDataExtension : SerializableDataExtensionBase
    {
        public override void OnSaveData()
        {
            try
            {
                IndexListing.SaveCopy();
                CompanyDesk.Save();
            }
            catch
            {
                // fail soft: never break a save
            }
        }
    }

    public class LoadingExtension : LoadingExtensionBase
    {
        public override void OnLevelUnloading()
        {
            try
            {
                TickerBoards.DestroyAll();
                OrderDesk.Reset();
                BondDesk.Reset();
                CompanyDesk.Reset();
                ChirpNotices.Reset();
                StockSymbols.Reset();
                IndexListing.ResetRebase();
                IndexListing.ResetRestored();
            }
            catch
            {
                // fail soft
            }
        }

        public override void OnLevelLoaded(LoadMode mode)
        {
            try
            {
                ModLog.Info("level loaded mode=" + mode);
                LogSaveData();
                OrderDesk.Reset();
                BondDesk.Reset();
                CompanyDesk.Reset();
                ChirpNotices.Reset();
                IndexListing.PostRestoredNoticeIfDue();
                IndexListing.ResetRebase();
                IndexListing.EnsureInLoadedCity();
                IndexListing.LogStatus();
                TestStockListing.EnsureInLoadedCity();
                TickerBoards.Ensure();
            }
            catch
            {
                // fail soft
            }
        }

        /// <summary>
        /// Lists the mod data blocks this save carries (every mod's, by key and size). The game keeps
        /// them all when it saves without the mod (SimulationManager.Data writes the whole table
        /// back), so after a city was saved with the mod off, the Finance Overhaul keys should
        /// still be here. Read-only.
        /// </summary>
        private static void LogSaveData()
        {
            ISerializableData w = SimulationManager.instance.m_SerializableDataWrapper;
            string[] keys = w.EnumerateData();
            var parts = new System.Collections.Generic.List<string>();
            foreach (string k in keys)
            {
                byte[] b = w.LoadData(k);
                parts.Add(k + " (" + (b == null ? 0 : b.Length) + " B)");
            }

            ModLog.Info(
                "save data: "
                    + keys.Length
                    + " block(s)"
                    + (parts.Count > 0 ? ": " + string.Join(", ", parts.ToArray()) : "")
            );
        }
    }
}

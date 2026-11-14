#if HAS_CITIES
using ICities;

namespace FinanceOverhaul
{
    public class LoadingExtension : LoadingExtensionBase
    {
        public override void OnLevelUnloading()
        {
            try
            {
                TickerBoards.DestroyAll();
                OrderDesk.Reset();
                StockSymbols.Reset();
                IndexListing.ResetRebase();
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
                OrderDesk.Reset();
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
    }
}
#endif

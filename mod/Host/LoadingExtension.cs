#if HAS_CITIES
using ICities;

namespace FinanceOverhaul
{
    public class LoadingExtension : LoadingExtensionBase
    {
        public override void OnLevelLoaded(LoadMode mode)
        {
            try
            {
                ModLog.Info("level loaded mode=" + mode);
                Patcher.LogDiscoverySummaryOnce();
            }
            catch
            {
                // fail soft
            }
        }
    }
}
#endif

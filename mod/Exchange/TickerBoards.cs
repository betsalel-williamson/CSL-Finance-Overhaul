using System; // Exception
using UnityEngine; // GameObject

// Game classes used without a using (global namespace, Assembly-CSharp): LoadingManager

namespace FinanceOverhaul
{
    /// <summary>
    /// Live ticker: scrolling LED boards (three dot-matrix rows fed from
    /// <see cref="TickerFeed.Current"/>) on the front facade of every Stock
    /// Exchange, in place of the hidden vanilla boards. Visual only, save-neutral,
    /// hot-reload safe (single root found by name and replaced; destroyed with its
    /// textures and materials on unload / disable).
    /// </summary>
    public static class TickerBoards
    {
        public const string RootName = "FinanceOverhaulTickerRoot";

        private static GameObject root;

        /// <summary>Main thread: after level load and after hot reload (PatchAll).</summary>
        public static void Ensure()
        {
            try
            {
                if (!LoadingManager.exists || !LoadingManager.instance.m_loadingComplete)
                {
                    return;
                }

                DestroyAll();
                root = new GameObject(RootName);
                root.AddComponent<TickerOverlay>();
                root.AddComponent<PortfolioOrders>();
                root.AddComponent<CityBondsSection>();
                root.AddComponent<CompanyCards>();
                root.AddComponent<InvestmentsList>();
                root.AddComponent<InvestmentsWheel>();
                root.AddComponent<WelcomeBackDialog>();
                root.AddComponent<IpoDialog>();
                root.AddComponent<IpoFireworks>();
                root.AddComponent<CompanyBuildingPanel>();
                root.AddComponent<CompanySitePanels>();
                root.AddComponent<CompanyAreaTab>();
                root.AddComponent<ChirpNoticePoster>();
                root.AddComponent<StockMonitorHost>();
                if (BuildInfo.ShowDevBuildIdentity)
                {
                    root.AddComponent<DevCapture>();
                    root.AddComponent<UiTestRunner>();
                }
                ModLog.Info("ticker: overlay attached");
            }
            catch (Exception ex)
            {
                ModLog.Warn("ticker: overlay attach failed: " + ex.Message);
            }
        }

        /// <summary>Level unload, mod disable, hot reload. Also removes roots left by older assemblies.</summary>
        public static void DestroyAll()
        {
            try
            {
                VanillaBoards.RestoreAll();
                if (root != null)
                {
                    UnityEngine.Object.Destroy(root);
                    root = null;
                }

                GameObject stale;
                while ((stale = GameObject.Find(RootName)) != null)
                {
                    stale.name = RootName + " (destroying)";
                    UnityEngine.Object.Destroy(stale);
                }
            }
            catch
            {
                // fail soft
            }
        }
    }
}

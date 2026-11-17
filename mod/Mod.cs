using CitiesHarmony.API;
using FinanceOverhaul.Core;
using ICities;

namespace FinanceOverhaul
{
    /// <summary>
    /// The mod as Cities sees it: name, enable and disable, and the Options page. Settings live
    /// here while the mod is enabled; the Options page is <see cref="ModOptions"/>, the file
    /// they are saved to is <see cref="ModSettingsStore"/>, the version text is
    /// <see cref="ModBuildInfo"/>.
    /// </summary>
    public class Mod : IUserMod
    {
        public string Name => ModBuildInfo.OptionsTitle;

        public string Description =>
            "Stock Exchange overhaul for Financial Districts: limit, stop-loss, take-profit and trailing stop orders, a Stock Monitor, an Index stock, city bonds, a live LED ticker and Chirper notices.";

        public static ModSettings Settings { get; private set; }

        /// <summary>The settings, read from disk when the mod has not loaded them yet.</summary>
        internal static ModSettings EnsureSettings()
        {
            if (Settings == null)
            {
                Settings = ModSettingsStore.Load();
            }

            return Settings;
        }

        internal static void SaveSettings()
        {
            ModSettingsStore.Save(Settings);
        }

        /// <summary>Whether an order type's flag is on (on when settings aren't loaded yet).</summary>
        internal static bool OrderKindEnabled(OrderKind kind)
        {
            return Settings == null || Settings.IsEnabled(kind);
        }

        /// <summary>Lifetime for orders placed from now on (open orders keep theirs).</summary>
        internal static int OrderLifetimeDays
        {
            get
            {
                return Settings == null
                    ? TradeOrder.DefaultLifetimeDays
                    : Settings.OrderLifetimeDays;
            }
        }

        public void OnEnabled()
        {
            Settings = ModSettingsStore.Load();
            ModLog.Info(
                "settings: indexStock="
                    + Settings.IndexStockEnabled
                    + " liveTicker="
                    + Settings.LiveTickerEnabled
            );

            ModLog.Info("mod enabled product=" + (ModBuildInfo.GetProductVersionDisplay() ?? "?"));

            try
            {
                HarmonyHelper.DoOnHarmonyReady(Patcher.PatchAll);
                if (!HarmonyHelper.IsHarmonyInstalled)
                {
                    Patcher.LogHarmonyMissingOnce();
                }
            }
            catch
            {
                Patcher.LogHarmonyMissingOnce();
            }
        }

        public void OnDisabled()
        {
            try
            {
                if (HarmonyHelper.IsHarmonyInstalled)
                {
                    Patcher.UnpatchAll();
                }
            }
            catch
            {
                // ignore
            }
            Settings = null;
            ModLog.Info("mod disabled");
        }

        public void OnSettingsUI(UIHelperBase helper)
        {
            if (helper != null)
            {
                ModOptions.Build(helper);
            }
        }
    }
}

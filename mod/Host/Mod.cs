using System;
using System.Globalization;
using System.IO;
using FinanceOverhaul.Market;
#if HAS_CITIES
using CitiesHarmony.API;
using ICities;
#endif

namespace FinanceOverhaul
{
    public class Mod
#if HAS_CITIES
        : IUserMod
#endif
    {
        public string Name => OptionsTitle;

        public string Description =>
            "Financial Districts market overhaul — MVP stock hook, live reload, future index funds & automation.";

        public static ModSettings Settings { get; private set; }

        public static string OptionsTitle
        {
            get
            {
                string version = BuildInfo.ShowDevBuildIdentity
                    ? GetAssemblyIdentityDisplay()
                    : GetProductVersionDisplay();
                if (string.IsNullOrEmpty(version))
                {
                    return "Finance Overhaul";
                }

                return "Finance Overhaul " + version;
            }
        }

        internal static string GetProductVersionDisplay()
        {
            try
            {
                return BuildInfo.ProductVersion;
            }
            catch
            {
                return null;
            }
        }

        internal static string GetAssemblyIdentityDisplay()
        {
            try
            {
                Version v = typeof(Mod).Assembly.GetName().Version;
                return v == null ? null : v.ToString();
            }
            catch
            {
                return null;
            }
        }

        internal static string GetAssemblyBuildTimestampUtcDisplay()
        {
            try
            {
                return BuildInfo.BuildTimestampUtc;
            }
            catch
            {
                return null;
            }
        }

        internal static string GetBuildInfoFooterDisplay()
        {
            if (!BuildInfo.ShowDevBuildIdentity)
            {
                return null;
            }

            string built = GetAssemblyBuildTimestampUtcDisplay();
            if (string.IsNullOrEmpty(built))
            {
                return null;
            }

            return "Built (UTC): " + built;
        }

        internal static string GetBuildInfoLocalDisplay()
        {
            if (!BuildInfo.ShowDevBuildIdentity)
            {
                return null;
            }

            string builtUtc = GetAssemblyBuildTimestampUtcDisplay();
            if (string.IsNullOrEmpty(builtUtc))
            {
                return null;
            }

            if (
                !DateTime.TryParse(
                    builtUtc,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                    out DateTime utc
                )
            )
            {
                return "Built (local): " + builtUtc;
            }

            DateTime local = utc.ToLocalTime();
            return "Built (local): "
                + local.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Options persist in a small key=value file so they survive hot reload
        /// (every reload calls OnEnabled on a fresh assembly) and game restarts.
        /// </summary>
        internal static string SettingsPath
        {
            get
            {
#if HAS_CITIES
                try
                {
                    return Path.Combine(
                        ColossalFramework.IO.DataLocation.localApplicationData,
                        SettingsFile.FileName
                    );
                }
                catch
                {
                    // fall through to temp
                }
#endif
                return Path.Combine(Path.GetTempPath(), SettingsFile.FileName);
            }
        }

        internal static ModSettings LoadSettings()
        {
            try
            {
                string path = SettingsPath;
                if (File.Exists(path))
                {
                    return SettingsFile.Parse(File.ReadAllText(path));
                }
            }
            catch (Exception ex)
            {
                ModLog.Warn("settings: read failed, using defaults: " + ex.Message);
            }

            return new ModSettings();
        }

        internal static void SaveSettings()
        {
            try
            {
                File.WriteAllText(SettingsPath, SettingsFile.Format(Settings));
            }
            catch (Exception ex)
            {
                ModLog.Warn("settings: write failed: " + ex.Message);
            }
        }

        public void OnEnabled()
        {
            Settings = LoadSettings();
            ModLog.Info(
                "settings: indexStock="
                    + Settings.IndexStockEnabled
                    + " liveTicker="
                    + Settings.LiveTickerEnabled
            );

            ModLog.Info("mod enabled product=" + (GetProductVersionDisplay() ?? "?"));

#if HAS_CITIES
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
#endif
        }

        public void OnDisabled()
        {
#if HAS_CITIES
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
#endif
            Settings = null;
            ModLog.Info("mod disabled");
        }

#if HAS_CITIES
        public void OnSettingsUI(UIHelperBase helper)
        {
            if (helper == null)
            {
                return;
            }

            if (Settings == null)
            {
                Settings = LoadSettings();
            }

            UIHelperBase group = helper.AddGroup(OptionsTitle);
            group.AddCheckbox(
                "Live Stock Exchange ticker (visual only)",
                Settings.LiveTickerEnabled,
                OnLiveTickerChanged
            );
            group.AddDropdown(
                "Stock Exchange ticker shows",
                new[] { "Change % (+3.22%)", "Price and ₡ change (₡951 +₡30)" },
                Settings.TickerShows == TickerDisplay.PriceAndChange ? 1 : 0,
                OnTickerShowsChanged
            );
            group.AddCheckbox(
                "Experimental: Index stock (adds a mod-only investment to saves)",
                Settings.IndexStockEnabled,
                OnIndexStockChanged
            );

            // Temporary feature flags, one per order type (see ModSettings).
            foreach (OrderKind kind in ModSettings.OrderKinds)
            {
                OrderKind k = kind;
                group.AddCheckbox(
                    OrderText.OptionLabel(k),
                    Settings.IsEnabled(k),
                    enabled => OnOrderKindChanged(k, enabled)
                );
            }

            // How long new orders stay open (a game, so the player chooses; default 90 days).
            int[] lifetimes = TradeOrder.LifetimeChoices;
            var lifetimeLabels = new string[lifetimes.Length];
            int selected = Array.IndexOf(lifetimes, TradeOrder.DefaultLifetimeDays);
            for (int i = 0; i < lifetimes.Length; i++)
            {
                lifetimeLabels[i] = OrderText.LifetimeLabel(lifetimes[i]);
                if (lifetimes[i] == Settings.OrderLifetimeDays)
                {
                    selected = i;
                }
            }

            group.AddDropdown(
                "Orders stay open for (new orders)",
                lifetimeLabels,
                selected,
                OnOrderLifetimeChanged
            );
            group.AddCheckbox(
                "Share value charts in the Stock Monitor style (testing)",
                Settings.ShareChartsEnabled,
                OnShareChartsChanged
            );
            group.AddCheckbox(
                "City bonds: sell bonds to raise money (testing; adds mod save data)",
                Settings.CityBondsEnabled,
                OnCityBondsChanged
            );
            group.AddCheckbox(
                "Stock Monitor window (testing)",
                Settings.Monitor.Enabled,
                OnStockMonitorChanged
            );

            if (BuildInfo.ShowDevBuildIdentity)
            {
                group.AddCheckbox(
                    "Dev: Test Stock (TST) for game UI tests (adds a listing; use a test save)",
                    Settings.DevTestStockEnabled,
                    OnDevTestStockChanged
                );
            }

            // Build info goes on the Options page itself after the group, as plain labels (not
            // nested groups, which each draw a rule): the group's own separator rule then sits
            // between the toggles and the build info.
            AddFooterLabel(helper, GetBuildInfoFooterDisplay());
            AddFooterLabel(helper, GetBuildInfoLocalDisplay());

            // Always available (dev and release): one click puts version, assembly identity
            // and build time on the clipboard for bug reports.
            copyBuildInfoButton =
                helper.AddButton(CopyBuildInfoLabel, OnCopyBuildInfo)
                as ColossalFramework.UI.UIButton;
        }

        private const string CopyBuildInfoLabel = "Copy build info";

        private static ColossalFramework.UI.UIButton copyBuildInfoButton;

        internal static string GetBuildInfoText()
        {
            return BuildInfoText.Format(
                GetProductVersionDisplay(),
                GetAssemblyIdentityDisplay(),
                GetAssemblyBuildTimestampUtcDisplay(),
                BuildInfo.ShowDevBuildIdentity
            );
        }

        private static void OnCopyBuildInfo()
        {
            try
            {
                string text = GetBuildInfoText();
                UnityEngine.GUIUtility.systemCopyBuffer = text;
                ModLog.Info("build info copied: " + text.Replace("\n", " | "));
                if (copyBuildInfoButton != null)
                {
                    copyBuildInfoButton.text = "Copied build info";
                }
            }
            catch (Exception ex)
            {
                ModLog.Warn("build info copy failed: " + ex.Message);
            }
        }

        private static void AddFooterLabel(UIHelperBase group, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            var helper = group as UIHelper;
            var panel = helper == null ? null : helper.self as ColossalFramework.UI.UIComponent;
            if (panel == null)
            {
                group.AddGroup(text); // fallback: still visible, with an extra rule
                return;
            }

            ColossalFramework.UI.UILabel label =
                panel.AddUIComponent<ColossalFramework.UI.UILabel>();
            label.text = text;
            label.textScale = 0.875f;
        }

        private static void OnIndexStockChanged(bool enabled)
        {
            if (Settings == null)
            {
                Settings = new ModSettings();
            }

            Settings.IndexStockEnabled = enabled;
            SaveSettings();
            ModLog.Info("index stock enabled=" + enabled);
            if (enabled)
            {
                // Mid-game: list Index now instead of waiting for the next city load.
                IndexListing.EnsureInLoadedCity();
            }
            else
            {
                ModLog.Info("index: disabling keeps an existing listing until the next city load");
            }
        }

        private static void OnDevTestStockChanged(bool enabled)
        {
            if (Settings == null)
            {
                Settings = new ModSettings();
            }

            Settings.DevTestStockEnabled = enabled;
            SaveSettings();
            ModLog.Info("dev test stock enabled=" + enabled);
            if (enabled)
            {
                TestStockListing.EnsureInLoadedCity();
            }
        }

        /// <summary>Whether an order type's flag is on (on when settings aren't loaded yet).</summary>
        internal static bool OrderKindEnabled(OrderKind kind)
        {
            return Settings == null || Settings.IsEnabled(kind);
        }

        /// <summary>Same as ticking the Options checkbox (used by the game UI tests).</summary>
        internal static void SetOrderKindEnabled(OrderKind kind, bool enabled)
        {
            OnOrderKindChanged(kind, enabled);
        }

        private static void OnOrderKindChanged(OrderKind kind, bool enabled)
        {
            if (Settings == null)
            {
                Settings = new ModSettings();
            }

            Settings.SetEnabled(kind, enabled);
            SaveSettings();
            ModLog.Info("orders: " + SettingsFile.OrderKey(kind) + "=" + enabled);
            OrderTicket.OnFlagsChanged();
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

        private static void OnTickerShowsChanged(int index)
        {
            if (Settings == null)
            {
                Settings = new ModSettings();
            }

            Settings.TickerShows =
                index == 1 ? TickerDisplay.PriceAndChange : TickerDisplay.ChangePercent;
            SaveSettings();
            ModLog.Info("ticker: shows " + Settings.TickerShows);
        }

        private static void OnOrderLifetimeChanged(int index)
        {
            if (Settings == null)
            {
                Settings = new ModSettings();
            }

            int[] lifetimes = TradeOrder.LifetimeChoices;
            if (index < 0 || index >= lifetimes.Length)
            {
                return;
            }

            Settings.OrderLifetimeDays = lifetimes[index];
            SaveSettings();
            ModLog.Info("orders: new orders stay open " + Settings.OrderLifetimeDays + " days");
        }

        private static void OnShareChartsChanged(bool enabled)
        {
            if (Settings == null)
            {
                Settings = new ModSettings();
            }

            Settings.ShareChartsEnabled = enabled;
            SaveSettings();
            ModLog.Info("share charts enabled=" + enabled);
            if (!enabled)
            {
                ShareChartOverlay.RemoveAll();
            }
        }

        private static void OnCityBondsChanged(bool enabled)
        {
            if (Settings == null)
            {
                Settings = new ModSettings();
            }

            Settings.CityBondsEnabled = enabled;
            SaveSettings();
            ModLog.Info("city bonds enabled=" + enabled);
        }

        private static void OnStockMonitorChanged(bool enabled)
        {
            if (Settings == null)
            {
                Settings = new ModSettings();
            }

            Settings.Monitor.Enabled = enabled;
            SaveSettings();
            ModLog.Info("stock monitor enabled=" + enabled);
            if (StockMonitorHost.Current != null)
            {
                StockMonitorHost.Current.ApplyFlag();
            }
        }

        private static void OnLiveTickerChanged(bool enabled)
        {
            if (Settings == null)
            {
                Settings = new ModSettings();
            }

            Settings.LiveTickerEnabled = enabled;
            SaveSettings();
            ModLog.Info("live ticker enabled=" + enabled);
        }
#endif
    }
}

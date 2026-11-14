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
            group.AddCheckbox(
                "Experimental: Index stock (adds a mod-only investment to saves)",
                Settings.IndexStockEnabled,
                OnIndexStockChanged
            );

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

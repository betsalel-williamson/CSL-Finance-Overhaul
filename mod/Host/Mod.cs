using System;
using System.Globalization;
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

        public static ProbeSettings Settings { get; private set; }

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

        public void OnEnabled()
        {
            if (Settings == null)
            {
                Settings = new ProbeSettings();
            }

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
                Settings = new ProbeSettings();
            }

            UIHelperBase group = helper.AddGroup(OptionsTitle);
            group.AddCheckbox(
                "MVP probe bias (+1% on hooked prices)",
                Settings.ProbeBiasEnabled,
                OnProbeBiasChanged
            );

            string footer = GetBuildInfoFooterDisplay();
            if (!string.IsNullOrEmpty(footer))
            {
                group.AddGroup(footer);
            }

            string local = GetBuildInfoLocalDisplay();
            if (!string.IsNullOrEmpty(local))
            {
                group.AddGroup(local);
            }
        }

        private static void OnProbeBiasChanged(bool enabled)
        {
            if (Settings == null)
            {
                Settings = new ProbeSettings();
            }

            Settings.ProbeBiasEnabled = enabled;
            ModLog.Info("probe bias enabled=" + enabled);
        }
#endif
    }
}

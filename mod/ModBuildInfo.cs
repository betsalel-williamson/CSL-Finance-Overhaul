using System; // DateTime, Version
using System.Globalization; // CultureInfo, DateTimeStyles
using FinanceOverhaul.Core; // BuildInfoText

namespace FinanceOverhaul
{
    /// <summary>
    /// What this build calls itself: the product version (package.json), the assembly identity
    /// (changes every build, so Cities reloads the mod) and the build time. Dev installs show
    /// the assembly identity and a Built footer; release installs show the product version.
    /// </summary>
    internal static class ModBuildInfo
    {
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
                Version v = typeof(ModBuildInfo).Assembly.GetName().Version;
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

        /// <summary>Version, assembly identity and build time, for bug reports.</summary>
        internal static string GetBuildInfoText()
        {
            return BuildInfoText.Format(
                GetProductVersionDisplay(),
                GetAssemblyIdentityDisplay(),
                GetAssemblyBuildTimestampUtcDisplay(),
                BuildInfo.ShowDevBuildIdentity
            );
        }
    }
}

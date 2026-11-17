using System;
using System.IO;
using FinanceOverhaul.Market;

namespace FinanceOverhaul
{
    /// <summary>
    /// Options on disk: a small key=value file (<see cref="SettingsFile"/>), so they survive
    /// hot reload (every reload calls OnEnabled on a fresh assembly) and game restarts.
    /// </summary>
    internal static class ModSettingsStore
    {
        internal static string Path
        {
            get
            {
                try
                {
                    return System.IO.Path.Combine(
                        ColossalFramework.IO.DataLocation.localApplicationData,
                        SettingsFile.FileName
                    );
                }
                catch
                {
                    // fall through to temp
                }
                return System.IO.Path.Combine(System.IO.Path.GetTempPath(), SettingsFile.FileName);
            }
        }

        internal static ModSettings Load()
        {
            try
            {
                string path = Path;
                if (File.Exists(path))
                {
                    return ForBuild(SettingsFile.Parse(File.ReadAllText(path)));
                }
            }
            catch (Exception ex)
            {
                ModLog.Warn("settings: read failed, using defaults: " + ex.Message);
            }

            return ForBuild(new ModSettings());
        }

        internal static void Save(ModSettings settings)
        {
            try
            {
                File.WriteAllText(Path, SettingsFile.Format(settings));
            }
            catch (Exception ex)
            {
                ModLog.Warn("settings: write failed: " + ex.Message);
            }
        }

        /// <summary>Release builds use the release value of every feature flag.</summary>
        private static ModSettings ForBuild(ModSettings s)
        {
            if (!BuildInfo.ShowDevBuildIdentity)
            {
                s.PinReleaseFlags();
            }

            return s;
        }
    }
}

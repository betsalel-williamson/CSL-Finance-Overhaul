using System;
using HarmonyLib;

namespace FinanceOverhaul
{
    /// <summary>Hooks on the game's keyboard shortcuts: Esc closes the Stock Monitor.</summary>
    public static partial class Patcher
    {
        private static int ApplyKeyPatches(Harmony harmony)
        {
            // The game's Esc closes one thing per press and opens the pause menu when nothing is
            // left to close; the Stock Monitor takes its turn before that. SteamEscape is the
            // same list for a controller's menu button.
            return TryPatch(harmony, GameInternals.Keys.Escape, nameof(EscapePrefix), null)
                + TryPatch(harmony, GameInternals.Keys.SteamEscape, nameof(EscapePrefix), null);
        }

        /// <summary>False skips the game's own handling: the press closed the IPO dialog or the Stock Monitor.</summary>
        private static bool EscapePrefix()
        {
            try
            {
                // The dialog on top closes first.
                if (IpoDialog.Current != null && IpoDialog.Current.HandleEscape())
                {
                    return false;
                }

                StockMonitorHost monitor = StockMonitorHost.Current;
                return monitor == null || !monitor.HandleEscape();
            }
            catch (Exception ex)
            {
                ModLog.Warn("keys: Esc handling failed: " + ex.Message);
                return true;
            }
        }
    }
}

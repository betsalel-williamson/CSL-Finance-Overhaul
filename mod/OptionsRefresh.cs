using System; // Exception
using UnityEngine; // GameObject, MonoBehaviour, Time

namespace FinanceOverhaul
{
    /// <summary>
    /// After the mod is switched on or reloaded in place (a new build while the game runs),
    /// asks the game's Options window to rebuild its mod pages once, so Options → Finance
    /// Overhaul shows this build's options. The game only rebuilds them when a mod is switched
    /// on or off while the window is open. Waits a moment so the new build is the one the game
    /// asks for its page.
    /// </summary>
    internal sealed class OptionsRefresh : MonoBehaviour
    {
        private const string MarkerName = "FinanceOverhaulOptionsRefresh";
        private float at;

        public static void Schedule()
        {
            try
            {
                GameObject old = GameObject.Find(MarkerName);
                if (old != null)
                {
                    Destroy(old);
                }

                var marker = new GameObject(MarkerName);
                DontDestroyOnLoad(marker);
                marker.AddComponent<OptionsRefresh>().at = Time.unscaledTime + 1f;
            }
            catch (Exception ex)
            {
                ModLog.Warn("options: page refresh not scheduled: " + ex.Message);
            }
        }

        private void Update()
        {
            if (Time.unscaledTime < at)
            {
                return;
            }

            try
            {
                if (GameInternals.Options.RebuildModPages())
                {
                    ModLog.Info("options: mod pages rebuilt for this build");
                }
            }
            catch (Exception ex)
            {
                ModLog.Warn("options: page refresh failed: " + ex.Message);
            }

            Destroy(gameObject);
        }
    }
}

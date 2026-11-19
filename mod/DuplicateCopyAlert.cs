using System; // Exception
using System.Collections.Generic; // Dictionary
using System.Reflection; // Assembly
using ColossalFramework.PlatformServices; // PublishedFileId
using ColossalFramework.Plugins; // PluginManager
using ColossalFramework.UI; // UIView
using FinanceOverhaul.Core; // DuplicateCopies, ModCopy
using ICities; // IUserMod
using UnityEngine; // GameObject, MonoBehaviour, Resources, Time

// Game classes used without a using (global namespace, Assembly-CSharp): ConfirmPanel,
//   LoadingManager, PackageEntry

namespace FinanceOverhaul
{
    /// <summary>
    /// Asks which copy to keep when more than one copy of the mod is switched on (the Workshop
    /// copy and a local one, say): both would hook the game and act on the city. Uses the
    /// game's own Yes / No dialog. The decision and wording are <see cref="DuplicateCopies"/>.
    /// Runs once the game's UI is up, after this copy is switched on.
    /// </summary>
    internal sealed class DuplicateCopyAlert : MonoBehaviour
    {
        // One waiting check per game, whichever copy made it: two copies that both have this
        // check must not both ask. Renamed once it has asked (see Update).
        private const string MarkerName = "FinanceOverhaulDuplicateCopyAlert";
        private const string AssemblyName = "FinanceOverhaul";

        private const string ConfirmPanelName = "ConfirmPanel"; // not shown: the game's panel name

        private float nextTry;
        private bool asked;
        private bool waitLogged;

        private static bool watching;

        /// <summary>
        /// Dev builds' game UI tests: the real check, timing and dialog, over every installed
        /// copy (switched on or not), and an answer is only logged: nothing is switched off.
        /// </summary>
        internal static bool Rehearsal;

        /// <summary>
        /// Checks now and again whenever a mod is switched on or off, so a second copy
        /// enabled later in the session is caught too.
        /// </summary>
        public static void Watch()
        {
            Schedule();
            if (watching)
            {
                return;
            }

            try
            {
                PluginManager.instance.eventPluginsStateChanged += Schedule;
                watching = true;
            }
            catch (Exception ex)
            {
                ModLog.Warn("duplicate copies: cannot watch for a second copy: " + ex.Message);
            }
        }

        public static void Unwatch()
        {
            if (!watching)
            {
                return;
            }

            try
            {
                PluginManager.instance.eventPluginsStateChanged -= Schedule;
            }
            catch
            {
                // fail soft
            }

            watching = false;
        }

        public static void Schedule()
        {
            try
            {
                GameObject existing = GameObject.Find(MarkerName);
                if (existing != null)
                {
                    if (existing.GetComponent<DuplicateCopyAlert>() != null)
                    {
                        return; // this build is already checking
                    }

                    // Left by another build: the copy that was switched off or reloaded, or an
                    // older build whose check had already asked and would never ask again.
                    // This build checks instead.
                    ModLog.Info("duplicate copies: taking over the check from another build");
                    Destroy(existing);
                }

                var marker = new GameObject(MarkerName);
                DontDestroyOnLoad(marker);
                marker.AddComponent<DuplicateCopyAlert>();
            }
            catch (Exception ex)
            {
                ModLog.Warn("duplicate copies: check not started: " + ex.Message);
            }
        }

        private void Update()
        {
            if (asked || Time.unscaledTime < nextTry)
            {
                return;
            }

            nextTry = Time.unscaledTime + 1f;
            try
            {
                if (
                    UIView.GetAView() == null
                    || UIView.library == null
                    || LoadingManager.instance.m_currentlyLoading
                )
                {
                    return; // the dialog needs the game's UI; try again shortly
                }

                SyncContentManagerRows();
                Dictionary<ModCopy, PluginManager.PluginInfo> copies = Copies(!Rehearsal);
                if (!DuplicateCopies.Conflict(copies.Keys))
                {
                    Destroy(gameObject);
                    return;
                }

                // The game has one Yes / No dialog. Asked while another question is open, it
                // keeps that question's answer handler and only takes our text: the player
                // would answer us and the other asker would get it. Wait for it to close.
                ConfirmPanel panel = UIView.library.Get<ConfirmPanel>(ConfirmPanelName);
                if (UIView.HasModalInput() || (panel != null && panel.component.isVisible))
                {
                    if (!waitLogged)
                    {
                        ModLog.Info("duplicate copies: another dialog is open; asking after it");
                        waitLogged = true;
                    }

                    return;
                }

                asked = true;
                // Asked: a later check (a city loading, a copy switched on) starts afresh and
                // waits for this question to close instead of finding this one and stopping.
                gameObject.name = MarkerName + "-asked";
                ModLog.Warn(
                    "duplicate copies: " + copies.Count + " copies of the mod are switched on"
                );
                ConfirmPanel.ShowModal(
                    DuplicateCopies.Title(),
                    DuplicateCopies.Message(copies.Keys),
                    (component, answer) => Answered(copies, answer)
                );
            }
            catch (Exception ex)
            {
                ModLog.Warn("duplicate copies: check failed: " + ex.Message);
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Yes (1) and No (0) choose the copy to keep. Closed any other way (Escape: -1)
        /// changes nothing, and the question comes back when a city or the menu next loads.
        /// </summary>
        private void Answered(Dictionary<ModCopy, PluginManager.PluginInfo> copies, int answer)
        {
            ModLog.Info("duplicate copies: answer " + answer + (Rehearsal ? " (rehearsal)" : ""));
            if (answer != 0 && answer != 1)
            {
                Destroy(gameObject);
                return;
            }

            Apply(copies, answer == 1);
        }

        private void Apply(Dictionary<ModCopy, PluginManager.PluginInfo> copies, bool yes)
        {
            try
            {
                PluginManager.PluginInfo mine = PluginManager.instance.FindPluginInfo(
                    Assembly.GetExecutingAssembly()
                );
                bool switchedOffAnother = false;
                foreach (ModCopy copy in DuplicateCopies.ToSwitchOff(copies.Keys, yes))
                {
                    PluginManager.PluginInfo plugin = copies[copy];
                    ModLog.Info(
                        "duplicate copies: switching off "
                            + DuplicateCopies.Describe(copy)
                            + (Rehearsal ? " (rehearsal: left on)" : "")
                    );
                    if (Rehearsal)
                    {
                        continue;
                    }

                    plugin.isEnabled = false;
                    switchedOffAnother |= plugin != mine;
                }

                // A copy from before 0.2.0 removes every copy's hooks when it is switched off:
                // put this copy's back if it is the one that stays.
                if (switchedOffAnother && mine != null && mine.isEnabled)
                {
                    Patcher.Reapply();
                }
            }
            catch (Exception ex)
            {
                ModLog.Warn("duplicate copies: could not switch a copy off: " + ex.Message);
            }
            finally
            {
                SyncContentManagerRows();
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Content Manager reads a row's On / Off box only when it fills the row in, so a copy
        /// switched off here would still show On. Sets each copy's box to whether it is on.
        /// </summary>
        private static void SyncContentManagerRows()
        {
            try
            {
                // Rows of a closed or scrolled-away list are inactive: include those too.
                foreach (PackageEntry row in Resources.FindObjectsOfTypeAll<PackageEntry>())
                {
                    PluginManager.PluginInfo plugin = row.pluginInfo;
                    if (plugin == null || !IsCopy(plugin) || row.entryActive == plugin.isEnabled)
                    {
                        continue;
                    }

                    row.entryActive = plugin.isEnabled;
                    ModLog.Info(
                        "duplicate copies: Content Manager row for "
                            + plugin.modPath
                            + " now shows "
                            + (plugin.isEnabled ? "on" : "off")
                    );
                }
            }
            catch (Exception ex)
            {
                ModLog.Warn("duplicate copies: Content Manager rows not updated: " + ex.Message);
            }
        }

        private static bool IsCopy(PluginManager.PluginInfo plugin)
        {
            foreach (Assembly a in plugin.GetAssemblies())
            {
                if (a != null && a.GetName().Name == AssemblyName)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Every copy of the mod the game has loaded; only those switched on, or all.</summary>
        private static Dictionary<ModCopy, PluginManager.PluginInfo> Copies(bool enabledOnly)
        {
            var copies = new Dictionary<ModCopy, PluginManager.PluginInfo>();
            foreach (PluginManager.PluginInfo plugin in PluginManager.instance.GetPluginsInfo())
            {
                if (plugin == null || (enabledOnly && !plugin.isEnabled))
                {
                    continue;
                }

                Assembly assembly = null;
                foreach (Assembly a in plugin.GetAssemblies())
                {
                    if (a != null && a.GetName().Name == AssemblyName)
                    {
                        assembly = a;
                    }
                }

                if (assembly == null)
                {
                    continue;
                }

                var mod = plugin.userModInstance as IUserMod;
                copies[
                    new ModCopy
                    {
                        Name = mod == null ? null : mod.Name,
                        Version = assembly.GetName().Version,
                        Workshop = plugin.publishedFileID != PublishedFileId.invalid,
                        Path = plugin.modPath,
                    }
                ] = plugin;
            }

            return copies;
        }

        /// <summary>
        /// Dev builds' game UI tests: shows the dialog for every installed copy, switched on
        /// or not, and changes nothing whichever answer is given.
        /// </summary>
        internal static int Preview()
        {
            Dictionary<ModCopy, PluginManager.PluginInfo> copies = Copies(false);
            if (DuplicateCopies.Conflict(copies.Keys))
            {
                ConfirmPanel.ShowModal(
                    DuplicateCopies.Title(),
                    DuplicateCopies.Message(copies.Keys),
                    (component, answer) => { }
                );
            }

            return copies.Count;
        }
    }
}

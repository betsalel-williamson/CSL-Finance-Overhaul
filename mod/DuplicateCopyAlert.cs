using System;
using System.Collections.Generic;
using System.Reflection;
using ColossalFramework.PlatformServices;
using ColossalFramework.Plugins;
using ColossalFramework.UI;
using FinanceOverhaul.Core;
using ICities;
using UnityEngine;

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
        // One object by this name per game, whichever copy made it: two copies that both have
        // this check must not both ask.
        private const string MarkerName = "FinanceOverhaulDuplicateCopyAlert";
        private const string AssemblyName = "FinanceOverhaul";

        private float nextTry;
        private bool asked;

        private static bool watching;

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
                if (GameObject.Find(MarkerName) != null)
                {
                    return;
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

                Dictionary<ModCopy, PluginManager.PluginInfo> copies = Copies(true);
                if (!DuplicateCopies.Conflict(copies.Keys))
                {
                    Destroy(gameObject);
                    return;
                }

                asked = true;
                ModLog.Warn(
                    "duplicate copies: " + copies.Count + " copies of the mod are switched on"
                );
                ConfirmPanel.ShowModal(
                    DuplicateCopies.Title(),
                    DuplicateCopies.Message(copies.Keys),
                    (component, answer) => Apply(copies, answer == 1)
                );
            }
            catch (Exception ex)
            {
                ModLog.Warn("duplicate copies: check failed: " + ex.Message);
                Destroy(gameObject);
            }
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
                        "duplicate copies: switching off " + DuplicateCopies.Describe(copy)
                    );
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
                Destroy(gameObject);
            }
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

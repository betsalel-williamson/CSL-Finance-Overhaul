using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace FinanceOverhaul
{
    /// <summary>
    /// Harmony entry — keeps HarmonyLib out of <see cref="Mod"/> so Content Manager
    /// can still load the type when Cities Harmony is missing. Stock math lives in
    /// <c>FinanceOverhaul.Core</c>; this type only wires Cities assemblies. The hooks
    /// themselves are grouped by feature in the other <c>Patcher.*.cs</c> files.
    /// </summary>
    public static partial class Patcher
    {
        public const string HarmonyId = "com.betsalel.financeoverhaul";

        private static bool patched;
        private static bool harmonyMissingLogged;

        // What this assembly patched. A second copy of the mod (Workshop and local, or the
        // assembly from before a hot reload) uses the same Harmony id, so unpatching by id
        // would also take that copy's hooks away.
        private static readonly List<KeyValuePair<MethodBase, MethodInfo>> ownPatches =
            new List<KeyValuePair<MethodBase, MethodInfo>>();

        public static void PatchAll()
        {
            if (patched)
            {
                return;
            }

            try
            {
                Harmony harmony = new Harmony(HarmonyId);
                int index = ApplyIndexPatches(harmony) + ApplyChartPatches(harmony);
                int ui = ApplyShareFieldPatches(harmony);
                int ticker = ApplyDailyPatches(harmony);
                patched = true;
                ModLog.Info(
                    "harmony patched index=" + index + " shareField=" + ui + " ticker=" + ticker
                );
                // Hot reload / mid-game enable: list Index in the running city if enabled.
                IndexListing.EnsureInLoadedCity();
                TestStockListing.EnsureInLoadedCity();
                // Hot reload: re-attach the live ticker overlay in a loaded city.
                TickerBoards.Ensure();
            }
            catch (Exception ex)
            {
                LogHarmonyMissingOnce("patch failed: " + ex.Message);
            }
        }

        public static void UnpatchAll()
        {
            if (!patched)
            {
                return;
            }

            try
            {
                TickerBoards.DestroyAll();
                ShareChartOverlay.RemoveAll();
            }
            catch
            {
                // fail soft
            }

            try
            {
                ShareAmountField.Detach();
                OrderTicket.Detach();
            }
            catch
            {
                // fail soft
            }

            try
            {
                Harmony harmony = new Harmony(HarmonyId);
                foreach (KeyValuePair<MethodBase, MethodInfo> patch in ownPatches)
                {
                    try
                    {
                        harmony.Unpatch(patch.Key, patch.Value);
                    }
                    catch
                    {
                        // keep removing the rest
                    }
                }
            }
            catch
            {
                // ignore
            }

            ownPatches.Clear();
            patched = false;
        }

        /// <summary>Removes this copy's hooks and applies them again.</summary>
        public static void Reapply()
        {
            UnpatchAll();
            patched = false;
            PatchAll();
        }

        public static void LogHarmonyMissingOnce()
        {
            LogHarmonyMissingOnce(null);
        }

        public static void LogHarmonyMissingOnce(string detail)
        {
            if (harmonyMissingLogged)
            {
                return;
            }

            harmonyMissingLogged = true;
            string line =
                "Finance Overhaul: Cities Harmony is not ready. Subscribe to Cities Harmony and re-enable this mod.";
            if (!string.IsNullOrEmpty(detail))
            {
                line = line + " (" + detail + ")";
            }

            ModLog.Warn(line);
        }

        private static int TryPatch(
            Harmony harmony,
            MethodBase target,
            string prefixName,
            string postfixName
        )
        {
            if (target == null)
            {
                ModLog.Warn("index: patch target missing for " + (prefixName ?? postfixName));
                return 0;
            }

            try
            {
                MethodInfo prefix = Own(prefixName);
                MethodInfo postfix = Own(postfixName);
                harmony.Patch(
                    target,
                    prefix: prefix == null ? null : new HarmonyMethod(prefix),
                    postfix: postfix == null ? null : new HarmonyMethod(postfix)
                );
                if (prefix != null)
                {
                    ownPatches.Add(new KeyValuePair<MethodBase, MethodInfo>(target, prefix));
                }

                if (postfix != null)
                {
                    ownPatches.Add(new KeyValuePair<MethodBase, MethodInfo>(target, postfix));
                }

                return 1;
            }
            catch (Exception ex)
            {
                ModLog.Warn("index: skip patch " + target.Name + ": " + ex.Message);
                return 0;
            }
        }

        private static MethodInfo Own(string name)
        {
            return name == null
                ? null
                : typeof(Patcher).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);
        }
    }
}

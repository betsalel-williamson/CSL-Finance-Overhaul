#if HAS_CITIES
using System;
using System.Reflection;
using FinanceOverhaul.Market;
using HarmonyLib;

namespace FinanceOverhaul
{
    /// <summary>
    /// Harmony entry — keeps HarmonyLib out of <see cref="Mod"/> so Content Manager
    /// can still load the type when Cities Harmony is missing. Stock math lives in
    /// <c>FinanceOverhaul.Market</c>; this type only wires Cities assemblies.
    /// </summary>
    public static class Patcher
    {
        public const string HarmonyId = "com.betsalel.financeoverhaul";

        private static bool patched;
        private static bool harmonyMissingLogged;
        private static bool discoveryLogged;

        public static void PatchAll()
        {
            if (patched)
            {
                return;
            }

            try
            {
                Harmony harmony = new Harmony(HarmonyId);
                int applied = ApplyStockUpdatePostfixes(harmony);
                patched = true;
                ModLog.Info("harmony patched targets=" + applied);
                LogDiscoverySummaryOnce();
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
                Harmony harmony = new Harmony(HarmonyId);
                harmony.UnpatchAll(HarmonyId);
            }
            catch
            {
                // ignore
            }

            patched = false;
            discoveryLogged = false;
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

        public static void LogDiscoverySummaryOnce()
        {
            if (discoveryLogged)
            {
                return;
            }

            discoveryLogged = true;
            try
            {
                Assembly game = typeof(SimulationManager).Assembly;
                StockMarketProbe.DiscoveryResult discovery = StockMarketProbe.Discover(game);
                ModLog.Info(
                    "stock probe types="
                        + discovery.CandidateTypeNames.Count
                        + " methods="
                        + discovery.CandidateMethodNames.Count
                );
                int limit = Math.Min(12, discovery.CandidateTypeNames.Count);
                for (int i = 0; i < limit; i++)
                {
                    ModLog.Info("stock type candidate: " + discovery.CandidateTypeNames[i]);
                }
            }
            catch (Exception ex)
            {
                ModLog.Warn("stock probe failed: " + ex.Message);
            }
        }

        private static int ApplyStockUpdatePostfixes(Harmony harmony)
        {
            const int maxPatches = 8;
            int applied = 0;
            Assembly game = typeof(SimulationManager).Assembly;
            StockMarketProbe.DiscoveryResult discovery = StockMarketProbe.Discover(game);
            MethodInfo postfix = typeof(Patcher).GetMethod(
                nameof(StockUpdatePostfix),
                BindingFlags.Static | BindingFlags.NonPublic
            );

            applied += PatchMethodSubset(
                harmony,
                game,
                discovery,
                postfix,
                preferPriceName: true,
                maxPatches - applied
            );
            if (applied < maxPatches)
            {
                applied += PatchMethodSubset(
                    harmony,
                    game,
                    discovery,
                    postfix,
                    preferPriceName: false,
                    maxPatches - applied
                );
            }

            return applied;
        }

        private static int PatchMethodSubset(
            Harmony harmony,
            Assembly game,
            StockMarketProbe.DiscoveryResult discovery,
            MethodInfo postfix,
            bool preferPriceName,
            int remaining
        )
        {
            int applied = 0;
            for (int i = 0; i < discovery.CandidateMethodNames.Count && applied < remaining; i++)
            {
                string entry = discovery.CandidateMethodNames[i];
                int sep = entry.LastIndexOf("::", StringComparison.Ordinal);
                if (sep <= 0)
                {
                    continue;
                }

                string typeName = entry.Substring(0, sep);
                string methodName = entry.Substring(sep + 2);
                bool hasPrice =
                    methodName.IndexOf("Price", StringComparison.OrdinalIgnoreCase) >= 0;
                if (preferPriceName != hasPrice)
                {
                    continue;
                }

                Type type = game.GetType(typeName);
                if (type == null)
                {
                    continue;
                }

                MethodInfo[] methods = type.GetMethods(
                    BindingFlags.Instance
                        | BindingFlags.Static
                        | BindingFlags.Public
                        | BindingFlags.NonPublic
                );
                for (int m = 0; m < methods.Length && applied < remaining; m++)
                {
                    MethodInfo method = methods[m];
                    if (method == null || method.Name != methodName)
                    {
                        continue;
                    }

                    if (method.IsAbstract || method.ContainsGenericParameters)
                    {
                        continue;
                    }

                    try
                    {
                        harmony.Patch(method, postfix: new HarmonyMethod(postfix));
                        applied++;
                        ModLog.Info("patched " + entry);
                    }
                    catch (Exception ex)
                    {
                        ModLog.Warn("skip patch " + entry + ": " + ex.Message);
                    }
                }
            }

            return applied;
        }

        private static void StockUpdatePostfix(object __instance)
        {
            try
            {
                ProbeBiasApplier.TryApply(__instance, Mod.Settings);
            }
            catch
            {
                // fail soft — never break simulation ticks
            }
        }
    }
}
#endif

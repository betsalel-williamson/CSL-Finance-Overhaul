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
                int index = ApplyIndexPatches(harmony);
                int ui = ApplyShareFieldPatches(harmony);
                patched = true;
                ModLog.Info(
                    "harmony patched targets=" + applied + " index=" + index + " shareField=" + ui
                );
                // Hot reload / mid-game enable: list Index in the running city if enabled.
                IndexListing.EnsureInLoadedCity();
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
                ShareAmountField.Detach();
            }
            catch
            {
                // fail soft
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

        private static int ApplyIndexPatches(Harmony harmony)
        {
            int applied = 0;
            applied += TryPatch(
                harmony,
                AccessTools.Method(typeof(InvestmentCollection), "Awake"),
                null,
                nameof(IndexCollectionAwakePostfix)
            );
            applied += TryPatch(
                harmony,
                AccessTools.Method(typeof(InvestmentCollection), "OnDestroy"),
                nameof(IndexCollectionOnDestroyPrefix),
                null
            );
            // One hook for every investment type: SafeCalculateValue is declared on the
            // base class and runs after vanilla random variance, so Index carries none.
            applied += TryPatch(
                harmony,
                AccessTools.Method(typeof(InvestmentAI), "SafeCalculateValue"),
                null,
                nameof(IndexSafeCalculateValuePostfix)
            );
            applied += TryPatch(
                harmony,
                AccessTools.Method(typeof(InvestmentAI), "GetLocalizedName"),
                nameof(IndexLocalizedNamePrefix),
                null
            );
            applied += TryPatch(
                harmony,
                AccessTools.Method(typeof(InvestmentAI), "GetLocalizedDescription"),
                nameof(IndexLocalizedDescriptionPrefix),
                null
            );
            return applied;
        }

        private static int ApplyShareFieldPatches(Harmony harmony)
        {
            if (!ShareAmountField.IsReady)
            {
                ModLog.Warn(
                    "share field: vanilla BuySellInvestmentPanel members changed; skipping"
                );
                return 0;
            }

            int applied = 0;
            applied += TryPatch(
                harmony,
                AccessTools.Method(typeof(BuySellInvestmentPanel), "Awake"),
                null,
                nameof(ShareFieldAwakePostfix)
            );
            // Hot reload / mid-game enable: the panel's Awake already ran, so also attach
            // the first time the dialog is shown.
            applied += TryPatch(
                harmony,
                AccessTools.Method(typeof(BuySellInvestmentPanel), "ShowPanel"),
                null,
                nameof(ShareFieldShowPanelPostfix)
            );
            applied += TryPatch(
                harmony,
                AccessTools.Method(typeof(BuySellInvestmentPanel), "SetSliderValue"),
                null,
                nameof(ShareFieldSetSliderValuePostfix)
            );
            applied += TryPatch(
                harmony,
                AccessTools.Method(typeof(BuySellInvestmentPanel), "Update"),
                null,
                nameof(ShareFieldUpdatePostfix)
            );
            applied += TryPatch(
                harmony,
                AccessTools.Method(typeof(BuySellInvestmentPanel), "OnKeyDown"),
                nameof(ShareFieldOnKeyDownPrefix),
                null
            );
            return applied;
        }

        private static void ShareFieldAwakePostfix(BuySellInvestmentPanel __instance)
        {
            try
            {
                ShareAmountField.Attach(__instance);
            }
            catch
            {
                // fail soft — vanilla slider still works
            }
        }

        private static void ShareFieldShowPanelPostfix(BuySellInvestmentPanel __result)
        {
            try
            {
                ShareAmountField.Attach(__result);
                ShareAmountField.MirrorFromLabel();
            }
            catch
            {
                // fail soft
            }
        }

        private static void ShareFieldUpdatePostfix()
        {
            try
            {
                ShareAmountField.Tick();
            }
            catch
            {
                // fail soft — never break the dialog's frame update
            }
        }

        private static void ShareFieldSetSliderValuePostfix()
        {
            try
            {
                ShareAmountField.MirrorFromLabel();
            }
            catch
            {
                // fail soft
            }
        }

        /// <summary>
        /// Vanilla OnKeyDown maps Enter to clicking Buy. While the typed field has
        /// focus, let the field consume keys so Enter submits the number instead.
        /// </summary>
        private static bool ShareFieldOnKeyDownPrefix()
        {
            return !ShareAmountField.HasFocus;
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
                HarmonyMethod prefix =
                    prefixName == null
                        ? null
                        : new HarmonyMethod(
                            typeof(Patcher).GetMethod(
                                prefixName,
                                BindingFlags.Static | BindingFlags.NonPublic
                            )
                        );
                HarmonyMethod postfix =
                    postfixName == null
                        ? null
                        : new HarmonyMethod(
                            typeof(Patcher).GetMethod(
                                postfixName,
                                BindingFlags.Static | BindingFlags.NonPublic
                            )
                        );
                harmony.Patch(target, prefix: prefix, postfix: postfix);
                return 1;
            }
            catch (Exception ex)
            {
                ModLog.Warn("index: skip patch " + target.Name + ": " + ex.Message);
                return 0;
            }
        }

        private static void IndexCollectionAwakePostfix(InvestmentCollection __instance)
        {
            try
            {
                IndexListing.Register(__instance);
            }
            catch
            {
                // fail soft
            }
        }

        private static void IndexCollectionOnDestroyPrefix(InvestmentCollection __instance)
        {
            try
            {
                IndexListing.Unregister();
            }
            catch
            {
                // fail soft
            }
        }

        private static void IndexSafeCalculateValuePostfix(
            InvestmentAI __instance,
            ref Investment data,
            ref float __result
        )
        {
            try
            {
                IndexListing.ApplyMark(__instance, ref data, ref __result);
            }
            catch
            {
                // fail soft — never break simulation ticks
            }
        }

        private static bool IndexLocalizedNamePrefix(InvestmentAI __instance, ref string __result)
        {
            if (!IndexListing.IsIndex(__instance))
            {
                return true;
            }

            __result = CityIndexBasket.IndexName;
            return false;
        }

        private static bool IndexLocalizedDescriptionPrefix(
            InvestmentAI __instance,
            ref string __result
        )
        {
            if (!IndexListing.IsIndex(__instance))
            {
                return true;
            }

            __result =
                "Equal-weight city index of every unlocked sector stock. It moves by their average daily change, so one sector crash hurts less than holding it alone.";
            return false;
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

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

        public static void PatchAll()
        {
            if (patched)
            {
                return;
            }

            try
            {
                Harmony harmony = new Harmony(HarmonyId);
                int index = ApplyIndexPatches(harmony);
                int ui = ApplyShareFieldPatches(harmony);
                int ticker = TryPatch(
                    harmony,
                    AccessTools.Method(typeof(EconomyManager), "SimulationStepInvestments"),
                    null,
                    nameof(TickerSimulationStepPostfix)
                );
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
                harmony.UnpatchAll(HarmonyId);
            }
            catch
            {
                // ignore
            }

            patched = false;
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
                AccessTools.Method(typeof(InvestmentAI), "CreateInvestment"),
                null,
                nameof(IndexCreateInvestmentPostfix)
            );
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
            // Display only: vanilla resamples history so the chart's last point lands
            // between yesterday and today; end it on today's actual price.
            applied += TryPatch(
                harmony,
                AccessTools.Method(typeof(Investment), "FillGraphValues"),
                null,
                nameof(GraphEndsAtTodayPostfix)
            );
            if (ShareChartOverlay.IsReady)
            {
                applied += TryPatch(
                    harmony,
                    AccessTools.Method(typeof(BuySellInvestmentPanel), "UpdateBindings"),
                    null,
                    nameof(ShareChartDialogPostfix)
                );
                applied += TryPatch(
                    harmony,
                    AccessTools.Method(typeof(InvestmentTooltip), "Update"),
                    null,
                    nameof(ShareChartTooltipPostfix)
                );
            }
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
            applied += TryPatch(
                harmony,
                AccessTools.Method(typeof(BuySellInvestmentPanel), "OnButtonClicked"),
                nameof(OrderTicketButtonPrefix),
                null
            );
            applied += TryPatch(
                harmony,
                AccessTools.Method(typeof(EconomyPanel), "UpdateInvestments"),
                null,
                nameof(PortfolioOrdersArrangePostfix)
            );
            applied += TryPatch(
                harmony,
                AccessTools.Method(typeof(BuySellInvestmentPanel), "UpdatePriceNumbers"),
                null,
                nameof(OrderTicketPricePostfix)
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

            try
            {
                OrderTicket.Attach(__instance);
            }
            catch
            {
                // fail soft — vanilla Buy still works
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

            try
            {
                OrderTicket.Attach(__result);
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
                OrderTicket.Refresh();
            }
            catch
            {
                // fail soft
            }
        }

        /// <summary>Right after vanilla sorts Your Portfolio, put order cards back in place.</summary>
        private static void PortfolioOrdersArrangePostfix(EconomyPanel __instance)
        {
            try
            {
                PortfolioOrders.Arrange(__instance);
                PortfolioAvgCost.Apply(__instance);
            }
            catch
            {
                // fail soft — vanilla list kept
            }
        }

        private static void OrderTicketPricePostfix()
        {
            try
            {
                OrderTicket.AfterVanillaPrice();
            }
            catch
            {
                // fail soft
            }
        }

        /// <summary>Prefix: a limit order replaces the vanilla buy; otherwise vanilla runs.</summary>
        private static bool OrderTicketButtonPrefix(BuySellInvestmentPanel __instance)
        {
            try
            {
                return !OrderTicket.HandleButton(__instance);
            }
            catch (Exception ex)
            {
                // Never market-buy when the player chose Limit and something broke.
                ModLog.Warn("order ticket: place failed: " + ex.Message);
                return !OrderTicket.OrderActive;
            }
        }

        /// <summary>
        /// Vanilla OnKeyDown maps Enter to clicking Buy. While the typed field has
        /// focus, let the field consume keys so Enter submits the number instead.
        /// </summary>
        private static bool ShareFieldOnKeyDownPrefix()
        {
            return !ShareAmountField.HasFocus && !OrderTicket.PriceHasFocus;
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
                TestStockListing.Register(__instance);
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
                TestStockListing.Unregister();
            }
            catch
            {
                // fail soft
            }
        }

        private static void TickerSimulationStepPostfix()
        {
            try
            {
                TickerFeed.OnInvestmentsStepped();
            }
            catch
            {
                // fail soft — never break simulation ticks
            }

            try
            {
                OrderDesk.OnInvestmentsStepped();
            }
            catch (Exception ex)
            {
                ModLog.Warn("orders: daily check failed: " + ex.Message);
            }

            try
            {
                BondDesk.OnInvestmentsStepped();
            }
            catch (Exception ex)
            {
                ModLog.Warn("bonds: daily payments failed: " + ex.Message);
            }
        }

        private static void IndexCreateInvestmentPostfix(
            InvestmentAI __instance,
            ref Investment data
        )
        {
            try
            {
                if (IndexListing.IsIndex(__instance))
                {
                    IndexListing.BackfillHistory(ref data);
                }
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
                TestStockListing.ApplyMark(__instance, ref data, ref __result);
            }
            catch
            {
                // fail soft — never break simulation ticks
            }
        }

        private static void ShareChartDialogPostfix(BuySellInvestmentPanel __instance)
        {
            ShareChartOverlay.OnDialogBindings(__instance);
        }

        private static void ShareChartTooltipPostfix(InvestmentTooltip __instance)
        {
            ShareChartOverlay.OnTooltipUpdate(__instance);
        }

        private static void GraphEndsAtTodayPostfix(ref Investment __instance, float[] graphValues)
        {
            try
            {
                if (
                    graphValues != null
                    && graphValues.Length > 0
                    && __instance.allValues != null
                    && __instance.allValues.Count > 0
                )
                {
                    graphValues[graphValues.Length - 1] = __instance.value;
                }
            }
            catch
            {
                // fail soft — vanilla chart kept
            }
        }

        private static bool IndexLocalizedNamePrefix(InvestmentAI __instance, ref string __result)
        {
            if (TestStockListing.IsTest(__instance))
            {
                __result = TestStock.DisplayName;
                return false;
            }

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
            if (TestStockListing.IsTest(__instance))
            {
                __result =
                    "Dev-only test listing for automated game UI scenarios. Its price only moves when a test sets it. Not a real company.";
                return false;
            }

            if (!IndexListing.IsIndex(__instance))
            {
                return true;
            }

            __result =
                "Equal-weight city index of every unlocked sector stock. Each day it moves by the geometric average of their price changes, so one sector crash hurts less than holding it alone.";
            return false;
        }
    }
}
#endif

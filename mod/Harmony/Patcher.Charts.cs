using HarmonyLib; // AccessTools, Harmony

// Game classes used without a using (global namespace, Assembly-CSharp): BuySellInvestmentPanel,
//   Investment, InvestmentTooltip

namespace FinanceOverhaul
{
    /// <summary>Hooks for share value charts (the Buy and Sell dialog and the stock tooltip).</summary>
    public static partial class Patcher
    {
        private static int ApplyChartPatches(Harmony harmony)
        {
            int applied = 0;
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
    }
}

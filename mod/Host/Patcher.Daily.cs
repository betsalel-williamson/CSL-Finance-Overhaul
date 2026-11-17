#if HAS_CITIES
using System;
using HarmonyLib;

namespace FinanceOverhaul
{
    /// <summary>Hooks on the game's daily investment step: ticker feed, open orders, bond payments.</summary>
    public static partial class Patcher
    {
        private static int ApplyDailyPatches(Harmony harmony)
        {
            int ticker = TryPatch(
                harmony,
                AccessTools.Method(typeof(EconomyManager), "SimulationStepInvestments"),
                null,
                nameof(TickerSimulationStepPostfix)
            );
            TryPatch(
                harmony,
                AccessTools.Method(typeof(EconomyManager), "SimulationStepInvestments"),
                nameof(StressStepPrefix),
                nameof(StressStepPostfix)
            );
            // Game bug: a loaded investment table lists used slots as free (scenario 51).
            TryPatch(
                harmony,
                AccessTools.Method(typeof(EconomyManager), "DeserializeInvestments"),
                null,
                nameof(RepairFreeSlotsPostfix)
            );
            return ticker;
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
    }
}
#endif

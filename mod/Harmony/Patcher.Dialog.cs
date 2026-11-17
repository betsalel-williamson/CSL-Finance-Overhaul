using System;
using HarmonyLib;

namespace FinanceOverhaul
{
    /// <summary>
    /// Hooks for the Buy and Sell dialog (typed share amount, order ticket) and Your Portfolio
    /// (order cards, average cost).
    /// </summary>
    public static partial class Patcher
    {
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
                OrderTicket.Tick();
            }
            catch
            {
                // fail soft — never break the dialog's frame update
            }
        }

        private static void ShareFieldSetSliderValuePostfix(BuySellInvestmentPanel __instance)
        {
            try
            {
                SliderThumbSync.Redraw(__instance);
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
    }
}

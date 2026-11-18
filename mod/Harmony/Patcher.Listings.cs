using System;
using FinanceOverhaul.Core;
using HarmonyLib;

namespace FinanceOverhaul
{
    /// <summary>Hooks for the stocks the mod lists: the Index, local companies, the dev test stock and stress stocks.</summary>
    public static partial class Patcher
    {
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
            applied += TryPatch(
                harmony,
                AccessTools.Method(typeof(InvestmentAI), "SetSpriteName"),
                nameof(CoinIconPrefix),
                nameof(CoinIconPostfix)
            );
            return applied;
        }

        private static void CoinIconPrefix(ColossalFramework.UI.UISprite sprite)
        {
            try
            {
                CoinIcons.Before(sprite);
            }
            catch
            {
                // fail soft
            }
        }

        private static void CoinIconPostfix(
            InvestmentAI __instance,
            ColossalFramework.UI.UISprite sprite
        )
        {
            try
            {
                CoinIcons.After(__instance, sprite);
            }
            catch
            {
                // fail soft
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
                    IndexListing.RestoreInto(ref data);
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
                CompanyListing.ApplyMark(__instance, ref __result);
            }
            catch
            {
                // fail soft — never break simulation ticks
            }
        }

        private static void RepairFreeSlotsPostfix()
        {
            try
            {
                StressStocks.RepairFreeSlots();
            }
            catch (Exception ex)
            {
                ModLog.Warn("investments: free-slot repair failed: " + ex.Message);
            }
        }

        private static void StressStepPrefix()
        {
            StressStocks.StepBegin();
        }

        private static void StressStepPostfix()
        {
            StressStocks.StepEnd();
        }

        private static bool IndexLocalizedNamePrefix(InvestmentAI __instance, ref string __result)
        {
            if (StressStocks.IsStress(__instance))
            {
                __result = StressStock.DisplayName(__instance.m_info.m_name);
                return false;
            }

            int companyId;
            if (CompanyListing.IsCompany(__instance, out companyId))
            {
                LocalCompany company = CompanyDesk.CompanyOf(companyId);
                if (company != null)
                {
                    __result = company.Name;
                    return false;
                }
            }

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
            int companyId;
            if (CompanyListing.IsCompany(__instance, out companyId))
            {
                LocalCompany company = CompanyDesk.CompanyOf(companyId);
                if (company != null)
                {
                    __result = Strings.Format(
                        "company.description",
                        company.Name,
                        (CompanyDesk.Rules ?? new IpoRules()).CityStakePercent.ToString()
                    );
                    return false;
                }
            }

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

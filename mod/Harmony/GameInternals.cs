using System.Reflection;
using ColossalFramework.UI;
using HarmonyLib;

namespace FinanceOverhaul
{
    /// <summary>
    /// Every private member of the game that the mod reads or calls by reflection, in one
    /// place: a game update that renames one shows up here, not scattered across the UI code.
    /// A member the game no longer has is null; callers check before use and fall back to the
    /// vanilla behaviour.
    /// </summary>
    internal static class GameInternals
    {
        /// <summary>The Buy / Sell dialog (<see cref="BuySellInvestmentPanel"/>).</summary>
        internal static class BuySell
        {
            public static readonly FieldInfo ShareLabel = Field("m_labelShareSliderNumber");
            public static readonly FieldInfo Slider = Field("m_slider");
            public static readonly FieldInfo SliderValue = Field("m_sliderValue");
            public static readonly FieldInfo Mode = Field("m_mode");
            public static readonly FieldInfo InvestmentId = Field("m_investmentID");
            public static readonly FieldInfo Button = Field("m_button");
            public static readonly FieldInfo Graph = Field("m_graph");
            public static readonly MethodInfo SetSliderValue = Method("SetSliderValue");
            public static readonly MethodInfo GetMaximumShareCount = Method("GetMaximumShareCount");
            public static readonly MethodInfo OnClosed = Method("OnClosed");
            public static readonly MethodInfo UpdatePriceNumbers = Method("UpdatePriceNumbers");

            private static FieldInfo Field(string name)
            {
                return AccessTools.Field(typeof(BuySellInvestmentPanel), name);
            }

            private static MethodInfo Method(string name)
            {
                return AccessTools.Method(typeof(BuySellInvestmentPanel), name);
            }
        }

        /// <summary>The Economy panel (<see cref="EconomyPanel"/>).</summary>
        internal static class Economy
        {
            public static readonly FieldInfo InvestmentTab = Field("kInvestmentTab");
            public static readonly FieldInfo LoansTab = Field("kLoansTab");
            public static readonly FieldInfo InvestmentsList = Field(
                "m_investmentsScrollablePanel"
            );
            public static readonly FieldInfo PurchasableInvestments = Field(
                "m_purchasableInvestments"
            );
            public static readonly MethodInfo OnCloseButton = AccessTools.Method(
                typeof(EconomyPanel),
                "OnCloseButton"
            );

            /// <summary>Index of a tab of the Economy panel (3 when the game's constant is gone).</summary>
            public static int TabIndex(FieldInfo tab)
            {
                return tab == null ? 3 : (int)tab.GetValue(null);
            }

            private static FieldInfo Field(string name)
            {
                return AccessTools.Field(typeof(EconomyPanel), name);
            }
        }

        /// <summary>The stock tooltip on Economy → Investments (<see cref="InvestmentTooltip"/>).</summary>
        internal static class Tooltip
        {
            public static readonly FieldInfo Graph = AccessTools.Field(
                typeof(InvestmentTooltip),
                "m_graph"
            );
            public static readonly FieldInfo InvestmentId = AccessTools.Field(
                typeof(InvestmentTooltip),
                "m_investmentID"
            );
        }

        /// <summary>The economy simulation (<see cref="EconomyManager"/>).</summary>
        internal static class Manager
        {
            /// <summary>The city's cash itself (the public LastCashAmount only updates on a sim step).</summary>
            public static readonly FieldInfo CashAmount = AccessTools.Field(
                typeof(EconomyManager),
                "m_cashAmount"
            );
            public static readonly MethodInfo InvestmentsUpdated = AccessTools.Method(
                typeof(EconomyManager),
                "InvestmentsUpdated"
            );
        }

        /// <summary>The game's UI toolkit (ColossalFramework.UI).</summary>
        internal static class Ui
        {
            public static readonly MethodInfo SliderUpdateValueIndicators = AccessTools.Method(
                typeof(UISlider),
                "UpdateValueIndicators",
                new[] { typeof(float) }
            );
            public static readonly MethodInfo PixelsToUnits = AccessTools.Method(
                typeof(UIComponent),
                "PixelsToUnits"
            );
            public static readonly FieldInfo TextCursorIndex = TextField("m_CursorIndex");
            public static readonly FieldInfo TextSelectionStart = TextField("m_SelectionStart");
            public static readonly FieldInfo TextSelectionEnd = TextField("m_SelectionEnd");
            public static readonly FieldInfo TextScrollIndex = TextField("m_ScrollIndex");

            private static FieldInfo TextField(string name)
            {
                return AccessTools.Field(typeof(UITextField), name);
            }
        }
    }
}

using System.Reflection; // ConstructorInfo, FieldInfo, MethodInfo, ParameterInfo
using ColossalFramework.UI; // UIComponent, UIKeyEventParameter, UISlider, UITextField
using HarmonyLib; // AccessTools

// Game classes used without a using (global namespace, Assembly-CSharp): BuySellInvestmentPanel,
//   ChirpPanel, EconomyManager, EconomyPanel, GameKeyShortcuts, InvestmentTooltip,
//   OptionsMainPanel

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
        /// <summary>The game's keyboard shortcuts while a city is loaded (<see cref="GameKeyShortcuts"/>).</summary>
        internal static class Keys
        {
            /// <summary>What Esc does: closes one open thing, or opens the pause menu.</summary>
            public static readonly MethodInfo Escape = AccessTools.Method(
                typeof(GameKeyShortcuts),
                "Escape"
            );

            /// <summary>The same list for a controller's menu button.</summary>
            public static readonly MethodInfo SteamEscape = AccessTools.Method(
                typeof(GameKeyShortcuts),
                "SteamEscape"
            );

            /// <summary>
            /// Game UI tests: runs the game's own Esc handling, as a key press would. False when
            /// the game has no such handler loaded.
            /// </summary>
            public static bool PressEscape()
            {
                GameKeyShortcuts shortcuts =
                    UnityEngine.Object.FindObjectOfType<GameKeyShortcuts>();
                if (shortcuts == null || Escape == null)
                {
                    return false;
                }

                Escape.Invoke(shortcuts, null);
                return true;
            }
        }

        /// <summary>The game's Options window (<see cref="OptionsMainPanel"/>).</summary>
        internal static class Options
        {
            /// <summary>Rebuilds the category list and every mod's page (calls each OnSettingsUI).</summary>
            public static readonly MethodInfo CreateCategories = AccessTools.Method(
                typeof(OptionsMainPanel),
                "CreateCategories"
            );

            /// <summary>
            /// Rebuilds the mods' pages, as the game does when a mod is switched on or off: a
            /// mod reloaded in place gets no such event, and its page would stay the old
            /// build's. False when the window or the method is not there.
            /// </summary>
            public static bool RebuildModPages()
            {
                if (CreateCategories == null)
                {
                    return false;
                }

                bool done = false;
                foreach (
                    OptionsMainPanel panel in UnityEngine.Resources.FindObjectsOfTypeAll<OptionsMainPanel>()
                )
                {
                    if (panel != null && panel.gameObject.scene.IsValid())
                    {
                        CreateCategories.Invoke(panel, null);
                        done = true;
                    }
                }

                return done;
            }
        }

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
            public static readonly MethodInfo SetInvestment = Method("SetInvestment");
            public static readonly FieldInfo VerticalLayout = Field("m_verticalLayoutPanel");

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

            /// <summary>The factor the game applies to a business's income when it books the city's tax.</summary>
            public static readonly FieldInfo TaxMultiplier = AccessTools.Field(
                typeof(EconomyManager),
                "m_taxMultiplier"
            );
            public static readonly MethodInfo InvestmentsUpdated = AccessTools.Method(
                typeof(EconomyManager),
                "InvestmentsUpdated"
            );
        }

        /// <summary>The game's UI toolkit (ColossalFramework.UI).</summary>
        /// <summary>The Chirper (ChirpPanel).</summary>
        internal static class Chirper
        {
            /// <summary>The scrolling list of chirps the panel shows (its last 16).</summary>
            public static readonly FieldInfo Container = AccessTools.Field(
                typeof(ChirpPanel),
                "m_Container"
            );
        }

        internal static class Ui
        {
            /// <summary>The mouse wheel as the game delivers it: to a control, then up its parents.</summary>
            public static readonly MethodInfo MouseWheel = AccessTools.Method(
                typeof(UIComponent),
                "OnMouseWheelEvenIfInactive"
            );

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

            /// <summary>The text field's own handlers for a key going down and a typed character.</summary>
            public static readonly MethodInfo TextOnKeyDown = AccessTools.Method(
                typeof(UITextField),
                "OnKeyDown"
            );
            public static readonly MethodInfo TextOnKeyPress = AccessTools.Method(
                typeof(UITextField),
                "OnKeyPress"
            );

            /// <summary>
            /// A key event as the game's input hands one to a control (its constructor is not
            /// public): no modifiers held. Null when the game's type has changed shape.
            /// </summary>
            public static UIKeyEventParameter KeyEvent(
                UIComponent source,
                UnityEngine.KeyCode key,
                char character
            )
            {
                foreach (
                    ConstructorInfo c in AccessTools.GetDeclaredConstructors(
                        typeof(UIKeyEventParameter)
                    )
                )
                {
                    ParameterInfo[] ps = c.GetParameters();
                    var args = new object[ps.Length];
                    bool known = true;
                    for (int i = 0; i < ps.Length; i++)
                    {
                        System.Type t = ps[i].ParameterType;
                        if (t == typeof(UnityEngine.KeyCode))
                        {
                            args[i] = key;
                        }
                        else if (t == typeof(char))
                        {
                            args[i] = character;
                        }
                        else if (t == typeof(bool))
                        {
                            args[i] = false;
                        }
                        else if (t.IsAssignableFrom(typeof(UIComponent)))
                        {
                            args[i] = source;
                        }
                        else
                        {
                            known = false;
                        }
                    }

                    if (known)
                    {
                        return c.Invoke(args) as UIKeyEventParameter;
                    }
                }

                return null;
            }

            private static FieldInfo TextField(string name)
            {
                return AccessTools.Field(typeof(UITextField), name);
            }
        }
    }
}

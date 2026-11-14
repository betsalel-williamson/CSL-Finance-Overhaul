#if HAS_CITIES
using System;
using System.Reflection;
using ColossalFramework.UI;
using FinanceOverhaul.Market;
using HarmonyLib;
using UnityEngine;

namespace FinanceOverhaul
{
    /// <summary>
    /// First native-UI edit: makes the Invest dialog's share count typeable.
    /// Vanilla shows the count in a read-only UILabel (LabelShareSliderNumber)
    /// driven by the slider. We overlay a numeric UITextField on that label;
    /// submitting it feeds the vanilla private SetSliderValue, so price, limits
    /// and Buy/Sell stay vanilla. UI-only — nothing is written to the save.
    /// </summary>
    public static class ShareAmountField
    {
        private static readonly FieldInfo LabelField = AccessTools.Field(
            typeof(BuySellInvestmentPanel),
            "m_labelShareSliderNumber"
        );
        private static readonly FieldInfo SliderField = AccessTools.Field(
            typeof(BuySellInvestmentPanel),
            "m_slider"
        );
        private static readonly MethodInfo SetSliderValueMethod = AccessTools.Method(
            typeof(BuySellInvestmentPanel),
            "SetSliderValue"
        );
        private static readonly MethodInfo GetMaximumMethod = AccessTools.Method(
            typeof(BuySellInvestmentPanel),
            "GetMaximumShareCount"
        );

        private static UITextField field;
        private static UILabel label;
        private static BuySellInvestmentPanel owner;
        private static bool failureLogged;

        public static bool IsReady
        {
            get
            {
                return LabelField != null
                    && SliderField != null
                    && SetSliderValueMethod != null
                    && GetMaximumMethod != null;
            }
        }

        public static bool HasFocus
        {
            get { return field != null && field.hasFocus; }
        }

        /// <summary>Postfix target: BuySellInvestmentPanel.Awake.</summary>
        public static void Attach(BuySellInvestmentPanel panel)
        {
            if (panel == null || !IsReady)
            {
                return;
            }

            try
            {
                label = LabelField.GetValue(panel) as UILabel;
                if (label == null || label.parent == null)
                {
                    LogOnce("share field: vanilla share label not found; leaving slider-only UI");
                    return;
                }

                owner = panel;
                field = label.parent.AddUIComponent<UITextField>();
                field.name = "FinanceOverhaulShareAmountField";
                field.size = new Vector2(Mathf.Max(label.width + 16f, 64f), label.height + 4f);
                field.relativePosition = new Vector3(
                    label.relativePosition.x + label.width - field.width,
                    label.relativePosition.y - 2f
                );
                field.anchor = label.anchor;
                field.numericalOnly = true;
                field.allowFloats = false;
                field.maxLength = 9;
                field.selectOnFocus = true;
                field.submitOnFocusLost = true;
                field.builtinKeyNavigation = true;
                field.readOnly = false;
                field.horizontalAlignment = UIHorizontalAlignment.Center;
                field.verticalAlignment = UIVerticalAlignment.Middle;
                field.padding = new RectOffset(4, 4, 2, 2);
                field.textScale = label.textScale;
                field.textColor = label.textColor;
                field.normalBgSprite = "TextFieldPanel";
                field.hoveredBgSprite = "TextFieldPanelHovered";
                field.focusedBgSprite = "TextFieldPanel";
                field.selectionSprite = "EmptySprite";
                field.color = new Color32(28, 28, 28, 255);
                field.tooltip = "Type a share amount, then press Enter";
                field.text = label.text;
                field.eventTextSubmitted += OnSubmitted;
                field.eventTextCancelled += (c, v) => Mirror(label.text);
                label.isVisible = false;
                ModLog.Info("share field: attached typed share amount to Invest dialog");
            }
            catch (Exception ex)
            {
                Detach();
                LogOnce("share field: attach failed, slider-only UI kept: " + ex.Message);
            }
        }

        /// <summary>Postfix target: BuySellInvestmentPanel.SetSliderValue (slider / ± moved).</summary>
        public static void Mirror(string text)
        {
            if (field == null || field.hasFocus)
            {
                return;
            }

            field.text = text ?? string.Empty;
        }

        public static void MirrorFromLabel()
        {
            if (label != null)
            {
                Mirror(label.text);
            }
        }

        private static void OnSubmitted(UIComponent component, string text)
        {
            try
            {
                if (owner == null)
                {
                    return;
                }

                UISlider slider = SliderField.GetValue(owner) as UISlider;
                uint max = (uint)GetMaximumMethod.Invoke(owner, null);
                uint min = slider == null ? 0u : (uint)Mathf.Max(0f, slider.minValue);
                if (ShareAmountInput.TryParse(text, min, max, out uint shares))
                {
                    SetSliderValueMethod.Invoke(owner, new object[] { shares });
                }

                // Always show what vanilla actually holds (clamped or unchanged).
                field.text = label.text;
            }
            catch (Exception ex)
            {
                LogOnce("share field: submit failed: " + ex.Message);
            }
        }

        private static void Detach()
        {
            if (label != null)
            {
                label.isVisible = true;
            }

            if (field != null)
            {
                try
                {
                    UnityEngine.Object.Destroy(field.gameObject);
                }
                catch
                {
                    // fail soft
                }
            }

            field = null;
            owner = null;
        }

        private static void LogOnce(string message)
        {
            if (failureLogged)
            {
                return;
            }

            failureLogged = true;
            ModLog.Warn(message);
        }
    }
}
#endif

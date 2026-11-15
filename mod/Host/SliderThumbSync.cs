#if HAS_CITIES
using System;
using System.Reflection;
using ColossalFramework.UI;
using HarmonyLib;

namespace FinanceOverhaul
{
    /// <summary>
    /// Keeps the Invest dialog's slider thumb where its value is. Colossal's UISlider
    /// redraws the thumb only when the value changes (set_value skips an equal value, and
    /// set_minValue/set_maxValue never redraw), so when the dialog changes the range but
    /// keeps the value (e.g. Sell, value = owned = new maximum, after a Buy left the same
    /// number on a wider range) the thumb stays drawn for the old range. After every
    /// SetSliderValue we redraw it with the slider's own private UpdateValueIndicators.
    /// UI only; fail soft (logs once, vanilla stays as it was).
    /// </summary>
    internal static class SliderThumbSync
    {
        private static readonly FieldInfo SliderField = AccessTools.Field(
            typeof(BuySellInvestmentPanel),
            "m_slider"
        );
        private static readonly MethodInfo UpdateValueIndicatorsMethod = AccessTools.Method(
            typeof(UISlider),
            "UpdateValueIndicators",
            new[] { typeof(float) }
        );
        private static bool failureLogged;

        /// <summary>Postfix helper: BuySellInvestmentPanel.SetSliderValue.</summary>
        public static void Redraw(BuySellInvestmentPanel panel)
        {
            if (panel == null || SliderField == null || UpdateValueIndicatorsMethod == null)
            {
                LogOnce("slider thumb: vanilla members not found; thumb left to vanilla");
                return;
            }

            try
            {
                UISlider slider = SliderField.GetValue(panel) as UISlider;
                if (slider == null || !(slider.maxValue > slider.minValue))
                {
                    // Empty range: vanilla disables the slider; the thumb has no place.
                    return;
                }

                UpdateValueIndicatorsMethod.Invoke(slider, new object[] { slider.value });
            }
            catch (Exception ex)
            {
                LogOnce("slider thumb: redraw failed: " + ex.Message);
            }
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

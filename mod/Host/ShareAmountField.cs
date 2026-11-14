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
    /// driven by the slider. We overlay a numeric UITextField on that label and
    /// drive the vanilla private SetSliderValue, so price, limits and Buy/Sell stay
    /// vanilla. UI-only — nothing is written to the save.
    /// <para>
    /// Editing (rules in <see cref="ShareAmountEditor"/>): typing moves the slider
    /// live; Enter, Tab, or clicking outside applies; Escape restores the value the
    /// field had when it gained focus. Those keys are consumed so the dialog does
    /// not also Buy (Enter) or close (Escape).
    /// </para>
    /// Hot-reload safe: attach is idempotent (Awake or first ShowPanel), stale fields
    /// left by an older assembly are removed by name, and Detach restores vanilla.
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
        private static readonly FieldInfo SliderValueField = AccessTools.Field(
            typeof(BuySellInvestmentPanel),
            "m_sliderValue"
        );
        private static readonly MethodInfo SetSliderValueMethod = AccessTools.Method(
            typeof(BuySellInvestmentPanel),
            "SetSliderValue"
        );
        private static readonly MethodInfo GetMaximumMethod = AccessTools.Method(
            typeof(BuySellInvestmentPanel),
            "GetMaximumShareCount"
        );

        public const string FieldName = "FinanceOverhaulShareAmountField";

        private static readonly ShareAmountEditor Editor = new ShareAmountEditor();

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
                    && SliderValueField != null
                    && SetSliderValueMethod != null
                    && GetMaximumMethod != null;
            }
        }

        public static bool HasFocus
        {
            get { return field != null && field.hasFocus; }
        }

        /// <summary>Postfix targets: BuySellInvestmentPanel.Awake and ShowPanel.</summary>
        public static void Attach(BuySellInvestmentPanel panel)
        {
            if (panel == null || !IsReady)
            {
                return;
            }

            if (field != null && owner == panel)
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

                RemoveStaleFields(label.parent);
                owner = panel;
                field = label.parent.AddUIComponent<UITextField>();
                field.name = FieldName;
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
                // Raises eventKeyDown before the field's own Enter/Escape handling.
                field.builtinKeyNavigation = true;
                field.readOnly = false;
                field.horizontalAlignment = UIHorizontalAlignment.Center;
                field.verticalAlignment = UIVerticalAlignment.Middle;
                if (label.font != null)
                {
                    field.font = label.font;
                }

                field.textScale = label.textScale;
                // UITextField draws text from padding.top (verticalAlignment is ignored),
                // so center it from the rendered line height.
                field.padding = CenteredPadding(field);
                field.textColor = label.textColor;
                field.normalBgSprite = "TextFieldPanel";
                field.hoveredBgSprite = "TextFieldPanelHovered";
                field.focusedBgSprite = "TextFieldPanel";
                field.selectionSprite = "EmptySprite";
                field.color = new Color32(28, 28, 28, 255);
                field.tooltip = "Type a share amount · Enter/Tab apply · Esc reverts";
                field.text = label.text;
                field.eventGotFocus += OnGotFocus;
                field.eventTextChanged += OnTextChanged;
                field.eventKeyDown += OnKeyDown;
                field.eventTextSubmitted += (c, text) => CommitAndShow(text);
                field.eventTextCancelled += (c, text) => Revert();
                field.eventLostFocus += (c, p) => CommitAndShow(field.text);
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
        public static void MirrorFromLabel()
        {
            if (field == null || label == null || field.hasFocus)
            {
                return;
            }

            field.text = label.text ?? string.Empty;
        }

        /// <summary>
        /// Postfix target: BuySellInvestmentPanel.Update. Clicking anywhere outside
        /// the focused field unfocuses it, which applies the typed value.
        /// </summary>
        public static void UnfocusOnOutsideClick()
        {
            if (field == null || !field.hasFocus)
            {
                return;
            }

            if (Input.GetMouseButtonDown(0) && !field.containsMouse)
            {
                field.Unfocus();
            }
        }

        private static void OnGotFocus(UIComponent component, UIFocusEventParameter p)
        {
            if (owner == null)
            {
                return;
            }

            Editor.Begin((uint)SliderValueField.GetValue(owner));
        }

        private static void OnTextChanged(UIComponent component, string text)
        {
            try
            {
                if (owner == null || !field.hasFocus)
                {
                    return;
                }

                GetRange(out uint min, out uint max);
                if (Editor.TryLiveValue(text, min, max, out uint live))
                {
                    SetSlider(live);
                }
            }
            catch (Exception ex)
            {
                LogOnce("share field: live update failed: " + ex.Message);
            }
        }

        private static void OnKeyDown(UIComponent component, UIKeyEventParameter p)
        {
            if (p == null || p.used)
            {
                return;
            }

            switch (p.keycode)
            {
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                case KeyCode.Tab:
                    p.Use();
                    CommitAndShow(field.text);
                    field.Unfocus();
                    break;
                case KeyCode.Escape:
                    p.Use();
                    Revert();
                    field.Unfocus();
                    break;
            }
        }

        private static void CommitAndShow(string text)
        {
            try
            {
                if (owner == null || field == null)
                {
                    return;
                }

                GetRange(out uint min, out uint max);
                SetSlider(Editor.Commit(text, min, max));
                // Show what vanilla actually holds (clamped, or unchanged if invalid).
                field.text = label.text;
            }
            catch (Exception ex)
            {
                LogOnce("share field: apply failed: " + ex.Message);
            }
        }

        private static void Revert()
        {
            try
            {
                if (owner == null || field == null || !Editor.IsEditing)
                {
                    return;
                }

                SetSlider(Editor.Cancel());
                field.text = label.text;
            }
            catch (Exception ex)
            {
                LogOnce("share field: revert failed: " + ex.Message);
            }
        }

        private static RectOffset CenteredPadding(UITextField f)
        {
            float lineHeight = f.font != null ? f.font.lineHeight * f.textScale : label.height;
            int top = Mathf.Max(0, Mathf.RoundToInt((f.height - lineHeight) * 0.5f));
            return new RectOffset(4, 4, top, 0);
        }

        private static void GetRange(out uint min, out uint max)
        {
            UISlider slider = SliderField.GetValue(owner) as UISlider;
            max = (uint)GetMaximumMethod.Invoke(owner, null);
            min = slider == null ? 0u : (uint)Mathf.Max(0f, slider.minValue);
        }

        private static void SetSlider(uint shares)
        {
            SetSliderValueMethod.Invoke(owner, new object[] { shares });
        }

        /// <summary>
        /// Removes fields created by any loaded copy of this mod (hot reload keeps the
        /// old assembly's UI objects alive), matching by name, not by type.
        /// </summary>
        private static void RemoveStaleFields(UIComponent parent)
        {
            UIComponent[] children = parent.GetComponentsInChildren<UIComponent>(true);
            for (int i = 0; i < children.Length; i++)
            {
                UIComponent c = children[i];
                if (c != null && c != field && c.name == FieldName)
                {
                    UnityEngine.Object.Destroy(c.gameObject);
                }
            }
        }

        /// <summary>Called from Patcher.UnpatchAll (hot reload, disable).</summary>
        public static void Detach()
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

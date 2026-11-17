using System;
using System.Reflection;
using ColossalFramework.UI;
using FinanceOverhaul.Core;
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
        private static readonly FieldInfo LabelField = GameInternals.BuySell.ShareLabel;
        private static readonly FieldInfo SliderField = GameInternals.BuySell.Slider;
        private static readonly FieldInfo SliderValueField = GameInternals.BuySell.SliderValue;
        private static readonly MethodInfo SetSliderValueMethod = GameInternals
            .BuySell
            .SetSliderValue;
        private static readonly MethodInfo GetMaximumMethod = GameInternals
            .BuySell
            .GetMaximumShareCount;

        private static readonly MethodInfo PixelsToUnitsMethod = GameInternals.Ui.PixelsToUnits;

        private const float DigitHeightEm = 0.87f;
        private const float DigitTopEm = 0f;

        public const string FieldName = "FinanceOverhaulShareAmountField";
        public const string CaretName = "FinanceOverhaulShareAmountCaret";

        private static readonly FieldInfo CursorIndexField = GameInternals.Ui.TextCursorIndex;
        private static readonly FieldInfo SelectionStartField = GameInternals.Ui.TextSelectionStart;
        private static readonly FieldInfo SelectionEndField = GameInternals.Ui.TextSelectionEnd;
        private static readonly FieldInfo ScrollIndexField = GameInternals.Ui.TextScrollIndex;

        private static readonly ShareAmountEditor Editor = new ShareAmountEditor();

        private static UITextField field;
        private static UILabel label;
        private static BuySellInvestmentPanel owner;
        private static bool failureLogged;

        // Custom caret (vanilla draws its caret from the digit tops, so it cannot
        // reach above the text). Null when the private cursor fields are missing;
        // the vanilla caret is kept in that case.
        private static UISprite caret;
        private static CaretLayout.Bar caretBar;
        private static int lastCursor = -1;
        private static string lastText;
        private static float lastChange;

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
                // Left alignment keeps Colossal's caret/selection math correct; we
                // center the digits with padding instead (see Recenter).
                field.horizontalAlignment = UIHorizontalAlignment.Left;
                if (label.font != null)
                {
                    field.font = label.font;
                }

                field.textScale = label.textScale;
                field.textColor = label.textColor;
                field.normalBgSprite = "TextFieldPanel";
                field.hoveredBgSprite = "TextFieldPanelHovered";
                field.focusedBgSprite = "TextFieldPanel";
                field.selectionSprite = "EmptySprite";
                field.color = FoTheme.Field;
                field.tooltip = Strings.Get("order.shares.tip");
                field.text = label.text;
                Recenter(true);
                field.eventGotFocus += OnGotFocus;
                field.eventTextChanged += OnTextChanged;
                field.eventKeyDown += OnKeyDown;
                field.eventTextSubmitted += (c, text) => CommitAndShow(text);
                field.eventTextCancelled += (c, text) => Revert();
                field.eventLostFocus += (c, p) => CommitAndShow(field.text);
                AttachCaret();
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
            Recenter(false);
        }

        /// <summary>
        /// Postfix target: BuySellInvestmentPanel.Update. Clicking anywhere outside
        /// the focused field unfocuses it (which applies the typed value), and the
        /// custom caret follows the cursor and blinks.
        /// </summary>
        public static void Tick()
        {
            if (field == null)
            {
                return;
            }

            if (field.hasFocus && Input.GetMouseButtonDown(0) && !field.containsMouse)
            {
                field.Unfocus();
            }

            UpdateCaret();
        }

        private static void AttachCaret()
        {
            if (
                CursorIndexField == null
                || SelectionStartField == null
                || SelectionEndField == null
                || ScrollIndexField == null
            )
            {
                LogOnce("share field: cursor fields not found; keeping vanilla caret");
                return;
            }

            caret = field.AddUIComponent<UISprite>();
            caret.name = CaretName;
            // Same atlas + sprite the field uses for its selection highlight.
            caret.atlas = field.atlas;
            caret.spriteName = field.selectionSprite;
            caret.color = field.textColor;
            caret.isInteractive = false;
            caret.size = new Vector2(1.5f, caretBar.Height > 0f ? caretBar.Height : 14f);
            caret.isVisible = false;
            field.cursorWidth = 0;
        }

        private static void UpdateCaret()
        {
            if (caret == null)
            {
                return;
            }

            try
            {
                if (!field.hasFocus)
                {
                    caret.isVisible = false;
                    lastCursor = -1;
                    return;
                }

                string text = field.text ?? string.Empty;
                int cursor = (int)CursorIndexField.GetValue(field);
                int selStart = (int)SelectionStartField.GetValue(field);
                int selEnd = (int)SelectionEndField.GetValue(field);
                int scroll = (int)ScrollIndexField.GetValue(field);
                float now = Time.realtimeSinceStartup;
                if (cursor != lastCursor || text != lastText)
                {
                    lastCursor = cursor;
                    lastText = text;
                    lastChange = now;
                }

                bool selecting = selStart != selEnd;
                if (selecting || !CaretLayout.IsVisible(now - lastChange, field.cursorBlinkTime))
                {
                    caret.isVisible = false;
                    return;
                }

                int from = Mathf.Clamp(scroll, 0, text.Length);
                int to = Mathf.Clamp(cursor, from, text.Length);
                float x = field.padding.left + MeasureWidth(text.Substring(from, to - from));
                caret.relativePosition = new Vector3(
                    Mathf.Min(x, field.width - caret.width),
                    caretBar.Top
                );
                caret.height = caretBar.Height;
                caret.isVisible = true;
            }
            catch (Exception ex)
            {
                // Fall back to the vanilla caret for the rest of the session.
                LogOnce("share field: custom caret failed, using vanilla caret: " + ex.Message);
                UnityEngine.Object.Destroy(caret.gameObject);
                caret = null;
                field.cursorWidth = 1;
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
                Recenter(false);
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
                Recenter(false);
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
                Recenter(false);
            }
            catch (Exception ex)
            {
                LogOnce("share field: revert failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Centers the digits by padding (left alignment keeps caret/selection math
        /// right). Horizontal: measured text width. Vertical: the digit glyphs, not
        /// the font's full line box, with equal bottom padding so the selection
        /// highlight wraps the digits.
        /// </summary>
        private static void Recenter(bool logMetrics)
        {
            if (field == null || field.font == null)
            {
                return;
            }

            try
            {
                float scale = field.textScale;
                float size = field.font.size * scale;
                // Calibrated in game (2026-11-13, box 27 units, font 16 × 0.875): Colossal's
                // dynamic font draws digits from the top of the line box (not down from
                // the baseline), about 0.87 em tall.
                float glyphHeight = size * DigitHeightEm;
                float glyphTop = size * DigitTopEm;
                UIDynamicFont dynamicFont = field.font as UIDynamicFont;
                float textWidth = MeasureWidth(field.text);
                TextBoxLayout.Padding p = TextBoxLayout.Center(
                    field.width,
                    field.height,
                    textWidth,
                    glyphTop,
                    glyphHeight,
                    0f,
                    4
                );
                field.padding = new RectOffset(p.Left, 4, p.Top, p.Bottom);
                caretBar = CaretLayout.Measure(p.Top, glyphTop, glyphHeight, size);
                if (logMetrics)
                {
                    ModLog.Info(
                        "share field: metrics box="
                            + field.width
                            + "x"
                            + field.height
                            + " fontSize="
                            + field.font.size
                            + " lineHeight="
                            + field.font.lineHeight
                            + " baseline="
                            + (dynamicFont != null ? dynamicFont.baseline.ToString() : "n/a")
                            + " scale="
                            + scale
                            + " textW="
                            + textWidth
                            + " pad=L"
                            + p.Left
                            + "/T"
                            + p.Top
                            + "/B"
                            + p.Bottom
                    );
                }
            }
            catch (Exception ex)
            {
                LogOnce("share field: centering failed, text stays left: " + ex.Message);
            }
        }

        private static float MeasureWidth(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0f;
            }

            UIFontRenderer renderer = field.font.ObtainRenderer();
            try
            {
                renderer.wordWrap = false;
                renderer.processMarkup = false;
                renderer.textScale = field.textScale;
                if (PixelsToUnitsMethod != null)
                {
                    renderer.pixelRatio = (float)PixelsToUnitsMethod.Invoke(field, null);
                }

                return renderer.MeasureString(text).x;
            }
            finally
            {
                renderer.Release();
            }
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

            caret = null;
            lastCursor = -1;
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

using System.Collections.Generic; // List
// ColossalFramework.UI: UIAnchorStyle, UIComponent, UIHorizontalAlignment, UILabel, UIPanel,
//   UISlider, UITextField, UIVerticalAlignment
using ColossalFramework.UI;
using FinanceOverhaul.Core; // IpoTuning, ModSettings, Strings, TuningAudience, TuningGroup
using ICities; // UIHelperBase
using UnityEngine; // RectOffset, Vector2, Vector3

namespace FinanceOverhaul
{
    /// <summary>
    /// The Local IPOs values in Options (docs/features/local-ipos.md, Options): one slider per
    /// <see cref="IpoTuning"/>, in the player's groups while Local IPOs is on, and the finer
    /// workings in dev builds. Each slider's label says its value; a change is used from the
    /// next game day and saved with the player's settings.
    /// </summary>
    internal static class IpoOptions
    {
        private sealed class Row
        {
            public IpoTuning Tuning;
            public UISlider Slider;
            public UILabel Label;
            public UITextField Field;
        }

        private static readonly List<Row> rows = new List<Row>();

        /// <summary>The player's groups (every build, while Local IPOs is on).</summary>
        public static void AddPlayerGroups(UIHelperBase helper, ModSettings settings)
        {
            rows.RemoveAll(r => r.Tuning.Audience == TuningAudience.Player);
            foreach (
                TuningGroup group in new[]
                {
                    TuningGroup.Listing,
                    TuningGroup.Candidates,
                    TuningGroup.Prices,
                    TuningGroup.BuyoutsAndClosing,
                }
            )
            {
                AddGroup(helper, settings, group, TuningAudience.Player);
            }

            helper.AddButton(
                Strings.Get("options.ipo.reset"),
                () => Reset(settings, TuningAudience.Player)
            );
        }

        /// <summary>The finer workings (dev builds only).</summary>
        public static void AddDevGroup(UIHelperBase helper, ModSettings settings)
        {
            rows.RemoveAll(r => r.Tuning.Audience == TuningAudience.Dev);
            UIHelperBase group = AddGroup(helper, settings, TuningGroup.Market, TuningAudience.Dev);
            group.AddButton(
                Strings.Get("options.ipo.resetDev"),
                () => Reset(settings, TuningAudience.Dev)
            );
        }

        private static UIHelperBase AddGroup(
            UIHelperBase helper,
            ModSettings settings,
            TuningGroup which,
            TuningAudience audience
        )
        {
            UIHelperBase group = helper.AddGroup(Strings.Get("options.ipo.group." + which));
            foreach (IpoTuning t in IpoTuning.All)
            {
                if (t.Group != which || t.Audience != audience)
                {
                    continue;
                }

                IpoTuning tuning = t;
                var row = new Row { Tuning = tuning };
                row.Slider =
                    group.AddSlider(
                        tuning.Label(settings.Ipo),
                        tuning.Min * tuning.Factor,
                        tuning.Max * tuning.Factor,
                        tuning.Step * tuning.Factor,
                        tuning.Shown(tuning.Get(settings.Ipo)),
                        v => OnChanged(settings, row, v)
                    ) as UISlider;
                row.Label =
                    row.Slider == null || row.Slider.parent == null
                        ? null
                        : row.Slider.parent.Find<UILabel>("Label"); // not shown: the game's template name
                Widen(row);
                AddFieldAndEnds(settings, row);
                if (row.Slider != null)
                {
                    row.Slider.name = "FinanceOverhaulIpoOption-" + tuning.Key; // not shown: a control name
                    // Saved once the player lets go, not on every step of a drag.
                    row.Slider.eventMouseUp += (c, p) => Mod.SaveSettings();
                }

                rows.Add(row);
            }

            return group;
        }

        // The game's slider row is narrow: a sentence with its value would wrap onto the slider.
        // The slider leaves room on its right for the number field; Min and Max sit under its
        // two ends.
        private const float RowWidth = 680f;
        private const float SliderWidth = 520f;
        private const float FieldWidth = 110f;
        private const float FieldHeight = 26f;
        private const float EndsHeight = 16f;

        /// <summary>Control names: the number field of an option is this plus its key.</summary>
        internal const string FieldPrefix = "FinanceOverhaulIpoField-"; // not shown: a control name

        private static void Widen(Row row)
        {
            if (row.Slider == null || row.Slider.parent == null)
            {
                return;
            }

            row.Slider.parent.width = RowWidth;
            row.Slider.width = SliderWidth;
            if (row.Label != null)
            {
                row.Label.autoSize = false;
                row.Label.wordWrap = false;
                row.Label.width = RowWidth;
            }
        }

        /// <summary>
        /// The row, laid out by hand (the game's row stacks its children, which would put the
        /// field and the ends under the slider and over the next row): the sentence on top, the
        /// slider under it with the number field on its right, Min and Max under its ends.
        /// </summary>
        private static void AddFieldAndEnds(ModSettings settings, Row row)
        {
            UIComponent parent = row.Slider == null ? null : row.Slider.parent;
            if (parent == null)
            {
                return;
            }

            UISlider slider = row.Slider;
            var panel = parent as UIPanel;
            if (panel != null)
            {
                panel.autoLayout = false;
            }

            // The game anchors its label and slider to the row's bottom: anchor them to the top
            // and size the row first, so nothing moves when it grows.
            const float labelHeight = 22f;
            float endsY = labelHeight + 8f + FieldHeight + 2f;
            const UIAnchorStyle TopLeft = UIAnchorStyle.Top | UIAnchorStyle.Left;
            slider.anchor = TopLeft;
            if (row.Label != null)
            {
                row.Label.anchor = TopLeft;
            }

            parent.height = endsY + EndsHeight + 10f;

            // The game's label is a tall box with its text at the bottom: a line's height, on top.
            if (row.Label != null)
            {
                row.Label.autoHeight = false;
                row.Label.height = labelHeight;
                row.Label.verticalAlignment = UIVerticalAlignment.Top;
                row.Label.relativePosition = new Vector3(0f, 0f);
            }

            float sliderY = labelHeight + 8f + (FieldHeight - slider.height) / 2f;
            slider.relativePosition = new Vector3(0f, sliderY);

            UITextField field = parent.AddUIComponent<UITextField>();
            field.name = FieldPrefix + row.Tuning.Key;
            field.size = new Vector2(FieldWidth, FieldHeight);
            field.relativePosition = new Vector3(SliderWidth + 16f, labelHeight + 8f);
            field.normalBgSprite = FoTheme.GameFieldSprite;
            field.hoveredBgSprite = FoTheme.GameFieldSprite;
            field.focusedBgSprite = FoTheme.GameFieldSprite;
            field.selectionSprite = "EmptySprite"; // not shown: a game sprite name
            field.color = FoTheme.Field;
            field.textColor = FoTheme.GameText;
            field.horizontalAlignment = UIHorizontalAlignment.Right;
            field.padding = new RectOffset(6, 8, 6, 4);
            field.builtinKeyNavigation = true;
            field.readOnly = false;
            field.selectOnFocus = true;
            field.submitOnFocusLost = true;
            field.numericalOnly = false; // a minus and separators are allowed; checked on submit
            field.maxLength = 8;
            field.tooltip = Strings.Get("options.ipo.field.tip");
            if (row.Label != null && row.Label.font != null)
            {
                field.font = row.Label.font;
                field.textScale = 0.875f;
            }

            field.text = row.Tuning.FieldText(settings.Ipo);
            field.eventTextSubmitted += (c, text) => OnTyped(settings, row, text);
            row.Field = field;

            UILabel min = EndLabel(parent, row.Tuning.MinLabel(), row.Label);
            min.relativePosition = new Vector3(0f, endsY);
            UILabel max = EndLabel(parent, row.Tuning.MaxLabel(), row.Label);
            max.relativePosition = new Vector3(SliderWidth - max.width, endsY);
        }

        private static UILabel EndLabel(UIComponent parent, string text, UILabel style)
        {
            UILabel label = parent.AddUIComponent<UILabel>();
            if (style != null && style.font != null)
            {
                label.font = style.font;
            }

            label.textScale = 0.75f;
            label.textColor = FoTheme.TextMuted;
            label.text = text;
            return label;
        }

        /// <summary>
        /// Game UI tests: types <paramref name="text"/> into the newest page's field for this
        /// key and submits it, as the player would. Returns the field's text afterwards (null
        /// without such a field).
        /// </summary>
        internal static string TestType(string key, string text)
        {
            for (int i = rows.Count - 1; i >= 0; i--)
            {
                Row row = rows[i];
                if (row.Field != null && row.Tuning.Key == key)
                {
                    row.Field.text = text;
                    OnTyped(Mod.EnsureSettings(), row, text);
                    return row.Field.text;
                }
            }

            return null;
        }

        /// <summary>A number typed and submitted: snapped and kept in range, then shown back.</summary>
        private static void OnTyped(ModSettings settings, Row row, string text)
        {
            int shown;
            if (IpoTuning.TryParseTyped(text, out shown))
            {
                int stored = row.Tuning.Stored(shown);
                if (row.Slider != null)
                {
                    row.Slider.value = row.Tuning.Shown(stored); // runs OnChanged
                }

                if (row.Tuning.Get(settings.Ipo) != stored)
                {
                    OnChanged(settings, row, row.Tuning.Shown(stored));
                }

                Mod.SaveSettings();
            }

            if (row.Field != null)
            {
                row.Field.text = row.Tuning.FieldText(settings.Ipo);
            }
        }

        private static void OnChanged(ModSettings settings, Row row, float shown)
        {
            int stored = row.Tuning.Stored((int)System.Math.Round(shown));
            if (row.Tuning.Get(settings.Ipo) == stored)
            {
                return;
            }

            row.Tuning.Set(settings.Ipo, stored);
            if (row.Label != null)
            {
                row.Label.text = row.Tuning.Label(settings.Ipo);
            }

            if (row.Field != null && !row.Field.hasFocus)
            {
                row.Field.text = row.Tuning.FieldText(settings.Ipo);
            }

            ModLog.Info("ipo option " + row.Tuning.Key + "=" + stored);
        }

        private static void Reset(ModSettings settings, TuningAudience audience)
        {
            IpoTuning.Reset(settings.Ipo, audience);
            foreach (Row row in rows)
            {
                if (row.Tuning.Audience != audience)
                {
                    continue;
                }

                int stored = row.Tuning.Get(settings.Ipo);
                if (row.Slider != null)
                {
                    row.Slider.value = row.Tuning.Shown(stored);
                }

                if (row.Label != null)
                {
                    row.Label.text = row.Tuning.Label(settings.Ipo);
                }

                if (row.Field != null)
                {
                    row.Field.text = row.Tuning.FieldText(settings.Ipo);
                }
            }

            Mod.SaveSettings();
            ModLog.Info("ipo options reset (" + audience + ")");
        }
    }
}

using System.Collections.Generic; // List
// ColossalFramework.UI: LayoutDirection, UIAnchorStyle, UIButton, UIComponent, UIDropDown,
//   UIHorizontalAlignment, UILabel, UIPanel, UISlider, UITextField, UIVerticalAlignment
using ColossalFramework.UI;
// FinanceOverhaul.Core: IpoPresets, IpoRules, IpoTuning, IpoUserPreset, ModSettings, Strings,
//   TuningAudience, TuningGroup, TuningPreset
using FinanceOverhaul.Core;
using ICities; // UIHelperBase
using UnityEngine; // RectOffset, Vector2, Vector3

// Game classes used without a using (global namespace, Assembly-CSharp): UIHelper

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

        private static UIDropDown presetDropdown;
        private static string[] presetItems = new string[0];
        private static bool presetSyncing;
        private static UIButton deleteButton;

        /// <summary>Control names (game UI tests).</summary>
        internal const string PresetDropdownName = "FinanceOverhaulIpoPreset"; // not shown: a control name
        internal const string SaveAsName = "FinanceOverhaulIpoPresetSaveAs"; // not shown: a control name
        internal const string DeleteName = "FinanceOverhaulIpoPresetDelete"; // not shown: a control name
        internal const string SectionButtonPrefix = "FinanceOverhaulIpoSection-"; // not shown: a control name

        /// <summary>The player's groups (every build, while Local IPOs is on).</summary>
        public static void AddPlayerGroups(UIHelperBase helper, ModSettings settings)
        {
            rows.RemoveAll(r => r.Tuning.Audience == TuningAudience.Player);
            AddPresets(helper, settings);
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

            RefreshPresets(settings);
        }

        /// <summary>
        /// The presets section: the preset in use (a built-in, a saved one or New Preset),
        /// Save as… and Delete, as in Trackpad Camera Control.
        /// </summary>
        private static void AddPresets(UIHelperBase helper, ModSettings settings)
        {
            UIHelperBase group = helper.AddGroup(Strings.Get("options.ipo.group.Presets"));
            presetItems = IpoPresets.Names(settings.Ipo, settings.IpoUserPresets).ToArray();
            string current = IpoPresets.Identify(settings.Ipo, settings.IpoUserPresets);
            presetDropdown =
                group.AddDropdown(
                    Strings.Get("options.ipo.preset"),
                    presetItems,
                    System.Array.IndexOf(presetItems, current),
                    index => OnPresetPicked(settings, index)
                ) as UIDropDown;
            if (presetDropdown != null)
            {
                presetDropdown.name = PresetDropdownName;
                presetDropdown.width = 380f; // room for a saved preset's name
                presetDropdown.tooltip = Strings.Get("options.ipo.preset.tip");
            }

            UIComponent container = Container(group);
            if (container == null)
            {
                return;
            }

            UIPanel buttons = ButtonRow(container);
            UIButton save = FoButton.Add(
                buttons,
                SaveAsName,
                Strings.Get("options.ipo.preset.saveAs"),
                new Vector2(140f, 30f),
                0.875f
            );
            save.eventClick += (c, e) => OpenSaveAs(settings);
            deleteButton = FoButton.Add(
                buttons,
                DeleteName,
                Strings.Get("options.ipo.preset.delete"),
                new Vector2(140f, 30f),
                0.875f
            );
            deleteButton.eventClick += (c, e) => DeleteCurrent(settings);
        }

        private static void OnPresetPicked(ModSettings settings, int index)
        {
            if (presetSyncing || index < 0 || index >= presetItems.Length)
            {
                return;
            }

            if (IpoPresets.ApplyNamed(settings.Ipo, settings.IpoUserPresets, presetItems[index]))
            {
                ModLog.Info("ipo preset " + presetItems[index]);
                AfterChange(settings);
            }
        }

        /// <summary>Opens Save as…: the values now are kept under the name given.</summary>
        internal static void OpenSaveAs(ModSettings settings)
        {
            FoNameDialog.Show(
                Strings.Get("options.ipo.preset.saveTitle"),
                IpoPresets.SuggestName(settings.IpoUserPresets),
                Strings.Get("options.ipo.preset.ok"),
                Strings.Get("options.ipo.preset.cancel"),
                name =>
                {
                    if (!IpoPresets.Save(settings.IpoUserPresets, settings.Ipo, name))
                    {
                        return false; // empty or a built-in's name: the dialog stays open
                    }

                    ModLog.Info("ipo preset saved: " + IpoPresets.Clean(name));
                    AfterChange(settings);
                    return true;
                }
            );
        }

        /// <summary>Deletes the saved preset in use (not a built-in or New Preset).</summary>
        internal static bool DeleteCurrent(ModSettings settings)
        {
            string current = IpoPresets.Identify(settings.Ipo, settings.IpoUserPresets);
            if (!IpoPresets.Delete(settings.IpoUserPresets, current))
            {
                return false;
            }

            ModLog.Info("ipo preset deleted: " + current);
            AfterChange(settings);
            return true;
        }

        /// <summary>Applies a quick preset to one section, as its buttons do.</summary>
        internal static void ApplySection(
            ModSettings settings,
            TuningGroup group,
            TuningPreset preset
        )
        {
            int changed = IpoTuning.Apply(settings.Ipo, group, preset);
            ModLog.Info(
                "ipo section " + group + " set to " + preset + " (" + changed + " value(s))"
            );
            AfterChange(settings);
        }

        /// <summary>Game UI tests: the values and saved presets are replaced, and the page shows them.</summary>
        internal static void TestPutBack(IpoRules values, List<IpoUserPreset> presets)
        {
            ModSettings settings = Mod.EnsureSettings();
            foreach (IpoTuning t in IpoTuning.All)
            {
                t.Set(settings.Ipo, t.Get(values));
            }

            settings.IpoUserPresets.Clear();
            settings.IpoUserPresets.AddRange(presets);
            AfterChange(settings);
        }

        /// <summary>The preset the dropdown shows now (null without one).</summary>
        internal static string ShownPreset
        {
            get
            {
                return
                    presetDropdown == null
                    || presetDropdown.selectedIndex < 0
                    || presetDropdown.selectedIndex >= presetItems.Length
                    ? null
                    : presetItems[presetDropdown.selectedIndex];
            }
        }

        /// <summary>After values changed in bulk: every row, the presets and the file.</summary>
        private static void AfterChange(ModSettings settings)
        {
            foreach (Row row in rows)
            {
                ShowRow(settings, row);
            }

            RefreshPresets(settings);
            Mod.SaveSettings();
        }

        private static void ShowRow(ModSettings settings, Row row)
        {
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

        /// <summary>The dropdown shows the preset the values are now; Delete is for a saved one.</summary>
        private static void RefreshPresets(ModSettings settings)
        {
            if (presetDropdown == null)
            {
                return;
            }

            presetItems = IpoPresets.Names(settings.Ipo, settings.IpoUserPresets).ToArray();
            string current = IpoPresets.Identify(settings.Ipo, settings.IpoUserPresets);
            presetSyncing = true;
            try
            {
                presetDropdown.items = presetItems;
                presetDropdown.selectedIndex = System.Array.IndexOf(presetItems, current);
            }
            finally
            {
                presetSyncing = false;
            }

            if (deleteButton != null)
            {
                bool saved = false;
                foreach (IpoUserPreset p in settings.IpoUserPresets)
                {
                    saved |= p.Name == current;
                }

                deleteButton.isEnabled = saved;
            }
        }

        private static UIComponent Container(UIHelperBase group)
        {
            var h = group as UIHelper;
            return h == null ? null : h.self as UIComponent;
        }

        private static UIPanel ButtonRow(UIComponent container)
        {
            UIPanel row = container.AddUIComponent<UIPanel>();
            row.size = new Vector2(RowWidth, 34f);
            row.autoLayout = true;
            row.autoLayoutDirection = LayoutDirection.Horizontal;
            row.autoLayoutPadding = new RectOffset(0, 10, 2, 2);
            return row;
        }

        /// <summary>Less likely, Defaults and More likely for one section (the dev section: Defaults).</summary>
        private static void AddSectionButtons(
            UIHelperBase group,
            ModSettings settings,
            TuningGroup which
        )
        {
            UIComponent container = Container(group);
            if (container == null)
            {
                return;
            }

            UIPanel buttons = ButtonRow(container);
            string tip = Strings.Get("options.ipo.preset.tip." + which);
            foreach (
                TuningPreset preset in IpoTuning.HasPresets(which)
                    ? new[]
                    {
                        TuningPreset.LessLikely,
                        TuningPreset.Default,
                        TuningPreset.MoreLikely,
                    }
                    : new[] { TuningPreset.Default }
            )
            {
                TuningPreset p = preset;
                UIButton b = FoButton.Add(
                    buttons,
                    SectionButtonPrefix + which + "-" + preset,
                    Strings.Get("options.ipo.preset." + preset),
                    new Vector2(140f, 30f),
                    0.875f
                );
                b.tooltip = tip;
                b.eventClick += (c, e) => ApplySection(settings, which, p);
            }
        }

        /// <summary>The finer workings (dev builds only).</summary>
        public static void AddDevGroup(UIHelperBase helper, ModSettings settings)
        {
            rows.RemoveAll(r => r.Tuning.Audience == TuningAudience.Dev);
            AddGroup(helper, settings, TuningGroup.Market, TuningAudience.Dev);
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

            AddSectionButtons(group, settings, which);
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
            RefreshPresets(settings);
        }
    }
}

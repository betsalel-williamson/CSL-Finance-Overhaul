using System.Collections.Generic; // List
using ColossalFramework.UI; // UILabel, UISlider
using FinanceOverhaul.Core; // IpoTuning, ModSettings, Strings, TuningAudience, TuningGroup
using ICities; // UIHelperBase

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
        private const float RowWidth = 680f;
        private const float SliderWidth = 600f;

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
            }

            Mod.SaveSettings();
            ModLog.Info("ipo options reset (" + audience + ")");
        }
    }
}

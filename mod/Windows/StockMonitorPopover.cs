using System;
using System.Collections.Generic;
using ColossalFramework.UI;
using FinanceOverhaul.Core;
using UnityEngine;

namespace FinanceOverhaul
{
    /// <summary>
    /// Stock Monitor settings popover (Figma band 10, pin 2), opened by the gear: background
    /// opacity (0–90%), solid while hovered, compact rows, sparklines, ₡ or % change, and which
    /// stocks show. Floats beside the window (right, or left near the screen edge) and follows
    /// it. Everything is kept per player in the settings file.
    /// </summary>
    public sealed partial class StockMonitorHost
    {
        public const string PopoverName = Prefix + "SettingsPanel";
        private const float PopW = 260f;
        private const float TrackW = 232f;

        private UIPanel popover;
        private UILabel opacityValue;
        private UIPanel trackFill;
        private UIPanel knob;
        private UIPanel stockGrid;
        private readonly List<Action> refreshers = new List<Action>();

        // Refreshers up to here belong to the popover itself; later ones to the stock grid.
        private int fixedRefreshers;

        private void TogglePopover()
        {
            if (popover != null && popover.isVisible)
            {
                HidePopover();
                return;
            }

            if (popover == null)
            {
                BuildPopover();
            }

            BuildStockGrid();
            popover.isVisible = true;
            RefreshPopover();
            FollowWindow();
            popover.BringToFront();
        }

        private void HidePopover()
        {
            if (popover != null)
            {
                popover.isVisible = false;
            }
        }

        private void DestroyPopover()
        {
            if (popover != null)
            {
                Destroy(popover.gameObject);
            }

            popover = null;
            refreshers.Clear();
        }

        /// <summary>Keeps the popover beside the window (it is a separate floating panel).</summary>
        private void FollowWindow()
        {
            if (popover == null || !popover.isVisible || window == null)
            {
                return;
            }

            Vector3 w = window.absolutePosition;
            float screenW = UIView.GetAView().GetScreenResolution().x;
            float x = w.x + Width + 6f;
            if (x + PopW > screenW)
            {
                x = w.x - PopW - 6f;
            }

            popover.absolutePosition = Clamp(new Vector3(x, w.y), popover.size);
        }

        private void RefreshPopover()
        {
            if (popover == null || !popover.isVisible)
            {
                return;
            }

            foreach (Action refresh in refreshers)
            {
                refresh();
            }
        }

        private void BuildPopover()
        {
            UIView view = UIView.GetAView();
            popover = (UIPanel)view.AddUIComponent(typeof(UIPanel));
            popover.name = PopoverName;
            popover.width = PopW;
            popover.backgroundSprite = "GenericPanelWhite";
            popover.color = new Color32(10, 13, 15, 235);

            UILabel title = Label(
                popover,
                Prefix + "SettingsTitle",
                "Stock Monitor settings",
                0.9f,
                White
            );
            title.relativePosition = new Vector3(14f, 10f);

            // Opacity slider: our own track (no guessed game sprites), 5% steps.
            UILabel opacityLabel = Label(popover, null, "Background opacity", 0.75f, White);
            opacityLabel.relativePosition = new Vector3(14f, 40f);
            opacityValue = Label(popover, Prefix + "OpacityValue", "", 0.75f, Accent);
            opacityValue.autoSize = false;
            opacityValue.size = new Vector2(60f, 16f);
            opacityValue.textAlignment = UIHorizontalAlignment.Right;
            opacityValue.relativePosition = new Vector3(PopW - 74f, 40f);

            UIPanel track = popover.AddUIComponent<UIPanel>();
            track.name = Prefix + "OpacityTrack";
            track.size = new Vector2(TrackW, 20f); // a generous hit area around the 4 px bar
            track.relativePosition = new Vector3(14f, 56f);
            UIPanel bar = Box(track, new Color32(255, 255, 255, 64), TrackW, 4f, 0f, 8f);
            bar.isInteractive = false;
            trackFill = Box(track, Accent, 0f, 4f, 0f, 8f);
            trackFill.isInteractive = false;
            knob = Box(track, White, 12f, 12f, 0f, 4f);
            knob.isInteractive = false;
            track.eventClick += (c, p) => SetOpacityFromMouse(track);
            track.eventMouseMove += (c, p) =>
            {
                if (Input.GetMouseButton(0))
                {
                    SetOpacityFromMouse(track);
                }
            };
            refreshers.Add(() =>
            {
                float o = Settings.Opacity;
                opacityValue.text = StockMonitor.PercentText(o);
                float x = TrackW * o / StockMonitorSettings.MaxOpacity;
                trackFill.width = x;
                knob.relativePosition = new Vector3(Mathf.Clamp(x - 6f, 0f, TrackW - 12f), 4f);
            });

            UILabel range = Label(
                popover,
                null,
                "0% (see-through, like City Vitals) – 90%",
                0.6f,
                Muted
            );
            range.relativePosition = new Vector3(14f, 78f);

            float y = 100f;
            y = Toggle(
                "SolidOnHover",
                "Solid while the mouse is over it",
                y,
                () => Settings.TransparentUnhovered,
                v => Settings.TransparentUnhovered = v
            );
            y = Toggle(
                "Compact",
                "Compact rows (hide names)",
                y,
                () => Settings.CompactRows,
                v => Settings.CompactRows = v
            );
            y = Toggle(
                "Sparklines",
                "Show sparklines",
                y,
                () => Settings.ShowSparklines,
                v => Settings.ShowSparklines = v
            );
            y = Toggle(
                "ChangeCurrency",
                "Change as ₡ instead of %",
                y,
                () => Settings.ChangeAsCurrency,
                v => Settings.ChangeAsCurrency = v
            );

            UILabel stocks = Label(popover, null, "Stocks shown", 0.8f, White);
            stocks.relativePosition = new Vector3(14f, y + 6f);

            fixedRefreshers = refreshers.Count;
            stockGrid = popover.AddUIComponent<UIPanel>();
            stockGrid.name = Prefix + "StockGrid";
            stockGrid.relativePosition = new Vector3(14f, y + 28f);
            stockGrid.width = PopW - 28f;
        }

        private float Toggle(string key, string text, float y, Func<bool> get, Action<bool> set)
        {
            UIPanel row = CheckRow(
                popover,
                Prefix + "Opt-" + key,
                text,
                14f,
                y,
                0.75f,
                get,
                v =>
                {
                    set(v);
                    Mod.SaveSettings();
                    ApplyOpacity();
                    Refresh(true);
                }
            );
            return y + 24f;
        }

        /// <summary>Rebuilt on every open: the listed stocks can change between opens.</summary>
        private void BuildStockGrid()
        {
            UiCleanup.RemoveChildrenWhere(stockGrid, c => true);
            if (refreshers.Count > fixedRefreshers)
            {
                refreshers.RemoveRange(fixedRefreshers, refreshers.Count - fixedRefreshers);
            }
            List<MonitorQuote> quotes = Quotes();
            quotes.Sort((a, b) => string.CompareOrdinal(a.Symbol, b.Symbol));
            const float cellW = 58f,
                cellH = 20f;
            for (int i = 0; i < quotes.Count; i++)
            {
                MonitorQuote q = quotes[i];
                var check = new StockCheck { Prefab = q.Prefab };
                float x = (i % 4) * cellW,
                    y = (i / 4) * cellH;
                CheckRow(
                    stockGrid,
                    Prefix + "Show-" + q.Symbol,
                    q.Symbol,
                    x,
                    y,
                    0.7f,
                    check.Get,
                    v =>
                    {
                        check.Set(v);
                        Mod.SaveSettings();
                        Refresh(true);
                    }
                );
            }

            int rowsCount = (quotes.Count + 3) / 4;
            stockGrid.height = rowsCount * cellH;
            UILabel note = stockGrid.parent.Find<UILabel>(Prefix + "SettingsNote");
            if (note == null)
            {
                note = Label(
                    popover,
                    Prefix + "SettingsNote",
                    "Kept per player (settings file), not in the city save.",
                    0.6f,
                    Muted
                );
                note.autoSize = false;
                note.wordWrap = true;
                note.autoHeight = true;
                note.width = PopW - 28f;
            }

            note.relativePosition = new Vector3(
                14f,
                stockGrid.relativePosition.y + stockGrid.height + 8f
            );
            popover.height = note.relativePosition.y + Mathf.Max(note.height, 14f) + 12f;
        }

        private sealed class StockCheck
        {
            public string Prefab;

            public bool Get()
            {
                return !Settings.Hidden.Contains(Prefab);
            }

            public void Set(bool shown)
            {
                if (shown)
                {
                    Settings.Hidden.Remove(Prefab);
                }
                else
                {
                    Settings.Hidden.Add(Prefab);
                }
            }
        }

        /// <summary>A checkbox (the game's own box and tick) and a label; the row toggles.</summary>
        private UIPanel CheckRow(
            UIComponent parent,
            string name,
            string text,
            float x,
            float y,
            float scale,
            Func<bool> get,
            Action<bool> set
        )
        {
            UIPanel row = parent.AddUIComponent<UIPanel>();
            row.name = name;
            row.relativePosition = new Vector3(x, y);
            Action<bool> show = FoCheckbox.AddBox(row, 0f, 0f);
            UILabel label = Label(row, null, text, scale, White);
            label.relativePosition = new Vector3(FoCheckbox.Size + 5f, 1f);
            label.isInteractive = false;
            row.size = new Vector2(label.width + FoCheckbox.Size + 9f, 18f);

            Action paint = () => show(get());
            refreshers.Add(paint);
            row.eventClick += (c, p) =>
            {
                set(!get());
                paint();
            };
            paint();
            return row;
        }

        private static UIPanel Box(
            UIComponent parent,
            Color32 colour,
            float w,
            float h,
            float x,
            float y
        )
        {
            UIPanel p = parent.AddUIComponent<UIPanel>();
            p.backgroundSprite = "EmptySprite";
            p.color = colour;
            p.size = new Vector2(w, h);
            p.relativePosition = new Vector3(x, y);
            return p;
        }

        private void SetOpacityFromMouse(UIComponent track)
        {
            Vector2 mouse = TestMouse ?? MouseInUi();
            float f = (mouse.x - track.absolutePosition.x) / track.width;
            Settings.Opacity = StockMonitor.OpacityAt(f);
            Mod.SaveSettings();
            ApplyOpacity();
            RefreshPopover();
        }
    }
}

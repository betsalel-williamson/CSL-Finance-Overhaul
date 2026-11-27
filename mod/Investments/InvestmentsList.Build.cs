// ColossalFramework.UI: UIHorizontalAlignment, UIOrientation, UIPanel, UIScrollbar, UISlicedSprite,
//   UITextureSprite, UIVerticalAlignment
using ColossalFramework.UI;
using FinanceOverhaul.Core; // InvestmentFilter, InvestmentList, StockMonitor, Strings
using UnityEngine; // Mathf, Vector2, Vector3

namespace FinanceOverhaul
{
    /// <summary>Building the list: the tabs, Group and Sort, the view, its scrollbar and the overview.</summary>
    public partial class InvestmentsList
    {
        // ---------- build ----------

        private bool Build()
        {
            page = Page();
            if (page == null)
            {
                return false;
            }

            UiCleanup.RemoveChildrenNamed(page, ListName);
            HideGameControls();
            list = page.AddUIComponent<UIPanel>();
            list.name = ListName;
            list.relativePosition = new Vector3(16f, 12f);
            list.size = new Vector2(page.width - 32f, page.height - 59f);
            // Two columns: the list on the left, the overview (or later the trade) on the right.
            float left = Mathf.Floor((list.width - ColumnGap) * 0.52f);

            InvestmentFilter[] filters = InvestmentList.Filters;
            var labels = new string[filters.Length];
            var widths = new float[filters.Length];
            for (int i = 0; i < filters.Length; i++)
            {
                labels[i] = InvestmentList.FilterLabel(filters[i], quotes);
                widths[i] = TabWidth;
            }

            tabBar = FoToggleTabs.Add(
                list,
                Prefix + "Tabs",
                labels,
                widths,
                26f,
                0.75f,
                i => Pick(filters[i])
            );
            // Sub tabs: the filters sit under the Economy panel's own tabs.
            tabBar.Secondary();
            tabBar.Panel.relativePosition = new Vector3(0f, 2f);
            for (int i = 0; i < filters.Length; i++)
            {
                tabBar[i].name = Prefix + "Tab-" + filters[i];
                tabs[filters[i]] = tabBar[i];
            }

            float x = tabBar.Panel.width;

            // Sections only when asked for: All is one list sorted as a whole.
            groupButton = FoButton.Add(
                list,
                Prefix + "Group",
                InvestmentList.GroupLabel(grouped),
                new Vector2(GroupWidth, 26f),
                0.8f
            );
            groupButton.relativePosition = new Vector3(x + 8f, 2f);
            groupButton.eventClick += (c, p) =>
            {
                grouped = !grouped;
                Top();
            };

            sortDirection = FoButton.Add(
                list,
                Prefix + "SortDirection",
                string.Empty,
                new Vector2(22f, 22f),
                0.8f
            );
            sortDirection.relativePosition = new Vector3(left - 22f, 4f);
            sortDirection.eventClick += (c, p) =>
            {
                reversed = !reversed;
                Top();
            };
            sortArrow = sortDirection.AddUIComponent<UITextureSprite>();
            sortArrow.name = Prefix + "SortArrow";
            sortArrow.size = new Vector2(10f, 6f);
            sortArrow.relativePosition = new Vector3(6f, 8f);
            sortArrow.isInteractive = false;
            // As the Stock Monitor: the sort is a muted line of text to click, then ▲/▼.
            sortLabel = FoText.Label(list, Prefix + "Sort", "", 0.75f, FoTheme.TextMuted);
            sortLabel.autoSize = false;
            sortLabel.size = new Vector2(SortWidth, 26f);
            sortLabel.textAlignment = UIHorizontalAlignment.Right;
            sortLabel.verticalAlignment = UIVerticalAlignment.Middle;
            sortLabel.relativePosition = new Vector3(left - 22f - 8f - SortWidth, 2f);
            sortLabel.isInteractive = true;
            sortLabel.eventClick += (c, p) =>
            {
                sort = StockMonitor.NextSort(sort);
                reversed = false;
                Top();
            };

            view = list.AddUIComponent<UIPanel>();
            view.name = Prefix + "View";
            view.backgroundSprite = "GenericPanel";
            view.color = FoTheme.GameStockCard;
            view.relativePosition = new Vector3(0f, BarH);
            view.size = new Vector2(left, list.height - BarH);
            view.clipChildren = true;
            // The wheel scrolls the column under the cursor: the stocks only for a wheel over
            // them (the overview's lists take their own).
            view.eventMouseWheel += (c, p) =>
            {
                if (p.used)
                {
                    return;
                }

                ScrollBy(-Mathf.Sign(p.wheelDelta) * InvestmentList.NotchPixels);
                p.Use();
            };

            bar = view.AddUIComponent<UIScrollbar>();
            bar.name = Prefix + "Scrollbar";
            bar.orientation = UIOrientation.Vertical;
            bar.size = new Vector2(12f, view.height - 2f * Pad);
            bar.relativePosition = new Vector3(view.width - 16f, Pad);
            bar.incrementAmount = InvestmentList.RowHeight;
            UISlicedSprite track = bar.AddUIComponent<UISlicedSprite>();
            track.spriteName = "ScrollbarTrack";
            track.size = bar.size;
            track.relativePosition = Vector3.zero;
            bar.trackObject = track;
            UISlicedSprite thumb = track.AddUIComponent<UISlicedSprite>();
            thumb.spriteName = "ScrollbarThumb";
            thumb.width = bar.width;
            bar.thumbObject = thumb;
            bar.eventValueChanged += (c, v) =>
            {
                if (!settingBar)
                {
                    offset = InvestmentList.Clamp(v, tops, view.height);
                    Fill();
                }
            };

            empty = FoText.Label(
                view,
                Prefix + "Empty",
                Strings.Get("investments.empty"),
                0.85f,
                FoTheme.TextMuted
            );
            empty.relativePosition = new Vector3(16f, 16f);

            float rowWidth = view.width - 2f * Pad - 20f;
            for (int i = 0; i < Pool; i++)
            {
                pool.Add(new Row(view, i, rowWidth, TogglePills, SparkFor, SparkSprite));
            }

            overview = new InvestmentsOverview(
                list,
                new Vector3(left + ColumnGap, 0f),
                new Vector2(list.width - left - ColumnGap, list.height),
                () => pillsInCurrency,
                TogglePills
            );
            Current = this;
            ModLog.Info(
                "investments list: shown in place of the game's two lists (list "
                    + left
                    + " px, overview "
                    + (list.width - left - ColumnGap)
                    + " px)"
            );
            Read();
            return true;
        }
    }
}

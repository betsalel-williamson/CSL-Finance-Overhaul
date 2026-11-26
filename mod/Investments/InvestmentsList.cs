using System; // Action, Array, Exception, Math
using System.Collections.Generic; // Dictionary, IList, KeyValuePair, List
// ColossalFramework.UI: UIButton, UIComponent, UIHorizontalAlignment, UILabel, UIOrientation,
//   UIPanel, UIScrollbar, UISlicedSprite, UISprite, UITextureSprite, UIVerticalAlignment, UIView
using ColossalFramework.UI;
// FinanceOverhaul.Core: BondText, InvestmentFilter, InvestmentList, InvestmentRow,
//   InvestmentRowKind, MonitorQuote, MonitorSort, StockMonitor, StockMonitorSettings, Strings,
//   TradeOrder
using FinanceOverhaul.Core;
// UnityEngine: Color32, FilterMode, Mathf, MonoBehaviour, Screen, Texture2D, TextureFormat,
//   TextureWrapMode, Time, Vector2, Vector3
using UnityEngine;

// Game classes used without a using (global namespace, Assembly-CSharp): BuySellInvestmentPanel,
//   EconomyManager, EconomyPanel, Investment, InvestmentAI, InvestmentTooltip,
//   ToolsModifierControl

namespace FinanceOverhaul
{
    /// <summary>
    /// Economy → Investments as one sectioned list (design: investments-and-research.md, Figma
    /// UI Map band 20), shown while Options → Feature flags → "Investments list and research" is
    /// on. The game's page has five controls of its own (two titles, two lists, the Total value
    /// bar): they are hidden and this list takes their place; switching the flag off, or a hot
    /// reload, shows them again. Filter tabs (All, Owned, Local) and the Stock Monitor's sorts;
    /// sections Your portfolio, Local companies and All stocks; a fixed pool of rows refilled
    /// from the scroll offset, so any number of stocks scrolls alike and the place is kept
    /// through a purchase, the daily update and closing the panel. Buy and Sell open the game's
    /// own dialog. The list's rules are <see cref="InvestmentList"/>. Lives on the overlay root.
    /// </summary>
    public class InvestmentsList : MonoBehaviour
    {
        public const string Prefix = "FinanceOverhaulInvestments";
        public const string ListName = Prefix + "List";

        private const float BarH = 34f;
        private const float Pad = 4f;
        private const int Pool = 20;

        private static readonly string[] GameControls =
        {
            "LabelPurchasable", // not shown: a game control name
            "LabelPortfolio", // not shown: a game control name
            "InvestmentsPanel", // not shown: a game control name
            "OwnedInvestmentsPanel", // not shown: a game control name
            "OwnedInvestmentsSummaryPanel", // not shown: a game control name
        };

        /// <summary>The list on screen, for game UI tests (null when not shown).</summary>
        internal static InvestmentsList Current { get; private set; }

        private UIComponent page;
        private UIPanel list;
        private UIPanel view;
        private UIScrollbar bar;
        private UILabel empty;
        private UILabel sortLabel;
        private UIButton sortDirection;
        private UITextureSprite sortArrow;
        private bool sortArrowDown;
        private readonly Dictionary<InvestmentFilter, UIButton> tabs =
            new Dictionary<InvestmentFilter, UIButton>();
        private readonly List<Row> pool = new List<Row>();

        private InvestmentFilter filter = InvestmentFilter.All;
        private MonitorSort localSort = MonitorSort.Change;
        private bool localReversed;
        private bool localCurrency;

        /// <summary>
        /// The list keeps its own saved sort, direction and ₡ / % pills; with Options → "follow the
        /// Stock Monitor" on, it uses (and changes) the Monitor's instead, so the two agree.
        /// </summary>
        private static bool Synced
        {
            get { return Mod.Settings != null && Mod.Settings.InvestmentListSortSynced; }
        }

        private static StockMonitorSettings Monitor
        {
            get { return Mod.Settings == null ? null : Mod.Settings.Monitor; }
        }

        private MonitorSort sort
        {
            get
            {
                return Mod.Settings == null ? localSort
                    : Synced ? Monitor.Sort
                    : Mod.Settings.InvestmentListSort;
            }
            set
            {
                localSort = value;
                if (Mod.Settings != null)
                {
                    if (Synced)
                    {
                        Monitor.Sort = value;
                    }
                    else
                    {
                        Mod.Settings.InvestmentListSort = value;
                    }

                    Mod.SaveSettings();
                }
            }
        }

        private bool reversed
        {
            get
            {
                return Mod.Settings == null ? localReversed
                    : Synced ? Monitor.SortReversed
                    : Mod.Settings.InvestmentListSortReversed;
            }
            set
            {
                localReversed = value;
                if (Mod.Settings != null)
                {
                    if (Synced)
                    {
                        Monitor.SortReversed = value;
                    }
                    else
                    {
                        Mod.Settings.InvestmentListSortReversed = value;
                    }

                    Mod.SaveSettings();
                }
            }
        }

        private bool InCurrency
        {
            get
            {
                return Mod.Settings == null ? localCurrency
                    : Synced ? Monitor.ChangeAsCurrency
                    : Mod.Settings.InvestmentListChangeAsCurrency;
            }
            set
            {
                localCurrency = value;
                if (Mod.Settings != null)
                {
                    if (Synced)
                    {
                        Monitor.ChangeAsCurrency = value;
                    }
                    else
                    {
                        Mod.Settings.InvestmentListChangeAsCurrency = value;
                    }

                    Mod.SaveSettings();
                }
            }
        }

        /// <summary>A click on any pill: ₡ ↔ % for the whole list (and the Stock Monitor when synced).</summary>
        /// <summary>What the rows' pills show, as of the last read.</summary>
        private static bool pillsInCurrency;

        private void TogglePills()
        {
            InCurrency = !InCurrency;
            Read();
        }

        private bool grouped;
        private FoToggleTabs tabBar;
        private UIButton groupButton;
        private List<MonitorQuote> quotes = new List<MonitorQuote>();
        private List<InvestmentRow> rows = new List<InvestmentRow>();
        private float[] tops = { 0f };
        private float offset;
        private bool settingBar;
        private float nextRead;

        /// <summary>The rows in the list now (headings included), for game UI tests.</summary>
        internal IList<InvestmentRow> Rows
        {
            get { return rows; }
        }

        internal float Offset
        {
            get { return offset; }
        }

        private void Update()
        {
            try
            {
                bool wanted = Mod.Settings != null && Mod.Settings.InvestmentListEnabled;
                if (!wanted)
                {
                    if (list != null)
                    {
                        Remove();
                    }

                    return;
                }

                EconomyPanel economy = ToolsModifierControl.economyPanel;
                if (economy == null || !economy.isInvestmentOpen)
                {
                    return;
                }

                if (list == null && !Build())
                {
                    return;
                }

                // The game shows its own controls again when it lays the tab out.
                HideGameControls();
                Hover();
                if (Time.unscaledTime >= nextRead)
                {
                    nextRead = Time.unscaledTime + 0.25f;
                    Read();
                }
            }
            catch (Exception ex)
            {
                ModLog.Warn("investments list: " + ex.Message);
                nextRead = Time.unscaledTime + 10f;
            }
        }

        private void OnDestroy()
        {
            try
            {
                Remove();
            }
            catch
            {
                // fail soft on unload
            }
        }

        /// <summary>Shows the game's own lists again and takes the mod's list out.</summary>
        private void Remove()
        {
            UIComponent p = page ?? Page();
            if (p != null)
            {
                foreach (string name in GameControls)
                {
                    UIComponent c = p.Find(name);
                    if (c != null)
                    {
                        c.isVisible = true;
                    }
                }

                UiCleanup.RemoveChildrenNamed(p, ListName);
            }

            foreach (Row r in pool)
            {
                r.Destroy();
            }

            pool.Clear();
            tabs.Clear();
            tabBar = null;
            if (sortArrow != null && sortArrow.texture != null)
            {
                Destroy(sortArrow.texture);
            }

            Row.HideChart();
            hovered = null;
            list = null;
            page = null;
            if (Current == this)
            {
                Current = null;
            }

            ModLog.Info("investments list: the game's lists are back");
        }

        /// <summary>The Investments page of the Economy panel, or null.</summary>
        private static UIComponent Page()
        {
            EconomyPanel economy = ToolsModifierControl.economyPanel;
            UIComponent container =
                economy == null ? null : economy.component.Find("EconomyContainer");
            return container == null ? null : container.Find("Investments");
        }

        private void HideGameControls()
        {
            foreach (string name in GameControls)
            {
                UIComponent c = page.Find(name);
                if (c != null && c.isVisible)
                {
                    c.isVisible = false;
                }
            }
        }

        // ---------- data ----------

        private void Read()
        {
            pillsInCurrency = InCurrency;
            quotes = MarketQuotes.Read(true);
            List<InvestmentRow> next = InvestmentList.Rows(
                quotes,
                filter,
                sort,
                reversed,
                grouped,
                new List<TradeOrder>(OrderDesk.Snapshot()),
                InCurrency
            );
            offset = InvestmentList.KeepInView(rows, offset, next, view.height);
            rows = next;
            tops = InvestmentList.Tops(rows);
            foreach (KeyValuePair<InvestmentFilter, UIButton> tab in tabs)
            {
                tab.Value.text = InvestmentList.FilterLabel(tab.Key, quotes);
            }

            tabBar.Choose(Array.IndexOf(InvestmentList.Filters, filter));

            sortLabel.text = StockMonitor.SortLabel(sort, InCurrency);
            groupButton.text = InvestmentList.GroupLabel(grouped);
            FoButton.SetSelected(groupButton, grouped);
            bool down = StockMonitor.IsDescending(sort, reversed);
            if (sortArrow.texture == null || down != sortArrowDown)
            {
                if (sortArrow.texture != null)
                {
                    Destroy(sortArrow.texture);
                }

                sortArrow.texture = FoButton.ArrowTexture(down);
                sortArrowDown = down;
            }

            sortDirection.tooltip = StockMonitor.DirectionText(sort, reversed);
            empty.isVisible = rows.Count == 0;
            float content = tops[tops.Length - 1];
            settingBar = true;
            bar.minValue = 0f;
            bar.maxValue = Mathf.Max(content, view.height);
            bar.scrollSize = view.height;
            bar.value = offset;
            settingBar = false;
            bar.isVisible = content > view.height;
            Fill();
        }

        private Row hovered;

        /// <summary>
        /// Over a stock's 30-day line the game's stock tooltip opens with its large chart, as
        /// over the game's own cards; it closes when the pointer leaves. Reads the pointer each
        /// frame (<see cref="FoPointer"/>, which game UI tests can stand in for).
        /// </summary>
        private void Hover()
        {
            // A dialog over the panel (the game's Buy / Sell, any modal) owns the mouse: nothing
            // behind it is outlined or opens its chart.
            if (UIView.GetModalComponent() != null)
            {
                foreach (Row r in pool)
                {
                    r.Outline(false);
                }

                if (hovered != null)
                {
                    hovered = null;
                    Row.HideChart();
                }

                return;
            }

            Vector2 at = FoPointer.Position;
            Row over = null;
            foreach (Row r in pool)
            {
                // The stock under the pointer is outlined: a click on it opens the dialog.
                r.Outline(r.OverStock(at, view));
                if (r.OverLine(at, view))
                {
                    over = r;
                }
            }

            if (over == hovered)
            {
                return;
            }

            hovered = over;
            if (over == null)
            {
                Row.HideChart();
            }
            else
            {
                over.ShowChart();
            }
        }

        /// <summary>Scrolls so that stock's line is at the top (game UI tests); false if not listed.</summary>
        internal bool ScrollTo(string symbol)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Kind == InvestmentRowKind.Stock && rows[i].Key == symbol)
                {
                    ScrollBy(tops[i] - offset);
                    return true;
                }
            }

            return false;
        }

        /// <summary>Moves the list by that many pixels (the wheel, game UI tests).</summary>
        internal void ScrollBy(float pixels)
        {
            offset = InvestmentList.Clamp(offset + pixels, tops, view.height);
            settingBar = true;
            bar.value = offset;
            settingBar = false;
            Fill();
        }

        /// <summary>Picks a filter tab (its click, game UI tests).</summary>
        internal void Pick(InvestmentFilter f)
        {
            filter = f;
            Top();
        }

        /// <summary>A new tab, grouping or order starts from the top of the list.</summary>
        private void Top()
        {
            offset = 0f;
            rows = new List<InvestmentRow>();
            Read();
        }

        private void Fill()
        {
            int first = InvestmentList.FirstVisible(tops, offset);
            for (int i = 0; i < pool.Count; i++)
            {
                int k = first + i;
                bool inView = k < rows.Count && tops[k] - offset < view.height;
                pool[i].Show(inView ? rows[k] : null, rows, inView ? tops[k] - offset : 0f);
            }
        }

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

            InvestmentFilter[] filters = InvestmentList.Filters;
            var labels = new string[filters.Length];
            var widths = new float[filters.Length];
            for (int i = 0; i < filters.Length; i++)
            {
                labels[i] = InvestmentList.FilterLabel(filters[i], quotes);
                widths[i] = 100f;
            }

            tabBar = FoToggleTabs.Add(
                list,
                Prefix + "Tabs",
                labels,
                widths,
                26f,
                0.8f,
                i => Pick(filters[i])
            );
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
                new Vector2(100f, 26f),
                0.8f
            );
            groupButton.relativePosition = new Vector3(x + 16f, 2f);
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
            sortDirection.relativePosition = new Vector3(list.width - 22f, 4f);
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
            sortLabel.size = new Vector2(150f, 26f);
            sortLabel.textAlignment = UIHorizontalAlignment.Right;
            sortLabel.verticalAlignment = UIVerticalAlignment.Middle;
            sortLabel.relativePosition = new Vector3(list.width - 22f - 8f - 150f, 2f);
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
            view.size = new Vector2(list.width, list.height - BarH);
            view.clipChildren = true;
            list.eventMouseWheel += (c, p) =>
            {
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
                pool.Add(new Row(view, i, rowWidth, TogglePills));
            }

            Current = this;
            ModLog.Info("investments list: shown in place of the game's two lists");
            Read();
            return true;
        }

        /// <summary>
        /// One pooled line, renamed for what it shows (a test finds "…Row-GGIBuy" wherever it
        /// scrolled): a section heading, a stock (Figma band 18, Condensed B), or an open order
        /// tucked under its stock.
        /// </summary>
        private sealed class Row
        {
            private const int SparkW = 120,
                SparkH = 28,
                SparkScale = 2;
            private const float SparkX = 430f;
            private const float PriceRight = 720f;
            private const float LockWidth = 280f;

            private readonly UIPanel back;
            private readonly UISprite coin;
            private readonly UILabel symbol;
            private readonly UILabel badge;
            private readonly UILabel name;
            private readonly UILabel holding;
            private readonly UILabel heading;
            private readonly UILabel detail;
            private readonly UITextureSprite spark;
            private readonly Texture2D sparkTexture;
            private readonly UILabel price;
            private readonly UILabel change;
            private readonly UILabel gain;
            private readonly UILabel lockLine;
            private readonly UIPanel lockTrack;
            private readonly UIPanel lockFill;
            private readonly UILabel orderLine;
            private readonly UIButton cancel;
            private readonly UIButton buy;
            private readonly UIButton sell;
            private readonly float width;
            private string key;
            private string drawn;
            private int slot;
            private int orderId;
            private bool locked;
            private uint available;
            private uint owned;
            private InvestmentRowKind kind;
            private readonly UIPanel[] outline;
            private bool outlined;

            public Row(UIComponent parent, int index, float width, Action togglePills)
            {
                this.width = width;
                back = parent.AddUIComponent<UIPanel>();
                back.name = Prefix + "Pool" + index;
                back.backgroundSprite = "GenericPanelWhite";
                back.size = new Vector2(width, InvestmentList.RowHeight - 4f);
                back.isVisible = false;

                coin = back.AddUIComponent<UISprite>();
                coin.size = new Vector2(36f, 36f);
                coin.relativePosition = new Vector3(8f, 5f);
                symbol = FoText.Label(back, "", "", 1.0f, FoTheme.GameText);
                symbol.relativePosition = new Vector3(54f, 3f);
                badge = FoPill.AddBadge(back, 0.6f, FoTheme.Header, FoTheme.Accent);
                badge.text = Strings.Get("investments.row.local");
                name = FoText.Label(back, "", "", 0.72f, FoTheme.TextMuted);
                name.relativePosition = new Vector3(54f, 24f);
                holding = FoText.Label(back, "", "", 0.72f, FoTheme.TextMuted);
                holding.relativePosition = new Vector3(54f, 40f);
                // A click anywhere on a stock's line (not on its buttons) opens the game's
                // dialog, as Buy does; the line is outlined while the pointer is over it.
                back.eventClick += (c, p) =>
                {
                    if (kind == InvestmentRowKind.Stock)
                    {
                        Open(BuySellInvestmentPanel.Mode.Buy);
                    }
                };
                outline = new UIPanel[4];
                for (int i = 0; i < outline.Length; i++)
                {
                    outline[i] = back.AddUIComponent<UIPanel>();
                    outline[i].backgroundSprite = "EmptySprite";
                    outline[i].color = FoTheme.Accent;
                    outline[i].isInteractive = false;
                    outline[i].isVisible = false;
                }

                heading = FoText.Label(back, "", "", 0.85f, FoTheme.GameText);
                heading.relativePosition = new Vector3(12f, 6f);
                detail = FoText.Label(back, "", "", 0.8f, FoTheme.GameValue);

                sparkTexture = new Texture2D(
                    SparkW * SparkScale,
                    SparkH * SparkScale,
                    TextureFormat.ARGB32,
                    false
                );
                sparkTexture.filterMode = FilterMode.Bilinear;
                sparkTexture.wrapMode = TextureWrapMode.Clamp;
                spark = back.AddUIComponent<UITextureSprite>();
                spark.texture = sparkTexture;
                spark.size = new Vector2(SparkW, SparkH);
                spark.relativePosition = new Vector3(SparkX, 9f);
                // Over the line: the game's own stock tooltip with its large chart (the list
                // checks the pointer each frame, see Hover).
                spark.isInteractive = false;

                price = FoText.Label(back, "", "", 1.0f, FoTheme.GameValue);
                change = FoPill.AddValue(back, new Vector2(66f, 18f), 0.75f, FoTheme.Header);
                change.relativePosition = new Vector3(PriceRight - 66f, 25f);
                // As in the Stock Monitor: a click on the pill shows every change in ₡ or %.
                change.isInteractive = true;
                change.tooltip = Strings.Get("monitor.pill.tip");
                change.eventClick += (c, p) =>
                {
                    p.Use(); // not the line's own click (the dialog)
                    togglePills();
                };
                gain = FoText.Label(back, "", "", 0.72f, FoTheme.GamePositive);

                lockLine = FoText.Label(back, "", "", 0.75f, FoTheme.GameText);
                lockLine.relativePosition = new Vector3(SparkX, 6f);
                lockTrack = back.AddUIComponent<UIPanel>();
                lockTrack.backgroundSprite = "EmptySprite";
                lockTrack.color = FoTheme.BarTrack;
                lockTrack.size = new Vector2(LockWidth, 4f);
                lockTrack.relativePosition = new Vector3(SparkX, 28f);
                lockFill = back.AddUIComponent<UIPanel>();
                lockFill.backgroundSprite = "EmptySprite";
                lockFill.color = FoTheme.OfferGold;
                lockFill.size = new Vector2(0f, 4f);
                lockFill.relativePosition = new Vector3(SparkX, 28f);

                orderLine = FoText.Label(back, "", "", 0.72f, FoTheme.Accent);
                orderLine.relativePosition = new Vector3(54f, 4f);
                cancel = FoButton.Add(
                    back,
                    "",
                    Strings.Get("order.card.cancel"),
                    new Vector2(70f, 18f),
                    0.65f
                );
                cancel.relativePosition = new Vector3(width - 82f, 2f);
                cancel.eventClick += (c, p) =>
                {
                    p.Use(); // the line's own click must not open the dialog too
                    OrderDesk.Cancel(orderId);
                };

                buy = FoButton.Add(
                    back,
                    "",
                    Strings.Get("investments.buy"),
                    new Vector2(70f, 28f),
                    0.85f
                );
                buy.relativePosition = new Vector3(width - 160f, 9f);
                buy.eventClick += (c, p) =>
                {
                    p.Use();
                    Open(BuySellInvestmentPanel.Mode.Buy);
                };
                sell = FoButton.Add(
                    back,
                    "",
                    Strings.Get("investments.sell"),
                    new Vector2(70f, 28f),
                    0.85f
                );
                sell.relativePosition = new Vector3(width - 82f, 9f);
                sell.eventClick += (c, p) =>
                {
                    p.Use();
                    Open(BuySellInvestmentPanel.Mode.Sell);
                };
            }

            public void Destroy()
            {
                if (sparkTexture != null)
                {
                    UnityEngine.Object.Destroy(sparkTexture);
                }
            }

            public string Key
            {
                get { return key; }
            }

            /// <summary>The pointer is over this stock's 30-day line (inside the view).</summary>
            public bool OverLine(Vector2 at, UIComponent view)
            {
                if (!back.isVisible || !spark.isVisible || slot <= 0 || locked)
                {
                    return false;
                }

                Vector3 p = spark.absolutePosition;
                Vector3 v = view.absolutePosition;
                return at.x >= p.x
                    && at.x <= p.x + spark.width
                    && at.y >= Mathf.Max(p.y, v.y)
                    && at.y <= Mathf.Min(p.y + spark.height, v.y + view.height);
            }

            /// <summary>The pointer is over this stock's line (inside the view), not a heading or order.</summary>
            public bool OverStock(Vector2 at, UIComponent view)
            {
                if (!back.isVisible || kind != InvestmentRowKind.Stock || locked)
                {
                    return false;
                }

                Vector3 p = back.absolutePosition;
                Vector3 v = view.absolutePosition;
                return at.x >= p.x
                    && at.x <= p.x + back.width
                    && at.y >= Mathf.Max(p.y, v.y)
                    && at.y <= Mathf.Min(p.y + back.height, v.y + view.height);
            }

            /// <summary>A 2-pixel accent outline around the line, or none.</summary>
            public void Outline(bool on)
            {
                if (
                    on == outlined
                    && (!on || (outline[0].width == back.width && outline[2].height == back.height))
                )
                {
                    return;
                }

                outlined = on;
                const float t = 2f;
                float w = back.width,
                    h = back.height;
                outline[0].size = new Vector2(w, t);
                outline[0].relativePosition = Vector3.zero;
                outline[1].size = new Vector2(w, t);
                outline[1].relativePosition = new Vector3(0f, h - t);
                outline[2].size = new Vector2(t, h);
                outline[2].relativePosition = Vector3.zero;
                outline[3].size = new Vector2(t, h);
                outline[3].relativePosition = new Vector3(w - t, 0f);
                foreach (UIPanel edge in outline)
                {
                    edge.isVisible = on;
                    edge.BringToFront();
                }
            }

            private void Open(BuySellInvestmentPanel.Mode mode)
            {
                // Nothing left to buy: the line opens Sell when the city holds some, else nothing
                // (the game's dialog would open on an empty share range).
                if (mode == BuySellInvestmentPanel.Mode.Buy && available == 0)
                {
                    if (owned == 0)
                    {
                        return;
                    }

                    mode = BuySellInvestmentPanel.Mode.Sell;
                }

                if (slot > 0 && !locked)
                {
                    HideChart();
                    BuySellInvestmentPanel.ShowPanel((byte)slot, mode);
                }
            }

            private static UIComponent Tooltip()
            {
                EconomyPanel economy = ToolsModifierControl.economyPanel;
                return economy == null ? null : economy.component.Find("InvestmentTooltip");
            }

            public void ShowChart()
            {
                UIComponent tip = Tooltip();
                InvestmentTooltip shows =
                    tip == null ? null : tip.GetComponent<InvestmentTooltip>();
                if (shows == null || slot <= 0 || locked)
                {
                    return;
                }

                shows.SetInvestment((byte)slot);
                // Beside the line, as the game puts it beside a card; on the left near the edge.
                Vector3 at = spark.absolutePosition;
                float x = at.x + spark.width + 12f;
                UIView view = back.GetUIView();
                float screen = view == null ? Screen.width : view.fixedWidth;
                if (x + tip.width > screen)
                {
                    x = at.x - 12f - tip.width;
                }

                tip.absolutePosition = new Vector3(x, back.absolutePosition.y);
                tip.Show();
                tip.BringToFront();
            }

            public static void HideChart()
            {
                UIComponent tip = Tooltip();
                if (tip != null)
                {
                    tip.Hide();
                }
            }

            public void Show(InvestmentRow row, IList<InvestmentRow> all, float y)
            {
                back.isVisible = row != null;
                if (row == null)
                {
                    return;
                }

                back.relativePosition = new Vector3(Pad, Pad + y);
                if (row.Key != key)
                {
                    key = row.Key;
                    drawn = null;
                    Rename(row);
                }

                kind = row.Kind;
                bool isStock = kind == InvestmentRowKind.Stock;
                bool isOrder = kind == InvestmentRowKind.Order;
                bool isHeading = kind == InvestmentRowKind.Heading;
                back.height = row.Height - 4f;
                back.color =
                    isHeading ? FoTheme.Header
                    : isOrder ? FoTheme.PanelAlt
                    : FoTheme.SectionFill;
                heading.isVisible = isHeading;
                detail.isVisible = isHeading;
                orderLine.isVisible = isOrder;
                cancel.isVisible = isOrder;
                foreach (UIComponent c in new UIComponent[] { coin, symbol, name })
                {
                    c.isVisible = isStock;
                }

                MonitorQuote q = row.Stock;
                slot = q == null ? 0 : q.Id;
                locked = q != null && q.Locked;
                available = q == null ? 0u : q.Available;
                owned = q == null ? 0u : q.Owned;
                buy.isEnabled = available > 0;
                buy.tooltip = available > 0 ? string.Empty : Strings.Get("order.side.buyNone");
                // Every time: the "nothing changed" return below skips the rest.
                buy.isVisible = isStock && !locked;
                sell.isVisible = isStock && q.Owned > 0;
                if (!isStock)
                {
                    foreach (
                        UIComponent c in new UIComponent[]
                        {
                            badge,
                            holding,
                            spark,
                            price,
                            change,
                            gain,
                            lockLine,
                            lockTrack,
                            lockFill,
                        }
                    )
                    {
                        c.isVisible = false;
                    }
                }

                if (isHeading)
                {
                    heading.text = InvestmentList.Heading(row.Section);
                    detail.text = InvestmentList.HeadingDetail(row.Section, all);
                    detail.relativePosition = new Vector3(width - 12f - detail.width, 7f);
                    return;
                }

                if (isOrder)
                {
                    orderId = row.Order.Id;
                    orderLine.text = InvestmentList.OrderLine(row.Order, OrderDesk.Today);
                    return;
                }

                string signature =
                    pillsInCurrency
                    + "|"
                    + q.Price.ToString("0.##")
                    + "|"
                    + q.ChangeRate.ToString("0.##")
                    + "|"
                    + q.Owned
                    + "|"
                    + q.Spent
                    + "|"
                    + q.Locked
                    + "|"
                    + q.UnlockText
                    + "|"
                    + Strings.Language;
                if (signature == drawn)
                {
                    return;
                }

                drawn = signature;
                Investment[] buffer = EconomyManager.instance.m_investments.m_buffer;
                InvestmentAI ai =
                    buffer[slot].Info == null ? null : buffer[slot].Info.m_investmentAI;
                if (ai != null)
                {
                    ai.SetSpriteName(coin);
                    name.tooltip = ai.GetLocalizedDescription();
                }

                symbol.text = q.Symbol;
                name.text = q.Name;
                badge.isVisible = q.Local;
                badge.relativePosition = new Vector3(
                    symbol.relativePosition.x + symbol.width + 8f,
                    6f
                );
                lockLine.isVisible = locked;
                lockTrack.isVisible = locked;
                lockFill.isVisible = locked;
                spark.isVisible = !locked;
                price.isVisible = !locked;
                change.isVisible = !locked;
                if (locked)
                {
                    lockLine.text = InvestmentList.LockLine(q);
                    lockFill.width = LockWidth * Mathf.Clamp01(q.UnlockProgress);
                }
                else
                {
                    Color32 colour = FoTheme.Change(StockMonitor.ChangeColour(q.ChangeRate));
                    FoSparkline.Draw(sparkTexture, q.History, colour, SparkScale + 1);
                    price.text = BondText.WholeMoney((long)Math.Round(q.Price * 100.0));
                    price.relativePosition = new Vector3(PriceRight - price.width, 3f);
                    // One measure in both units: the game's change rate, or that change in ₡.
                    change.text = pillsInCurrency
                        ? StockMonitor.ChangeAmountFromRate(q.Price, q.ChangeRate)
                        : StockMonitor.ChangeText(q.ChangeRate);
                    change.color = colour;
                }

                string held = InvestmentList.Holding(q);
                holding.isVisible = held != null;
                gain.isVisible = held != null;
                if (held != null)
                {
                    holding.text = held;
                    gain.text = InvestmentList.Gain(q);
                    int sign = InvestmentList.GainSign(q);
                    gain.textColor =
                        sign > 0 ? FoTheme.GamePositive
                        : sign < 0 ? FoTheme.GameNegative
                        : FoTheme.GameValue;
                    gain.relativePosition = new Vector3(PriceRight - gain.width, 45f);
                }
            }

            private void Rename(InvestmentRow row)
            {
                string n =
                    row.Kind == InvestmentRowKind.Heading ? Prefix + "Heading-" + row.Section
                    : row.Kind == InvestmentRowKind.Order ? Prefix + "Order-" + row.Order.Id
                    : Prefix + "Row-" + row.Stock.Symbol;
                back.name = n;
                symbol.name = n + "Symbol";
                badge.name = n + "Badge";
                name.name = n + "Name";
                holding.name = n + "Holding";
                heading.name = n + "Title";
                detail.name = n + "Detail";
                spark.name = n + "Spark";
                price.name = n + "Price";
                change.name = n + "Change";
                gain.name = n + "Gain";
                lockLine.name = n + "Lock";
                orderLine.name = n + "Text";
                cancel.name = n + "Cancel";
                buy.name = n + "Buy";
                sell.name = n + "Sell";
                for (int i = 0; i < outline.Length; i++)
                {
                    outline[i].name = n + "Outline" + (i == 0 ? string.Empty : i.ToString());
                }
            }
        }
    }
}

using System;
using System.Collections.Generic;
using ColossalFramework.UI;
using FinanceOverhaul.Core;
using UnityEngine;

namespace FinanceOverhaul
{
    /// <summary>
    /// Stock Monitor: a round toolbar button in the top-left mod row and a floating,
    /// see-through window (City Vitals style) listing every listed stock with its 30-day
    /// sparkline, price and change. Lives on the ticker root, so level unload and hot reload
    /// destroy it; OnDestroy removes its UI. UI only: nothing goes into the city save; the
    /// open state, positions, filter, sort and opacity live in the settings file.
    /// Prices move once per game day, so rows are rebuilt only when a cheap signature
    /// (day, holdings, open orders, filter, sort) changes, checked twice a second.
    /// </summary>
    public sealed partial class StockMonitorHost : MonoBehaviour
    {
        public const string ButtonName = "FinanceOverhaulStockMonitorButton";
        public const string WindowName = "FinanceOverhaulStockMonitor";
        public const string Prefix = "FinanceOverhaulStockMonitor";

        private const float Width = 360f;
        private const float TitleHeight = FoWindow.TitleBarHeight;
        private const float BarHeight = 34f;
        private const float RowHeight = 52f;
        private const float CompactRowHeight = 34f;

        private static float RowH
        {
            get { return Settings.CompactRows ? CompactRowHeight : RowHeight; }
        }
        private const float FooterHeight = 28f;

        // An order line: side and type over the order's wording (regular), or one line (compact).
        private const float OrderLineHeight = 34f;
        private const float OrderLineCompactHeight = 18f;
        private const float OrderLineLeft = 20f;
        private const float OrderTextMinScale = 0.55f;
        private const int VisibleRows = 10;
        private const int SparkW = 72,
            SparkH = 28,
            SparkScale = 2;

        private static readonly Color32 White = new Color32(245, 247, 250, 255);
        private static readonly Color32 Muted = FoTheme.ChartLabel;
        private static readonly Color32 Cyan = FoTheme.Accent;
        private static readonly Color32 Gold = FoTheme.TickerSymbol;
        private static readonly Color32 GoldBack = new Color32(70, 52, 24, 255);
        private static readonly Color32 PillText = FoTheme.Header; // black reads best on the light game colours
        private static readonly Color32 Gain = FoTheme.TickerGain;
        private static readonly Color32 Loss = FoTheme.Loss;
        private static readonly Color32 Flat = new Color32(159, 176, 192, 255);
        private static readonly Color32 Divider = new Color32(255, 255, 255, 26);

        public static StockMonitorHost Current { get; private set; }

        private UIButton button;
        private UITextureSprite buttonIcon;
        private UIPanel window;
        private UIPanel background;
        private UIScrollablePanel list;
        private UILabel footer;
        private UILabel empty;
        private UILabel sortLabel;
        private UIButton sortDirection;
        private UITextureSprite sortArrow;
        private bool sortArrowDown;
        private readonly Dictionary<MonitorFilter, UIButton> filterTabs =
            new Dictionary<MonitorFilter, UIButton>();
        private readonly List<Row> rows = new List<Row>();
        private string shownSignature;
        private float nextCheck;
        private bool dragging;
        private Vector3 lastMouse;

        private sealed class Row
        {
            public UIPanel Panel;
            public UILabel Symbol;
            public UILabel Name;
            public UIPanel Owned;
            public UILabel Orders;
            public UITextureSprite Spark;
            public Texture2D Texture;
            public UILabel Price;
            public UILabel Change;
            public UIPanel Line;
            public int Id = -1;
            public string Sym;
            public float[] History;

            /// <summary>Orders tab: one per open order of this stock, reused between refreshes.</summary>
            public readonly List<OrderLineUi> Lines = new List<OrderLineUi>();
        }

        /// <summary>One order under its stock row on the Orders tab (Figma band 10, pin 15).</summary>
        private sealed class OrderLineUi
        {
            public UIPanel Panel;
            public UIPanel Bar;
            public UILabel Type;
            public UILabel Distance;
            public UILabel Detail;
        }

        private static StockMonitorSettings Settings
        {
            get { return Mod.Settings == null ? new StockMonitorSettings() : Mod.Settings.Monitor; }
        }

        private void Start()
        {
            Current = this;
            RemoveStale();
            ApplyFlag();
        }

        /// <summary>Options flag changed (or first start): build or remove everything.</summary>
        public void ApplyFlag()
        {
            try
            {
                if (Settings.Enabled)
                {
                    if (button == null)
                    {
                        BuildButton();
                    }

                    ApplyOpacity();
                    SetOpen(Settings.Open, false);
                }
                else
                {
                    DestroyUi();
                }
            }
            catch (Exception ex)
            {
                ModLog.Warn("stock monitor: apply failed: " + ex.Message);
                DestroyUi();
            }
        }

        private void Update()
        {
            ShareChartOverlay.UpdateHover(); // the Invest dialog / tooltip charts
            if (button == null)
            {
                return;
            }

            DragButton();
            FadeWindow();
            FollowWindow();
            UpdateHover();
            if (window == null || !window.isVisible || Time.unscaledTime < nextCheck)
            {
                return;
            }

            nextCheck = Time.unscaledTime + 0.5f;
            try
            {
                Refresh(false);
            }
            catch (Exception ex)
            {
                ModLog.Warn("stock monitor: refresh failed: " + ex.Message);
                nextCheck = Time.unscaledTime + 10f;
            }
        }

        private void OnDestroy()
        {
            DestroyUi();
            if (Current == this)
            {
                Current = null;
            }
        }

        // ---------- open / close ----------

        /// <summary>
        /// Open or close outright (game UI tests; never toggles a player's window). Closing also
        /// returns to the list, so a scenario starts from a known view.
        /// </summary>
        public void SetOpen(bool open)
        {
            if (!open && detailId >= 0)
            {
                ShowList();
            }

            SetOpen(open, true);
        }

        public void Toggle()
        {
            SetOpen(window == null || !window.isVisible, true);
        }

        private void SetOpen(bool open, bool save)
        {
            if (open && window == null)
            {
                BuildWindow();
            }

            if (window != null)
            {
                window.isVisible = open;
            }

            if (!open)
            {
                HidePopover();
            }

            if (open)
            {
                Refresh(true);
            }

            StyleButton(open);
            if (save && Settings.Open != open)
            {
                Settings.Open = open;
                Mod.SaveSettings();
            }
        }

        // ---------- toolbar button ----------

        private void BuildButton()
        {
            UIView view = UIView.GetAView();
            button = (UIButton)view.AddUIComponent(typeof(UIButton));
            button.name = ButtonName;
            button.size = new Vector2(46f, 46f);
            button.normalBgSprite = "RoundBackBig";
            button.hoveredBgSprite = "RoundBackBigHovered";
            button.pressedBgSprite = "RoundBackBigPressed";
            button.focusedBgSprite = "RoundBackBig";
            button.tooltip = "Stock Monitor (right-drag to move)";
            button.playAudioEvents = true;
            button.eventClick += (c, p) =>
            {
                if (p.buttons == UIMouseButton.Left)
                {
                    Toggle();
                }
            };
            button.eventMouseDown += (c, p) =>
            {
                if (p.buttons == UIMouseButton.Right)
                {
                    dragging = true;
                    lastMouse = Input.mousePosition;
                }
            };

            buttonIcon = button.AddUIComponent<UITextureSprite>();
            buttonIcon.name = Prefix + "ButtonIcon";
            buttonIcon.size = new Vector2(26f, 26f);
            buttonIcon.relativePosition = new Vector3(10f, 10f);
            buttonIcon.isInteractive = false;
            buttonIcon.texture = IconTexture();

            // Default: in the top-left mod row, right of the first two buttons.
            float x = float.IsNaN(Settings.ButtonX) ? 177f : Settings.ButtonX;
            float y = float.IsNaN(Settings.ButtonY) ? 10f : Settings.ButtonY;
            button.absolutePosition = Clamp(new Vector3(x, y), button.size);
            StyleButton(false);
        }

        private void StyleButton(bool open)
        {
            if (button == null)
            {
                return;
            }

            button.normalBgSprite = open ? "RoundBackBigPressed" : "RoundBackBig";
            button.focusedBgSprite = button.normalBgSprite;
        }

        private void DragButton()
        {
            if (!dragging)
            {
                return;
            }

            if (!Input.GetMouseButton(1))
            {
                dragging = false;
                Settings.ButtonX = button.absolutePosition.x;
                Settings.ButtonY = button.absolutePosition.y;
                Mod.SaveSettings();
                return;
            }

            // Screen pixels → UI units (UI y grows downward).
            Vector3 delta = Input.mousePosition - lastMouse;
            lastMouse = Input.mousePosition;
            float scale = UIView.GetAView().GetScreenResolution().y / Mathf.Max(1f, Screen.height);
            button.absolutePosition = Clamp(
                button.absolutePosition + new Vector3(delta.x * scale, -delta.y * scale),
                button.size
            );
        }

        private static Vector3 Clamp(Vector3 p, Vector2 size)
        {
            Vector2 res = UIView.GetAView().GetScreenResolution();
            return new Vector3(
                Mathf.Clamp(p.x, 0f, Mathf.Max(0f, res.x - size.x)),
                Mathf.Clamp(p.y, 0f, Mathf.Max(0f, res.y - size.y))
            );
        }

        private static Texture2D IconTexture()
        {
            const int s = 52;
            byte[] mask = Sparkline.Render(new[] { 2f, 5f, 4f, 7f, 6f, 10f }, s, 36, 5);
            var tex = new Texture2D(s, s, TextureFormat.ARGB32, false);
            tex.filterMode = FilterMode.Bilinear;
            var px = new Color32[s * s];
            for (int y = 0; y < 36; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    // texture rows go bottom-up; centre the 36-pixel line vertically
                    px[(s - 1 - (y + 8)) * s + x] =
                        mask[y * s + x] == Sparkline.Line ? Gain : FoTheme.Clear;
                }
            }

            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        // ---------- window ----------

        private void BuildWindow()
        {
            UIView view = UIView.GetAView();
            window = (UIPanel)view.AddUIComponent(typeof(UIPanel));
            window.name = WindowName;
            window.size = new Vector2(Width, Height(VisibleRows));
            window.clipChildren = false;

            background = window.AddUIComponent<UIPanel>();
            background.name = Prefix + "Background";
            background.backgroundSprite = "GenericPanelWhite";
            background.color = FoTheme.Window;
            background.size = window.size;
            background.relativePosition = Vector3.zero;
            background.isInteractive = false;
            ApplyOpacity();

            // Title bar: drag handle, centred title, the game's gear and close buttons.
            UIDragHandle drag = window.AddUIComponent<UIDragHandle>();
            drag.name = Prefix + "Drag";
            drag.target = window;
            drag.size = new Vector2(Width - FoWindow.ButtonsWidth, TitleHeight);
            drag.relativePosition = Vector3.zero;
            drag.eventMouseUp += (c, p) => SaveWindowPosition();

            FoWindow.AddTitle(window, Prefix + "Title", "Stock Monitor", Width);

            UIButton settings = FoWindow.AddGear(window, Prefix + "Settings", Width);
            settings.tooltip = "Stock Monitor settings";
            settings.eventClick += (c, p) => TogglePopover();

            UIButton close = FoWindow.AddClose(window, Prefix + "Close", Width);
            close.eventClick += (c, p) => SetOpen(false, true);

            // Filter tabs + sort.
            float x = 12f;
            foreach (
                MonitorFilter f in new[]
                {
                    MonitorFilter.All,
                    MonitorFilter.Portfolio,
                    MonitorFilter.Orders,
                }
            )
            {
                MonitorFilter filter = f;
                UIButton tab = window.AddUIComponent<UIButton>();
                tab.name = Prefix + "Filter-" + f;
                tab.text = StockMonitor.FilterLabel(f);
                tab.textScale = 0.75f;
                // Orders is wide enough for its count, "Orders (12)".
                tab.size = new Vector2(
                    f == MonitorFilter.All ? 44f
                        : f == MonitorFilter.Portfolio ? 70f
                        : 78f,
                    22f
                );
                tab.normalBgSprite = "ButtonMenu";
                tab.hoveredBgSprite = "ButtonMenuHovered";
                tab.pressedBgSprite = "ButtonMenuPressed";
                tab.relativePosition = new Vector3(x, TitleHeight + 6f);
                tab.eventClick += (c, p) => SetFilter(filter);
                filterTabs[f] = tab;
                x += tab.width + 4f;
            }

            sortLabel = Label(window, Prefix + "Sort", "", 0.75f, Muted);
            sortLabel.autoSize = false;
            sortLabel.size = new Vector2(120f, 22f);
            sortLabel.textAlignment = UIHorizontalAlignment.Right;
            sortLabel.verticalAlignment = UIVerticalAlignment.Middle;
            sortLabel.relativePosition = new Vector3(Width - 158f, TitleHeight + 6f);
            sortLabel.isInteractive = true;
            sortLabel.eventClick += (c, p) => CycleSort();

            // ▲/▼: flips the sort's direction (highest/lowest first, A→Z/Z→A).
            sortDirection = window.AddUIComponent<UIButton>();
            sortDirection.name = Prefix + "SortDirection";
            sortDirection.size = new Vector2(22f, 22f);
            sortDirection.relativePosition = new Vector3(Width - 32f, TitleHeight + 6f);
            sortDirection.normalBgSprite = "ButtonMenu";
            sortDirection.hoveredBgSprite = "ButtonMenuHovered";
            sortDirection.pressedBgSprite = "ButtonMenuPressed";
            sortDirection.eventClick += (c, p) => FlipSort();
            sortArrow = sortDirection.AddUIComponent<UITextureSprite>();
            sortArrow.name = Prefix + "SortArrow";
            sortArrow.size = new Vector2(10f, 6f);
            sortArrow.relativePosition = new Vector3(6f, 8f);
            sortArrow.isInteractive = false;

            list = window.AddUIComponent<UIScrollablePanel>();
            list.name = Prefix + "Rows";
            list.size = new Vector2(Width, RowHeight * VisibleRows);
            list.relativePosition = new Vector3(0f, TitleHeight + BarHeight);
            list.clipChildren = true;
            list.autoLayout = true;
            list.autoLayoutDirection = LayoutDirection.Vertical;
            list.scrollWheelDirection = UIOrientation.Vertical;
            list.builtinKeyNavigation = true;

            empty = Label(window, Prefix + "Empty", "", 0.85f, Muted);
            empty.relativePosition = new Vector3(14f, TitleHeight + BarHeight + 12f);

            footer = Label(window, Prefix + "Footer", "", 0.65f, Muted);
            footer.relativePosition = new Vector3(12f, 0f);

            float wx = float.IsNaN(Settings.WindowX) ? 12f : Settings.WindowX;
            float wy = float.IsNaN(Settings.WindowY) ? 64f : Settings.WindowY;
            window.absolutePosition = Clamp(new Vector3(wx, wy), window.size);
            StyleFilter();
        }

        private static float Height(int visibleRows)
        {
            return TitleHeight + BarHeight + RowH * visibleRows + FooterHeight;
        }

        private void SaveWindowPosition()
        {
            if (window == null)
            {
                return;
            }

            Settings.WindowX = window.absolutePosition.x;
            Settings.WindowY = window.absolutePosition.y;
            Mod.SaveSettings();
        }

        private void SetFilter(MonitorFilter filter)
        {
            Settings.Filter = filter;
            Mod.SaveSettings();
            StyleFilter();
            Refresh(true);
        }

        private void FlipSort()
        {
            if (Settings.Filter == MonitorFilter.Orders)
            {
                Settings.OrdersSortReversed = !Settings.OrdersSortReversed;
            }
            else
            {
                Settings.SortReversed = !Settings.SortReversed;
            }

            Mod.SaveSettings();
            Refresh(true);
        }

        /// <summary>A small filled triangle, pointing down for highest/Z first, up for lowest/A first.</summary>
        private static Texture2D ArrowTexture(bool down)
        {
            const int w = 10,
                h = 6;
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            t.name = "FinanceOverhaulSortArrow";
            t.filterMode = FilterMode.Point;
            for (int y = 0; y < h; y++)
            {
                // row y counted from the texture bottom; the wide edge is at the top for "down".
                int fromWide = down ? h - 1 - y : y;
                int half = w / 2 - fromWide * w / (2 * h);
                for (int x = 0; x < w; x++)
                {
                    bool lit = x >= w / 2 - half && x < w / 2 + half;
                    t.SetPixel(x, y, lit ? new Color32(230, 236, 240, 255) : FoTheme.Clear);
                }
            }

            t.Apply();
            return t;
        }

        /// <summary>
        /// The Orders tab is grouped by stock and cycles its own sorts (nearest, expires,
        /// symbol); All and Portfolio share the list sort. Each keeps its own choice.
        /// </summary>
        private void CycleSort()
        {
            if (Settings.Filter == MonitorFilter.Orders)
            {
                Settings.OrdersSortReversed = false; // a new sort starts in its natural direction
                Settings.OrdersSort = MonitorOrders.NextSort(Settings.OrdersSort);
            }
            else
            {
                Settings.SortReversed = false;
                Settings.Sort = StockMonitor.NextSort(Settings.Sort);
            }

            Mod.SaveSettings();
            Refresh(true);
        }

        private void ApplyOpacity()
        {
            if (background != null)
            {
                background.opacity = StockMonitorSettings.HoverOpacity;
            }

            if (window != null)
            {
                FoWindow.SetOpacity(window, Settings.WindowOpacity(window.containsMouse));
            }
        }

        /// <summary>City Vitals style: solid while the mouse is over the window, see-through otherwise (~0.15 s fade).</summary>
        private void FadeWindow()
        {
            if (background == null || window == null || !window.isVisible)
            {
                return;
            }

            // The whole window fades: text, pills and charts with the background.
            float target = Settings.WindowOpacity(window.containsMouse);
            float now = window.opacity;
            if (Mathf.Abs(now - target) > 0.005f)
            {
                FoWindow.SetOpacity(
                    window,
                    Mathf.MoveTowards(now, target, Time.unscaledDeltaTime * 6f)
                );
            }
        }

        private void StyleFilter()
        {
            foreach (KeyValuePair<MonitorFilter, UIButton> kv in filterTabs)
            {
                bool on = kv.Key == Settings.Filter;
                kv.Value.normalBgSprite = on ? "ButtonMenuPressed" : "ButtonMenu";
                kv.Value.focusedBgSprite = kv.Value.normalBgSprite;
                kv.Value.textColor = on ? (Color32)Color.white : new Color32(190, 198, 206, 255);
            }
        }

        // ---------- data ----------

        private void Refresh(bool force)
        {
            if (window == null || !window.isVisible)
            {
                return;
            }

            if (detailId >= 0)
            {
                RefreshDetail(force);
                return;
            }

            List<MonitorQuote> quotes = Quotes();
            TradeOrder[] orders = OrderDesk.Snapshot();
            int today = OrderDesk.Today;
            string signature = Signature(quotes, orders, today);
            if (!force && signature == shownSignature)
            {
                return;
            }

            shownSignature = signature;
            bool ordersTab = Settings.Filter == MonitorFilter.Orders;
            // Every tab shows how many orders are open on the stocks listed.
            List<MonitorOrderGroup> groups = MonitorOrders.Groups(
                quotes,
                orders,
                today,
                Mod.OrderKindEnabled,
                Settings.Hidden,
                Settings.OrdersSort,
                Settings.OrdersSortReversed
            );
            UIButton ordersTabButton;
            if (filterTabs.TryGetValue(MonitorFilter.Orders, out ordersTabButton))
            {
                ordersTabButton.text = MonitorOrders.TabLabel(MonitorOrders.Count(groups));
            }

            List<MonitorQuote> shown;
            if (ordersTab)
            {
                // Grouped by stock: the groups come already in the tab's own order.
                shown = new List<MonitorQuote>(groups.Count);
                foreach (MonitorOrderGroup g in groups)
                {
                    shown.Add(g.Quote);
                }
            }
            else
            {
                shown = StockMonitor.Rows(
                    quotes,
                    Settings.Filter,
                    Settings.Sort,
                    Settings.Hidden,
                    Settings.SortReversed,
                    Settings.ChangeAsCurrency
                );
            }

            while (rows.Count < shown.Count)
            {
                rows.Add(BuildRow());
            }

            float content = 0f;
            for (int i = 0; i < rows.Count; i++)
            {
                bool used = i < shown.Count;
                rows[i].Panel.isVisible = used;
                if (used)
                {
                    Fill(rows[i], shown[i], ordersTab ? groups[i].Lines : null);
                    content += rows[i].Panel.height;
                }
                else
                {
                    Unname(rows[i]);
                }
            }

            // As tall as its rows, up to ten plain rows; more than that scrolls.
            list.height = Mathf.Clamp(content, RowH, RowH * VisibleRows);
            window.height = TitleHeight + BarHeight + list.height + FooterHeight;
            background.size = window.size;
            footer.relativePosition = new Vector3(12f, TitleHeight + BarHeight + list.height + 7f);
            footer.text = StockMonitor.Footer(SimulationManager.instance.m_currentGameTime);
            empty.text = shown.Count == 0 ? StockMonitor.EmptyText(Settings.Filter) : "";
            empty.isVisible = shown.Count == 0;
            sortLabel.text = ordersTab
                ? MonitorOrders.SortLabel(Settings.OrdersSort)
                : StockMonitor.SortLabel(Settings.Sort, Settings.ChangeAsCurrency);
            bool down = ordersTab
                ? MonitorOrders.IsDescending(Settings.OrdersSortReversed)
                : StockMonitor.IsDescending(Settings.Sort, Settings.SortReversed);
            if (sortArrowDown != down || sortArrow.texture == null)
            {
                if (sortArrow.texture != null)
                {
                    Destroy(sortArrow.texture);
                }

                sortArrow.texture = ArrowTexture(down);
                sortArrowDown = down;
            }

            sortDirection.tooltip = ordersTab
                ? MonitorOrders.DirectionText(Settings.OrdersSort, Settings.OrdersSortReversed)
                : StockMonitor.DirectionText(Settings.Sort, Settings.SortReversed);
        }

        private static string Signature(List<MonitorQuote> quotes, TradeOrder[] orders, int today)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(today)
                .Append('|')
                .Append(Settings.Filter)
                .Append('|')
                .Append(Settings.Sort)
                .Append(Settings.SortReversed)
                .Append(Settings.OrdersSort)
                .Append(Settings.OrdersSortReversed);
            // What an order line shows: its trigger (a trailing stop rises) and paused state.
            foreach (TradeOrder o in orders)
            {
                sb.Append('#')
                    .Append(o.Id)
                    .Append(':')
                    .Append(o.TriggerPrice)
                    .Append(Mod.OrderKindEnabled(o.Kind) ? 'a' : 'p');
            }

            sb.Append('|')
                .Append(Settings.CompactRows)
                .Append(Settings.ShowSparklines)
                .Append(Settings.ChangeAsCurrency)
                .Append(Settings.Hidden.Count);
            foreach (MonitorQuote q in quotes)
            {
                sb.Append('|')
                    .Append(q.Symbol)
                    .Append(':')
                    .Append(q.Owned)
                    .Append(':')
                    .Append(q.OpenOrders)
                    .Append(':')
                    .Append(q.Price.ToString("0.##"));
            }

            return sb.ToString();
        }

        private static List<MonitorQuote> Quotes()
        {
            var quotes = new List<MonitorQuote>();
            var orders = new Dictionary<string, int>();
            foreach (TradeOrder o in OrderDesk.Snapshot())
            {
                int n;
                orders.TryGetValue(o.InvestmentKey, out n);
                orders[o.InvestmentKey] = n + 1;
            }

            Investment[] buffer = EconomyManager.instance.m_investments.m_buffer;
            for (int i = 0; i < buffer.Length; i++)
            {
                if ((buffer[i].m_flags & Investment.Flags.Created) == 0)
                {
                    continue;
                }

                InvestmentInfo info = buffer[i].Info;
                InvestmentAI ai = info == null ? null : info.m_investmentAI;
                if (ai == null || !ai.IsAvailable((byte)i, ref buffer[i]))
                {
                    continue;
                }

                string name = ai.GetLocalizedName();
                var history = new float[Investment.kPastValueCount];
                buffer[i].FillGraphValues(history);
                int count;
                orders.TryGetValue(info.name, out count);
                quotes.Add(
                    new MonitorQuote
                    {
                        Id = i,
                        Prefab = info.name,
                        Symbol = StockSymbols.For(info),
                        Name = name,
                        Price = buffer[i].value,
                        ChangeRate = ai.CalculateChangeRate(
                            (byte)i,
                            ref buffer[i],
                            TickerFeed.PanelChangeRateWindow
                        ),
                        History = history,
                        Owned = ai.OwnedCount(ref buffer[i]),
                        OpenOrders = count,
                    }
                );
            }

            return quotes;
        }

        // ---------- rows ----------

        private Row BuildRow()
        {
            var r = new Row();
            r.Panel = list.AddUIComponent<UIPanel>();
            r.Panel.size = new Vector2(Width, RowHeight);
            r.Panel.eventClick += (c, p) =>
            {
                if (r.Id >= 0)
                {
                    ShowDetail(r.Id);
                }
            };

            UIPanel line = r.Line = r.Panel.AddUIComponent<UIPanel>();
            line.backgroundSprite = "EmptySprite"; // borderless: a true 1 px line
            line.color = Divider;
            line.size = new Vector2(Width - 24f, 1f);
            line.relativePosition = new Vector3(12f, RowHeight - 1f);
            line.isInteractive = false;

            r.Symbol = Label(r.Panel, null, "", 1.0f, White);
            r.Symbol.relativePosition = new Vector3(12f, 7f);

            r.Owned = r.Panel.AddUIComponent<UIPanel>();
            r.Owned.backgroundSprite = "EmptySprite";
            r.Owned.color = Cyan;
            r.Owned.size = new Vector2(7f, 7f);
            r.Owned.isInteractive = false;

            r.Orders = Label(r.Panel, null, "", 0.6f, Gold);
            r.Orders.backgroundSprite = "GenericPanelWhite";
            r.Orders.color = GoldBack;
            r.Orders.padding = new RectOffset(4, 4, 2, 1);

            r.Name = Label(r.Panel, null, "", 0.7f, Muted);
            r.Name.relativePosition = new Vector3(12f, 29f);

            r.Texture = new Texture2D(
                SparkW * SparkScale,
                SparkH * SparkScale,
                TextureFormat.ARGB32,
                false
            );
            r.Texture.filterMode = FilterMode.Bilinear;
            r.Texture.wrapMode = TextureWrapMode.Clamp;
            r.Spark = r.Panel.AddUIComponent<UITextureSprite>();
            r.Spark.size = new Vector2(SparkW, SparkH);
            r.Spark.relativePosition = new Vector3(186f, (RowHeight - SparkH) / 2f);
            r.Spark.texture = r.Texture;
            r.Spark.isInteractive = false;

            r.Price = Label(r.Panel, null, "", 0.9f, White);
            r.Price.autoSize = false;
            r.Price.size = new Vector2(90f, 18f);
            r.Price.textAlignment = UIHorizontalAlignment.Right;
            r.Price.relativePosition = new Vector3(Width - 102f, 6f);

            r.Change = Label(r.Panel, null, "", 0.8f, PillText);
            r.Change.autoSize = false;
            FoText.Heavy(r.Change); // dark text on the loss red needs the extra weight
            r.Change.size = new Vector2(70f, 20f);
            r.Change.textAlignment = UIHorizontalAlignment.Right;
            r.Change.verticalAlignment = UIVerticalAlignment.Middle;
            r.Change.backgroundSprite = "GenericPanelWhite";
            r.Change.padding = new RectOffset(0, 6, 2, 0);
            r.Change.relativePosition = new Vector3(Width - 82f, 27f);
            foreach (UIComponent child in r.Panel.components)
            {
                child.isInteractive = false; // the whole row is one click target
            }

            // ...except the pill, which flips ₡ / % (Figma pin 6).
            r.Change.isInteractive = true;
            r.Change.tooltip = "Click: show the change in ₡ or %";
            r.Change.eventClick += (c, p) =>
            {
                p.Use();
                Settings.ChangeAsCurrency = !Settings.ChangeAsCurrency;
                Mod.SaveSettings();
                Refresh(true);
                RefreshPopover();
            };
            return r;
        }

        /// <summary>
        /// A spare row keeps no stock's name: two controls called "Row-Oil" (one hidden) would
        /// make lookups by name, such as the game UI tests', find the wrong one.
        /// </summary>
        private static void Unname(Row r)
        {
            if (r.Id < 0)
            {
                return;
            }

            r.Id = -1;
            r.Sym = null;
            r.Panel.name = Prefix + "SpareRow";
            foreach (UIComponent c in r.Panel.GetComponentsInChildren<UIComponent>(true))
            {
                if (c != r.Panel)
                {
                    c.name = Prefix + "Spare";
                }
            }
        }

        /// <param name="lines">Orders tab: this stock's orders, drawn under its row; else null.</param>
        private static void Fill(Row r, MonitorQuote q, List<MonitorOrderLine> lines)
        {
            r.Id = q.Id;
            r.Sym = q.Symbol;
            r.History = StockDetail.Range(q.History, StockDetail.LongRange);
            r.Panel.name = Prefix + "Row-" + q.Symbol;
            r.Symbol.name = Prefix + "Symbol-" + q.Symbol;
            r.Symbol.text = q.Symbol;
            r.Name.text = q.Name;
            r.Spark.name = Prefix + "Spark-" + q.Symbol;
            r.Price.name = Prefix + "Price-" + q.Symbol;
            r.Price.text = StockMonitor.PriceText(q.Price);
            r.Change.name = Prefix + "Change-" + q.Symbol;
            // One measure for both pill modes and the line: the game's change rate (as the
            // Economy panel and the LED show it); ₡ mode is the same change in ₡.
            r.Change.text = Settings.ChangeAsCurrency
                ? StockMonitor.ChangeAmountFromRate(q.Price, q.ChangeRate)
                : StockMonitor.ChangeText(q.ChangeRate);
            Color32 colour = ColourOf(StockMonitor.ChangeColour(q.ChangeRate));
            r.Change.color = colour;

            // Layout: full rows (name under the symbol, price over the pill) or compact
            // (one line, names hidden).
            bool compact = Settings.CompactRows;
            float h = compact ? CompactRowHeight : RowHeight;
            r.Panel.height = h;
            r.Line.relativePosition = new Vector3(12f, h - 1f);
            r.Name.isVisible = !compact;
            r.Symbol.relativePosition = new Vector3(12f, compact ? 7f : 7f);
            r.Spark.isVisible = Settings.ShowSparklines;
            // Compact: leave room for a 6-digit price (₡54,191) between the line and the pill.
            r.Spark.relativePosition = new Vector3(compact ? 134f : 186f, (h - SparkH) / 2f);
            r.Price.relativePosition = compact
                ? new Vector3(Width - 178f, 8f)
                : new Vector3(Width - 102f, 6f);
            r.Change.relativePosition = compact
                ? new Vector3(Width - 82f, 7f)
                : new Vector3(Width - 82f, 27f);

            float x = 12f + r.Symbol.width + 6f;
            r.Owned.isVisible = q.Owned > 0;
            r.Owned.name = Prefix + "Owned-" + q.Symbol;
            if (r.Owned.isVisible)
            {
                r.Owned.relativePosition = new Vector3(x, 13f);
                x += 13f;
            }

            // The Orders tab lists the orders themselves, so the count badge is not needed there.
            string badge = lines != null ? null : StockMonitor.OrdersBadge(q.OpenOrders);
            r.Orders.isVisible = badge != null;
            r.Orders.name = Prefix + "Orders-" + q.Symbol;
            if (badge != null)
            {
                r.Orders.text = badge;
                r.Orders.relativePosition = new Vector3(x, 9f);
            }

            DrawSpark(r.Texture, q.History, colour);
            FillOrderLines(r, lines, h, compact);
        }

        /// <summary>
        /// One line per open order under the stock's row; the row grows to hold them and the
        /// divider moves to its new bottom edge.
        /// </summary>
        private static void FillOrderLines(
            Row r,
            List<MonitorOrderLine> lines,
            float rowHeight,
            bool compact
        )
        {
            int count = lines == null ? 0 : lines.Count;
            while (r.Lines.Count < count)
            {
                r.Lines.Add(BuildOrderLine(r.Panel));
            }

            float y = rowHeight - 3f; // tuck the first line up under the row's own content
            for (int i = 0; i < r.Lines.Count; i++)
            {
                OrderLineUi ui = r.Lines[i];
                ui.Panel.isVisible = i < count;
                if (i >= count)
                {
                    continue;
                }

                ui.Panel.relativePosition = new Vector3(0f, y);
                y += FillOrderLine(ui, lines[i], compact);
            }

            if (count > 0)
            {
                y += 5f;
                r.Panel.height = y;
                r.Line.relativePosition = new Vector3(12f, y - 1f);
            }
        }

        private static OrderLineUi BuildOrderLine(UIPanel row)
        {
            var ui = new OrderLineUi();
            ui.Panel = row.AddUIComponent<UIPanel>();
            ui.Panel.size = new Vector2(Width, OrderLineHeight);
            ui.Panel.isInteractive = false; // the whole row is one click target

            ui.Bar = ui.Panel.AddUIComponent<UIPanel>();
            ui.Bar.backgroundSprite = "EmptySprite";
            ui.Bar.relativePosition = new Vector3(12f, 3f);

            ui.Type = Label(ui.Panel, null, "", 0.6f, White);
            ui.Distance = Label(ui.Panel, null, "", 0.6f, Muted);
            ui.Distance.autoSize = true;
            ui.Detail = Label(ui.Panel, null, "", 0.7f, White);
            foreach (UIComponent c in ui.Panel.components)
            {
                c.isInteractive = false;
            }

            return ui;
        }

        /// <summary>Draws one order line and returns the height it took.</summary>
        private static float FillOrderLine(OrderLineUi ui, MonitorOrderLine line, bool compact)
        {
            // Side colour as on the Your Portfolio cards: the game's gain for a buy, its loss
            // colour for a sell, muted when the order's type is switched off in Options.
            Color32 side =
                line.Paused ? Muted
                : line.IsSell ? FoTheme.GameNegative
                : FoTheme.GamePositive;
            string id = line.OrderId.ToString(System.Globalization.CultureInfo.InvariantCulture);
            ui.Panel.name = Prefix + "OrderLine-" + id;
            ui.Type.name = Prefix + "OrderType-" + id;
            ui.Detail.name = Prefix + "OrderDetail-" + id;
            ui.Distance.name = Prefix + "OrderDistance-" + id;
            ui.Bar.color = side;

            string distance = compact ? line.CompactDistance : line.Distance;
            ui.Distance.isVisible = distance != null;
            float right = Width - 12f;
            float distanceWidth = 0f;
            if (distance != null)
            {
                ui.Distance.text = distance;
                distanceWidth = ui.Distance.width + 8f;
                ui.Distance.relativePosition = new Vector3(right - ui.Distance.width, 3f);
            }

            float height;
            if (compact)
            {
                // One line: the whole order in its short form, in the side colour.
                ui.Type.isVisible = false;
                ui.Detail.textColor = line.Paused ? Muted : White;
                float textHeight = FitOrderText(
                    ui.Detail,
                    line.Compact,
                    0.65f,
                    right - OrderLineLeft - distanceWidth,
                    13f
                );
                ui.Detail.relativePosition = new Vector3(OrderLineLeft, 2f);
                height = Mathf.Max(OrderLineCompactHeight, textHeight + 5f);
            }
            else
            {
                // Two tiers: side and type (with the distance at the right), then the wording.
                ui.Type.isVisible = true;
                ui.Type.text = line.Type;
                ui.Type.textColor = side;
                ui.Type.relativePosition = new Vector3(OrderLineLeft, 3f);
                ui.Detail.textColor = line.Paused ? Muted : White;
                float textHeight = FitOrderText(
                    ui.Detail,
                    line.Detail,
                    0.7f,
                    right - OrderLineLeft,
                    15f
                );
                ui.Detail.relativePosition = new Vector3(OrderLineLeft, 16f);
                height = Mathf.Max(OrderLineHeight, 16f + textHeight + 3f);
            }

            ui.Panel.height = height;
            ui.Bar.size = new Vector2(2f, height - 6f);
            return height;
        }

        /// <summary>
        /// The text on one line at its normal scale, scaled down when it nearly fits, wrapped
        /// when it does not: never cut off. Returns the label's height.
        /// </summary>
        private static float FitOrderText(
            UILabel label,
            string text,
            float baseScale,
            float width,
            float lineHeight
        )
        {
            label.autoHeight = false;
            label.wordWrap = false;
            label.text = text;
            LabelFit.Result fit = LabelFit.SingleLine(
                baseScale,
                OrderTextMinScale,
                width,
                scale =>
                {
                    label.autoSize = false;
                    label.textScale = scale;
                    label.autoSize = true;
                    return label.width;
                }
            );
            label.autoSize = false;
            label.textScale = fit.Scale;
            if (fit.Wrap)
            {
                label.wordWrap = true;
                label.width = width;
                label.autoHeight = true;
                return Mathf.Max(lineHeight, label.height);
            }

            label.size = new Vector2(width, lineHeight);
            return lineHeight;
        }

        private static void DrawSpark(Texture2D tex, float[] history, Color32 colour)
        {
            int w = tex.width,
                h = tex.height;
            byte[] mask = Sparkline.Render(history, w, h, SparkScale + 1);
            var px = new Color32[w * h];
            var dash = new Color32(colour.r, colour.g, colour.b, 200);
            var clear = FoTheme.Clear;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    byte m = mask[y * w + x];
                    px[(h - 1 - y) * w + x] =
                        m == Sparkline.Line ? colour
                        : m == Sparkline.Baseline ? dash
                        : clear;
                }
            }

            tex.SetPixels32(px);
            tex.Apply();
        }

        internal static Color32 ColourOf(byte rate)
        {
            return rate == LedText.Gain ? Gain
                : rate == LedText.Loss ? Loss
                : Flat;
        }

        internal static UILabel Label(
            UIComponent parent,
            string name,
            string text,
            float scale,
            Color32 colour
        )
        {
            UILabel l = parent.AddUIComponent<UILabel>();
            if (name != null)
            {
                l.name = name;
            }

            l.text = text;
            l.textScale = scale;
            l.textColor = colour;
            l.useDropShadow = true;
            l.dropShadowColor = new Color32(0, 0, 0, 220);
            l.dropShadowOffset = new Vector2(1f, -1f);
            return l;
        }

        // ---------- cleanup ----------

        private void DestroyUi()
        {
            foreach (Row r in rows)
            {
                if (r.Texture != null)
                {
                    Destroy(r.Texture);
                }
            }

            rows.Clear();
            DestroyDetail();
            hoverTip = null; // destroyed with the window
            hoverLine = null;
            hoverDot = null;
            filterTabs.Clear();
            if (buttonIcon != null && buttonIcon.texture != null)
            {
                Destroy(buttonIcon.texture);
            }

            DestroyPopover();
            if (window != null)
            {
                Destroy(window.gameObject);
                window = null;
            }

            if (button != null)
            {
                Destroy(button.gameObject);
                button = null;
            }

            shownSignature = null;
            dragging = false;
        }

        /// <summary>UI left by an older assembly (hot reload): snapshot, then destroy.</summary>
        private static void RemoveStale()
        {
            var stale = new List<GameObject>();
            foreach (UIComponent c in FindObjectsOfType<UIComponent>())
            {
                if (
                    c != null
                    && (c.name == ButtonName || c.name == WindowName || c.name == PopoverName)
                )
                {
                    stale.Add(c.gameObject);
                }
            }

            foreach (GameObject go in stale)
            {
                go.name += " (destroying)";
                Destroy(go);
            }
        }
    }
}

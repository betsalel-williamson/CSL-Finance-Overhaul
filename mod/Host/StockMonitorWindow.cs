#if HAS_CITIES
using System;
using System.Collections.Generic;
using ColossalFramework.UI;
using FinanceOverhaul.Market;
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
        private const float TitleHeight = 38f;
        private const float BarHeight = 34f;
        private const float RowHeight = 52f;
        private const float CompactRowHeight = 34f;

        private static float RowH
        {
            get { return Settings.CompactRows ? CompactRowHeight : RowHeight; }
        }
        private const float FooterHeight = 28f;
        private const int VisibleRows = 10;
        private const int SparkW = 72,
            SparkH = 28,
            SparkScale = 2;

        private static readonly Color32 White = new Color32(245, 247, 250, 255);
        private static readonly Color32 Muted = new Color32(158, 163, 170, 255);
        private static readonly Color32 Cyan = new Color32(101, 214, 248, 255);
        private static readonly Color32 Gold = new Color32(239, 176, 88, 255);
        private static readonly Color32 GoldBack = new Color32(70, 52, 24, 255);
        private static readonly Color32 PillText = new Color32(9, 14, 17, 255); // black reads best on the light game colours
        private static readonly Color32 Gain = new Color32(78, 244, 101, 255);
        private static readonly Color32 Loss = new Color32(233, 66, 79, 255);
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
            FadeBackground();
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
                        mask[y * s + x] == Sparkline.Line ? Gain : new Color32(0, 0, 0, 0);
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
            background.color = new Color32(10, 13, 15, 255);
            background.size = window.size;
            background.relativePosition = Vector3.zero;
            background.isInteractive = false;
            ApplyOpacity();

            // Title bar: drag handle, centred title, the game's gear and close buttons.
            UIDragHandle drag = window.AddUIComponent<UIDragHandle>();
            drag.name = Prefix + "Drag";
            drag.target = window;
            drag.size = new Vector2(Width - 80f, TitleHeight);
            drag.relativePosition = Vector3.zero;
            drag.eventMouseUp += (c, p) => SaveWindowPosition();

            UILabel title = Label(window, Prefix + "Title", "Stock Monitor", 1.1f, White);
            title.autoSize = false;
            title.size = new Vector2(Width, TitleHeight);
            title.textAlignment = UIHorizontalAlignment.Center;
            title.verticalAlignment = UIVerticalAlignment.Middle;
            title.relativePosition = Vector3.zero;
            title.isInteractive = false;

            UIButton settings = window.AddUIComponent<UIButton>();
            settings.name = Prefix + "Settings";
            settings.size = new Vector2(30f, 30f);
            settings.normalBgSprite = "RoundBackBig";
            settings.hoveredBgSprite = "RoundBackBigHovered";
            settings.pressedBgSprite = "RoundBackBigPressed";
            settings.normalFgSprite = "Options";
            settings.relativePosition = new Vector3(Width - 72f, 4f);
            settings.tooltip = "Stock Monitor settings";
            settings.eventClick += (c, p) => TogglePopover();

            UIButton close = window.AddUIComponent<UIButton>();
            close.name = Prefix + "Close";
            close.size = new Vector2(32f, 32f);
            close.normalBgSprite = "buttonclose";
            close.hoveredBgSprite = "buttonclosehover";
            close.pressedBgSprite = "buttonclosepressed";
            close.relativePosition = new Vector3(Width - 38f, 3f);
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
                tab.size = new Vector2(f == MonitorFilter.Portfolio ? 74f : 52f, 22f);
                tab.normalBgSprite = "ButtonMenu";
                tab.hoveredBgSprite = "ButtonMenuHovered";
                tab.pressedBgSprite = "ButtonMenuPressed";
                tab.relativePosition = new Vector3(x, TitleHeight + 6f);
                tab.eventClick += (c, p) => SetFilter(filter);
                filterTabs[f] = tab;
                x += tab.width + 6f;
            }

            sortLabel = Label(window, Prefix + "Sort", "", 0.75f, Muted);
            sortLabel.autoSize = false;
            sortLabel.size = new Vector2(120f, 22f);
            sortLabel.textAlignment = UIHorizontalAlignment.Right;
            sortLabel.verticalAlignment = UIVerticalAlignment.Middle;
            sortLabel.relativePosition = new Vector3(Width - 132f, TitleHeight + 6f);
            sortLabel.isInteractive = true;
            sortLabel.eventClick += (c, p) => CycleSort();

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

        private void CycleSort()
        {
            Settings.Sort = StockMonitor.NextSort(Settings.Sort);
            Mod.SaveSettings();
            Refresh(true);
        }

        private void ApplyOpacity()
        {
            if (background != null)
            {
                background.opacity = Settings.BackgroundOpacity(
                    window != null && window.containsMouse
                );
            }
        }

        /// <summary>City Vitals style: solid while the mouse is over the window, see-through otherwise (~0.15 s fade).</summary>
        private void FadeBackground()
        {
            if (background == null || window == null || !window.isVisible)
            {
                return;
            }

            float target = Settings.BackgroundOpacity(window.containsMouse);
            float now = background.opacity;
            if (Mathf.Abs(now - target) > 0.005f)
            {
                background.opacity = Mathf.MoveTowards(now, target, Time.unscaledDeltaTime * 6f);
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
            int today = OrderDesk.Today;
            string signature = Signature(quotes, today);
            if (!force && signature == shownSignature)
            {
                return;
            }

            shownSignature = signature;
            List<MonitorQuote> shown = StockMonitor.Rows(
                quotes,
                Settings.Filter,
                Settings.Sort,
                Settings.Hidden
            );
            while (rows.Count < shown.Count)
            {
                rows.Add(BuildRow());
            }

            for (int i = 0; i < rows.Count; i++)
            {
                bool used = i < shown.Count;
                rows[i].Panel.isVisible = used;
                if (used)
                {
                    Fill(rows[i], shown[i]);
                }
            }

            int visible = Mathf.Clamp(shown.Count, 1, VisibleRows);
            list.height = RowH * visible;
            window.height = Height(visible);
            background.size = window.size;
            footer.relativePosition = new Vector3(12f, TitleHeight + BarHeight + list.height + 7f);
            footer.text = StockMonitor.Footer(SimulationManager.instance.m_currentGameTime);
            empty.text = shown.Count == 0 ? StockMonitor.EmptyText(Settings.Filter) : "";
            empty.isVisible = shown.Count == 0;
            sortLabel.text = StockMonitor.SortLabel(Settings.Sort);
        }

        private static string Signature(List<MonitorQuote> quotes, int today)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(today).Append('|').Append(Settings.Filter).Append('|').Append(Settings.Sort);
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
            r.Change.useDropShadow = false;
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

        private static void Fill(Row r, MonitorQuote q)
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

            string badge = StockMonitor.OrdersBadge(q.OpenOrders);
            r.Orders.isVisible = badge != null;
            r.Orders.name = Prefix + "Orders-" + q.Symbol;
            if (badge != null)
            {
                r.Orders.text = badge;
                r.Orders.relativePosition = new Vector3(x, 9f);
            }

            DrawSpark(r.Texture, q.History, colour);
        }

        private static void DrawSpark(Texture2D tex, float[] history, Color32 colour)
        {
            int w = tex.width,
                h = tex.height;
            byte[] mask = Sparkline.Render(history, w, h, SparkScale + 1);
            var px = new Color32[w * h];
            var dash = new Color32(colour.r, colour.g, colour.b, 150);
            var clear = new Color32(0, 0, 0, 0);
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
#endif

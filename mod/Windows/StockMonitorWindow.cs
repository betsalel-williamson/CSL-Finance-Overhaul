using System; // Exception
using System.Collections.Generic; // Dictionary, List
// ColossalFramework.UI: LayoutDirection, UIButton, UIComponent, UIDragHandle,
//   UIHorizontalAlignment, UILabel, UIOrientation, UIPanel, UIScrollablePanel, UITextureSprite,
//   UIVerticalAlignment, UIView
using ColossalFramework.UI;
// FinanceOverhaul.Core: MonitorFilter, MonitorOrders, StockMonitor, StockMonitorSettings, Strings
using FinanceOverhaul.Core;
using UnityEngine; // Color32, GameObject, Mathf, MonoBehaviour, Texture2D, Time, Vector2, Vector3

// Game classes used without a using (global namespace, Assembly-CSharp): BuySellInvestmentPanel

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

        private static readonly Color32 White = FoTheme.TextPrimary;
        private static readonly Color32 Muted = FoTheme.TextMuted;
        private static readonly Color32 Cyan = FoTheme.Accent;
        private static readonly Color32 Gold = FoTheme.TickerSymbol;
        private static readonly Color32 GoldBack = FoTheme.HeldBack;
        private static readonly Color32 PillText = FoTheme.Header; // black reads best on the light game colours
        private static readonly Color32 Gain = FoTheme.TickerGain;
        private static readonly Color32 Loss = FoTheme.Loss;
        private static readonly Color32 Flat = FoTheme.TickerFlat;
        private static readonly Color32 Divider = FoTheme.Divider;

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
        private FoToggleTabs filterBar;
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

        /// <summary>
        /// Esc, before the game acts on it: closes the settings popover if it is open, else the
        /// window. False (the game handles the key) when the window is closed or the game's
        /// Buy/Sell dialog is open, which the game closes first.
        /// </summary>
        public bool HandleEscape()
        {
            if (window == null || !window.isVisible)
            {
                return false;
            }

            BuySellInvestmentPanel dialog = BuySellInvestmentPanel.instance;
            if (dialog != null && dialog.isVisible)
            {
                return false;
            }

            if (popover != null && popover.isVisible)
            {
                HidePopover();
            }
            else
            {
                SetOpen(false, true);
            }

            return true;
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

            FoWindow.AddTitle(window, Prefix + "Title", Strings.Get("monitor.title"), Width);

            UIButton settings = FoWindow.AddGear(window, Prefix + "Settings", Width);
            settings.tooltip = Strings.Get("monitor.settings.tip");
            settings.eventClick += (c, p) => TogglePopover();

            UIButton close = FoWindow.AddClose(window, Prefix + "Close", Width);
            close.eventClick += (c, p) => SetOpen(false, true);

            // Filter tab group + sort.
            MonitorFilter[] filters =
            {
                MonitorFilter.All,
                MonitorFilter.Portfolio,
                MonitorFilter.Orders,
            };
            filterBar = FoToggleTabs.Add(
                window,
                Prefix + "Filters",
                new[]
                {
                    StockMonitor.FilterLabel(MonitorFilter.All),
                    StockMonitor.FilterLabel(MonitorFilter.Portfolio),
                    StockMonitor.FilterLabel(MonitorFilter.Orders),
                },
                // Orders is wide enough for its count, "Orders (12)".
                new[] { 44f, 70f, 78f },
                22f,
                0.75f,
                i => SetFilter(filters[i])
            );
            filterBar.Panel.relativePosition = new Vector3(12f, TitleHeight + 6f);
            for (int i = 0; i < filters.Length; i++)
            {
                filterBar[i].name = Prefix + "Filter-" + filters[i];
                filterTabs[filters[i]] = filterBar[i];
            }

            sortLabel = FoText.Label(window, Prefix + "Sort", "", 0.75f, Muted);
            sortLabel.autoSize = false;
            sortLabel.size = new Vector2(120f, 22f);
            sortLabel.textAlignment = UIHorizontalAlignment.Right;
            sortLabel.verticalAlignment = UIVerticalAlignment.Middle;
            sortLabel.relativePosition = new Vector3(Width - 158f, TitleHeight + 6f);
            sortLabel.isInteractive = true;
            sortLabel.eventClick += (c, p) => CycleSort();

            // ▲/▼: flips the sort's direction (highest/lowest first, A→Z/Z→A).
            sortDirection = FoButton.Add(
                window,
                Prefix + "SortDirection",
                "",
                new Vector2(22f, 22f),
                0.75f
            );
            sortDirection.relativePosition = new Vector3(Width - 32f, TitleHeight + 6f);
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

            empty = FoText.Label(window, Prefix + "Empty", "", 0.85f, Muted);
            empty.relativePosition = new Vector3(14f, TitleHeight + BarHeight + 12f);

            footer = FoText.Label(window, Prefix + "Footer", "", 0.65f, Muted);
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
            if (filterBar != null)
            {
                filterBar.Choose((int)Settings.Filter);
            }
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
            filterBar = null;
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

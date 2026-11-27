using System; // Action, Array, Func
using System.Collections.Generic; // Dictionary, KeyValuePair, List
using System.Text; // StringBuilder
// ColossalFramework.UI: UIComponent, UILabel, UIOrientation, UIPanel, UIScrollablePanel,
//   UITextureSprite
using ColossalFramework.UI;
// FinanceOverhaul.Core: MonitorQuote, MonitorSort, OverviewPage, OverviewPages, StockDetail,
//   Strings, TradeOrder
using FinanceOverhaul.Core;
using UnityEngine; // Color32, Texture2D, Vector2, Vector3

namespace FinanceOverhaul
{
    /// <summary>
    /// One view of the overview (Market, Portfolio, Heatmap, City, Feed): its own panel in the
    /// body, drawn from the latest quotes when it is the one on show.
    /// </summary>
    internal interface IOverviewView
    {
        UIPanel Panel { get; }

        /// <summary>Adds what else, besides prices and orders, should make it draw again.</summary>
        void Sign(StringBuilder sig);

        void Draw(OverviewFrame frame);
    }

    /// <summary>What a view draws from: the quotes, the chart range, the pills' unit.</summary>
    internal sealed class OverviewFrame
    {
        public List<MonitorQuote> Quotes;

        /// <summary>The days the charts span (7 or 30), one choice for every view.</summary>
        public int Days;

        /// <summary>The pills show ₡ (points), not % (one switch for all, the list's too).</summary>
        public bool InPoints;

        /// <summary>The Investments list's sort, for a view that lists every stock (City).</summary>
        public MonitorSort Sort;

        /// <summary>The list's sort runs the other way (its ▲/▼).</summary>
        public bool Reversed;
    }

    /// <summary>
    /// The right-hand column of Economy → Investments (Figma band 20, "Two columns"): a sub tab
    /// group that picks one view at a time, and the view itself, drawn in the game's panel art
    /// and the theme's colours. Each view is its own class (<see cref="OverviewMarket"/>,
    /// <see cref="OverviewPortfolio"/>, <see cref="OverviewHeatmap"/>, <see cref="OverviewCity"/>,
    /// <see cref="OverviewFeed"/>) behind <see cref="IOverviewView"/>; this shell holds what they
    /// share (the body, the 7D | 30D choice, the pills' switch, chart boxes, scrolling lists)
    /// and redraws the one on show only when something it shows has changed. Their numbers come
    /// from the library; the views only draw them.
    /// </summary>
    internal sealed class InvestmentsOverview
    {
        internal const string Name = InvestmentsList.Prefix + "Overview";
        internal const float Pad = 12f;
        internal const int ChartScale = 2;
        private const float BarH = 34f;

        private readonly UIPanel root;
        private readonly FoToggleTabs tabs;
        private readonly Func<bool> pillsInPoints;
        private readonly Func<MonitorSort> listSort;
        private readonly Func<bool> listReversed;
        private readonly Dictionary<OverviewPage, IOverviewView> views =
            new Dictionary<OverviewPage, IOverviewView>();

        private List<MonitorQuote> quotes = new List<MonitorQuote>();
        private string drawn;

        /// <summary>Orders the Portfolio and Feed views show instead of the city's (game UI tests: the longest text).</summary>
        internal static List<TradeOrder> TestOrders;

        /// <summary>The body the views fill (the game's card art, as the list's).</summary>
        internal UIPanel Body { get; private set; }

        /// <summary>Textures the views drew into; destroyed with the overview.</summary>
        internal List<Texture2D> Textures { get; } = new List<Texture2D>();

        /// <summary>A click on any pill: flips every pill between % and ₡, the list's too.</summary>
        internal Action TogglePills { get; private set; }

        private static OverviewPage Page
        {
            get
            {
                return Mod.Settings == null
                    ? OverviewPage.Market
                    : OverviewPages.Shown(Mod.Settings.InvestmentOverviewPage);
            }
            set
            {
                if (Mod.Settings != null)
                {
                    Mod.Settings.InvestmentOverviewPage = value;
                    Mod.SaveSettings();
                }
            }
        }

        /// <summary>The days every view's chart spans (7 or 30), one choice for all.</summary>
        private static int Days
        {
            get
            {
                return
                    Mod.Settings != null
                    && Mod.Settings.InvestmentOverviewDays == StockDetail.ShortRange
                    ? StockDetail.ShortRange
                    : StockDetail.LongRange;
            }
            set
            {
                if (Mod.Settings != null)
                {
                    Mod.Settings.InvestmentOverviewDays = value;
                    Mod.SaveSettings();
                }
            }
        }

        /// <param name="inPoints">The list's pills show ₡ (points), not % (one switch for all).</param>
        /// <param name="toggle">A click on any pill: flips every pill, the list's too.</param>
        /// <param name="sort">The list's sort (City lists the stocks in the same order).</param>
        /// <param name="reversed">The list's sort direction.</param>
        public InvestmentsOverview(
            UIComponent parent,
            Vector3 at,
            Vector2 size,
            Func<bool> inPoints,
            Action toggle,
            Func<MonitorSort> sort,
            Func<bool> reversed
        )
        {
            pillsInPoints = inPoints;
            listSort = sort;
            listReversed = reversed;
            TogglePills = toggle;
            root = parent.AddUIComponent<UIPanel>();
            root.name = Name;
            root.relativePosition = at;
            root.size = size;

            // ---- header: a sub tab per view, level with the list's tabs ----
            var labels = new string[OverviewPages.All.Length];
            var widths = new float[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                labels[i] = OverviewPages.Tab(OverviewPages.All[i]);
                widths[i] = 88f;
            }

            tabs = FoToggleTabs.Add(
                root,
                Name + "Tabs",
                labels,
                widths,
                26f,
                0.8f,
                i => Pick(OverviewPages.All[i])
            );
            tabs.Secondary();
            tabs.Panel.relativePosition = new Vector3(0f, 2f);
            for (int i = 0; i < labels.Length; i++)
            {
                OverviewPage v = OverviewPages.All[i];
                tabs[i].name = Name + "Tab-" + v;
                if (!OverviewPages.IsBuilt(v))
                {
                    tabs.SetEnabled(i, false, Strings.Get("overview.tab.later"));
                }
            }

            // ---- body: the game's card art, as the list's ----
            Body = root.AddUIComponent<UIPanel>();
            Body.name = Name + "Body";
            Body.backgroundSprite = "GenericPanel";
            Body.color = FoTheme.GameStockCard;
            Body.relativePosition = new Vector3(0f, BarH);
            Body.size = new Vector2(size.x, size.y - BarH);

            views[OverviewPage.Market] = new OverviewMarket(this);
            views[OverviewPage.Portfolio] = new OverviewPortfolio(this);
            views[OverviewPage.Heatmap] = new OverviewHeatmap(this);
            views[OverviewPage.City] = new OverviewCity(this);
            views[OverviewPage.Feed] = new OverviewFeed(this);
        }

        /// <summary>The page on show (game UI tests).</summary>
        internal OverviewPage Showing
        {
            get { return Page; }
        }

        /// <summary>Shows a view (its tab, game UI tests); a view not built yet is ignored.</summary>
        internal void Pick(OverviewPage page)
        {
            if (!OverviewPages.IsBuilt(page))
            {
                return;
            }

            Page = page;
            Redraw();
        }

        /// <summary>New prices (the list's own read): redraws only when something changed.</summary>
        public void Refresh(List<MonitorQuote> latest)
        {
            quotes = latest ?? new List<MonitorQuote>();
            Draw();
        }

        /// <summary>Draws the view on show again now (a choice inside it changed).</summary>
        internal void Redraw()
        {
            drawn = null;
            Draw();
        }

        public void Destroy()
        {
            foreach (Texture2D t in Textures)
            {
                if (t != null)
                {
                    UnityEngine.Object.Destroy(t);
                }
            }

            Textures.Clear();
            if (root != null)
            {
                UnityEngine.Object.Destroy(root.gameObject);
            }
        }

        private void Draw()
        {
            OverviewPage page = Page;
            IOverviewView view = views[page];
            var frame = new OverviewFrame
            {
                Quotes = quotes,
                Days = Days,
                InPoints = pillsInPoints(),
                Sort = listSort(),
                Reversed = listReversed(),
            };
            var sig = new StringBuilder();
            sig.Append(page)
                .Append('|')
                .Append(frame.Days)
                .Append('|')
                .Append(Strings.Language)
                .Append('|')
                .Append(frame.InPoints)
                .Append('|')
                .Append(frame.Sort)
                .Append(frame.Reversed)
                .Append('|')
                .Append(OrderDesk.Today);
            foreach (MonitorQuote q in quotes)
            {
                sig.Append('|')
                    .Append(q.Symbol)
                    .Append(':')
                    .Append(q.Price.ToString("0.##"))
                    .Append(':')
                    .Append(q.ChangeRate.ToString("0.##"))
                    .Append(':')
                    .Append(q.Locked)
                    .Append(':')
                    .Append(q.Owned)
                    .Append(':')
                    .Append(q.Spent);
            }

            foreach (TradeOrder o in OrderDesk.Snapshot())
            {
                sig.Append("|o").Append(o.Id);
            }

            sig.Append(TestOrders == null ? "|city" : "|test");
            view.Sign(sig);
            string signature = sig.ToString();
            if (signature == drawn)
            {
                return;
            }

            drawn = signature;
            tabs.Choose(Array.IndexOf(OverviewPages.All, page));
            foreach (KeyValuePair<OverviewPage, IOverviewView> v in views)
            {
                v.Value.Panel.isVisible = v.Key == page;
            }

            view.Draw(frame);
        }

        // ---------- shared building blocks ----------

        /// <summary>A view's own panel, filling the body.</summary>
        internal UIPanel NewView(string name)
        {
            UIPanel p = Body.AddUIComponent<UIPanel>();
            p.name = Name + name;
            p.size = Body.size;
            p.relativePosition = Vector3.zero;
            p.isVisible = false;
            return p;
        }

        /// <summary>7D | 30D for a view's chart: one choice for every view.</summary>
        internal FoToggleTabs RangeTabs(UIComponent parent, string name, float right, float y)
        {
            FoToggleTabs range = FoToggleTabs.Add(
                parent,
                name,
                new[]
                {
                    StockDetail.RangeChip(StockDetail.ShortRange),
                    StockDetail.RangeChip(StockDetail.LongRange),
                },
                new[] { 40f, 40f },
                20f,
                0.7f,
                i =>
                {
                    Days = i == 0 ? StockDetail.ShortRange : StockDetail.LongRange;
                    Redraw();
                }
            );
            range.Secondary();
            range[0].name = name + "-" + StockDetail.ShortRange;
            range[1].name = name + "-" + StockDetail.LongRange;
            range.Panel.relativePosition = new Vector3(right - 80f, y);
            return range;
        }

        internal static UILabel Heading(UIComponent parent, string text, float x, float y)
        {
            UILabel l = FoText.Label(parent, null, text, 0.8f, FoTheme.GameHeading);
            l.relativePosition = new Vector3(x, y);
            return l;
        }

        /// <summary>Opens the game's dialog on a stock, as a click on its line in the list does.</summary>
        internal static void OpenDialog(MonitorQuote q)
        {
            if (q != null)
            {
                TradeDialog.ShowForLine(q.Id, q.Locked, q.Available, q.Owned);
            }
        }

        /// <summary>
        /// A list under a view's header that scrolls with the mouse wheel and its scrollbar
        /// (Kit <see cref="FoScrollbar"/>, shown only when the list is longer than the view).
        /// </summary>
        internal static UIScrollablePanel Scroller(UIPanel view, string name, float top)
        {
            float height = view.height - top - Pad;
            UIScrollablePanel list = view.AddUIComponent<UIScrollablePanel>();
            list.name = name;
            list.relativePosition = new Vector3(Pad, top);
            list.size = new Vector2(view.width - 2f * Pad - FoScrollbar.Width - 4f, height);
            list.clipChildren = true;
            list.autoLayout = false;
            list.scrollWheelDirection = UIOrientation.Vertical;
            list.scrollWheelAmount = 26;
            // The game's scrolling list only takes the mouse wheel with this on; without it the
            // wheel passed on to the Investments list and scrolled the stocks instead.
            list.builtinKeyNavigation = true;
            list.verticalScrollbar = FoScrollbar.Add(
                view,
                name + "Scrollbar",
                new Vector3(view.width - Pad - FoScrollbar.Width, top),
                height
            );
            return list;
        }

        /// <summary>
        /// A line chart on the game's chart black, its high and low inside it and "N days ago …
        /// Today" under it. Values named <c>name + "Chart"</c>, <c>"High"</c>, <c>"Low"</c>.
        /// </summary>
        internal sealed class ChartBox
        {
            private readonly Texture2D texture;
            private readonly UILabel high;
            private readonly UILabel low;
            private readonly UILabel start;
            private readonly float height;

            public ChartBox(
                UIComponent parent,
                string name,
                float x,
                float y,
                float width,
                float height,
                List<Texture2D> owned
            )
            {
                this.height = height;
                UIPanel back = parent.AddUIComponent<UIPanel>();
                back.name = name + "ChartBack";
                back.backgroundSprite = "GenericPanelWhite";
                back.color = FoTheme.GameChartBack;
                back.size = new Vector2(width, height);
                back.relativePosition = new Vector3(x, y);
                back.isInteractive = false;
                texture = FoTexture.Canvas(
                    (int)(width - 16f) * ChartScale,
                    (int)(height - 24f) * ChartScale
                );
                owned.Add(texture);
                UITextureSprite line = back.AddUIComponent<UITextureSprite>();
                line.name = name + "Chart";
                line.texture = texture;
                line.size = new Vector2(width - 16f, height - 24f);
                line.relativePosition = new Vector3(8f, 12f);
                line.isInteractive = false;
                high = FoText.Label(back, name + "High", "", 0.65f, FoTheme.ChartLabel);
                high.relativePosition = new Vector3(8f, 2f);
                low = FoText.Label(back, name + "Low", "", 0.65f, FoTheme.ChartLabel);
                start = FoText.Label(parent, name + "Start", "", 0.65f, FoTheme.ChartLabel);
                start.relativePosition = new Vector3(x, y + height + 3f);
                UILabel today = FoText.Label(
                    parent,
                    name + "Today",
                    Strings.Get("overview.market.today"),
                    0.65f,
                    FoTheme.ChartLabel
                );
                today.relativePosition = new Vector3(x + width - today.width, y + height + 3f);
            }

            public void Draw(
                float[] series,
                Color32 colour,
                string highText,
                string lowText,
                int days
            )
            {
                FoSparkline.Draw(texture, series, colour, ChartScale + 1);
                high.text = Strings.Format("overview.market.high", highText);
                low.text = Strings.Format("overview.market.low", lowText);
                low.relativePosition = new Vector3(8f, height - low.height - 2f);
                start.text = Strings.Format("overview.market.daysAgo", days.ToString());
            }
        }
    }
}

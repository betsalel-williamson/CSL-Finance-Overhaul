using System; // Action, Array, Func
using System.Collections.Generic; // List
using System.Text; // StringBuilder
using ColossalFramework.UI; // UIComponent, UILabel, UIPanel, UITextureSprite
// FinanceOverhaul.Core: LocalCompany, MonitorQuote, OverviewPage, OverviewPages, StockDetail,
//   Strings, TradeOrder
using FinanceOverhaul.Core;
using UnityEngine; // Color32, Texture2D, Vector2, Vector3

// Game classes used without a using (global namespace, Assembly-CSharp): BuySellInvestmentPanel

namespace FinanceOverhaul
{
    /// <summary>
    /// The right-hand column of Economy → Investments (Figma band 20, "Two columns"): a sub tab
    /// group that picks one view at a time (a view not built yet would be disabled), and the view
    /// itself, drawn in the game's panel art and the theme's colours. Each view is a partial
    /// file (<c>.Market</c>, <c>.Portfolio</c>, <c>.Heatmap</c>, <c>.City</c>, <c>.Feed</c>);
    /// their numbers come from the library (<see cref="MarketOverview"/>,
    /// <see cref="PortfolioOverview"/>, <see cref="HeatmapOverview"/>, <see cref="CityOverview"/>,
    /// <see cref="FeedOverview"/>), this only draws them.
    /// </summary>
    internal sealed partial class InvestmentsOverview
    {
        private const string Name = InvestmentsList.Prefix + "Overview";
        private const float BarH = 34f;
        private const float Pad = 12f;
        private const int ChartScale = 2;

        private readonly UIPanel root;
        private readonly FoToggleTabs views;
        private readonly UIPanel body;
        private readonly List<Texture2D> textures = new List<Texture2D>();
        private readonly Func<bool> pillsInPoints;
        private readonly Action togglePills;

        private List<MonitorQuote> quotes = new List<MonitorQuote>();
        private string drawn;

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
        public InvestmentsOverview(
            UIComponent parent,
            Vector3 at,
            Vector2 size,
            Func<bool> inPoints,
            Action toggle
        )
        {
            pillsInPoints = inPoints;
            togglePills = toggle;
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

            views = FoToggleTabs.Add(
                root,
                Name + "Tabs",
                labels,
                widths,
                26f,
                0.8f,
                i => Pick(OverviewPages.All[i])
            );
            views.Secondary();
            views.Panel.relativePosition = new Vector3(0f, 2f);
            for (int i = 0; i < labels.Length; i++)
            {
                OverviewPage v = OverviewPages.All[i];
                views[i].name = Name + "Tab-" + v;
                if (!OverviewPages.IsBuilt(v))
                {
                    views.SetEnabled(i, false, Strings.Get("overview.tab.later"));
                }
            }

            // ---- body: the game's card art, as the list's ----
            body = root.AddUIComponent<UIPanel>();
            body.name = Name + "Body";
            body.backgroundSprite = "GenericPanel";
            body.color = FoTheme.GameStockCard;
            body.relativePosition = new Vector3(0f, BarH);
            body.size = new Vector2(size.x, size.y - BarH);

            BuildMarket();
            BuildPortfolio();
            BuildHeatmap();
            BuildCity();
            BuildFeed();
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
            drawn = null;
            Draw();
        }

        /// <summary>New prices (the list's own read): redraws only when something changed.</summary>
        public void Refresh(List<MonitorQuote> latest)
        {
            quotes = latest ?? new List<MonitorQuote>();
            Draw();
        }

        public void Destroy()
        {
            foreach (Texture2D t in textures)
            {
                if (t != null)
                {
                    UnityEngine.Object.Destroy(t);
                }
            }

            textures.Clear();
            if (root != null)
            {
                UnityEngine.Object.Destroy(root.gameObject);
            }
        }

        private void Draw()
        {
            OverviewPage page = Page;
            int days = Days;
            var sig = new StringBuilder();
            sig.Append(page)
                .Append('|')
                .Append(days)
                .Append('|')
                .Append(Strings.Language)
                .Append('|')
                .Append(pillsInPoints())
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

            sig.Append(TestOrders == null ? "|city" : "|test")
                .Append("|h")
                .Append(HeatSpan)
                .Append("|n")
                .Append(ChirpNotices.Log.Snapshot().Count);
            if (page == OverviewPage.Feed)
            {
                foreach (LocalCompany c in FeedCompanies())
                {
                    sig.Append("|c").Append(c.Symbol).Append(':').Append(c.Status);
                }
            }

            string signature = sig.ToString();
            if (signature == drawn)
            {
                return;
            }

            drawn = signature;
            views.Choose(Array.IndexOf(OverviewPages.All, page));
            market.isVisible = page == OverviewPage.Market;
            portfolio.isVisible = page == OverviewPage.Portfolio;
            heatmap.isVisible = page == OverviewPage.Heatmap;
            city.isVisible = page == OverviewPage.City;
            feed.isVisible = page == OverviewPage.Feed;
            switch (page)
            {
                case OverviewPage.Market:
                    DrawMarket(days);
                    break;
                case OverviewPage.Portfolio:
                    DrawPortfolio(days);
                    break;
                case OverviewPage.Heatmap:
                    DrawHeatmap();
                    break;
                case OverviewPage.City:
                    DrawCity(days);
                    break;
                default:
                    DrawFeed();
                    break;
            }
        }

        // ---------- shared building blocks ----------

        /// <summary>A view's own panel, filling the body.</summary>
        private UIPanel View(string name)
        {
            UIPanel p = body.AddUIComponent<UIPanel>();
            p.name = Name + name;
            p.size = body.size;
            p.relativePosition = Vector3.zero;
            p.isVisible = false;
            return p;
        }

        /// <summary>7D | 30D for a view's chart: one choice for every view.</summary>
        private FoToggleTabs RangeTabs(UIComponent parent, string name, float right, float y)
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
                    drawn = null;
                    Draw();
                }
            );
            range.Secondary();
            range[0].name = name + "-" + StockDetail.ShortRange;
            range[1].name = name + "-" + StockDetail.LongRange;
            range.Panel.relativePosition = new Vector3(right - 80f, y);
            return range;
        }

        private UILabel Heading(UIComponent parent, string text, float x, float y)
        {
            UILabel l = FoText.Label(parent, null, text, 0.8f, FoTheme.GameHeading);
            l.relativePosition = new Vector3(x, y);
            return l;
        }

        /// <summary>Opens the game's dialog on a stock, as a click on its line in the list does.</summary>
        private static void OpenDialog(MonitorQuote q)
        {
            if (q == null || q.Locked || q.Id <= 0)
            {
                return;
            }

            BuySellInvestmentPanel.Mode mode = BuySellInvestmentPanel.Mode.Buy;
            if (q.Available == 0)
            {
                if (q.Owned == 0)
                {
                    return;
                }

                mode = BuySellInvestmentPanel.Mode.Sell;
            }

            BuySellInvestmentPanel.ShowPanel((byte)q.Id, mode);
        }

        /// <summary>
        /// A line chart on the game's chart black, its high and low inside it and "N days ago …
        /// Today" under it. Values named <c>name + "Chart"</c>, <c>"High"</c>, <c>"Low"</c>.
        /// </summary>
        private sealed class ChartBox
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

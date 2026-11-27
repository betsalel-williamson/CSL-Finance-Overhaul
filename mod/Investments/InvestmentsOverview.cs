using System; // Array, Math
using System.Collections.Generic; // List
using System.Text; // StringBuilder
using ColossalFramework.UI; // UIComponent, UILabel, UIPanel, UITextureSprite
// FinanceOverhaul.Core: MarketOverview, MonitorQuote, OverviewPage, OverviewPages, StockDetail,
//   StockMonitor, Strings
using FinanceOverhaul.Core;
// UnityEngine: Color32, FilterMode, Texture2D, TextureFormat, TextureWrapMode, Vector2, Vector3
using UnityEngine;

// Game classes used without a using (global namespace, Assembly-CSharp): BuySellInvestmentPanel

namespace FinanceOverhaul
{
    /// <summary>
    /// The right-hand column of Economy → Investments (Figma band 20, "Two columns"): a sub tab
    /// group that picks one view at a time (views not built yet are disabled), and the view
    /// itself, drawn in the game's panel art and the theme's colours. The pages' numbers
    /// come from <see cref="MarketOverview"/> and the rest of the library; this only draws them.
    /// Page 1 is the Market overview: the market index over 7 or 30 days, today's top gainers
    /// and losers (a click opens the stock's dialog) and how many stocks rose, fell or stayed.
    /// </summary>
    internal sealed class InvestmentsOverview
    {
        private const string Name = InvestmentsList.Prefix + "Overview";
        private const float BarH = 34f;
        private const float Pad = 12f;
        private const float ChartH = 150f;
        private const int ChartScale = 2;
        private const int Movers = 3;
        private const float MoverH = 26f;

        private readonly UIPanel root;
        private readonly FoToggleTabs views;
        private readonly UIPanel body;
        private readonly List<Texture2D> textures = new List<Texture2D>();

        // Market page
        private readonly UIPanel market;
        private readonly UILabel indexKey;
        private readonly UILabel indexValue;
        private readonly UILabel indexChange;
        private readonly FoToggleTabs range;
        private readonly UITextureSprite chart;
        private readonly Texture2D chartTexture;
        private readonly UILabel chartHigh;
        private readonly UILabel chartLow;
        private readonly UILabel chartStart;
        private readonly Mover[] gainers = new Mover[Movers];
        private readonly Mover[] losers = new Mover[Movers];
        private readonly UILabel noGainers;
        private readonly UILabel noLosers;
        private readonly UILabel breadth;

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

        public InvestmentsOverview(UIComponent parent, Vector3 at, Vector2 size)
        {
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

            // ---- page 1: Market overview ----
            market = body.AddUIComponent<UIPanel>();
            market.name = Name + "Market";
            market.size = body.size;
            market.relativePosition = Vector3.zero;
            float w = body.width - 2f * Pad;

            indexKey = FoText.Label(
                market,
                Name + "MarketIndexKey",
                Strings.Get("overview.market.index"),
                0.75f,
                FoTheme.GameDialogBody
            );
            indexKey.relativePosition = new Vector3(Pad, Pad);
            indexValue = FoText.Label(market, Name + "MarketIndex", "", 1.4f, FoTheme.GameText);
            indexValue.relativePosition = new Vector3(Pad, Pad + 16f);
            indexValue.tooltip = Strings.Get("overview.market.indexTip");
            indexChange = FoText.Label(market, Name + "MarketChange", "", 0.8f, FoTheme.GameText);

            range = FoToggleTabs.Add(
                market,
                Name + "MarketRange",
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
            range[0].name = Name + "MarketRange-" + StockDetail.ShortRange;
            range[1].name = Name + "MarketRange-" + StockDetail.LongRange;
            range.Panel.relativePosition = new Vector3(market.width - Pad - 80f, Pad + 4f);

            float chartY = Pad + 52f;
            UIPanel chartBack = market.AddUIComponent<UIPanel>();
            chartBack.name = Name + "MarketChartBack";
            chartBack.backgroundSprite = "GenericPanelWhite";
            chartBack.color = FoTheme.GameChartBack;
            chartBack.size = new Vector2(w, ChartH);
            chartBack.relativePosition = new Vector3(Pad, chartY);
            chartBack.isInteractive = false;
            chartTexture = new Texture2D(
                (int)(w - 16f) * ChartScale,
                (int)(ChartH - 24f) * ChartScale,
                TextureFormat.ARGB32,
                false
            );
            chartTexture.filterMode = FilterMode.Bilinear;
            chartTexture.wrapMode = TextureWrapMode.Clamp;
            textures.Add(chartTexture);
            chart = chartBack.AddUIComponent<UITextureSprite>();
            chart.name = Name + "MarketChart";
            chart.texture = chartTexture;
            chart.size = new Vector2(w - 16f, ChartH - 24f);
            chart.relativePosition = new Vector3(8f, 12f);
            chart.isInteractive = false;
            chartHigh = FoText.Label(chartBack, Name + "MarketHigh", "", 0.65f, FoTheme.ChartLabel);
            chartHigh.relativePosition = new Vector3(8f, 2f);
            chartLow = FoText.Label(chartBack, Name + "MarketLow", "", 0.65f, FoTheme.ChartLabel);
            chartStart = FoText.Label(market, Name + "MarketStart", "", 0.65f, FoTheme.ChartLabel);
            chartStart.relativePosition = new Vector3(Pad, chartY + ChartH + 3f);
            UILabel today = FoText.Label(
                market,
                Name + "MarketToday",
                Strings.Get("overview.market.today"),
                0.65f,
                FoTheme.ChartLabel
            );
            today.relativePosition = new Vector3(Pad + w - today.width, chartY + ChartH + 3f);

            float moversY = chartY + ChartH + 26f;
            float half = (w - Pad) / 2f;
            Heading(Strings.Get("overview.market.gainers"), Pad, moversY);
            Heading(Strings.Get("overview.market.losers"), Pad + half + Pad, moversY);
            for (int i = 0; i < Movers; i++)
            {
                float y = moversY + 20f + i * MoverH;
                gainers[i] = new Mover(market, Name + "MarketGainer-" + i, Pad, y, half);
                losers[i] = new Mover(market, Name + "MarketLoser-" + i, Pad + half + Pad, y, half);
            }

            noGainers = None(Name + "MarketNoGainers", Pad, moversY + 22f);
            noLosers = None(Name + "MarketNoLosers", Pad + half + Pad, moversY + 22f);

            breadth = FoText.Label(market, Name + "MarketBreadth", "", 0.75f, FoTheme.TextLabel);
            breadth.relativePosition = new Vector3(Pad, market.height - Pad - 14f);
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
            sig.Append(page).Append('|').Append(days).Append('|').Append(Strings.Language);
            foreach (MonitorQuote q in quotes)
            {
                sig.Append('|')
                    .Append(q.Symbol)
                    .Append(':')
                    .Append(q.Price.ToString("0.##"))
                    .Append(':')
                    .Append(q.ChangeRate.ToString("0.##"))
                    .Append(':')
                    .Append(q.Locked);
            }

            string signature = sig.ToString();
            if (signature == drawn)
            {
                return;
            }

            drawn = signature;
            views.Choose(Array.IndexOf(OverviewPages.All, page));
            market.isVisible = page == OverviewPage.Market;
            if (page == OverviewPage.Market)
            {
                DrawMarket(days);
            }
        }

        private void DrawMarket(int days)
        {
            range.Choose(days == StockDetail.ShortRange ? 0 : 1);
            // With the city index fund listed this is IDX's own chart, in ₡.
            MonitorQuote fund = MarketOverview.IndexFund(quotes);
            bool inMoney = fund != null;
            indexKey.text = MarketOverview.IndexLabel(fund);
            float[] index = MarketOverview.Index(quotes, days);
            float last = float.NaN;
            for (int i = index.Length - 1; i >= 0 && float.IsNaN(last); i--)
            {
                last = index[i];
            }

            indexValue.text = MarketOverview.IndexText(last, inMoney);
            float change = MarketOverview.RangeChange(index);
            indexChange.text = MarketOverview.RangeChangeText(change, days);
            int sign = Math.Sign(Math.Round(change * 10f));
            indexChange.textColor =
                sign > 0 ? FoTheme.GamePositive
                : sign < 0 ? FoTheme.GameNegative
                : FoTheme.GameValue;
            indexChange.relativePosition = new Vector3(
                indexValue.relativePosition.x + indexValue.width + 10f,
                indexValue.relativePosition.y + indexValue.height - indexChange.height - 3f
            );

            Color32 line =
                sign > 0 ? FoTheme.GamePositive
                : sign < 0 ? FoTheme.GameNegative
                : FoTheme.Accent;
            FoSparkline.Draw(chartTexture, index, line, ChartScale + 1);
            chartHigh.text = Strings.Format(
                "overview.market.high",
                MarketOverview.IndexText(StockDetail.High(index), inMoney)
            );
            chartLow.text = Strings.Format(
                "overview.market.low",
                MarketOverview.IndexText(StockDetail.Low(index), inMoney)
            );
            chartLow.relativePosition = new Vector3(8f, ChartH - chartLow.height - 2f);
            chartStart.text = Strings.Format("overview.market.daysAgo", days.ToString());

            List<MonitorQuote> up = MarketOverview.Movers(quotes, true, Movers);
            List<MonitorQuote> down = MarketOverview.Movers(quotes, false, Movers);
            for (int i = 0; i < Movers; i++)
            {
                gainers[i].Show(i < up.Count ? up[i] : null);
                losers[i].Show(i < down.Count ? down[i] : null);
            }

            noGainers.isVisible = up.Count == 0;
            noLosers.isVisible = down.Count == 0;
            breadth.text = MarketOverview.Breadth(quotes);
        }

        // ---------- building blocks ----------

        private void Heading(string text, float x, float y)
        {
            FoText.Label(market, null, text, 0.8f, FoTheme.GameHeading).relativePosition =
                new Vector3(x, y);
        }

        private UILabel None(string name, float x, float y)
        {
            UILabel l = FoText.Label(
                market,
                name,
                Strings.Get("overview.market.noMovers"),
                0.75f,
                FoTheme.TextMuted
            );
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

        /// <summary>One mover: symbol and name, price, change pill; a click opens its dialog.</summary>
        private sealed class Mover
        {
            private readonly UIPanel back;
            private readonly UILabel symbol;
            private readonly UILabel price;
            private readonly UILabel change;
            private readonly float width;
            private MonitorQuote quote;

            public Mover(UIComponent parent, string name, float x, float y, float width)
            {
                this.width = width;
                back = parent.AddUIComponent<UIPanel>();
                back.name = name;
                back.backgroundSprite = "GenericPanelWhite";
                back.color = FoTheme.SectionFill;
                back.size = new Vector2(width, MoverH - 3f);
                back.relativePosition = new Vector3(x, y);
                back.eventMouseEnter += (c, p) => back.color = FoTheme.SectionFillHover;
                back.eventMouseLeave += (c, p) => back.color = FoTheme.SectionFill;
                back.eventClick += (c, p) => OpenDialog(quote);
                symbol = FoText.Label(back, name + "Symbol", "", 0.85f, FoTheme.GameText);
                symbol.relativePosition = new Vector3(8f, 4f);
                symbol.isInteractive = false;
                price = FoText.Label(back, name + "Price", "", 0.8f, FoTheme.GameValue);
                price.isInteractive = false;
                change = FoPill.AddValue(back, new Vector2(62f, 17f), 0.7f, FoTheme.Header);
                change.name = name + "Change";
                change.relativePosition = new Vector3(width - 62f - 4f, 3f);
                change.isInteractive = false;
            }

            public void Show(MonitorQuote q)
            {
                quote = q;
                back.isVisible = q != null;
                if (q == null)
                {
                    return;
                }

                symbol.text = q.Symbol;
                back.tooltip = q.Name;
                price.text = StockMonitor.PriceText(q.Price);
                price.relativePosition = new Vector3(width - 62f - 4f - 8f - price.width, 5f);
                change.text = StockMonitor.ChangeText(q.ChangeRate);
                change.color = FoTheme.Change(StockMonitor.ChangeColour(q.ChangeRate));
            }
        }
    }
}

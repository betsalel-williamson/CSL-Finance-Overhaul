using System; // Action, Math
using System.Collections.Generic; // List
using System.Text; // StringBuilder
using ColossalFramework.UI; // UIComponent, UILabel, UIPanel
using FinanceOverhaul.Core; // MarketOverview, MonitorQuote, StockDetail, StockMonitor, Strings
using UnityEngine; // Color32, Vector2, Vector3

namespace FinanceOverhaul
{
    /// <summary>
    /// The Market view: the market index (the city index fund itself when listed) over 7 or 30
    /// days with its pill, today's top gainers and losers (a click opens the stock's dialog;
    /// ranked by ₡ while the pills show points) and how many stocks rose, fell or stayed.
    /// </summary>
    internal sealed class OverviewMarket : IOverviewView
    {
        private const string Name = InvestmentsOverview.Name;
        private const float Pad = InvestmentsOverview.Pad;
        private const int ChartScale = InvestmentsOverview.ChartScale;

        private readonly InvestmentsOverview host;
        private List<MonitorQuote> quotes = new List<MonitorQuote>();

        public OverviewMarket(InvestmentsOverview host)
        {
            this.host = host;
            BuildMarket();
        }

        public UIPanel Panel
        {
            get { return market; }
        }

        private const float MarketChartH = 150f;
        private const int Movers = 3;
        private const float MoverH = 26f;

        private UIPanel market;
        private UILabel indexKey;
        private UILabel indexValue;
        private UILabel indexChange;
        private UILabel indexDays;
        private FoToggleTabs marketRange;
        private InvestmentsOverview.ChartBox marketChart;
        private readonly Mover[] gainers = new Mover[Movers];
        private readonly Mover[] losers = new Mover[Movers];
        private UILabel noGainers;
        private UILabel noLosers;
        private UILabel breadth;

        private void BuildMarket()
        {
            market = host.NewView("Market");
            float w = host.Body.width - 2f * Pad;

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
            // As every pill: a click shows all of them in % or in points (₡).
            indexChange = FoPill.AddToggle(
                market,
                Name + "MarketChange",
                new Vector2(72f, 18f),
                0.75f,
                FoTheme.Header,
                Strings.Get("monitor.pill.tip"),
                host.TogglePills
            );
            indexDays = FoText.Label(market, Name + "MarketDays", "", 0.75f, FoTheme.TextMuted);
            marketRange = host.RangeTabs(
                market,
                Name + "MarketRange",
                market.width - Pad,
                Pad + 4f
            );

            float chartY = Pad + 52f;
            marketChart = new InvestmentsOverview.ChartBox(
                market,
                Name + "Market",
                Pad,
                chartY,
                w,
                MarketChartH,
                host.Textures
            );

            float moversY = chartY + MarketChartH + 26f;
            float half = (w - Pad) / 2f;
            InvestmentsOverview.Heading(
                market,
                Strings.Get("overview.market.gainers"),
                Pad,
                moversY
            );
            InvestmentsOverview.Heading(
                market,
                Strings.Get("overview.market.losers"),
                Pad + half + Pad,
                moversY
            );
            for (int i = 0; i < Movers; i++)
            {
                float y = moversY + 20f + i * MoverH;
                gainers[i] = new Mover(
                    market,
                    Name + "MarketGainer-" + i,
                    Pad,
                    y,
                    half,
                    host.TogglePills
                );
                losers[i] = new Mover(
                    market,
                    Name + "MarketLoser-" + i,
                    Pad + half + Pad,
                    y,
                    half,
                    host.TogglePills
                );
            }

            noGainers = None(Name + "MarketNoGainers", Pad, moversY + 22f);
            noLosers = None(Name + "MarketNoLosers", Pad + half + Pad, moversY + 22f);

            breadth = FoText.Label(market, Name + "MarketBreadth", "", 0.75f, FoTheme.TextLabel);
            breadth.relativePosition = new Vector3(Pad, market.height - Pad - 14f);
        }

        /// <summary>What else, besides prices and orders, makes this view draw again.</summary>
        public void Sign(StringBuilder sig) { }

        public void Draw(OverviewFrame frame)
        {
            quotes = frame.Quotes;
            int days = frame.Days;
            bool inPoints = frame.InPoints;
            marketRange.Choose(days == StockDetail.ShortRange ? 0 : 1);
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
            int sign = Math.Sign(Math.Round(change * 10f));
            indexChange.text = MarketOverview.RangePill(index, inMoney, inPoints);
            indexChange.color = FoTheme.Change(StockMonitor.ChangeColour(change));
            float pillY =
                indexValue.relativePosition.y + indexValue.height - indexChange.height - 4f;
            indexChange.relativePosition = new Vector3(
                indexValue.relativePosition.x + indexValue.width + 10f,
                pillY
            );
            indexDays.text = MarketOverview.OverDays(days);
            indexDays.relativePosition = new Vector3(
                indexChange.relativePosition.x + indexChange.width + 6f,
                pillY + 2f
            );

            Color32 line = FoTheme.Trend(sign, FoTheme.Accent);
            marketChart.Draw(
                index,
                line,
                MarketOverview.IndexText(StockDetail.High(index), inMoney),
                MarketOverview.IndexText(StockDetail.Low(index), inMoney),
                days
            );

            // Ranked as the pills read: by % or, in points, by the change in ₡.
            List<MonitorQuote> up = MarketOverview.Movers(quotes, true, Movers, inPoints);
            List<MonitorQuote> down = MarketOverview.Movers(quotes, false, Movers, inPoints);
            for (int i = 0; i < Movers; i++)
            {
                gainers[i].Show(i < up.Count ? up[i] : null, inPoints);
                losers[i].Show(i < down.Count ? down[i] : null, inPoints);
            }

            noGainers.isVisible = up.Count == 0;
            noLosers.isVisible = down.Count == 0;
            breadth.text = MarketOverview.Breadth(quotes);
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

        /// <summary>One mover: symbol, price, change pill; a click opens its dialog.</summary>
        private sealed class Mover
        {
            private readonly UIPanel back;
            private readonly UILabel symbol;
            private readonly UILabel price;
            private readonly UILabel change;
            private readonly float width;
            private MonitorQuote quote;

            public Mover(
                UIComponent parent,
                string name,
                float x,
                float y,
                float width,
                Action toggle
            )
            {
                this.width = width;
                back = FoRow.Add(
                    parent,
                    name,
                    new Vector2(width, MoverH - 3f),
                    new Vector3(x, y),
                    p => InvestmentsOverview.OpenDialog(quote)
                );
                symbol = FoText.Label(back, name + "Symbol", "", 0.85f, FoTheme.GameText);
                symbol.relativePosition = new Vector3(8f, 4f);
                symbol.isInteractive = false;
                price = FoText.Label(back, name + "Price", "", 0.8f, FoTheme.GameValue);
                price.isInteractive = false;
                // A click on the pill flips every pill between % and ₡ (not the dialog).
                change = FoPill.AddToggle(
                    back,
                    name + "Change",
                    new Vector2(62f, 17f),
                    0.7f,
                    FoTheme.Header,
                    Strings.Get("monitor.pill.tip"),
                    toggle
                );
                change.relativePosition = new Vector3(width - 62f - 4f, 3f);
            }

            public void Show(MonitorQuote q, bool inPoints)
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
                change.text = StockMonitor.PillText(q.Price, q.ChangeRate, inPoints);
                change.color = FoTheme.Change(StockMonitor.ChangeColour(q.ChangeRate));
            }
        }
    }
}

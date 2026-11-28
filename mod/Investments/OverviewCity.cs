using System; // Action
using System.Collections.Generic; // List
using System.Text; // StringBuilder
using ColossalFramework.UI; // UIComponent, UILabel, UIPanel, UIScrollablePanel
// FinanceOverhaul.Core: CityOverview, CityRow, HeatmapOverview, MonitorQuote, StockDetail,
//   StockMonitor, Strings
using FinanceOverhaul.Core;
using UnityEngine; // Vector2, Vector3

namespace FinanceOverhaul
{
    /// <summary>
    /// The City view: each stock beside the city figure the game reads to price it (oil
    /// productivity, the crime rate, the vehicles on the road…), what moves it, and its change
    /// over 7 or 30 days. The rows scroll with the mouse wheel; a click opens the stock's
    /// dialog. Figures are read by <see cref="CityFigures"/>, worded by <see cref="CityOverview"/>.
    /// </summary>
    internal sealed class OverviewCity : IOverviewView
    {
        private const string Name = InvestmentsOverview.Name;
        private const float Pad = InvestmentsOverview.Pad;
        private const int ChartScale = InvestmentsOverview.ChartScale;

        private readonly InvestmentsOverview host;
        private List<MonitorQuote> quotes = new List<MonitorQuote>();

        public OverviewCity(InvestmentsOverview host)
        {
            this.host = host;
            BuildCity();
        }

        public UIPanel Panel
        {
            get { return city; }
        }

        private const float CityRowH = 52f;

        private UIPanel city;
        private FoToggleTabs cityRange;
        private UIScrollablePanel cityList;
        private readonly List<CityRowView> cityRows = new List<CityRowView>();

        private void BuildCity()
        {
            city = host.NewView("City");
            UILabel heading = InvestmentsOverview.Heading(
                city,
                Strings.Get("overview.city.heading"),
                Pad,
                Pad + 2f
            );
            heading.name = Name + "CityHeading";
            heading.tooltip = Strings.Get("overview.city.tip");
            cityRange = host.RangeTabs(city, Name + "CityRange", city.width - Pad, Pad);
            cityList = InvestmentsOverview.Scroller(city, Name + "CityRows", Pad + 30f);
        }

        /// <summary>What else, besides prices and orders, makes this view draw again.</summary>
        public void Sign(StringBuilder sig) { }

        public void Draw(OverviewFrame frame)
        {
            quotes = frame.Quotes;
            int days = frame.Days;
            cityRange.Choose(days == StockDetail.ShortRange ? 0 : 1);
            CityFigures.Fill(quotes);
            // In the list's order: its sort and direction (Change by ₡ with the pills in points).
            List<CityRow> rows = CityOverview.Rows(
                quotes,
                days,
                frame.Sort,
                frame.Reversed,
                frame.InPoints
            );
            bool inPoints = frame.InPoints;
            while (cityRows.Count < rows.Count)
            {
                cityRows.Add(new CityRowView(cityList, cityRows.Count, host.TogglePills));
            }

            // A scrolled list keeps its offset in its rows' places: lay them out from it, or a
            // redraw (every new price) would put them back unscrolled and they drift away.
            float top = -cityList.scrollPosition.y;
            for (int i = 0; i < cityRows.Count; i++)
            {
                if (i < rows.Count)
                {
                    cityRows[i].Show(rows[i], top + i * CityRowH, days, inPoints);
                }
                else
                {
                    cityRows[i].Hide();
                }
            }
        }

        /// <summary>One stock: symbol and what drives it, the figure, the rule, its change.</summary>
        private sealed class CityRowView
        {
            private readonly UIPanel back;
            private readonly UILabel symbol;
            private readonly UILabel driver;
            private readonly UILabel figure;
            private readonly UILabel rule;
            private readonly UILabel change;
            private MonitorQuote quote;

            public CityRowView(UIComponent parent, int index, Action toggle)
            {
                string name = Name + "CityRow-" + index;
                float width = parent.width;
                back = FoRow.Add(
                    parent,
                    name,
                    new Vector2(width, CityRowH - 4f),
                    Vector3.zero,
                    p => InvestmentsOverview.OpenDialog(quote)
                );
                symbol = FoText.Label(back, name + "Symbol", "", 0.85f, FoTheme.GameText);
                symbol.relativePosition = new Vector3(8f, 4f);
                symbol.isInteractive = false;
                driver = FoText.Label(back, name + "Driver", "", 0.7f, FoTheme.TextMuted);
                driver.isInteractive = false;
                figure = FoText.Label(back, name + "Figure", "", 0.72f, FoTheme.GameValue);
                figure.relativePosition = new Vector3(8f, 22f);
                figure.isInteractive = false;
                rule = FoText.Label(back, name + "Rule", "", 0.65f, FoTheme.TextLabel);
                rule.relativePosition = new Vector3(8f, 35f);
                rule.isInteractive = false;
                change = FoPill.AddToggle(
                    back,
                    name + "Change",
                    new Vector2(62f, 17f),
                    0.7f,
                    FoTheme.Header,
                    Strings.Get("monitor.pill.tip"),
                    toggle
                );
                change.relativePosition = new Vector3(width - 62f - 6f, 4f);
            }

            public void Show(CityRow row, float y, int days, bool inPoints)
            {
                quote = row.Quote;
                back.isVisible = true;
                back.relativePosition = new Vector3(0f, y);
                back.tooltip = row.Quote.Name;
                symbol.text = row.Quote.Symbol;
                driver.text = row.Name;
                driver.relativePosition = new Vector3(8f + symbol.width + 8f, 6f);
                float room = back.width - 16f;
                FoText.Fit(figure, row.Figure, 0.72f, 0.6f, room, 13f);
                FoText.Fit(rule, row.Rule, 0.65f, 0.55f, room - 4f, 12f);
                change.text =
                    row.Quote.Locked ? Strings.Get("overview.heatmap.locked")
                    : inPoints
                        ? HeatmapOverview.MoneyChange(HeatmapOverview.Amount(row.Quote, days))
                    : StockMonitor.ChangeText(row.Change);
                change.color = FoTheme.Change(StockMonitor.ChangeColour(row.Change));
                Ink(row.Quote.Locked);
            }

            private void Ink(bool locked)
            {
                symbol.textColor = locked ? FoTheme.TextMuted : FoTheme.GameText;
                figure.textColor = locked ? FoTheme.TextMuted : FoTheme.GameValue;
            }

            public void Hide()
            {
                back.isVisible = false;
                quote = null;
            }
        }
    }
}

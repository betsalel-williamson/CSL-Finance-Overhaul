using System.Collections.Generic; // List
using ColossalFramework.UI; // UIButton, UIComponent, UILabel, UIPanel, UITextureSprite
// FinanceOverhaul.Core: MonitorQuote, PortfolioOverview, PortfolioSlice, StockDetail, StockMonitor,
//   Strings, TradeOrder
using FinanceOverhaul.Core;
using UnityEngine; // Color32, Mathf, Texture2D, Vector2, Vector3

namespace FinanceOverhaul
{
    /// <summary>
    /// The Portfolio view: what the city's shares are worth, today's change and the gain since
    /// they were bought, a chart of what today's holdings were worth each day, the holdings by
    /// value as a donut with its legend, and the open orders with Cancel.
    /// </summary>
    internal sealed partial class InvestmentsOverview
    {
        private const float PortfolioChartH = 100f;
        private const int Slices = 5;
        private const int OrderRows = 4;
        private const float OrderLineH = 12f;
        private const float DonutSize = 96f;

        private UIPanel portfolio;
        private UILabel worth;
        private FoStatGrid worthStats;
        private FoToggleTabs portfolioRange;
        private ChartBox portfolioChart;
        private UITextureSprite donut;
        private Texture2D donutTexture;
        private readonly LegendRow[] legend = new LegendRow[Slices];
        private UILabel noHoldings;
        private readonly OrderRow[] orderRows = new OrderRow[OrderRows];
        private UILabel noOrders;
        private UILabel moreOrders;
        private float ordersTop;

        /// <summary>Orders the view shows instead of the city's (game UI tests: the longest text).</summary>
        internal static List<TradeOrder> TestOrders;

        private void BuildPortfolio()
        {
            portfolio = View("Portfolio");
            float w = body.width - 2f * Pad;

            FoText
                .Label(
                    portfolio,
                    Name + "PortfolioValueKey",
                    Strings.Get("overview.portfolio.value"),
                    0.75f,
                    FoTheme.GameDialogBody
                )
                .relativePosition = new Vector3(Pad, Pad);
            worth = FoText.Label(portfolio, Name + "PortfolioValue", "", 1.4f, FoTheme.GameText);
            worth.relativePosition = new Vector3(Pad, Pad + 16f);
            portfolioRange = RangeTabs(
                portfolio,
                Name + "PortfolioRange",
                portfolio.width - Pad,
                Pad + 4f
            );
            worthStats = FoStatGrid.Add(
                portfolio,
                Name + "PortfolioStat",
                w,
                2,
                1,
                22f,
                0.75f,
                0.8f,
                FoTheme.GameDialogBody,
                FoTheme.Divider
            );
            worthStats.Panel.relativePosition = new Vector3(Pad, Pad + 46f);

            float chartY = Pad + 46f + 2f * 22f + 8f;
            portfolioChart = new ChartBox(
                portfolio,
                Name + "Portfolio",
                Pad,
                chartY,
                w,
                PortfolioChartH,
                textures
            );

            float lowerY = chartY + PortfolioChartH + 26f;
            float half = (w - Pad) / 2f;
            Heading(portfolio, Strings.Get("overview.portfolio.holdings"), Pad, lowerY);
            Heading(portfolio, Strings.Get("overview.portfolio.orders"), Pad + half + Pad, lowerY);

            donutTexture = FoTexture.Canvas(
                (int)DonutSize * ChartScale,
                (int)DonutSize * ChartScale
            );
            textures.Add(donutTexture);
            donut = portfolio.AddUIComponent<UITextureSprite>();
            donut.name = Name + "PortfolioDonut";
            donut.texture = donutTexture;
            donut.size = new Vector2(DonutSize, DonutSize);
            donut.relativePosition = new Vector3(Pad, lowerY + 22f);
            donut.isInteractive = false;
            float legendX = Pad + DonutSize + 10f;
            for (int i = 0; i < Slices; i++)
            {
                legend[i] = new LegendRow(
                    portfolio,
                    Name + "PortfolioSlice-" + i,
                    legendX,
                    lowerY + 24f + i * 18f,
                    Pad + half - legendX
                );
            }

            noHoldings = FoText.Label(
                portfolio,
                Name + "PortfolioNone",
                Strings.Get("overview.portfolio.none"),
                0.75f,
                FoTheme.TextMuted
            );
            noHoldings.autoSize = false;
            noHoldings.wordWrap = true;
            noHoldings.size = new Vector2(half, 48f);
            noHoldings.relativePosition = new Vector3(Pad, lowerY + 22f);

            float ordersX = Pad + half + Pad;
            ordersTop = lowerY + 20f;
            for (int i = 0; i < OrderRows; i++)
            {
                orderRows[i] = new OrderRow(
                    portfolio,
                    Name + "PortfolioOrder-" + i,
                    ordersX,
                    lowerY + 20f,
                    half
                );
            }

            noOrders = FoText.Label(
                portfolio,
                Name + "PortfolioNoOrders",
                Strings.Get("overview.portfolio.noOrders"),
                0.75f,
                FoTheme.TextMuted
            );
            noOrders.relativePosition = new Vector3(ordersX, lowerY + 22f);
            moreOrders = FoText.Label(
                portfolio,
                Name + "PortfolioMoreOrders",
                "",
                0.7f,
                FoTheme.TextMuted
            );
            moreOrders.relativePosition = new Vector3(ordersX, ordersTop);
        }

        private void DrawPortfolio(int days)
        {
            portfolioRange.Choose(days == StockDetail.ShortRange ? 0 : 1);
            double value = PortfolioOverview.Value(quotes);
            worth.text = PortfolioOverview.Money(value);
            double today = PortfolioOverview.DayChange(quotes);
            double gain = PortfolioOverview.Gain(quotes);
            worthStats.Set(
                0,
                0,
                Strings.Get("overview.portfolio.today"),
                PortfolioOverview.DayChangeText(quotes),
                Signed(today)
            );
            worthStats.Set(
                1,
                0,
                Strings.Get("overview.portfolio.gain"),
                PortfolioOverview.GainText(quotes),
                Signed(gain)
            );

            float[] history = PortfolioOverview.WorthHistory(quotes, days);
            int trend = StockDetail.Trend(history);
            Color32 line = FoTheme.Trend(trend, FoTheme.Accent);
            portfolioChart.Draw(
                history,
                line,
                history.Length == 0 ? "--" : StockMonitor.PriceText(StockDetail.High(history)),
                history.Length == 0 ? "--" : StockMonitor.PriceText(StockDetail.Low(history)),
                days
            );

            // The donut: the largest holdings by name, the rest as Other.
            List<PortfolioSlice> slices = PortfolioOverview.Allocation(quotes, Slices);
            var shares = new float[slices.Count];
            var colours = new Color32[Mathf.Max(1, slices.Count)];
            for (int i = 0; i < Slices; i++)
            {
                if (i < slices.Count)
                {
                    shares[i] = slices[i].Share;
                    colours[i] = slices[i].Other
                        ? FoTheme.SeriesOther
                        : FoTheme.Series[i % FoTheme.Series.Length];
                    legend[i].Show(slices[i], colours[i]);
                }
                else
                {
                    legend[i].Hide();
                }
            }

            donut.isVisible = slices.Count > 0;
            noHoldings.isVisible = slices.Count == 0;
            if (slices.Count > 0)
            {
                FoDonut.Draw(donutTexture, shares, colours, 0.58f);
            }

            // Open orders, each with its stock and Cancel, its terms wrapped under them; as many
            // as fit above the bottom edge, then "+N more in the list".
            var orders =
                TestOrders != null
                    ? new List<TradeOrder>(TestOrders)
                    : new List<TradeOrder>(OrderDesk.Snapshot());
            float y = ordersTop;
            float bottom = portfolio.height - Pad - 16f;
            int shown = 0;
            for (int i = 0; i < OrderRows; i++)
            {
                if (i < orders.Count)
                {
                    float h = orderRows[i].Show(orders[i], SymbolOf(orders[i]), OrderDesk.Today, y);
                    if (y + h > bottom)
                    {
                        orderRows[i].Hide();
                        continue;
                    }

                    y += h + 4f;
                    shown++;
                }
                else
                {
                    orderRows[i].Hide();
                }
            }

            for (int i = shown; i < OrderRows; i++)
            {
                orderRows[i].Hide();
            }

            noOrders.isVisible = orders.Count == 0;
            moreOrders.isVisible = orders.Count > shown;
            moreOrders.text = Strings.Format(
                "overview.portfolio.moreOrders",
                (orders.Count - shown).ToString()
            );
            moreOrders.relativePosition = new Vector3(moreOrders.relativePosition.x, y);
        }

        private static Color32 Signed(double amount)
        {
            return FoTheme.Trend(
                System.Math.Sign((long)System.Math.Round(amount)),
                FoTheme.GameValue
            );
        }

        private string SymbolOf(TradeOrder order)
        {
            foreach (MonitorQuote q in quotes)
            {
                if (q.Prefab == order.InvestmentKey)
                {
                    return q.Symbol;
                }
            }

            return order.InvestmentKey;
        }

        /// <summary>One line of the donut's legend: colour, symbol, value and share.</summary>
        private sealed class LegendRow
        {
            private readonly UIPanel swatch;
            private readonly UILabel label;
            private readonly UILabel value;
            private readonly float width;

            public LegendRow(UIComponent parent, string name, float x, float y, float width)
            {
                this.width = width;
                swatch = parent.AddUIComponent<UIPanel>();
                swatch.name = name + "Swatch";
                swatch.backgroundSprite = "EmptySprite"; // not shown: a game sprite name
                swatch.size = new Vector2(9f, 9f);
                swatch.relativePosition = new Vector3(x, y + 3f);
                swatch.isInteractive = false;
                label = FoText.Label(parent, name, "", 0.72f, FoTheme.GameText);
                label.relativePosition = new Vector3(x + 15f, y);
                value = FoText.Label(parent, name + "Value", "", 0.72f, FoTheme.GameValue);
                value.relativePosition = new Vector3(x, y);
                this.x = x;
                this.y = y;
            }

            private readonly float x;
            private readonly float y;

            public void Show(PortfolioSlice slice, Color32 colour)
            {
                swatch.color = colour;
                label.text = slice.Label;
                value.text = PortfolioOverview.Money(slice.Value);
                value.relativePosition = new Vector3(x + width - value.width, y);
                swatch.isVisible = true;
                label.isVisible = true;
                value.isVisible = true;
            }

            public void Hide()
            {
                swatch.isVisible = false;
                label.isVisible = false;
                value.isVisible = false;
            }
        }

        /// <summary>One open order: its stock and Cancel, the order under them.</summary>
        private sealed class OrderRow
        {
            private readonly UIPanel back;
            private readonly UILabel symbol;
            private readonly UILabel side;
            private readonly UILabel text;
            private readonly float width;
            private int orderId;

            public OrderRow(UIComponent parent, string name, float x, float y, float width)
            {
                this.width = width;
                back = FoRow.Add(parent, name, new Vector2(width, 36f), new Vector3(x, y), null);
                symbol = FoText.Label(back, name + "Symbol", "", 0.8f, FoTheme.GameText);
                symbol.relativePosition = new Vector3(8f, 3f);
                side = FoText.Label(back, name + "Side", "", 0.7f, FoTheme.Accent);
                text = FoText.Label(back, name + "Text", "", 0.65f, FoTheme.TextLabel);
                text.relativePosition = new Vector3(8f, 20f);
                UIButton cancel = FoButton.Add(
                    back,
                    name + "Cancel",
                    Strings.Get("order.card.cancel"),
                    new Vector2(64f, 18f),
                    0.65f
                );
                cancel.relativePosition = new Vector3(width - 64f - 4f, 2f);
                cancel.eventClick += (c, p) => OrderDesk.Cancel(orderId);
            }

            /// <summary>Fills the row at <paramref name="y"/>; returns its height.</summary>
            public float Show(TradeOrder order, string stock, int today, float y)
            {
                orderId = order.Id;
                back.isVisible = true;
                back.relativePosition = new Vector3(back.relativePosition.x, y);
                symbol.text = stock;
                // The stock and Cancel on the first line, the side on its own under them (it
                // can be as long as "SELL (trailing stop)"), then the terms, wrapped onto as
                // many lines as they need.
                float sideH = FoText.Fit(
                    side,
                    PortfolioOverview.OrderSide(order),
                    0.7f,
                    0.6f,
                    width - 16f,
                    OrderLineH + 1f
                );
                side.relativePosition = new Vector3(8f, 21f);
                float textY = 21f + sideH + 1f;
                float h = FoText.Fit(
                    text,
                    PortfolioOverview.OrderDetail(order, today),
                    0.65f,
                    0.6f,
                    width - 16f,
                    OrderLineH
                );
                text.relativePosition = new Vector3(8f, textY);
                back.height = textY + h + 5f;
                back.tooltip = side.text + " · " + text.text; // not shown: joins two shown strings for a tooltip
                return back.height;
            }

            public void Hide()
            {
                back.isVisible = false;
            }
        }
    }
}

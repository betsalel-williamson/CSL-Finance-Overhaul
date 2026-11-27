using System.Collections.Generic; // List
using ColossalFramework.UI; // UIComponent, UILabel, UIPanel
using FinanceOverhaul.Core; // CostBasis, OrderRules, StockDetail, StockMonitor, Strings, TradeOrder
using UnityEngine; // Color32, RectOffset, Vector3

// Game classes used without a using (global namespace, Assembly-CSharp): EconomyManager,
//   Investment, InvestmentAI, InvestmentInfo

namespace FinanceOverhaul
{
    /// <summary>
    /// What the Stock Monitor's detail view showed, in the Invest dialog itself (Figma:
    /// FO/InvestDialog): 7D | 30D tabs on the chart, the change since yesterday beside the
    /// current value, and a block of stats between the description and the trade (yesterday,
    /// high and low over the range shown, what the city owns, its average cost, the value it
    /// holds, its gain or loss, and its open orders). Refreshed from the dialog's own frame
    /// update, only when a number changed.
    /// </summary>
    public static partial class OrderTicket
    {
        private const string InfoName = "FinanceOverhaulOrderInfo";
        private const string ChangeName = "FinanceOverhaulOrderChange";
        private const string RangeName = "FinanceOverhaulOrderRange";
        private const float InfoRowHeight = 24f;
        private const int InfoRows = 3;

        private static UIPanel infoPanel;
        private static FoStatGrid infoGrid;
        private static UILabel changeLabel;
        private static FoToggleTabs rangeTabs;
        private static string infoSignature;
        private static float infoHeight;

        private static void BuildInfo(UIComponent root)
        {
            UIComponent layout = root.Find("VerticalLayoutPanel");
            UIComponent info = root.Find("PanelInvestmentInfo");
            UIComponent graph = root.Find("GraphParent");
            if (layout == null || info == null || dark == null)
            {
                LogOnce("order ticket: dialog info layout not recognised; no stats");
                return;
            }

            UiCleanup.RemoveChildrenNamed(layout, InfoName);
            infoPanel = layout.AddUIComponent<UIPanel>();
            infoPanel.name = InfoName;
            infoPanel.width = layout.width;
            infoPanel.isInteractive = false;
            infoGrid = FoStatGrid.Add(
                infoPanel,
                InfoName + "Stat",
                layout.width - 14f,
                InfoRows,
                3,
                InfoRowHeight,
                0.75f,
                0.8f,
                FoTheme.GameDialogBody,
                FoTheme.Divider
            );
            infoGrid.Panel.relativePosition = new Vector3(7f, 0f);
            infoHeight = InfoRows * InfoRowHeight + 6f;
            infoPanel.height = infoHeight;
            // Between the description and the trade (the layout stacks by order).
            infoPanel.zOrder = info.zOrder + 1;
            GrowDialog(infoHeight);

            // The change since yesterday, after "Current value: ₡X / share".
            UIComponent row = info.Find("LayoutPanelCurrentValue");
            UILabel unit = row == null ? null : row.Find<UILabel>("LabelUnit");
            if (row != null && unit != null)
            {
                UiCleanup.RemoveChildrenNamed(row, ChangeName);
                changeLabel = row.AddUIComponent<UILabel>();
                changeLabel.name = ChangeName;
                CopyLabelStyle(unit, changeLabel);
                changeLabel.padding = new RectOffset(10, 0, 0, 0);
            }

            // 7D | 30D on the chart, as in the Stock Monitor.
            if (graph != null)
            {
                UiCleanup.RemoveChildrenNamed(graph, RangeName);
                rangeTabs = FoToggleTabs.Add(
                    graph,
                    RangeName,
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
                        ShareChartOverlay.DialogDays =
                            i == 0 ? StockDetail.ShortRange : StockDetail.LongRange;
                        infoSignature = null;
                        if (owner != null)
                        {
                            ShareChartOverlay.OnDialogBindings(owner);
                        }
                    }
                );
                rangeTabs.Secondary();
                rangeTabs[0].name = RangeName + "-" + StockDetail.ShortRange;
                rangeTabs[1].name = RangeName + "-" + StockDetail.LongRange;
                rangeTabs.Panel.relativePosition = new Vector3(12f, 6f);
            }

            infoSignature = null;
        }

        /// <summary>Called every frame while the dialog is open: redraws only on a change.</summary>
        private static void RefreshInfo()
        {
            if (owner == null || infoGrid == null)
            {
                return;
            }

            byte id = (byte)IdField.GetValue(owner);
            Investment[] buffer = EconomyManager.instance.m_investments.m_buffer;
            InvestmentInfo info = buffer[id].Info;
            InvestmentAI ai = info == null ? null : info.m_investmentAI;
            if (ai == null)
            {
                return;
            }

            float price = buffer[id].value;
            uint owned = ai.OwnedCount(ref buffer[id]);
            long invested = buffer[id].m_playerSpentMoney;
            int days = ShareChartOverlay.DialogDays;
            var orders = new List<TradeOrder>();
            foreach (TradeOrder o in OrderDesk.Snapshot())
            {
                if (o.InvestmentKey == info.name)
                {
                    orders.Add(o);
                }
            }

            string signature =
                id
                + "|"
                + days
                + "|"
                + OrderDesk.Today
                + "|"
                + price.ToString("0.##")
                + "|"
                + owned
                + "|"
                + invested
                + "|"
                + orders.Count;
            if (signature == infoSignature)
            {
                return;
            }

            infoSignature = signature;
            if (rangeTabs != null)
            {
                rangeTabs.Choose(days == StockDetail.ShortRange ? 0 : 1);
            }

            var history = new float[Investment.kPastValueCount];
            buffer[id].FillGraphValues(history);
            history = CompanyListing.ChartDays(ai, history);
            float[] range = StockDetail.Range(history, days);
            float? yesterday = StockDetail.Yesterday(history);

            if (changeLabel != null)
            {
                // One rule everywhere: the game's change rate (list, pills, Economy panel, LED).
                float rate = ai.CalculateChangeRate(
                    id,
                    ref buffer[id],
                    TickerFeed.PanelChangeRateWindow
                );
                changeLabel.text = StockDetail.ChangeHeader(price, rate);
                changeLabel.textColor = FoTheme.Change(StockMonitor.ChangeColour(rate));
            }

            Color32 text = FoTheme.GameText;
            infoGrid.Set(
                0,
                0,
                Strings.Get("detail.yesterday"),
                yesterday.HasValue ? StockMonitor.PriceText(yesterday.Value) : "--",
                text
            );
            infoGrid.Set(
                0,
                1,
                StockDetail.HighLabel(days),
                StockMonitor.PriceText(StockDetail.High(range)),
                text
            );
            infoGrid.Set(
                0,
                2,
                StockDetail.LowLabel(days),
                StockMonitor.PriceText(StockDetail.Low(range)),
                text
            );
            infoGrid.Set(1, 0, Strings.Get("detail.youOwn"), StockDetail.SharesText(owned), text);
            infoGrid.Set(
                1,
                1,
                Strings.Get("detail.avgCost"),
                CostBasis.FormatAverage(
                    invested,
                    owned,
                    ColossalFramework.Globalization.LocaleManager.cultureInfo
                ) ?? "--",
                text
            );
            infoGrid.Set(
                1,
                2,
                Strings.Get("order.info.held"),
                owned > 0 ? StockMonitor.PriceText(OrderRules.Price(price, owned)) : "—",
                text
            );
            long gain = StockDetail.GainLoss(invested, owned, price);
            infoGrid.Set(
                2,
                0,
                StockDetail.GainLossLabel(invested, owned, price),
                StockDetail.GainLossText(invested, owned, price),
                FoTheme.Trend(System.Math.Sign(gain), text)
            );
            infoGrid.Set(
                2,
                1,
                Strings.Get("detail.openOrders"),
                StockDetail.OrdersSummary(orders),
                orders.Count > 0 ? FoTheme.TickerSymbol : FoTheme.GameDialogBody,
                2
            );
        }

        /// <summary>The whole dialog and its stacking panel grow (the trade section does not).</summary>
        private static void GrowDialog(float delta)
        {
            if (owner == null || delta == 0f)
            {
                return;
            }

            UIComponent layout = owner.component.Find("VerticalLayoutPanel");
            if (layout != null)
            {
                layout.height += delta;
            }

            owner.component.height += delta;
        }

        private static void DetachInfo()
        {
            if (infoPanel != null)
            {
                GrowDialog(-infoHeight);
            }

            Destroy(infoPanel);
            Destroy(changeLabel);
            if (rangeTabs != null)
            {
                Destroy(rangeTabs.Panel);
            }

            infoPanel = null;
            infoGrid = null;
            changeLabel = null;
            rangeTabs = null;
            infoSignature = null;
            infoHeight = 0f;
        }
    }
}

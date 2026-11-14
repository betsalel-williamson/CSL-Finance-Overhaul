#if HAS_CITIES
using System;
using System.Collections.Generic;
using ColossalFramework.UI;
using FinanceOverhaul.Market;
using UnityEngine;

namespace FinanceOverhaul
{
    /// <summary>
    /// Stock Monitor detail view (Figma band 10, pins 8–10): opened by clicking a row, shown in
    /// the same window in place of the list. Header with price and change vs yesterday, 7D / 30D
    /// chart (the game keeps 30 daily prices), stats like Your Portfolio, and Buy / Sell, which
    /// open the game's own Invest dialog (with the order tabs).
    /// </summary>
    public sealed partial class StockMonitorHost
    {
        private const float DetailHeight = 466f;
        private const float ChartX = 12f,
            ChartY = 116f,
            ChartW = 286f,
            ChartH = 150f;
        private const int ChartScale = 2;
        private const int AxisCount = 4;

        private int detailId = -1;
        private int detailDays = StockDetail.LongRange;
        private float[] dRangeShown = new float[0];
        private string dSymbolShown;
        private string detailSignature;
        private UIPanel detail;
        private UILabel dSymbol,
            dName,
            dPrice,
            dChange,
            dYesterday,
            dFromLabel,
            dToLabel;
        private UIButton dRange7,
            dRange30,
            dBuy,
            dSell;
        private UITextureSprite dChart;
        private Texture2D dTexture;
        private readonly List<UILabel> dAxis = new List<UILabel>();
        private readonly List<UILabel[]> dStats = new List<UILabel[]>();

        private static readonly Color32 Accent = new Color32(101, 214, 248, 255);

        private void ShowDetail(int id)
        {
            detailId = id;
            detailSignature = null;
            if (detail == null)
            {
                BuildDetail();
            }

            SetListVisible(false);
            detail.isVisible = true;
            RefreshDetail(true);
        }

        private void ShowList()
        {
            detailId = -1;
            if (detail != null)
            {
                detail.isVisible = false;
            }

            SetListVisible(true);
            Refresh(true);
        }

        private void SetListVisible(bool on)
        {
            list.isVisible = on;
            footer.isVisible = on;
            sortLabel.isVisible = on;
            empty.isVisible = on && !string.IsNullOrEmpty(empty.text);
            foreach (UIButton tab in filterTabs.Values)
            {
                tab.isVisible = on;
            }
        }

        private void BuildDetail()
        {
            detail = window.AddUIComponent<UIPanel>();
            detail.name = Prefix + "Detail";
            detail.size = new Vector2(Width, DetailHeight);
            detail.relativePosition = new Vector3(0f, TitleHeight);

            UILabel back = Label(detail, Prefix + "Back", "« All stocks", 0.75f, Accent);
            back.relativePosition = new Vector3(12f, 2f);
            back.isInteractive = true;
            back.eventClick += (c, p) => ShowList();

            dSymbol = Label(detail, Prefix + "DetailSymbol", "", 1.8f, White);
            dSymbol.relativePosition = new Vector3(12f, 20f);
            dName = Label(detail, Prefix + "DetailName", "", 0.8f, Muted);

            dPrice = RightLabel(Prefix + "DetailPrice", 1.25f, White, 22f);
            dChange = RightLabel(Prefix + "DetailChange", 0.8f, Gain, 50f);
            dYesterday = RightLabel(Prefix + "DetailYesterday", 0.68f, Muted, 68f);

            dRange7 = RangeButton(StockDetail.ShortRange, 12f);
            dRange30 = RangeButton(StockDetail.LongRange, 62f);
            UILabel note = Label(
                detail,
                Prefix + "DetailNote",
                "the game keeps 30 daily prices",
                0.6f,
                Muted
            );
            note.relativePosition = new Vector3(116f, 92f);

            UIPanel chartBack = detail.AddUIComponent<UIPanel>();
            chartBack.backgroundSprite = "EmptySprite";
            chartBack.color = new Color32(0, 0, 0, 255);
            chartBack.opacity = 0.35f;
            chartBack.size = new Vector2(ChartW, ChartH);
            chartBack.relativePosition = new Vector3(ChartX, ChartY);
            chartBack.isInteractive = false;

            dTexture = new Texture2D(
                (int)ChartW * ChartScale,
                (int)ChartH * ChartScale,
                TextureFormat.ARGB32,
                false
            );
            dTexture.filterMode = FilterMode.Bilinear;
            dTexture.wrapMode = TextureWrapMode.Clamp;
            dChart = detail.AddUIComponent<UITextureSprite>();
            dChart.name = Prefix + "DetailChart";
            dChart.size = new Vector2(ChartW, ChartH);
            dChart.relativePosition = new Vector3(ChartX, ChartY);
            dChart.texture = dTexture;

            for (int i = 0; i < AxisCount; i++)
            {
                UILabel axis = Label(detail, Prefix + "DetailAxis-" + i, "", 0.6f, Muted);
                dAxis.Add(axis);
            }

            dFromLabel = Label(detail, Prefix + "DetailFrom", "", 0.6f, Muted);
            dFromLabel.relativePosition = new Vector3(ChartX, ChartY + ChartH + 4f);
            dToLabel = Label(detail, Prefix + "DetailTo", "today", 0.6f, Muted);
            dToLabel.relativePosition = new Vector3(ChartX + ChartW - 28f, ChartY + ChartH + 4f);

            float y = ChartY + ChartH + 26f;
            for (int row = 0; row < 4; row++)
            {
                for (int col = 0; col < 2; col++)
                {
                    float x = 12f + col * 172f;
                    UILabel k = Label(detail, null, "", 0.7f, Muted);
                    k.relativePosition = new Vector3(x, y);
                    UILabel v = Label(
                        detail,
                        Prefix + "DetailStat-" + row + "-" + col,
                        "",
                        0.75f,
                        White
                    );
                    v.autoSize = false;
                    v.size = new Vector2(80f, 16f);
                    v.textAlignment = UIHorizontalAlignment.Right;
                    v.relativePosition = new Vector3(x + 82f, y);
                    UIPanel line = detail.AddUIComponent<UIPanel>();
                    line.backgroundSprite = "EmptySprite";
                    line.color = Divider;
                    line.size = new Vector2(162f, 1f);
                    line.relativePosition = new Vector3(x, y + 19f);
                    line.isInteractive = false;
                    dStats.Add(new[] { k, v });
                }

                y += 24f;
            }

            dBuy = ActionButton(
                Prefix + "DetailBuy",
                "Buy",
                12f,
                y + 12f,
                BuySellInvestmentPanel.Mode.Buy
            );
            dSell = ActionButton(
                Prefix + "DetailSell",
                "Sell",
                188f,
                y + 12f,
                BuySellInvestmentPanel.Mode.Sell
            );
            UILabel hint = Label(
                detail,
                Prefix + "DetailHint",
                "Buy / Sell open the game's Invest dialog (with order tabs)",
                0.6f,
                Muted
            );
            hint.relativePosition = new Vector3(12f, y + 56f);
            detail.isVisible = false;
        }

        private UILabel RightLabel(string name, float scale, Color32 colour, float y)
        {
            UILabel l = Label(detail, name, "", scale, colour);
            l.autoSize = false;
            l.size = new Vector2(170f, 24f * scale);
            l.textAlignment = UIHorizontalAlignment.Right;
            l.relativePosition = new Vector3(Width - 182f, y);
            return l;
        }

        private UIButton RangeButton(int days, float x)
        {
            UIButton b = detail.AddUIComponent<UIButton>();
            b.name = Prefix + "Range-" + days;
            b.text = StockDetail.RangeChip(days);
            b.textScale = 0.75f;
            b.size = new Vector2(44f, 22f);
            b.normalBgSprite = "ButtonMenu";
            b.hoveredBgSprite = "ButtonMenuHovered";
            b.pressedBgSprite = "ButtonMenuPressed";
            b.relativePosition = new Vector3(x, 88f);
            b.eventClick += (c, p) =>
            {
                detailDays = days;
                RefreshDetail(true);
            };
            return b;
        }

        private UIButton ActionButton(
            string name,
            string text,
            float x,
            float y,
            BuySellInvestmentPanel.Mode mode
        )
        {
            UIButton b = detail.AddUIComponent<UIButton>();
            b.name = name;
            b.text = text;
            b.textScale = 0.9f;
            b.size = new Vector2(160f, 32f);
            b.normalBgSprite = "ButtonMenu";
            b.hoveredBgSprite = "ButtonMenuHovered";
            b.pressedBgSprite = "ButtonMenuPressed";
            b.disabledBgSprite = "ButtonMenuDisabled";
            b.relativePosition = new Vector3(x, y);
            b.eventClick += (c, p) =>
            {
                if (detailId >= 0 && b.isEnabled)
                {
                    BuySellInvestmentPanel.ShowPanel((byte)detailId, mode);
                }
            };
            return b;
        }

        private void RefreshDetail(bool force)
        {
            if (detail == null || detailId < 0)
            {
                return;
            }

            Investment[] buffer = EconomyManager.instance.m_investments.m_buffer;
            InvestmentInfo info = detailId < buffer.Length ? buffer[detailId].Info : null;
            InvestmentAI ai = info == null ? null : info.m_investmentAI;
            if (ai == null || (buffer[detailId].m_flags & Investment.Flags.Created) == 0)
            {
                ShowList(); // delisted while open
                return;
            }

            float price = buffer[detailId].value;
            uint owned = ai.OwnedCount(ref buffer[detailId]);
            long invested = buffer[detailId].m_playerSpentMoney;
            var orders = new List<TradeOrder>();
            foreach (TradeOrder o in OrderDesk.Snapshot())
            {
                if (o.InvestmentKey == info.name)
                {
                    orders.Add(o);
                }
            }

            string signature =
                detailId
                + "|"
                + detailDays
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
            if (!force && signature == detailSignature)
            {
                return;
            }

            detailSignature = signature;
            var history = new float[Investment.kPastValueCount];
            buffer[detailId].FillGraphValues(history);
            float[] range = StockDetail.Range(history, detailDays);
            float? yesterday = StockDetail.Yesterday(history);

            string symbol = StockSymbols.For(info);
            dSymbol.text = symbol;
            dName.text = ai.GetLocalizedName();
            dName.relativePosition = new Vector3(12f + dSymbol.width + 10f, 38f);
            dPrice.text = StockMonitor.PriceText(price);
            // One rule everywhere: the game's change rate (list, pills, Economy panel, LED).
            float rate = ai.CalculateChangeRate(
                (byte)detailId,
                ref buffer[detailId],
                TickerFeed.PanelChangeRateWindow
            );
            Color32 colour = ColourOf(StockMonitor.ChangeColour(rate));
            dChange.text = StockDetail.ChangeHeader(price, rate);
            dChange.textColor = colour;
            dYesterday.text = StockDetail.YesterdayLine(yesterday);

            StyleRange(dRange7, detailDays == StockDetail.ShortRange);
            StyleRange(dRange30, detailDays == StockDetail.LongRange);
            dFromLabel.text = range.Length.ToString() + " days ago";
            dRangeShown = range;
            dSymbolShown = symbol;
            DrawChart(range, colour);

            SetStat(0, 0, "Today", StockMonitor.PriceText(price), White);
            SetStat(
                0,
                1,
                StockDetail.HighLabel(detailDays),
                StockMonitor.PriceText(StockDetail.High(range)),
                White
            );
            SetStat(
                1,
                0,
                "Yesterday",
                yesterday.HasValue ? StockMonitor.PriceText(yesterday.Value) : "--",
                White
            );
            SetStat(
                1,
                1,
                StockDetail.LowLabel(detailDays),
                StockMonitor.PriceText(StockDetail.Low(range)),
                White
            );
            SetStat(2, 0, "You own", StockDetail.SharesText(owned), White);
            SetStat(
                2,
                1,
                "Avg cost / share",
                CostBasis.FormatAverage(
                    invested,
                    owned,
                    ColossalFramework.Globalization.LocaleManager.cultureInfo
                ) ?? "--",
                White
            );
            long gain = StockDetail.GainLoss(invested, owned, price);
            SetStat(
                3,
                0,
                StockDetail.GainLossLabel(invested, owned, price),
                StockDetail.GainLossText(invested, owned, price),
                gain < 0 ? Loss
                    : gain > 0 ? Gain
                    : White
            );
            SetStat(
                3,
                1,
                "Open orders",
                StockDetail.OrdersSummary(orders),
                orders.Count > 0 ? Gold : Muted
            );

            dSell.isEnabled = owned > 0;
            dSell.tooltip = owned > 0 ? "" : "The city holds no shares of " + symbol;

            window.height = TitleHeight + DetailHeight;
            background.size = window.size;
            window.absolutePosition = Clamp(window.absolutePosition, window.size);
        }

        private void SetStat(int row, int col, string key, string value, Color32 colour)
        {
            UILabel[] kv = dStats[row * 2 + col];
            kv[0].text = key;
            kv[1].text = value;
            kv[1].textColor = colour;
        }

        private static void StyleRange(UIButton b, bool on)
        {
            b.normalBgSprite = on ? "ButtonMenuPressed" : "ButtonMenu";
            b.focusedBgSprite = b.normalBgSprite;
            b.textColor = on ? (Color32)Color.white : new Color32(190, 198, 206, 255);
        }

        private void DrawChart(float[] range, Color32 colour)
        {
            int w = dTexture.width,
                h = dTexture.height;
            const int thickness = 4;
            byte[] mask = Sparkline.Render(range, w, h, thickness, fillArea: true);
            float hi = StockDetail.High(range),
                lo = StockDetail.Low(range);
            float[] axis = StockDetail.AxisValues(range, AxisCount);
            var grid = new bool[h];
            for (int i = 0; i < AxisCount; i++)
            {
                bool shown = i < axis.Length;
                dAxis[i].isVisible = shown;
                if (!shown)
                {
                    continue;
                }

                int row = Sparkline.RowOf(axis[i], lo, hi, h, thickness);
                grid[Mathf.Clamp(row, 0, h - 1)] = true;
                dAxis[i].text = StockMonitor.PriceText(axis[i]);
                dAxis[i].relativePosition = new Vector3(
                    ChartX + ChartW + 4f,
                    ChartY + row / (float)ChartScale - 6f
                );
            }

            var px = new Color32[w * h];
            var dash = new Color32(colour.r, colour.g, colour.b, 160);
            var gridColour = new Color32(255, 255, 255, 40);
            var clear = new Color32(0, 0, 0, 0);
            for (int y = 0; y < h; y++)
            {
                byte areaAlpha = (byte)(80 * (1f - y / (float)h));
                for (int x = 0; x < w; x++)
                {
                    byte m = mask[y * w + x];
                    Color32 c =
                        m == Sparkline.Line ? colour
                        : m == Sparkline.Baseline ? dash
                        : m == Sparkline.Area ? new Color32(colour.r, colour.g, colour.b, areaAlpha)
                        : clear;
                    if (grid[y] && m != Sparkline.Line)
                    {
                        c = gridColour;
                    }

                    px[(h - 1 - y) * w + x] = c;
                }
            }

            dTexture.SetPixels32(px);
            dTexture.Apply();
        }

        private void DestroyDetail()
        {
            if (dTexture != null)
            {
                Destroy(dTexture);
                dTexture = null;
            }

            dAxis.Clear();
            dStats.Clear();
            detail = null; // destroyed with the window
            detailId = -1;
        }
    }
}
#endif

using System;
using System.Collections.Generic;
using ColossalFramework.UI;
using FinanceOverhaul.Core;
using UnityEngine;

namespace FinanceOverhaul
{
    /// <summary>Stock Monitor: what the list shows (quotes, order groups) and how each row is drawn.</summary>
    public sealed partial class StockMonitorHost
    {
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
            TradeOrder[] orders = OrderDesk.Snapshot();
            int today = OrderDesk.Today;
            string signature = Signature(quotes, orders, today);
            if (!force && signature == shownSignature)
            {
                return;
            }

            shownSignature = signature;
            bool ordersTab = Settings.Filter == MonitorFilter.Orders;
            // Every tab shows how many orders are open on the stocks listed.
            List<MonitorOrderGroup> groups = MonitorOrders.Groups(
                quotes,
                orders,
                today,
                Mod.OrderKindEnabled,
                Settings.Hidden,
                Settings.OrdersSort,
                Settings.OrdersSortReversed
            );
            UIButton ordersTabButton;
            if (filterTabs.TryGetValue(MonitorFilter.Orders, out ordersTabButton))
            {
                ordersTabButton.text = MonitorOrders.TabLabel(MonitorOrders.Count(groups));
            }

            List<MonitorQuote> shown;
            if (ordersTab)
            {
                // Grouped by stock: the groups come already in the tab's own order.
                shown = new List<MonitorQuote>(groups.Count);
                foreach (MonitorOrderGroup g in groups)
                {
                    shown.Add(g.Quote);
                }
            }
            else
            {
                shown = StockMonitor.Rows(
                    quotes,
                    Settings.Filter,
                    Settings.Sort,
                    Settings.Hidden,
                    Settings.SortReversed,
                    Settings.ChangeAsCurrency
                );
            }

            while (rows.Count < shown.Count)
            {
                rows.Add(BuildRow());
            }

            float content = 0f;
            for (int i = 0; i < rows.Count; i++)
            {
                bool used = i < shown.Count;
                rows[i].Panel.isVisible = used;
                if (used)
                {
                    Fill(rows[i], shown[i], ordersTab ? groups[i].Lines : null);
                    content += rows[i].Panel.height;
                }
                else
                {
                    Unname(rows[i]);
                }
            }

            // As tall as its rows, up to ten plain rows; more than that scrolls.
            list.height = Mathf.Clamp(content, RowH, RowH * VisibleRows);
            window.height = TitleHeight + BarHeight + list.height + FooterHeight;
            background.size = window.size;
            footer.relativePosition = new Vector3(12f, TitleHeight + BarHeight + list.height + 7f);
            footer.text = StockMonitor.Footer(SimulationManager.instance.m_currentGameTime);
            empty.text = shown.Count == 0 ? StockMonitor.EmptyText(Settings.Filter) : "";
            empty.isVisible = shown.Count == 0;
            sortLabel.text = ordersTab
                ? MonitorOrders.SortLabel(Settings.OrdersSort)
                : StockMonitor.SortLabel(Settings.Sort, Settings.ChangeAsCurrency);
            bool down = ordersTab
                ? MonitorOrders.IsDescending(Settings.OrdersSortReversed)
                : StockMonitor.IsDescending(Settings.Sort, Settings.SortReversed);
            if (sortArrowDown != down || sortArrow.texture == null)
            {
                if (sortArrow.texture != null)
                {
                    Destroy(sortArrow.texture);
                }

                sortArrow.texture = FoButton.ArrowTexture(down);
                sortArrowDown = down;
            }

            sortDirection.tooltip = ordersTab
                ? MonitorOrders.DirectionText(Settings.OrdersSort, Settings.OrdersSortReversed)
                : StockMonitor.DirectionText(Settings.Sort, Settings.SortReversed);
        }

        private static string Signature(List<MonitorQuote> quotes, TradeOrder[] orders, int today)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(today)
                .Append('|')
                .Append(Settings.Filter)
                .Append('|')
                .Append(Settings.Sort)
                .Append(Settings.SortReversed)
                .Append(Settings.OrdersSort)
                .Append(Settings.OrdersSortReversed);
            // What an order line shows: its trigger (a trailing stop rises) and paused state.
            foreach (TradeOrder o in orders)
            {
                sb.Append('#')
                    .Append(o.Id)
                    .Append(':')
                    .Append(o.TriggerPrice)
                    .Append(Mod.OrderKindEnabled(o.Kind) ? 'a' : 'p');
            }

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

            r.Symbol = FoText.Label(r.Panel, null, "", 1.0f, White);
            r.Symbol.relativePosition = new Vector3(12f, 7f);

            r.Owned = r.Panel.AddUIComponent<UIPanel>();
            r.Owned.backgroundSprite = "EmptySprite";
            r.Owned.color = Cyan;
            r.Owned.size = new Vector2(7f, 7f);
            r.Owned.isInteractive = false;

            r.Orders = FoPill.AddBadge(r.Panel, 0.6f, Gold, GoldBack);

            r.Name = FoText.Label(r.Panel, null, "", 0.7f, Muted);
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

            r.Price = FoText.Label(r.Panel, null, "", 0.9f, White);
            r.Price.autoSize = false;
            r.Price.size = new Vector2(90f, 18f);
            r.Price.textAlignment = UIHorizontalAlignment.Right;
            r.Price.relativePosition = new Vector3(Width - 102f, 6f);

            r.Change = FoPill.AddValue(r.Panel, new Vector2(70f, 20f), 0.8f, PillText);
            r.Change.relativePosition = new Vector3(Width - 82f, 27f);
            foreach (UIComponent child in r.Panel.components)
            {
                child.isInteractive = false; // the whole row is one click target
            }

            // ...except the pill, which flips ₡ / % (Figma pin 6).
            r.Change.isInteractive = true;
            r.Change.tooltip = Strings.Get("monitor.pill.tip");
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

        /// <summary>
        /// A spare row keeps no stock's name: two controls called "Row-Oil" (one hidden) would
        /// make lookups by name, such as the game UI tests', find the wrong one.
        /// </summary>
        private static void Unname(Row r)
        {
            if (r.Id < 0)
            {
                return;
            }

            r.Id = -1;
            r.Sym = null;
            r.Panel.name = Prefix + "SpareRow";
            foreach (UIComponent c in r.Panel.GetComponentsInChildren<UIComponent>(true))
            {
                if (c != r.Panel)
                {
                    c.name = Prefix + "Spare";
                }
            }
        }

        /// <param name="lines">Orders tab: this stock's orders, drawn under its row; else null.</param>
        private static void Fill(Row r, MonitorQuote q, List<MonitorOrderLine> lines)
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
            Color32 colour = FoTheme.Change(StockMonitor.ChangeColour(q.ChangeRate));
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

            // The Orders tab lists the orders themselves, so the count badge is not needed there.
            string badge = lines != null ? null : StockMonitor.OrdersBadge(q.OpenOrders);
            r.Orders.isVisible = badge != null;
            r.Orders.name = Prefix + "Orders-" + q.Symbol;
            if (badge != null)
            {
                r.Orders.text = badge;
                r.Orders.relativePosition = new Vector3(x, 9f);
            }

            FoSparkline.Draw(r.Texture, q.History, colour, SparkScale + 1);
            FillOrderLines(r, lines, h, compact);
        }

        /// <summary>
        /// One line per open order under the stock's row; the row grows to hold them and the
        /// divider moves to its new bottom edge.
        /// </summary>
        private static void FillOrderLines(
            Row r,
            List<MonitorOrderLine> lines,
            float rowHeight,
            bool compact
        )
        {
            int count = lines == null ? 0 : lines.Count;
            while (r.Lines.Count < count)
            {
                r.Lines.Add(BuildOrderLine(r.Panel));
            }

            float y = rowHeight - 3f; // tuck the first line up under the row's own content
            for (int i = 0; i < r.Lines.Count; i++)
            {
                OrderLineUi ui = r.Lines[i];
                ui.Panel.isVisible = i < count;
                if (i >= count)
                {
                    continue;
                }

                ui.Panel.relativePosition = new Vector3(0f, y);
                y += FillOrderLine(ui, lines[i], compact);
            }

            if (count > 0)
            {
                y += 5f;
                r.Panel.height = y;
                r.Line.relativePosition = new Vector3(12f, y - 1f);
            }
        }

        private static OrderLineUi BuildOrderLine(UIPanel row)
        {
            var ui = new OrderLineUi();
            ui.Panel = row.AddUIComponent<UIPanel>();
            ui.Panel.size = new Vector2(Width, OrderLineHeight);
            ui.Panel.isInteractive = false; // the whole row is one click target

            ui.Bar = ui.Panel.AddUIComponent<UIPanel>();
            ui.Bar.backgroundSprite = "EmptySprite";
            ui.Bar.relativePosition = new Vector3(12f, 3f);

            ui.Type = FoText.Label(ui.Panel, null, "", 0.6f, White);
            ui.Distance = FoText.Label(ui.Panel, null, "", 0.6f, Muted);
            ui.Distance.autoSize = true;
            ui.Detail = FoText.Label(ui.Panel, null, "", 0.7f, White);
            foreach (UIComponent c in ui.Panel.components)
            {
                c.isInteractive = false;
            }

            return ui;
        }

        /// <summary>Draws one order line and returns the height it took.</summary>
        private static float FillOrderLine(OrderLineUi ui, MonitorOrderLine line, bool compact)
        {
            // Side colour as on the Your Portfolio cards: the game's gain for a buy, its loss
            // colour for a sell, muted when the order's type is switched off in Options.
            Color32 side =
                line.Paused ? Muted
                : line.IsSell ? FoTheme.GameNegative
                : FoTheme.GamePositive;
            string id = line.OrderId.ToString(System.Globalization.CultureInfo.InvariantCulture);
            ui.Panel.name = Prefix + "OrderLine-" + id;
            ui.Type.name = Prefix + "OrderType-" + id;
            ui.Detail.name = Prefix + "OrderDetail-" + id;
            ui.Distance.name = Prefix + "OrderDistance-" + id;
            ui.Bar.color = side;

            string distance = compact ? line.CompactDistance : line.Distance;
            ui.Distance.isVisible = distance != null;
            float right = Width - 12f;
            float distanceWidth = 0f;
            if (distance != null)
            {
                ui.Distance.text = distance;
                distanceWidth = ui.Distance.width + 8f;
                ui.Distance.relativePosition = new Vector3(right - ui.Distance.width, 3f);
            }

            float height;
            if (compact)
            {
                // One line: the whole order in its short form, in the side colour.
                ui.Type.isVisible = false;
                ui.Detail.textColor = line.Paused ? Muted : White;
                float textHeight = FoText.Fit(
                    ui.Detail,
                    line.Compact,
                    0.65f,
                    OrderTextMinScale,
                    right - OrderLineLeft - distanceWidth,
                    13f
                );
                ui.Detail.relativePosition = new Vector3(OrderLineLeft, 2f);
                height = Mathf.Max(OrderLineCompactHeight, textHeight + 5f);
            }
            else
            {
                // Two tiers: side and type (with the distance at the right), then the wording.
                ui.Type.isVisible = true;
                ui.Type.text = line.Type;
                ui.Type.textColor = side;
                ui.Type.relativePosition = new Vector3(OrderLineLeft, 3f);
                ui.Detail.textColor = line.Paused ? Muted : White;
                float textHeight = FoText.Fit(
                    ui.Detail,
                    line.Detail,
                    0.7f,
                    OrderTextMinScale,
                    right - OrderLineLeft,
                    15f
                );
                ui.Detail.relativePosition = new Vector3(OrderLineLeft, 16f);
                height = Mathf.Max(OrderLineHeight, 16f + textHeight + 3f);
            }

            ui.Panel.height = height;
            ui.Bar.size = new Vector2(2f, height - 6f);
            return height;
        }
    }
}

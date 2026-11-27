using System; // Action, Func, Math
using System.Collections.Generic; // IList
// ColossalFramework.UI: UIButton, UIComponent, UILabel, UIPanel, UISprite, UITextureSprite, UIView
using ColossalFramework.UI;
// FinanceOverhaul.Core: BondText, DialogSide, DialogSides, InvestmentList, InvestmentRow,
//   InvestmentRowKind, MonitorQuote, StockMonitor, Strings
using FinanceOverhaul.Core;
using UnityEngine; // Color32, Mathf, Screen, Texture2D, Vector2, Vector3

// Game classes used without a using (global namespace, Assembly-CSharp): EconomyManager,
//   EconomyPanel, Investment, InvestmentAI, InvestmentTooltip, ToolsModifierControl

namespace FinanceOverhaul
{
    /// <summary>One pooled line of the list.</summary>
    public partial class InvestmentsList
    {
        /// <summary>
        /// One pooled line, renamed for what it shows (a test finds "…Row-GGIBuy" wherever it
        /// scrolled): a section heading, a stock (Figma band 18, Condensed B), or an open order
        /// tucked under its stock.
        /// </summary>
        private sealed class Row
        {
            private const int SparkW = 120,
                SparkH = 28,
                SparkScale = 2;

            // Placed from the line's right edge: the list is the left of two columns.
            private readonly float PriceRight;
            private readonly float SparkX;
            private readonly float LockWidth;

            private readonly UIPanel back;
            private readonly UISprite coin;
            private readonly UILabel symbol;
            private readonly UILabel badge;
            private readonly UILabel name;
            private readonly UILabel holding;
            private readonly UILabel heading;
            private readonly UILabel detail;

            // The 30-day line of the stock shown: that stock's own sprite (the list's), placed
            // over this line; null for a heading, an order or a locked stock.
            private UITextureSprite spark;
            private readonly Func<MonitorQuote, UITextureSprite> sparkSprite;
            private readonly Func<MonitorQuote, Color32, Texture2D> sparkFor;

            /// <summary>The size of a stock's 30-day line texture, and its line's thickness.</summary>
            public const float SparkWidth = SparkW,
                SparkHeight = SparkH;

            public const int SparkTextureWidth = SparkW * SparkScale,
                SparkTextureHeight = SparkH * SparkScale,
                SparkThickness = SparkScale + 1;
            private readonly UILabel price;
            private readonly UILabel change;
            private readonly UILabel gain;
            private readonly UILabel lockLine;
            private readonly UIPanel lockTrack;
            private readonly UIPanel lockFill;
            private readonly UILabel orderLine;
            private readonly UIButton cancel;
            private readonly UIPanel orderRule;
            private readonly float width;
            private string key;
            private string drawn;
            private int slot;
            private int orderId;
            private bool locked;
            private uint available;
            private uint owned;
            private InvestmentRowKind kind;
            private readonly UIPanel[] outline;
            private bool outlined;

            public Row(
                UIComponent parent,
                int index,
                float width,
                Action togglePills,
                Func<MonitorQuote, Color32, Texture2D> sparkFor,
                Func<MonitorQuote, UITextureSprite> sparkSprite
            )
            {
                this.width = width;
                this.sparkFor = sparkFor;
                this.sparkSprite = sparkSprite;
                PriceRight = width - 12f;
                SparkX = PriceRight - 90f - SparkW;
                LockWidth = PriceRight - SparkX;
                back = parent.AddUIComponent<UIPanel>();
                back.name = Prefix + "Pool" + index;
                back.backgroundSprite = "GenericPanelWhite";
                back.size = new Vector2(width, InvestmentList.RowHeight - 4f);
                back.isVisible = false;

                coin = back.AddUIComponent<UISprite>();
                coin.size = new Vector2(36f, 36f);
                coin.relativePosition = new Vector3(8f, 5f);
                symbol = FoText.Label(back, "", "", 1.0f, FoTheme.GameText);
                symbol.relativePosition = new Vector3(54f, 3f);
                badge = FoPill.AddBadge(back, 0.6f, FoTheme.Header, FoTheme.Accent);
                badge.text = Strings.Get("investments.row.local");
                name = FoText.Label(back, "", "", 0.72f, FoTheme.TextMuted);
                name.relativePosition = new Vector3(54f, 24f);
                holding = FoText.Label(back, "", "", 0.72f, FoTheme.TextMuted);
                holding.relativePosition = new Vector3(54f, 40f);
                // A click anywhere on a stock's line (not on its buttons) opens the game's
                // dialog, as Buy does; the line is outlined while the pointer is over it.
                back.eventClick += (c, p) =>
                {
                    if (kind == InvestmentRowKind.Stock)
                    {
                        Open();
                    }
                };
                outline = new UIPanel[4];
                for (int i = 0; i < outline.Length; i++)
                {
                    outline[i] = back.AddUIComponent<UIPanel>();
                    outline[i].backgroundSprite = "EmptySprite";
                    outline[i].color = FoTheme.Accent;
                    outline[i].isInteractive = false;
                    outline[i].isVisible = false;
                }

                heading = FoText.Label(back, "", "", 0.85f, FoTheme.GameText);
                heading.relativePosition = new Vector3(12f, 6f);
                detail = FoText.Label(back, "", "", 0.8f, FoTheme.GameValue);

                price = FoText.Label(back, "", "", 1.0f, FoTheme.GameValue);
                // As in the Stock Monitor: a click on the pill shows every change in ₡ or %
                // (not the line's own click, the dialog).
                change = FoPill.AddToggle(
                    back,
                    null,
                    new Vector2(66f, 18f),
                    0.75f,
                    FoTheme.Header,
                    Strings.Get("monitor.pill.tip"),
                    togglePills
                );
                change.relativePosition = new Vector3(PriceRight - 66f, 25f);
                gain = FoText.Label(back, "", "", 0.72f, FoTheme.GamePositive);

                lockLine = FoText.Label(back, "", "", 0.75f, FoTheme.GameText);
                lockLine.relativePosition = new Vector3(SparkX, 6f);
                lockTrack = back.AddUIComponent<UIPanel>();
                lockTrack.backgroundSprite = "EmptySprite";
                lockTrack.color = FoTheme.BarTrack;
                lockTrack.size = new Vector2(LockWidth, 4f);
                lockTrack.relativePosition = new Vector3(SparkX, 28f);
                lockFill = back.AddUIComponent<UIPanel>();
                lockFill.backgroundSprite = "EmptySprite";
                lockFill.color = FoTheme.OfferGold;
                lockFill.size = new Vector2(0f, 4f);
                lockFill.relativePosition = new Vector3(SparkX, 28f);

                // An order is the lower part of its stock's card: same fill, a thin rule between.
                orderRule = back.AddUIComponent<UIPanel>();
                orderRule.backgroundSprite = "EmptySprite"; // not shown: a game sprite name
                orderRule.color = FoTheme.Divider;
                orderRule.size = new Vector2(width - 54f - 12f, 1f);
                orderRule.relativePosition = new Vector3(54f, 0f);
                orderRule.isInteractive = false;
                orderLine = FoText.Label(back, "", "", 0.72f, FoTheme.Accent);
                orderLine.relativePosition = new Vector3(54f, 4f);
                cancel = FoButton.Add(
                    back,
                    "",
                    Strings.Get("order.card.cancel"),
                    new Vector2(70f, 18f),
                    0.65f
                );
                cancel.relativePosition = new Vector3(width - 82f, 2f);
                cancel.eventClick += (c, p) =>
                {
                    p.Use(); // the line's own click must not open the dialog too
                    OrderDesk.Cancel(orderId);
                };
            }

            public void Destroy()
            {
                // The lines and their textures are the list's, one per stock.
                spark = null;
            }

            /// <summary>The stock's line shown over this pooled line now, or null.</summary>
            public UITextureSprite Spark
            {
                get { return back.isVisible ? spark : null; }
            }

            public string Key
            {
                get { return key; }
            }

            /// <summary>The pointer is over this stock's 30-day line (inside the view).</summary>
            public bool OverLine(Vector2 at, UIComponent view)
            {
                if (!back.isVisible || spark == null || !spark.isVisible || slot <= 0 || locked)
                {
                    return false;
                }

                Vector3 p = spark.absolutePosition;
                Vector3 v = view.absolutePosition;
                return at.x >= p.x
                    && at.x <= p.x + spark.width
                    && at.y >= Mathf.Max(p.y, v.y)
                    && at.y <= Mathf.Min(p.y + spark.height, v.y + view.height);
            }

            /// <summary>The pointer is over this stock's line (inside the view), not a heading or order.</summary>
            public bool OverStock(Vector2 at, UIComponent view)
            {
                if (!back.isVisible || kind != InvestmentRowKind.Stock || locked)
                {
                    return false;
                }

                Vector3 p = back.absolutePosition;
                Vector3 v = view.absolutePosition;
                return at.x >= p.x
                    && at.x <= p.x + back.width
                    && at.y >= Mathf.Max(p.y, v.y)
                    && at.y <= Mathf.Min(p.y + back.height, v.y + view.height);
            }

            /// <summary>A 2-pixel accent outline around the line, or none.</summary>
            public void Outline(bool on)
            {
                if (
                    on == outlined
                    && (!on || (outline[0].width == back.width && outline[2].height == back.height))
                )
                {
                    return;
                }

                outlined = on;
                const float t = 2f;
                float w = back.width,
                    h = back.height;
                outline[0].size = new Vector2(w, t);
                outline[0].relativePosition = Vector3.zero;
                outline[1].size = new Vector2(w, t);
                outline[1].relativePosition = new Vector3(0f, h - t);
                outline[2].size = new Vector2(t, h);
                outline[2].relativePosition = Vector3.zero;
                outline[3].size = new Vector2(t, h);
                outline[3].relativePosition = new Vector3(w - t, 0f);
                foreach (UIPanel edge in outline)
                {
                    edge.isVisible = on;
                    edge.BringToFront();
                }
            }

            /// <summary>
            /// The line's click: Buy, or Sell when nothing is left to buy, else nothing
            /// (<see cref="DialogSides.ForLine"/>); the hover chart goes first.
            /// </summary>
            private void Open()
            {
                DialogSide side = DialogSides.ForLine(slot, locked, available, owned);
                if (side != DialogSide.None)
                {
                    HideChart();
                    TradeDialog.Show(slot, side);
                }
            }

            private static UIComponent Tooltip()
            {
                EconomyPanel economy = ToolsModifierControl.economyPanel;
                return economy == null ? null : economy.component.Find("InvestmentTooltip");
            }

            public void ShowChart()
            {
                UIComponent tip = Tooltip();
                InvestmentTooltip shows =
                    tip == null ? null : tip.GetComponent<InvestmentTooltip>();
                if (shows == null || slot <= 0 || locked || spark == null)
                {
                    return;
                }

                shows.SetInvestment((byte)slot);
                // Beside the line, as the game puts it beside a card; on the left near the edge.
                Vector3 at = spark.absolutePosition;
                float x = at.x + spark.width + 12f;
                UIView view = back.GetUIView();
                float screen = view == null ? Screen.width : view.fixedWidth;
                if (x + tip.width > screen)
                {
                    x = at.x - 12f - tip.width;
                }

                tip.absolutePosition = new Vector3(x, back.absolutePosition.y);
                tip.Show();
                tip.BringToFront();
            }

            public static void HideChart()
            {
                UIComponent tip = Tooltip();
                if (tip != null)
                {
                    tip.Hide();
                }
            }

            public void Show(InvestmentRow row, IList<InvestmentRow> all, float y)
            {
                back.isVisible = row != null;
                if (row == null)
                {
                    return;
                }

                back.relativePosition = new Vector3(Pad, Pad + y);
                if (row.Key != key)
                {
                    key = row.Key;
                    drawn = null;
                    Rename(row);
                }

                kind = row.Kind;
                bool isStock = kind == InvestmentRowKind.Stock;
                bool isOrder = kind == InvestmentRowKind.Order;
                bool isHeading = kind == InvestmentRowKind.Heading;
                // A stock and its orders are one card: no gap until the last of them.
                back.height = row.Height - (row.JoinsNext ? 0f : 4f);
                back.color = isHeading ? FoTheme.Header : FoTheme.SectionFill;
                orderRule.isVisible = isOrder;
                heading.isVisible = isHeading;
                detail.isVisible = isHeading;
                orderLine.isVisible = isOrder;
                cancel.isVisible = isOrder;
                foreach (UIComponent c in new UIComponent[] { coin, symbol, name })
                {
                    c.isVisible = isStock;
                }

                MonitorQuote q = row.Stock;
                slot = q == null ? 0 : q.Id;
                locked = q != null && q.Locked;
                // The stock's own line, moved over this line: a pooled line never shows another
                // stock's line, whatever the game has drawn of it so far.
                spark = isStock && !locked ? sparkSprite(q) : null;
                if (spark != null)
                {
                    spark.relativePosition = back.relativePosition + new Vector3(SparkX, 9f);
                    spark.isVisible = true;
                }
                available = q == null ? 0u : q.Available;
                owned = q == null ? 0u : q.Owned;
                if (!isStock)
                {
                    foreach (
                        UIComponent c in new UIComponent[]
                        {
                            badge,
                            holding,
                            price,
                            change,
                            gain,
                            lockLine,
                            lockTrack,
                            lockFill,
                        }
                    )
                    {
                        c.isVisible = false;
                    }
                }

                if (isHeading)
                {
                    heading.text = InvestmentList.Heading(row.Section);
                    detail.text = InvestmentList.HeadingDetail(row.Section, all);
                    detail.relativePosition = new Vector3(width - 12f - detail.width, 7f);
                    return;
                }

                if (isOrder)
                {
                    orderId = row.Order.Id;
                    orderLine.text = InvestmentList.OrderLine(row.Order, OrderDesk.Today);
                    // The column is narrow: a long order line is scaled down to fit before Cancel.
                    float room = width - 82f - 8f - 54f;
                    orderLine.textScale = 0.72f;
                    if (orderLine.width > room)
                    {
                        orderLine.textScale = Mathf.Max(0.55f, 0.72f * room / orderLine.width);
                    }
                    return;
                }

                string signature =
                    pillsInCurrency
                    + "|"
                    + q.Price.ToString("0.##")
                    + "|"
                    + q.ChangeRate.ToString("0.##")
                    + "|"
                    + q.Owned
                    + "|"
                    + q.Spent
                    + "|"
                    + q.Locked
                    + "|"
                    + q.UnlockText
                    + "|"
                    + Strings.Language;
                if (signature == drawn)
                {
                    return;
                }

                drawn = signature;
                Investment[] buffer = EconomyManager.instance.m_investments.m_buffer;
                InvestmentAI ai =
                    buffer[slot].Info == null ? null : buffer[slot].Info.m_investmentAI;
                if (ai != null)
                {
                    ai.SetSpriteName(coin);
                    name.tooltip = ai.GetLocalizedDescription();
                }

                symbol.text = q.Symbol;
                name.text = q.Name;
                badge.isVisible = q.Local;
                badge.relativePosition = new Vector3(
                    symbol.relativePosition.x + symbol.width + 8f,
                    6f
                );
                lockLine.isVisible = locked;
                lockTrack.isVisible = locked;
                lockFill.isVisible = locked;
                price.isVisible = !locked;
                change.isVisible = !locked;
                if (locked)
                {
                    lockLine.text = InvestmentList.LockLine(q);
                    lockFill.width = LockWidth * Mathf.Clamp01(q.UnlockProgress);
                }
                else
                {
                    Color32 colour = FoTheme.Change(StockMonitor.ChangeColour(q.ChangeRate));
                    sparkFor(q, colour);
                    price.text = BondText.WholeMoney((long)Math.Round(q.Price * 100.0));
                    price.relativePosition = new Vector3(PriceRight - price.width, 3f);
                    // One measure in both units: the game's change rate, or that change in ₡.
                    change.text = StockMonitor.PillText(q.Price, q.ChangeRate, pillsInCurrency);
                    change.color = colour;
                }

                string held = InvestmentList.Holding(q);
                holding.isVisible = held != null;
                gain.isVisible = held != null;
                if (held != null)
                {
                    holding.text = held;
                    gain.text = InvestmentList.Gain(q);
                    int sign = InvestmentList.GainSign(q);
                    gain.textColor = FoTheme.Trend(sign, FoTheme.GameValue);
                    gain.relativePosition = new Vector3(PriceRight - gain.width, 45f);
                }
            }

            private void Rename(InvestmentRow row)
            {
                string n =
                    row.Kind == InvestmentRowKind.Heading ? Prefix + "Heading-" + row.Section
                    : row.Kind == InvestmentRowKind.Order ? Prefix + "Order-" + row.Order.Id
                    : Prefix + "Row-" + row.Stock.Symbol;
                back.name = n;
                symbol.name = n + "Symbol";
                badge.name = n + "Badge";
                name.name = n + "Name";
                holding.name = n + "Holding";
                heading.name = n + "Title";
                detail.name = n + "Detail";
                price.name = n + "Price";
                change.name = n + "Change";
                gain.name = n + "Gain";
                lockLine.name = n + "Lock";
                orderLine.name = n + "Text";
                cancel.name = n + "Cancel";
                for (int i = 0; i < outline.Length; i++)
                {
                    outline[i].name = n + "Outline" + (i == 0 ? string.Empty : i.ToString());
                }
            }
        }
    }
}

using System; // Exception, Math
using System.Collections.Generic; // Dictionary, IList, KeyValuePair, List
// ColossalFramework.UI: UIButton, UIComponent, UILabel, UIOrientation, UIPanel, UIScrollbar,
//   UISlicedSprite, UISprite, UITextureSprite
using ColossalFramework.UI;
// FinanceOverhaul.Core: BondText, InvestmentFilter, InvestmentList, InvestmentRow, MonitorQuote,
//   MonitorSort, StockMonitor, Strings
using FinanceOverhaul.Core;
// UnityEngine: Color32, FilterMode, Mathf, MonoBehaviour, Texture2D, TextureFormat,
//   TextureWrapMode, Time, Vector2, Vector3
using UnityEngine;

// Game classes used without a using (global namespace, Assembly-CSharp): BuySellInvestmentPanel,
//   EconomyManager, EconomyPanel, Investment, InvestmentAI, ToolsModifierControl

namespace FinanceOverhaul
{
    /// <summary>
    /// Economy → Investments as one sectioned list (design: investments-and-research.md, Figma
    /// UI Map band 20), shown while Options → Feature flags → "Investments list and research" is
    /// on. The game's page has five controls of its own (two titles, two lists, the Total value
    /// bar): they are hidden and this list takes their place; switching the flag off, or a hot
    /// reload, shows them again. Filter tabs (All, Owned, Local) and the Stock Monitor's sorts;
    /// sections Your portfolio, Local companies and All stocks; a fixed pool of rows refilled
    /// from the scroll offset, so any number of stocks scrolls alike and the place is kept
    /// through a purchase, the daily update and closing the panel. Buy and Sell open the game's
    /// own dialog. The list's rules are <see cref="InvestmentList"/>. Lives on the overlay root.
    /// </summary>
    public class InvestmentsList : MonoBehaviour
    {
        public const string Prefix = "FinanceOverhaulInvestments";
        public const string ListName = Prefix + "List";

        private const float BarH = 34f;
        private const float Pad = 4f;
        private const int Pool = 20;

        private static readonly string[] GameControls =
        {
            "LabelPurchasable", // not shown: a game control name
            "LabelPortfolio", // not shown: a game control name
            "InvestmentsPanel", // not shown: a game control name
            "OwnedInvestmentsPanel", // not shown: a game control name
            "OwnedInvestmentsSummaryPanel", // not shown: a game control name
        };

        /// <summary>The list on screen, for game UI tests (null when not shown).</summary>
        internal static InvestmentsList Current { get; private set; }

        private UIComponent page;
        private UIPanel list;
        private UIPanel view;
        private UIScrollbar bar;
        private UILabel empty;
        private UIButton sortButton;
        private UIButton sortDirection;
        private UITextureSprite sortArrow;
        private bool sortArrowDown;
        private readonly Dictionary<InvestmentFilter, UIButton> tabs =
            new Dictionary<InvestmentFilter, UIButton>();
        private readonly List<Row> pool = new List<Row>();

        private InvestmentFilter filter = InvestmentFilter.All;
        private MonitorSort sort = MonitorSort.Change;
        private bool reversed;
        private List<MonitorQuote> quotes = new List<MonitorQuote>();
        private List<InvestmentRow> rows = new List<InvestmentRow>();
        private float[] tops = { 0f };
        private float offset;
        private bool settingBar;
        private float nextRead;

        /// <summary>The rows in the list now (headings included), for game UI tests.</summary>
        internal IList<InvestmentRow> Rows
        {
            get { return rows; }
        }

        internal float Offset
        {
            get { return offset; }
        }

        private void Update()
        {
            try
            {
                bool wanted = Mod.Settings != null && Mod.Settings.InvestmentListEnabled;
                if (!wanted)
                {
                    if (list != null)
                    {
                        Remove();
                    }

                    return;
                }

                EconomyPanel economy = ToolsModifierControl.economyPanel;
                if (economy == null || !economy.isInvestmentOpen)
                {
                    return;
                }

                if (list == null && !Build())
                {
                    return;
                }

                // The game shows its own controls again when it lays the tab out.
                HideGameControls();
                if (Time.unscaledTime >= nextRead)
                {
                    nextRead = Time.unscaledTime + 0.25f;
                    Read();
                }
            }
            catch (Exception ex)
            {
                ModLog.Warn("investments list: " + ex.Message);
                nextRead = Time.unscaledTime + 10f;
            }
        }

        private void OnDestroy()
        {
            try
            {
                Remove();
            }
            catch
            {
                // fail soft on unload
            }
        }

        /// <summary>Shows the game's own lists again and takes the mod's list out.</summary>
        private void Remove()
        {
            UIComponent p = page ?? Page();
            if (p != null)
            {
                foreach (string name in GameControls)
                {
                    UIComponent c = p.Find(name);
                    if (c != null)
                    {
                        c.isVisible = true;
                    }
                }

                UiCleanup.RemoveChildrenNamed(p, ListName);
            }

            foreach (Row r in pool)
            {
                r.Destroy();
            }

            pool.Clear();
            tabs.Clear();
            if (sortArrow != null && sortArrow.texture != null)
            {
                Destroy(sortArrow.texture);
            }

            list = null;
            page = null;
            if (Current == this)
            {
                Current = null;
            }

            ModLog.Info("investments list: the game's lists are back");
        }

        /// <summary>The Investments page of the Economy panel, or null.</summary>
        private static UIComponent Page()
        {
            EconomyPanel economy = ToolsModifierControl.economyPanel;
            UIComponent container =
                economy == null ? null : economy.component.Find("EconomyContainer");
            return container == null ? null : container.Find("Investments");
        }

        private void HideGameControls()
        {
            foreach (string name in GameControls)
            {
                UIComponent c = page.Find(name);
                if (c != null && c.isVisible)
                {
                    c.isVisible = false;
                }
            }
        }

        // ---------- data ----------

        private void Read()
        {
            quotes = MarketQuotes.Read(true);
            List<InvestmentRow> next = InvestmentList.Rows(quotes, filter, sort, reversed);
            offset = InvestmentList.KeepInView(rows, offset, next, view.height);
            rows = next;
            tops = InvestmentList.Tops(rows);
            foreach (KeyValuePair<InvestmentFilter, UIButton> tab in tabs)
            {
                tab.Value.text = InvestmentList.FilterLabel(tab.Key, quotes);
                FoButton.SetSelected(tab.Value, tab.Key == filter);
            }

            sortButton.text = StockMonitor.SortLabel(sort, false);
            bool down = StockMonitor.IsDescending(sort, reversed);
            if (sortArrow.texture == null || down != sortArrowDown)
            {
                if (sortArrow.texture != null)
                {
                    Destroy(sortArrow.texture);
                }

                sortArrow.texture = FoButton.ArrowTexture(down);
                sortArrowDown = down;
            }

            sortDirection.tooltip = StockMonitor.DirectionText(sort, reversed);
            empty.isVisible = rows.Count == 0;
            float content = tops[tops.Length - 1];
            settingBar = true;
            bar.minValue = 0f;
            bar.maxValue = Mathf.Max(content, view.height);
            bar.scrollSize = view.height;
            bar.value = offset;
            settingBar = false;
            bar.isVisible = content > view.height;
            Fill();
        }

        /// <summary>Moves the list by that many pixels (the wheel, game UI tests).</summary>
        internal void ScrollBy(float pixels)
        {
            offset = InvestmentList.Clamp(offset + pixels, tops, view.height);
            settingBar = true;
            bar.value = offset;
            settingBar = false;
            Fill();
        }

        /// <summary>Picks a filter tab (its click, game UI tests).</summary>
        internal void Pick(InvestmentFilter f)
        {
            filter = f;
            offset = 0f;
            rows = new List<InvestmentRow>();
            Read();
        }

        private void Fill()
        {
            int first = InvestmentList.FirstVisible(tops, offset);
            for (int i = 0; i < pool.Count; i++)
            {
                int k = first + i;
                bool inView = k < rows.Count && tops[k] - offset < view.height;
                pool[i].Show(inView ? rows[k] : null, rows, inView ? tops[k] - offset : 0f);
            }
        }

        // ---------- build ----------

        private bool Build()
        {
            page = Page();
            if (page == null)
            {
                return false;
            }

            UiCleanup.RemoveChildrenNamed(page, ListName);
            HideGameControls();
            list = page.AddUIComponent<UIPanel>();
            list.name = ListName;
            list.relativePosition = new Vector3(16f, 12f);
            list.size = new Vector2(page.width - 32f, page.height - 59f);

            float x = 0f;
            foreach (InvestmentFilter f in InvestmentList.Filters)
            {
                InvestmentFilter which = f;
                UIButton tab = FoButton.Add(
                    list,
                    Prefix + "Tab-" + f,
                    InvestmentList.FilterLabel(f, quotes),
                    new Vector2(100f, 26f),
                    0.8f
                );
                tab.relativePosition = new Vector3(x, 2f);
                tab.eventClick += (c, p) => Pick(which);
                tabs[f] = tab;
                x += 104f;
            }

            sortDirection = FoButton.Add(
                list,
                Prefix + "SortDirection",
                string.Empty,
                new Vector2(22f, 22f),
                0.8f
            );
            sortDirection.relativePosition = new Vector3(list.width - 22f, 4f);
            sortDirection.eventClick += (c, p) =>
            {
                reversed = !reversed;
                Read();
            };
            sortArrow = sortDirection.AddUIComponent<UITextureSprite>();
            sortArrow.name = Prefix + "SortArrow";
            sortArrow.size = new Vector2(10f, 6f);
            sortArrow.relativePosition = new Vector3(6f, 8f);
            sortArrow.isInteractive = false;
            sortButton = FoButton.Add(
                list,
                Prefix + "Sort",
                StockMonitor.SortLabel(sort, false),
                new Vector2(150f, 26f),
                0.8f
            );
            sortButton.relativePosition = new Vector3(list.width - 22f - 6f - 150f, 2f);
            sortButton.eventClick += (c, p) =>
            {
                sort = StockMonitor.NextSort(sort);
                reversed = false;
                Read();
            };

            view = list.AddUIComponent<UIPanel>();
            view.name = Prefix + "View";
            view.backgroundSprite = "GenericPanel";
            view.color = FoTheme.GameStockCard;
            view.relativePosition = new Vector3(0f, BarH);
            view.size = new Vector2(list.width, list.height - BarH);
            view.clipChildren = true;
            list.eventMouseWheel += (c, p) =>
            {
                ScrollBy(
                    -Mathf.Sign(p.wheelDelta)
                        * InvestmentList.RowsPerNotch
                        * InvestmentList.RowHeight
                );
                p.Use();
            };

            bar = view.AddUIComponent<UIScrollbar>();
            bar.name = Prefix + "Scrollbar";
            bar.orientation = UIOrientation.Vertical;
            bar.size = new Vector2(12f, view.height - 2f * Pad);
            bar.relativePosition = new Vector3(view.width - 16f, Pad);
            bar.incrementAmount = InvestmentList.RowHeight;
            UISlicedSprite track = bar.AddUIComponent<UISlicedSprite>();
            track.spriteName = "ScrollbarTrack";
            track.size = bar.size;
            track.relativePosition = Vector3.zero;
            bar.trackObject = track;
            UISlicedSprite thumb = track.AddUIComponent<UISlicedSprite>();
            thumb.spriteName = "ScrollbarThumb";
            thumb.width = bar.width;
            bar.thumbObject = thumb;
            bar.eventValueChanged += (c, v) =>
            {
                if (!settingBar)
                {
                    offset = InvestmentList.Clamp(v, tops, view.height);
                    Fill();
                }
            };

            empty = FoText.Label(
                view,
                Prefix + "Empty",
                Strings.Get("investments.empty"),
                0.85f,
                FoTheme.TextMuted
            );
            empty.relativePosition = new Vector3(16f, 16f);

            float rowWidth = view.width - 2f * Pad - 20f;
            for (int i = 0; i < Pool; i++)
            {
                pool.Add(new Row(view, i, rowWidth));
            }

            Current = this;
            ModLog.Info("investments list: shown in place of the game's two lists");
            Read();
            return true;
        }

        /// <summary>One pooled line: a section heading or a stock, renamed for what it shows.</summary>
        private sealed class Row
        {
            private const int SparkW = 110,
                SparkH = 24,
                SparkScale = 2;

            private readonly UIPanel back;
            private readonly UISprite coin;
            private readonly UILabel symbol;
            private readonly UILabel name;
            private readonly UILabel heading;
            private readonly UILabel detail;
            private readonly UITextureSprite spark;
            private readonly Texture2D sparkTexture;
            private readonly UILabel price;
            private readonly UILabel change;
            private readonly UILabel holding;
            private readonly UILabel gain;
            private readonly UILabel orders;
            private readonly UILabel lockLine;
            private readonly UIPanel lockTrack;
            private readonly UIPanel lockFill;
            private readonly UIButton buy;
            private readonly UIButton sell;
            private readonly float width;
            private string key;
            private string drawn;
            private int slot;

            public Row(UIComponent parent, int index, float width)
            {
                this.width = width;
                back = parent.AddUIComponent<UIPanel>();
                back.name = Prefix + "Pool" + index;
                back.backgroundSprite = "GenericPanelWhite";
                back.size = new Vector2(width, InvestmentList.RowHeight - 4f);
                back.isVisible = false;

                coin = back.AddUIComponent<UISprite>();
                coin.size = new Vector2(30f, 30f);
                coin.relativePosition = new Vector3(8f, 5f);
                symbol = FoText.Label(back, "", "", 0.9f, FoTheme.GameText);
                symbol.relativePosition = new Vector3(46f, 3f);
                name = FoText.Label(back, "", "", 0.7f, FoTheme.TextMuted);
                name.relativePosition = new Vector3(46f, 22f);
                heading = FoText.Label(back, "", "", 0.85f, FoTheme.GameText);
                heading.relativePosition = new Vector3(12f, 6f);
                detail = FoText.Label(back, "", "", 0.8f, FoTheme.GameValue);

                sparkTexture = new Texture2D(
                    SparkW * SparkScale,
                    SparkH * SparkScale,
                    TextureFormat.ARGB32,
                    false
                );
                sparkTexture.filterMode = FilterMode.Bilinear;
                sparkTexture.wrapMode = TextureWrapMode.Clamp;
                spark = back.AddUIComponent<UITextureSprite>();
                spark.texture = sparkTexture;
                spark.size = new Vector2(SparkW, SparkH);
                spark.relativePosition = new Vector3(290f, 8f);

                price = FoText.Label(back, "", "", 0.9f, FoTheme.GameValue);
                change = FoPill.AddValue(back, new Vector2(64f, 18f), 0.75f, FoTheme.Header);
                change.relativePosition = new Vector3(510f, 11f);
                holding = FoText.Label(back, "", "", 0.75f, FoTheme.GameText);
                holding.relativePosition = new Vector3(592f, 4f);
                gain = FoText.Label(back, "", "", 0.75f, FoTheme.GamePositive);
                gain.relativePosition = new Vector3(592f, 21f);
                orders = FoPill.AddBadge(back, 0.65f, FoTheme.OfferGoldText, FoTheme.OfferGold);
                orders.relativePosition = new Vector3(790f, 12f);

                lockLine = FoText.Label(back, "", "", 0.75f, FoTheme.GameText);
                lockLine.relativePosition = new Vector3(290f, 5f);
                lockTrack = back.AddUIComponent<UIPanel>();
                lockTrack.backgroundSprite = "EmptySprite";
                lockTrack.color = FoTheme.BarTrack;
                lockTrack.size = new Vector2(280f, 4f);
                lockTrack.relativePosition = new Vector3(290f, 26f);
                lockFill = back.AddUIComponent<UIPanel>();
                lockFill.backgroundSprite = "EmptySprite";
                lockFill.color = FoTheme.OfferGold;
                lockFill.size = new Vector2(0f, 4f);
                lockFill.relativePosition = new Vector3(290f, 26f);

                buy = FoButton.Add(
                    back,
                    "",
                    Strings.Get("investments.buy"),
                    new Vector2(64f, 26f),
                    0.8f
                );
                buy.relativePosition = new Vector3(width - 140f, 7f);
                buy.eventClick += (c, p) =>
                    BuySellInvestmentPanel.ShowPanel((byte)slot, BuySellInvestmentPanel.Mode.Buy);
                sell = FoButton.Add(
                    back,
                    "",
                    Strings.Get("investments.sell"),
                    new Vector2(64f, 26f),
                    0.8f
                );
                sell.relativePosition = new Vector3(width - 70f, 7f);
                sell.eventClick += (c, p) =>
                    BuySellInvestmentPanel.ShowPanel((byte)slot, BuySellInvestmentPanel.Mode.Sell);
            }

            public void Destroy()
            {
                if (sparkTexture != null)
                {
                    UnityEngine.Object.Destroy(sparkTexture);
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

                bool isHeading = row.IsHeading;
                back.height =
                    (isHeading ? InvestmentList.HeadingHeight : InvestmentList.RowHeight) - 4f;
                back.color = isHeading ? FoTheme.Header : FoTheme.SectionFill;
                heading.isVisible = isHeading;
                detail.isVisible = isHeading;
                foreach (UIComponent c in new UIComponent[] { coin, symbol, name })
                {
                    c.isVisible = !isHeading;
                }

                // Every time: the "nothing changed" return below skips the rest.
                buy.isVisible = !isHeading && !row.Stock.Locked;
                sell.isVisible = !isHeading && row.Stock.Owned > 0;
                if (isHeading)
                {
                    heading.text = InvestmentList.Heading(row.Section);
                    detail.text = InvestmentList.HeadingDetail(row.Section, all);
                    detail.relativePosition = new Vector3(width - 12f - detail.width, 7f);
                    foreach (
                        UIComponent c in new UIComponent[]
                        {
                            spark,
                            price,
                            change,
                            holding,
                            gain,
                            orders,
                            lockLine,
                            lockTrack,
                            lockFill,
                        }
                    )
                    {
                        c.isVisible = false;
                    }

                    return;
                }

                MonitorQuote q = row.Stock;
                slot = q.Id;
                string signature =
                    q.Price.ToString("0.##")
                    + "|"
                    + q.ChangeRate.ToString("0.##")
                    + "|"
                    + q.Owned
                    + "|"
                    + q.Spent
                    + "|"
                    + q.OpenOrders
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
                bool locked = q.Locked;
                lockLine.isVisible = locked;
                lockTrack.isVisible = locked;
                lockFill.isVisible = locked;
                spark.isVisible = !locked;
                price.isVisible = !locked;
                change.isVisible = !locked;
                if (locked)
                {
                    lockLine.text = InvestmentList.LockLine(q);
                    lockFill.width = 280f * Mathf.Clamp01(q.UnlockProgress);
                }
                else
                {
                    Color32 colour = FoTheme.Change(StockMonitor.ChangeColour(q.ChangeRate));
                    FoSparkline.Draw(sparkTexture, q.History, colour, SparkScale + 1);
                    price.text = BondText.WholeMoney((long)Math.Round(q.Price * 100.0));
                    price.relativePosition = new Vector3(500f - price.width, 11f);
                    change.text = StockMonitor.ChangeText(q.ChangeRate);
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
                    gain.textColor =
                        sign > 0 ? FoTheme.GamePositive
                        : sign < 0 ? FoTheme.GameNegative
                        : FoTheme.GameValue;
                }

                string open = InvestmentList.Orders(q);
                orders.isVisible = open != null;
                if (open != null)
                {
                    orders.text = open;
                }
            }

            /// <summary>Names follow what the row shows, so a test finds "…Row-GGIBuy" wherever it scrolled.</summary>
            private void Rename(InvestmentRow row)
            {
                string n = row.IsHeading
                    ? Prefix + "Heading-" + row.Section
                    : Prefix + "Row-" + row.Stock.Symbol;
                back.name = n;
                symbol.name = n + "Symbol";
                name.name = n + "Name";
                heading.name = n + "Title";
                detail.name = n + "Detail";
                price.name = n + "Price";
                change.name = n + "Change";
                holding.name = n + "Holding";
                gain.name = n + "Gain";
                orders.name = n + "Orders";
                lockLine.name = n + "Lock";
                buy.name = n + "Buy";
                sell.name = n + "Sell";
            }
        }
    }
}

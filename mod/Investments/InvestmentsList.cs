using System; // Exception
using System.Collections.Generic; // Dictionary, IList, List
using ColossalFramework.UI; // UIButton, UIComponent, UILabel, UIPanel, UIScrollbar, UITextureSprite
// FinanceOverhaul.Core: InvestmentFilter, InvestmentRow, MonitorQuote, MonitorSort,
//   StockMonitorSettings
using FinanceOverhaul.Core;
using UnityEngine; // MonoBehaviour, Texture2D, Time

// Game classes used without a using (global namespace, Assembly-CSharp): EconomyPanel,
//   ToolsModifierControl

namespace FinanceOverhaul
{
    /// <summary>
    /// Economy → Investments as one sectioned list (design: investments-and-research.md, Figma
    /// UI Map band 20), shown while Options → Feature flags → "Investments list and research" is
    /// on. The game's page has five controls of its own (two titles, two lists, the Total value
    /// bar): they are hidden and this list takes their place; switching the flag off, or a hot
    /// reload, shows them again. Filter sub tabs (All, Owned, Orders, Local, Locked) and the Stock Monitor's sorts;
    /// sections Your portfolio, Local companies and All stocks; a fixed pool of rows refilled
    /// from the scroll offset, so any number of stocks scrolls alike and the place is kept
    /// through a purchase, the daily update and closing the panel. A click on a line opens the
    /// game's own dialog (<see cref="TradeDialog"/>); the overview is the right column. Split
    /// by concern: <c>.Data</c>, <c>.Sparks</c>, <c>.Build</c>, <c>.Row</c>. The list's rules are <see cref="InvestmentList"/>. Lives on the overlay root.
    /// </summary>
    public partial class InvestmentsList : MonoBehaviour
    {
        public const string Prefix = "FinanceOverhaulInvestments";
        public const string ListName = Prefix + "List";

        private const float BarH = 34f;
        private const float ColumnGap = 12f;
        private const float TabWidth = 66f;
        private const float GroupWidth = 60f;
        private const float SortWidth = 100f;
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
        private InvestmentsOverview overview;

        /// <summary>The right-hand column (game UI tests).</summary>
        internal InvestmentsOverview Overview
        {
            get { return overview; }
        }
        private UIPanel list;
        private UIPanel view;
        private UIScrollbar bar;
        private UILabel empty;
        private UILabel sortLabel;
        private UIButton sortDirection;
        private UITextureSprite sortArrow;
        private bool sortArrowDown;
        private readonly Dictionary<InvestmentFilter, UIButton> tabs =
            new Dictionary<InvestmentFilter, UIButton>();
        private readonly List<Row> pool = new List<Row>();

        private InvestmentFilter filter = InvestmentFilter.All;
        private MonitorSort localSort = MonitorSort.Change;
        private bool localReversed;
        private bool localCurrency;

        /// <summary>
        /// The list keeps its own saved sort, direction and ₡ / % pills; with Options → "follow the
        /// Stock Monitor" on, it uses (and changes) the Monitor's instead, so the two agree.
        /// </summary>
        private static bool Synced
        {
            get { return Mod.Settings != null && Mod.Settings.InvestmentListSortSynced; }
        }

        private static StockMonitorSettings Monitor
        {
            get { return Mod.Settings == null ? null : Mod.Settings.Monitor; }
        }

        private MonitorSort sort
        {
            get
            {
                return Mod.Settings == null ? localSort
                    : Synced ? Monitor.Sort
                    : Mod.Settings.InvestmentListSort;
            }
            set
            {
                localSort = value;
                if (Mod.Settings != null)
                {
                    if (Synced)
                    {
                        Monitor.Sort = value;
                    }
                    else
                    {
                        Mod.Settings.InvestmentListSort = value;
                    }

                    Mod.SaveSettings();
                }
            }
        }

        private bool reversed
        {
            get
            {
                return Mod.Settings == null ? localReversed
                    : Synced ? Monitor.SortReversed
                    : Mod.Settings.InvestmentListSortReversed;
            }
            set
            {
                localReversed = value;
                if (Mod.Settings != null)
                {
                    if (Synced)
                    {
                        Monitor.SortReversed = value;
                    }
                    else
                    {
                        Mod.Settings.InvestmentListSortReversed = value;
                    }

                    Mod.SaveSettings();
                }
            }
        }

        private bool InCurrency
        {
            get { return Mod.Settings == null ? localCurrency : Mod.Settings.PillsInCurrency; }
            set
            {
                localCurrency = value;
                if (Mod.Settings != null)
                {
                    if (Synced)
                    {
                        Monitor.ChangeAsCurrency = value;
                    }
                    else
                    {
                        Mod.Settings.InvestmentListChangeAsCurrency = value;
                    }

                    Mod.SaveSettings();
                }
            }
        }

        /// <summary>A click on any pill: ₡ ↔ % for the whole list (and the Stock Monitor when synced).</summary>
        /// <summary>What the rows' pills show, as of the last read.</summary>
        private static bool pillsInCurrency;

        private void TogglePills()
        {
            InCurrency = !InCurrency;
            Read();
        }

        private bool grouped;
        private FoToggleTabs tabBar;
        private UIButton groupButton;
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
                Hover();
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
            if (overview != null)
            {
                overview.Destroy();
                overview = null;
            }

            tabBar = null;
            if (sortArrow != null && sortArrow.texture != null)
            {
                Destroy(sortArrow.texture);
            }

            Row.HideChart();
            hovered = null;
            hoveredKey = null;
            foreach (Texture2D t in sparks.Values)
            {
                if (t != null)
                {
                    Destroy(t);
                }
            }

            sparks.Clear();
            sparksDrawn.Clear();
            sparkSprites.Clear(); // children of the list: gone with it
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
    }
}

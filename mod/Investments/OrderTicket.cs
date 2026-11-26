using System; // Exception
using System.Reflection; // FieldInfo, MethodInfo
using ColossalFramework.UI; // UIButton, UIComponent, UILabel, UIPanel, UITextField
// FinanceOverhaul.Core: LocalCompany, OrderKind, OrderText, ShareAmountInput, Strings, TradeOrder
using FinanceOverhaul.Core;
using UnityEngine; // Color32

// Game classes used without a using (global namespace, Assembly-CSharp): BuySellInvestmentPanel,
//   EconomyManager, EconomyPanel, Investment, InvestmentAI, ToolsModifierControl

namespace FinanceOverhaul
{
    /// <summary>
    /// Order ticket in the Invest dialog (Figma band 09, pins 1–5). Tabs above the share
    /// amount pick the order type for the dialog's mode: Buy → "Buy now" / "Limit";
    /// Sell → "Sell now" / "Stop-loss". "Buy now" / "Sell now" are the untouched vanilla
    /// trade. An order type shows a trigger price field, a plain-words rule, its own
    /// estimate row in place of the vanilla stats rows, and turns the vanilla button into
    /// "Place …", which places the order via <see cref="OrderDesk"/> instead of trading.
    /// Vanilla labels are never rewritten (the button is re-labelled from the game's
    /// localization). Hot-reload safe: stale parts are removed by name and
    /// <see cref="Detach"/> restores the vanilla dialog (button text and height).
    /// </summary>
    public static partial class OrderTicket
    {
        public const string TabsName = "FinanceOverhaulOrderTabs";
        public const string SidesName = "FinanceOverhaulOrderSides";
        public const string ViewHomeName = "FinanceOverhaulOrderViewHome";
        public const string PriceName = "FinanceOverhaulLimitPrice";
        public const string TabPrefix = "FinanceOverhaulOrderTab-";
        public const string PriceFieldName = "FinanceOverhaulLimitPriceField";
        public const string CaptionName = "FinanceOverhaulLimitCaption";
        public const string HintName = "FinanceOverhaulLimitHint";
        public const string EstimateName = "FinanceOverhaulLimitEstimate";

        private static readonly FieldInfo ModeField = GameInternals.BuySell.Mode;
        private static readonly FieldInfo IdField = GameInternals.BuySell.InvestmentId;
        private static readonly FieldInfo ButtonField = GameInternals.BuySell.Button;
        private static readonly FieldInfo SliderValueField = GameInternals.BuySell.SliderValue;
        private static readonly MethodInfo CloseMethod = GameInternals.BuySell.OnClosed;
        private static readonly MethodInfo UpdatePriceMethod = GameInternals
            .BuySell
            .UpdatePriceNumbers;

        private static readonly Color32 HintColor = FoTheme.TextMuted;
        private static readonly Color32 ErrorColor = FoTheme.GameNegative;
        private static readonly Color32 EstimateColor = FoTheme.GamePositive;

        /// <summary>One tab: an order kind, or null for the vanilla trade.</summary>
        private sealed class Tab
        {
            public string Key;
            public OrderKind? Kind;
            public bool Buy;

            public string Label()
            {
                return Kind.HasValue
                    ? OrderText.TabLabel(Kind.Value)
                    : Strings.Get(Buy ? "order.tab.buyNow" : "order.tab.sellNow");
            }

            public UIButton Button;
        }

        private static readonly Tab[] AllTabs =
        {
            new Tab
            {
                Key = "Market",
                Kind = null,
                Buy = true,
            },
            new Tab
            {
                Key = "Limit",
                Kind = OrderKind.LimitBuy,
                Buy = true,
            },
            new Tab
            {
                Key = "Market",
                Kind = null,
                Buy = false,
            },
            new Tab
            {
                Key = "StopLoss",
                Kind = OrderKind.StopLossSell,
                Buy = false,
            },
            new Tab
            {
                Key = "TakeProfit",
                Kind = OrderKind.TakeProfitSell,
                Buy = false,
            },
            new Tab
            {
                Key = "TrailingStop",
                Kind = OrderKind.TrailingStopSell,
                Buy = false,
            },
        };

        private static BuySellInvestmentPanel owner;
        private static UIPanel dark;
        private static UIPanel tabs;
        private static UIPanel sides;
        private static FoToggleTabs sideTabs;
        private static FoToggleTabs typeTabs;
        private static UIButton viewHome;
        private static UIPanel pricePanel;
        private static UILabel caption;
        private static UITextField priceField;
        private const int PriceMaxDigits = 7;
        private static FoGroupedField priceGrouping;
        private static UILabel hint;
        private static UILabel priceText; // vanilla, only ever repaired (never rewritten)
        private static UIComponent buyStats; // vanilla Price row + "can't afford"
        private static UIComponent sellStats; // vanilla Original / Current value / Gain rows
        private static UILabel estimate;
        private static OrderKind? active;
        private static float addedHeight;
        private static bool failureLogged;
        private static bool refreshing;

        public static bool IsReady
        {
            get
            {
                return ModeField != null
                    && GameInternals.BuySell.SetInvestment != null
                    && GameInternals.BuySell.VerticalLayout != null
                    && IdField != null
                    && ButtonField != null
                    && SliderValueField != null
                    && CloseMethod != null;
            }
        }

        public static bool PriceHasFocus
        {
            get { return priceField != null && priceField.hasFocus; }
        }

        /// <summary>An order type (not the vanilla trade) is selected.</summary>
        public static bool OrderActive
        {
            get { return active.HasValue; }
        }

        /// <summary>
        /// Options flag changed: hide or show tabs at once; a dialog showing a type that was
        /// just switched off goes back to the vanilla trade.
        /// </summary>
        public static void OnFlagsChanged()
        {
            try
            {
                if (owner == null || tabs == null)
                {
                    return;
                }

                if (active.HasValue && !Mod.OrderKindEnabled(active.Value))
                {
                    SetKind(null);
                    return;
                }

                StyleTabs();
                Relayout();
            }
            catch (Exception ex)
            {
                LogOnce("order ticket: flag refresh failed: " + ex.Message);
            }
        }

        /// <summary>Postfix targets: Awake and ShowPanel. Every open starts on the vanilla trade.</summary>
        public static void Attach(BuySellInvestmentPanel panel)
        {
            if (panel == null || !IsReady)
            {
                return;
            }

            try
            {
                if (owner != panel || tabs == null)
                {
                    Build(panel);
                }

                ResetForShow();
                infoSignature = null;
                ShowViewHome();
                NothingToBuy();
                CenterDialog();
            }
            catch (Exception ex)
            {
                Detach();
                LogOnce("order ticket: attach failed, vanilla trade kept: " + ex.Message);
            }
        }

        /// <summary>
        /// "View headquarters" beside the current value, for a local company only: closes the
        /// dialog and shows the company's building with its info panel, as the IPO dialog does.
        /// </summary>
        private static void ShowViewHome()
        {
            if (viewHome == null || owner == null)
            {
                return;
            }

            byte id = (byte)IdField.GetValue(owner);
            Investment[] buffer = EconomyManager.instance.m_investments.m_buffer;
            InvestmentAI ai = buffer[id].Info == null ? null : buffer[id].Info.m_investmentAI;
            int company;
            LocalCompany local = CompanyListing.IsCompany(ai, out company)
                ? CompanyDesk.CompanyOf(company)
                : null;
            viewHome.isVisible = local != null;
            viewHome.isEnabled = local != null && CompanyHome.Stands(local);
            viewHome.tooltip = viewHome.isEnabled
                ? string.Empty
                : Strings.Get("order.viewHome.lost");
        }

        private static void OnViewHome()
        {
            if (owner == null)
            {
                return;
            }

            byte id = (byte)IdField.GetValue(owner);
            Investment[] buffer = EconomyManager.instance.m_investments.m_buffer;
            InvestmentAI ai = buffer[id].Info == null ? null : buffer[id].Info.m_investmentAI;
            int company;
            if (CompanyListing.IsCompany(ai, out company))
            {
                // Out of the way of the building: the dialog, then the Economy window (its
                // toolbar tab closes as its close button would).
                BuySellInvestmentPanel.HidePanel();
                EconomyPanel economy = ToolsModifierControl.economyPanel;
                if (economy != null && economy.component.isVisible)
                {
                    economy.OnCloseButton();
                }

                CompanyHome.Show(CompanyDesk.CompanyOf(company), false);
            }
        }

        /// <summary>
        /// A Buy dialog with no shares left to buy (the city holds all it may) has an empty share
        /// range, and the game's slider then puts its thumb at no position at all, where it takes
        /// every click in the dialog. So it turns to Sell when the city holds shares, or closes.
        /// </summary>
        private static void NothingToBuy()
        {
            if (owner == null || !IsBuyMode(owner))
            {
                return;
            }

            byte id = (byte)IdField.GetValue(owner);
            if (Available(id) > 0)
            {
                return;
            }

            if (Owned(id) > 0)
            {
                ModLog.Info("order ticket: nothing left to buy, the dialog opens on Sell");
                SwitchSide(false);
            }
            else
            {
                ModLog.Info("order ticket: nothing to buy or sell, the dialog closes");
                BuySellInvestmentPanel.HidePanel();
            }
        }

        /// <summary>
        /// Prefix target: BuySellInvestmentPanel.OnButtonClicked. Returns true when the
        /// click was handled here (an order), so vanilla must not trade.
        /// </summary>
        public static bool HandleButton(BuySellInvestmentPanel panel)
        {
            if (!active.HasValue || panel != owner)
            {
                return false;
            }

            if (!Mod.OrderKindEnabled(active.Value))
            {
                SetKind(null); // switched off while open: back to the vanilla trade, no trade now
                return true;
            }

            int price;
            uint shares = (uint)SliderValueField.GetValue(panel);
            if (!TryPrice(out price) || shares == 0)
            {
                ShowError(
                    Strings.Get(
                        active.Value == OrderKind.TrailingStopSell
                            ? "order.enter.trailShares"
                            : "order.enter.priceShares"
                    )
                );
                return true; // never fall through to a market trade
            }

            byte id = (byte)IdField.GetValue(panel);
            string problem = Problem(active.Value, price, shares, Mark(id));
            if (problem != null)
            {
                ShowError(problem);
                return true;
            }

            TradeOrder order =
                active.Value == OrderKind.LimitBuy
                    ? OrderDesk.PlaceLimitBuy(id, shares, price)
                    : OrderDesk.PlaceSell(active.Value, id, shares, price);
            if (order == null)
            {
                ShowError(Strings.Get("order.problem.failed"));
                return true;
            }

            SetKind(null); // leave the dialog in its vanilla state
            CloseMethod.Invoke(panel, null);
            return true;
        }

        /// <summary>Why this order can't be placed, or null.</summary>
        private static string Problem(OrderKind kind, int price, uint shares, float mark)
        {
            if (kind == OrderKind.LimitBuy && (long)price * shares > EconomyManager.playerMoney)
            {
                return Strings.Get("order.problem.afford");
            }

            return OrderText.PlacementProblem(kind, price, mark);
        }

        /// <summary>Postfix target: SetSliderValue — keeps the rule and estimate current.</summary>
        /// <summary>Once a frame while the dialog is open: keeps the typed price grouped.</summary>
        public static void Tick()
        {
            RefreshInfo();
            if (priceGrouping != null && priceField != null && priceField.isVisible)
            {
                priceGrouping.Tick();
            }
        }

        public static void Refresh()
        {
            if (hint == null || !active.HasValue)
            {
                return;
            }

            int price;
            uint shares = owner == null ? 0 : (uint)SliderValueField.GetValue(owner);
            if (!TryPrice(out price))
            {
                ShowError(
                    Strings.Get(
                        active.Value == OrderKind.TrailingStopSell
                            ? "order.enter.trail"
                            : "order.enter.price"
                    )
                );
                return;
            }

            if (refreshing)
            {
                return;
            }

            refreshing = true;
            try
            {
                hint.textColor = HintColor;
                hint.text = OrderText.Hint(
                    active.Value,
                    price,
                    owner == null ? 0f : Mark((byte)IdField.GetValue(owner)),
                    Mod.OrderLifetimeDays
                );
                ShowEstimate(active.Value, price, shares);
                FitPricePanel();
            }
            finally
            {
                refreshing = false;
            }
        }

        /// <summary>
        /// Our estimate row stands in for the vanilla stats rows (hidden); the button follows
        /// whether the order could be placed. Vanilla labels are never rewritten.
        /// </summary>
        private static void ShowEstimate(OrderKind kind, int price, uint shares)
        {
            if (estimate == null || owner == null)
            {
                return;
            }

            float mark = Mark((byte)IdField.GetValue(owner));
            long amount = (long)OrderText.EstimatePerShare(kind, price, mark) * shares;
            string problem = Problem(kind, price, shares, mark);
            estimate.textColor = problem == null ? EstimateColor : ErrorColor;
            estimate.text = OrderText.EstimateText(kind, amount, problem);
            UIButton button = ButtonField.GetValue(owner) as UIButton;
            if (button != null)
            {
                button.isEnabled = problem == null;
            }
        }

        /// <summary>Back to vanilla: show its stats rows and let it recompute price and button state.</summary>
        private static void RestoreStats()
        {
            if (owner != null)
            {
                bool buy = IsBuyMode(owner);
                if (buyStats != null && buy)
                {
                    buyStats.isVisible = true;
                }

                if (sellStats != null && !buy)
                {
                    sellStats.isVisible = true;
                }
            }

            if (owner != null && UpdatePriceMethod != null)
            {
                UpdatePriceMethod.Invoke(owner, null);
            }
        }

        /// <summary>
        /// Postfix target: UpdatePriceNumbers (open, slider moves). An order type keeps our
        /// estimate; otherwise the vanilla Price label is put back in its place if moved.
        /// </summary>
        public static void AfterVanillaPrice()
        {
            if (active.HasValue)
            {
                Refresh();
            }
            else
            {
                PlacePriceLabel();
            }
        }

        public static void Detach()
        {
            try
            {
                DetachInfo();
                SetKind(null);
                if (dark != null && addedHeight != 0f)
                {
                    Grow(-addedHeight);
                }
            }
            catch
            {
                // fail soft
            }

            Destroy(tabs);
            Destroy(sides);
            Destroy(pricePanel);
            foreach (Tab t in AllTabs)
            {
                t.Button = null;
            }

            tabs = null;
            sides = null;
            sideTabs = null;
            typeTabs = null;
            Destroy(viewHome);
            viewHome = null;
            pricePanel = null;
            caption = null;
            priceField = null;
            priceGrouping = null;
            hint = null;
            estimate = null;
            buyStats = null;
            sellStats = null;
            priceText = null;
            dark = null;
            owner = null;
            addedHeight = 0f;
            active = null;
        }

        private static void SetKind(OrderKind? kind)
        {
            OrderKind? was = active;
            active = kind;
            StyleTabs();

            UIButton button = owner == null ? null : ButtonField.GetValue(owner) as UIButton;
            if (button != null && kind.HasValue)
            {
                button.text = OrderText.ButtonLabel(kind.Value);
            }
            else if (!kind.HasValue && was.HasValue)
            {
                ApplyVanillaButtonLabel();
            }

            if (!kind.HasValue && was.HasValue)
            {
                RestoreStats();
            }

            if (kind.HasValue && caption != null)
            {
                caption.text = OrderText.PriceCaption(kind.Value);
            }

            if (kind.HasValue && priceField != null && owner != null && kind != was)
            {
                // Limit: today's price; stop 10% below it; target 10% above it.
                float mark = Mark((byte)IdField.GetValue(owner));
                priceField.text = OrderText.SuggestedPrice(kind.Value, mark).ToString();
            }

            Relayout();
            Refresh();
        }

        /// <summary>
        /// The Buy | Sell switch: the open dialog turns to the other side in place, as the game
        /// would open it for that side (its own SetInvestment and layout), then the ticket is
        /// laid out again. Sell needs shares to sell.
        /// </summary>
        private static void SwitchSide(bool buy)
        {
            try
            {
                if (owner == null || IsBuyMode(owner) == buy)
                {
                    return;
                }

                byte id = (byte)IdField.GetValue(owner);
                if (!buy && Owned(id) == 0)
                {
                    return;
                }

                ModeField.SetValue(
                    owner,
                    buy ? BuySellInvestmentPanel.Mode.Buy : BuySellInvestmentPanel.Mode.Sell
                );
                GameInternals.BuySell.SetInvestment.Invoke(owner, new object[] { id });
                var layout = GameInternals.BuySell.VerticalLayout.GetValue(owner) as UIPanel;
                if (layout != null)
                {
                    layout.FitChildrenVertically();
                }

                owner.component.FitChildrenVertically();
                Attach(owner);
            }
            catch (Exception ex)
            {
                LogOnce("order ticket: buy / sell switch failed: " + ex.Message);
            }
        }

        private static uint Available(byte id)
        {
            Investment[] buffer = EconomyManager.instance.m_investments.m_buffer;
            InvestmentAI ai = buffer[id].Info == null ? null : buffer[id].Info.m_investmentAI;
            return ai == null ? 0u : ai.AvailableForBuyingCount(ref buffer[id]);
        }

        private static uint Owned(byte id)
        {
            Investment[] buffer = EconomyManager.instance.m_investments.m_buffer;
            InvestmentAI ai = buffer[id].Info == null ? null : buffer[id].Info.m_investmentAI;
            return ai == null ? 0u : ai.OwnedCount(ref buffer[id]);
        }

        private static bool IsBuyMode(BuySellInvestmentPanel panel)
        {
            return (BuySellInvestmentPanel.Mode)ModeField.GetValue(panel)
                == BuySellInvestmentPanel.Mode.Buy;
        }

        private static float Mark(byte id)
        {
            return EconomyManager.instance.m_investments.m_buffer[id].value;
        }

        private static bool TryPrice(out int price)
        {
            uint value;
            price = 0;
            if (
                priceField == null
                || !ShareAmountInput.TryParse(priceField.text, 1u, 1000000000u, out value)
            )
            {
                return false;
            }

            price = (int)value;
            return true;
        }

        private static void ShowError(string message)
        {
            if (hint != null)
            {
                hint.textColor = ErrorColor;
                hint.text = message;
                FitPricePanel();
            }
        }

        private static void LogOnce(string message)
        {
            if (!failureLogged)
            {
                failureLogged = true;
                ModLog.Warn(message);
            }
        }
    }
}

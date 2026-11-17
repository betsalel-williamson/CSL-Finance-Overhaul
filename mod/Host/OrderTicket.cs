using System;
using System.Reflection;
using ColossalFramework.UI;
using FinanceOverhaul.Core;
using HarmonyLib;
using UnityEngine;

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
    public static class OrderTicket
    {
        public const string TabsName = "FinanceOverhaulOrderTabs";
        public const string PriceName = "FinanceOverhaulLimitPrice";
        public const string TabPrefix = "FinanceOverhaulOrderTab-";
        public const string PriceFieldName = "FinanceOverhaulLimitPriceField";
        public const string CaptionName = "FinanceOverhaulLimitCaption";
        public const string HintName = "FinanceOverhaulLimitHint";
        public const string EstimateName = "FinanceOverhaulLimitEstimate";

        private static readonly FieldInfo ModeField = AccessTools.Field(
            typeof(BuySellInvestmentPanel),
            "m_mode"
        );
        private static readonly FieldInfo IdField = AccessTools.Field(
            typeof(BuySellInvestmentPanel),
            "m_investmentID"
        );
        private static readonly FieldInfo ButtonField = AccessTools.Field(
            typeof(BuySellInvestmentPanel),
            "m_button"
        );
        private static readonly FieldInfo SliderValueField = AccessTools.Field(
            typeof(BuySellInvestmentPanel),
            "m_sliderValue"
        );
        private static readonly MethodInfo CloseMethod = AccessTools.Method(
            typeof(BuySellInvestmentPanel),
            "OnClosed"
        );
        private static readonly MethodInfo UpdatePriceMethod = AccessTools.Method(
            typeof(BuySellInvestmentPanel),
            "UpdatePriceNumbers"
        );

        private static readonly Color32 HintColor = new Color32(160, 172, 182, 255);
        private static readonly Color32 ErrorColor = new Color32(233, 66, 79, 255);
        private static readonly Color32 EstimateColor = new Color32(78, 205, 96, 255);

        /// <summary>One tab: an order kind, or null for the vanilla trade.</summary>
        private sealed class Tab
        {
            public string Key;
            public OrderKind? Kind;
            public bool Buy;
            public string Label;
            public UIButton Button;
        }

        private static readonly Tab[] AllTabs =
        {
            new Tab
            {
                Key = "Market",
                Kind = null,
                Buy = true,
                Label = "Buy now",
            },
            new Tab
            {
                Key = "Limit",
                Kind = OrderKind.LimitBuy,
                Buy = true,
                Label = "Limit",
            },
            new Tab
            {
                Key = "Market",
                Kind = null,
                Buy = false,
                Label = "Sell now",
            },
            new Tab
            {
                Key = "StopLoss",
                Kind = OrderKind.StopLossSell,
                Buy = false,
                Label = OrderText.TabLabel(OrderKind.StopLossSell),
            },
            new Tab
            {
                Key = "TakeProfit",
                Kind = OrderKind.TakeProfitSell,
                Buy = false,
                Label = OrderText.TabLabel(OrderKind.TakeProfitSell),
            },
            new Tab
            {
                Key = "TrailingStop",
                Kind = OrderKind.TrailingStopSell,
                Buy = false,
                Label = OrderText.TabLabel(OrderKind.TrailingStopSell),
            },
        };

        private static BuySellInvestmentPanel owner;
        private static UIPanel dark;
        private static UIPanel tabs;
        private static UIPanel pricePanel;
        private static UILabel caption;
        private static UITextField priceField;
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
            }
            catch (Exception ex)
            {
                Detach();
                LogOnce("order ticket: attach failed, vanilla trade kept: " + ex.Message);
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
                    active.Value == OrderKind.TrailingStopSell
                        ? "Enter the trail in whole percent and at least one share."
                        : "Enter a price in whole ₡ and at least one share."
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
                ShowError("The order could not be placed.");
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
                return "The city can't afford this many shares at your limit.";
            }

            return OrderText.PlacementProblem(kind, price, mark);
        }

        /// <summary>Postfix target: SetSliderValue — keeps the rule and estimate current.</summary>
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
                    active.Value == OrderKind.TrailingStopSell
                        ? "Enter the trail in whole percent."
                        : "Enter a price in whole ₡."
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
            estimate.text =
                OrderText.EstimateLabel(kind)
                + amount.ToString("N0")
                + (problem == null ? string.Empty : " — " + problem);
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
        /// The Price label is localized with a ":" suffix, and reading its text includes the
        /// suffix, so older builds that saved and re-wrote it stacked colons ("Price:::").
        /// Re-apply the game's own string to repair it.
        /// </summary>
        private static void RepairPriceLabel()
        {
            if (
                priceText == null
                || !priceText.isLocalized
                || string.IsNullOrEmpty(priceText.localeID)
            )
            {
                return;
            }

            try
            {
                priceText.text = ColossalFramework.Globalization.Locale.Get(priceText.localeID);
                PlacePriceLabel();
            }
            catch
            {
                // fail soft
            }
        }

        private static void FitPricePanel()
        {
            if (pricePanel == null || hint == null)
            {
                return;
            }

            if (estimate != null)
            {
                estimate.relativePosition = new Vector3(
                    26f,
                    hint.relativePosition.y + hint.height + 6f
                );
            }

            float wanted =
                (
                    estimate != null
                        ? estimate.relativePosition.y + estimate.height
                        : hint.relativePosition.y + hint.height
                ) + 8f;
            if (Mathf.Abs(pricePanel.height - wanted) > 0.5f)
            {
                pricePanel.height = wanted;
                Relayout();
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

        /// <summary>
        /// Vanilla right-aligns the Price label 8 px before the value. Only placed while the
        /// row is shown and measured: a hidden label reports no width and would land on the value.
        /// </summary>
        private static void PlacePriceLabel()
        {
            UILabel value = dark == null ? null : dark.Find<UILabel>("LabelPriceValue");
            if (
                priceText == null
                || value == null
                || !priceText.isVisible
                || !value.isVisible
                || priceText.width < 1f
            )
            {
                return;
            }

            float x = value.relativePosition.x - priceText.width - 8f;
            if (Mathf.Abs(priceText.relativePosition.x - x) > 0.5f)
            {
                priceText.relativePosition = new Vector3(x, value.relativePosition.y);
            }
        }

        public static void Detach()
        {
            try
            {
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
            Destroy(pricePanel);
            foreach (Tab t in AllTabs)
            {
                t.Button = null;
            }

            tabs = null;
            pricePanel = null;
            caption = null;
            priceField = null;
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

        private static void Build(BuySellInvestmentPanel panel)
        {
            Detach();
            UIComponent root = panel.component;
            dark = root.Find<UIPanel>("DarkSection");
            UIComponent slider = dark == null ? null : dark.Find("PanelSlider");
            UILabel shareLabel = dark == null ? null : dark.Find<UILabel>("LabelShareAmount");
            UIButton button = ButtonField.GetValue(panel) as UIButton;
            if (dark == null || slider == null || button == null)
            {
                LogOnce("order ticket: dialog layout not recognised; vanilla trade kept");
                dark = null;
                return;
            }

            RemoveStale(dark);
            owner = panel;
            priceText = dark.Find<UILabel>("LabelPriceText");
            buyStats = dark.Find("PanelBuyStats");
            sellStats = dark.Find("PanelSellStats");

            // Tabs row above the share amount; one button per (mode, kind).
            tabs = dark.AddUIComponent<UIPanel>();
            tabs.name = TabsName;
            tabs.size = new Vector2(dark.width, 38f);
            foreach (Tab t in AllTabs)
            {
                Tab tab = t;
                tab.Button = AddTab(tabs, tab.Label);
                tab.Button.name = TabPrefix + tab.Key + (tab.Buy ? string.Empty : "Sell");
                tab.Button.eventClick += (c, p) => SetKind(tab.Kind);
            }

            // Trigger price row under the share amount, field in the share field's column.
            pricePanel = dark.AddUIComponent<UIPanel>();
            pricePanel.name = PriceName;
            pricePanel.size = new Vector2(dark.width, 62f);
            caption = pricePanel.AddUIComponent<UILabel>();
            caption.name = CaptionName;
            CopyLabelStyle(shareLabel, caption);
            caption.relativePosition = new Vector3(26f, 10f);
            priceField = pricePanel.AddUIComponent<UITextField>();
            priceField.name = PriceFieldName;
            priceField.size = new Vector2(96f, 27f);
            priceField.relativePosition = new Vector3(dark.width - 96f - 32f, 5f);
            priceField.numericalOnly = true;
            priceField.allowFloats = false;
            priceField.maxLength = 7; // ₡9,999,999 per share: far above any listing
            priceField.selectOnFocus = true;
            priceField.submitOnFocusLost = true;
            priceField.builtinKeyNavigation = true;
            priceField.readOnly = false;
            priceField.horizontalAlignment = UIHorizontalAlignment.Right;
            priceField.padding = new RectOffset(6, 8, 6, 4);
            priceField.normalBgSprite = "TextFieldPanel";
            priceField.hoveredBgSprite = "TextFieldPanelHovered";
            priceField.focusedBgSprite = "TextFieldPanel";
            priceField.selectionSprite = "EmptySprite";
            priceField.color = new Color32(28, 28, 28, 255);
            priceField.textColor = Color.white;
            priceField.tooltip = "Whole ₡ per share · orders trade at the daily price";
            if (shareLabel != null && shareLabel.font != null)
            {
                priceField.font = shareLabel.font;
            }

            priceField.eventTextChanged += (c, t) => Refresh();
            hint = pricePanel.AddUIComponent<UILabel>();
            hint.name = HintName;
            hint.relativePosition = new Vector3(26f, 38f);
            hint.textScale = 0.75f;
            hint.textColor = HintColor;
            // Wraps inside the dialog; the row grows with it (see FitPricePanel).
            hint.autoSize = false;
            hint.wordWrap = true;
            hint.autoHeight = true;
            hint.width = dark.width - 52f;
            estimate = pricePanel.AddUIComponent<UILabel>();
            estimate.name = EstimateName;
            estimate.autoSize = false;
            estimate.wordWrap = true;
            estimate.autoHeight = true;
            estimate.width = dark.width - 52f;
            estimate.textAlignment = UIHorizontalAlignment.Center;
            CopyLabelStyle(shareLabel, estimate);
            estimate.textScale = 0.875f;
            estimate.textColor = EstimateColor;

            // Place both just around the slider block (autoLayout follows zOrder).
            tabs.zOrder = slider.zOrder;
            pricePanel.zOrder = slider.zOrder + 1;
            ModLog.Info("order ticket: attached (buy and sell order types)");
        }

        private static UIButton AddTab(UIPanel parent, string text)
        {
            UIButton tab = parent.AddUIComponent<UIButton>();
            tab.text = text;
            tab.size = new Vector2(108f, 28f);
            tab.textScale = 0.875f;
            tab.normalBgSprite = "ButtonMenu";
            tab.hoveredBgSprite = "ButtonMenuHovered";
            tab.pressedBgSprite = "ButtonMenuPressed";
            tab.focusedBgSprite = "ButtonMenu";
            tab.disabledBgSprite = "ButtonMenuDisabled";
            tab.textColor = new Color32(200, 210, 220, 255);
            return tab;
        }

        /// <summary>
        /// A fresh open starts on the vanilla trade. Vanilla labels the button by localeID
        /// and skips re-applying an unchanged id, so always re-apply the localized label
        /// for this mode (also repairs labels left by older builds).
        /// </summary>
        private static void ResetForShow()
        {
            active = null;
            ApplyVanillaButtonLabel();
            RepairPriceLabel();
            if (priceField != null)
            {
                priceField.text = string.Empty; // re-filled from today's price per order type
            }

            StyleTabs();
            Relayout();
        }

        /// <summary>The game's own localized label for the dialog's mode ("Buy" / "Sell").</summary>
        private static void ApplyVanillaButtonLabel()
        {
            UIButton button = owner == null ? null : ButtonField.GetValue(owner) as UIButton;
            if (button == null)
            {
                return;
            }

            bool buy = IsBuyMode(owner);
            string label;
            try
            {
                label = ColossalFramework.Globalization.Locale.Get(
                    buy ? "INVESTMENTS_BUY" : "INVESTMENTS_SELL"
                );
            }
            catch
            {
                label = buy ? "Buy" : "Sell";
            }

            button.text = label;
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

        private static void StyleTabs()
        {
            if (owner == null)
            {
                return;
            }

            bool buy = IsBuyMode(owner);
            float x = 26f;
            foreach (Tab t in AllTabs)
            {
                if (t.Button == null)
                {
                    continue;
                }

                bool shown =
                    t.Buy == buy && (!t.Kind.HasValue || Mod.OrderKindEnabled(t.Kind.Value));
                t.Button.isVisible = shown;
                if (!shown)
                {
                    continue;
                }

                t.Button.relativePosition = new Vector3(x, 6f);
                x += t.Button.width + 8f;
                bool on = t.Kind == active;
                t.Button.normalBgSprite = on ? "ButtonMenuPressed" : "ButtonMenu";
                t.Button.focusedBgSprite = t.Button.normalBgSprite;
                t.Button.textColor = on ? (Color32)Color.white : new Color32(170, 180, 190, 255);
            }
        }

        /// <summary>
        /// Tabs in both modes; the price row only for an order type, where it stands in for
        /// the mode's vanilla stats rows. Grows the dialog to fit.
        /// </summary>
        private static void Relayout()
        {
            if (dark == null || owner == null)
            {
                return;
            }

            bool buy = IsBuyMode(owner);
            UIComponent stats = buy ? buyStats : sellStats;
            // With every order type of this mode switched off there is nothing to choose:
            // no tabs row, the dialog is the vanilla one.
            tabs.isVisible = AnyOrderTab(buy);
            pricePanel.isVisible = active.HasValue;
            float wanted = tabs.isVisible ? tabs.height : 0f;
            if (active.HasValue)
            {
                wanted += pricePanel.height;
                if (stats != null)
                {
                    stats.isVisible = false;
                    wanted -= stats.height;
                }
            }

            Grow(wanted - addedHeight);
            addedHeight = wanted;
        }

        private static bool AnyOrderTab(bool buy)
        {
            foreach (Tab t in AllTabs)
            {
                if (t.Buy == buy && t.Kind.HasValue && Mod.OrderKindEnabled(t.Kind.Value))
                {
                    return true;
                }
            }

            return false;
        }

        private static void Grow(float delta)
        {
            if (delta == 0f)
            {
                return;
            }

            if (!dark.autoFitChildrenVertically)
            {
                dark.height += delta;
            }

            UIComponent layout = dark.parent;
            if (layout != null && layout != owner.component)
            {
                layout.height += delta;
            }

            owner.component.height += delta;
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

        private static void CopyLabelStyle(UILabel from, UILabel to)
        {
            if (from == null)
            {
                return;
            }

            to.font = from.font;
            to.textScale = from.textScale;
            to.textColor = from.textColor;
        }

        private static void RemoveStale(UIComponent parent)
        {
            UiCleanup.RemoveChildrenNamed(parent, TabsName, PriceName);
        }

        private static void Destroy(UIComponent c)
        {
            if (c != null)
            {
                try
                {
                    UnityEngine.Object.Destroy(c.gameObject);
                }
                catch
                {
                    // fail soft
                }
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

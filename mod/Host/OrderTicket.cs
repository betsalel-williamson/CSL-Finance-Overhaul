#if HAS_CITIES
using System;
using System.Reflection;
using ColossalFramework.UI;
using FinanceOverhaul.Market;
using HarmonyLib;
using UnityEngine;

namespace FinanceOverhaul
{
    /// <summary>
    /// Limit buy ticket in the Invest dialog (Figma band 09, pins 1–4). Buy mode gains
    /// "Buy now" / "Limit" tabs above the share amount; Limit shows a limit price field
    /// and turns the vanilla button into "Place limit order", which places an order via
    /// <see cref="OrderDesk"/> instead of buying. "Buy now" is the untouched vanilla buy.
    /// Sell mode hides the ticket. Hot-reload safe: stale parts are removed by name and
    /// <see cref="Detach"/> restores the vanilla dialog (button text and height).
    /// </summary>
    public static class OrderTicket
    {
        public const string TabsName = "FinanceOverhaulOrderTabs";
        public const string PriceName = "FinanceOverhaulLimitPrice";
        public const string BuyNowTabName = "FinanceOverhaulOrderTabBuyNow";
        public const string LimitTabName = "FinanceOverhaulOrderTabLimit";
        public const string PriceFieldName = "FinanceOverhaulLimitPriceField";
        public const string HintName = "FinanceOverhaulLimitHint";
        public const string EstimateName = "FinanceOverhaulLimitEstimate";
        public const string PlaceLabel = "Place limit order";

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

        private static BuySellInvestmentPanel owner;
        private static UIPanel dark;
        private static UIPanel tabs;
        private static UIPanel pricePanel;
        private static UIButton buyNowTab;
        private static UIButton limitTab;
        private static UITextField priceField;
        private static UILabel hint;
        private static UILabel priceText; // vanilla, only ever repaired (never rewritten)
        private static UIComponent buyStats; // vanilla Price row + "can't afford"
        private static UILabel estimate;
        private static bool limitActive;
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

        public static bool LimitActive
        {
            get { return limitActive; }
        }

        /// <summary>Postfix targets: Awake and ShowPanel. Resets to "Buy now" on every show.</summary>
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
                LogOnce("order ticket: attach failed, vanilla Buy kept: " + ex.Message);
            }
        }

        /// <summary>
        /// Prefix target: BuySellInvestmentPanel.OnButtonClicked. Returns true when the
        /// click was handled here (a limit order), so vanilla must not buy.
        /// </summary>
        public static bool HandleButton(BuySellInvestmentPanel panel)
        {
            if (!limitActive || panel != owner || !IsBuyMode(panel))
            {
                return false;
            }

            int price;
            uint shares = (uint)SliderValueField.GetValue(panel);
            if (!TryPrice(out price) || shares == 0)
            {
                ShowError("Enter a limit price in whole ₡ and at least one share.");
                return true; // never fall through to a market buy
            }

            if ((long)price * shares > EconomyManager.playerMoney)
            {
                ShowError("The city can't afford this many shares at your limit.");
                return true;
            }

            byte id = (byte)IdField.GetValue(panel);
            TradeOrder order = OrderDesk.PlaceLimitBuy(id, shares, price);
            if (order == null)
            {
                ShowError("The order could not be placed.");
                return true;
            }

            SetLimit(false); // leave the dialog in its vanilla state
            CloseMethod.Invoke(panel, null);
            return true;
        }

        /// <summary>Postfix target: SetSliderValue — keeps the estimate in the hint current.</summary>
        public static void Refresh()
        {
            if (hint == null || !limitActive)
            {
                return;
            }

            int price;
            uint shares = owner == null ? 0 : (uint)SliderValueField.GetValue(owner);
            if (!TryPrice(out price))
            {
                ShowError("Enter a limit price in whole ₡.");
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
                hint.text =
                    "Buys when the daily price is at or below ₡"
                    + price.ToString("N0")
                    + ". Good for "
                    + TradeOrder.LifetimeDays
                    + " days.";
                ShowEstimate((long)price * shares);
                FitPricePanel();
            }
            finally
            {
                refreshing = false;
            }
        }

        /// <summary>
        /// Limit mode: our own estimate row replaces the vanilla Price row (hidden), and the
        /// button follows affordability at the limit. Vanilla labels are never rewritten.
        /// </summary>
        private static void ShowEstimate(long cost)
        {
            if (estimate == null)
            {
                return;
            }

            long cash = EconomyManager.playerMoney;
            bool affordable = cost <= cash;
            estimate.textColor = affordable ? EstimateColor : ErrorColor;
            estimate.text =
                "Est. cost (at limit): ₡"
                + cost.ToString("N0")
                + (affordable ? string.Empty : " — more than the city's ₡" + cash.ToString("N0"));
            UIButton button = owner == null ? null : ButtonField.GetValue(owner) as UIButton;
            if (button != null)
            {
                button.isEnabled = affordable;
            }
        }

        /// <summary>Back to vanilla: show its Price row and let it recompute price and Buy state.</summary>
        private static void RestorePriceRow()
        {
            if (buyStats != null && owner != null && IsBuyMode(owner))
            {
                buyStats.isVisible = true;
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
                // Vanilla right-aligns the label 8 px before the value; older builds moved it.
                UILabel value = dark == null ? null : dark.Find<UILabel>("LabelPriceValue");
                if (value != null)
                {
                    priceText.relativePosition = new Vector3(
                        value.relativePosition.x - priceText.width - 8f,
                        value.relativePosition.y
                    );
                }
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

        /// <summary>Postfix target: UpdatePriceNumbers — vanilla rewrites the Price row; keep the estimate.</summary>
        public static void AfterVanillaPrice()
        {
            if (limitActive)
            {
                Refresh();
            }
        }

        /// <summary>E2E: switches the ticket like a click on a tab.</summary>
        public static void SelectLimit(bool limit)
        {
            UIButton tab = limit ? limitTab : buyNowTab;
            if (tab != null)
            {
                tab.SimulateClick();
            }
        }

        /// <summary>E2E: types into the limit price field.</summary>
        public static void TypePrice(string text)
        {
            if (priceField != null)
            {
                priceField.text = text;
                Refresh();
            }
        }

        /// <summary>E2E: presses the dialog's main button.</summary>
        public static void PressButton()
        {
            UIButton button = owner == null ? null : ButtonField.GetValue(owner) as UIButton;
            if (button != null)
            {
                button.SimulateClick();
            }
        }

        public static void Detach()
        {
            try
            {
                SetLimit(false);
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
            tabs = null;
            pricePanel = null;
            buyNowTab = null;
            limitTab = null;
            priceField = null;
            hint = null;
            estimate = null;
            buyStats = null;
            priceText = null;
            dark = null;
            owner = null;
            addedHeight = 0f;
            limitActive = false;
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
                LogOnce("order ticket: dialog layout not recognised; vanilla Buy kept");
                dark = null;
                return;
            }

            RemoveStale(dark);
            owner = panel;
            priceText = dark.Find<UILabel>("LabelPriceText");
            buyStats = dark.Find("PanelBuyStats");

            // Tabs row above the share amount.
            tabs = dark.AddUIComponent<UIPanel>();
            tabs.name = TabsName;
            tabs.size = new Vector2(dark.width, 38f);
            buyNowTab = AddTab(tabs, "Buy now", 26f);
            buyNowTab.name = BuyNowTabName;
            limitTab = AddTab(tabs, "Limit", 26f + 116f);
            limitTab.name = LimitTabName;
            buyNowTab.eventClick += (c, p) => SetLimit(false);
            limitTab.eventClick += (c, p) => SetLimit(true);

            // Limit price row under the share amount, field in the share field's column.
            pricePanel = dark.AddUIComponent<UIPanel>();
            pricePanel.name = PriceName;
            pricePanel.size = new Vector2(dark.width, 62f);
            UILabel caption = pricePanel.AddUIComponent<UILabel>();
            caption.text = "Limit price (₡ per share)";
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
            priceField.tooltip =
                "Whole ₡ per share · the order buys at the daily price, never above this";
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
            ModLog.Info(
                "order ticket: attached (DarkSection autoLayout="
                    + dark.autoLayout
                    + ", autoFit="
                    + dark.autoFitChildrenVertically
                    + ")"
            );
        }

        private static UIButton AddTab(UIPanel parent, string text, float x)
        {
            UIButton tab = parent.AddUIComponent<UIButton>();
            tab.text = text;
            tab.size = new Vector2(108f, 28f);
            tab.relativePosition = new Vector3(x, 6f);
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
        /// A fresh open starts on "Buy now". Vanilla has just set the button text for this
        /// mode ("Buy" or "Sell"), so it is left alone; only our own overrides are undone.
        /// </summary>
        private static void ResetForShow()
        {
            limitActive = false;
            // Vanilla labels the button by localeID and skips re-applying an unchanged id,
            // so any text written on it sticks across opens. Always re-apply the localized
            // label for this mode (also repairs labels left by older builds).
            ApplyVanillaButtonLabel();

            if (buyNowTab != null)
            {
                StyleTab(buyNowTab, true);
                StyleTab(limitTab, false);
            }

            RepairPriceLabel();

            if (priceField != null)
            {
                priceField.text = string.Empty; // re-filled from today's price on Limit
            }

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

        private static void SetLimit(bool limit)
        {
            bool was = limitActive;
            limitActive = limit;
            if (buyNowTab != null)
            {
                StyleTab(buyNowTab, !limit);
                StyleTab(limitTab, limit);
            }

            UIButton button = owner == null ? null : ButtonField.GetValue(owner) as UIButton;
            if (button != null && limit)
            {
                button.text = PlaceLabel;
            }
            else if (!limit && was)
            {
                ApplyVanillaButtonLabel();
            }

            if (!limit && was)
            {
                RestorePriceRow();
            }

            if (
                limit
                && priceField != null
                && string.IsNullOrEmpty(priceField.text)
                && owner != null
            )
            {
                byte id = (byte)IdField.GetValue(owner);
                float mark = EconomyManager.instance.m_investments.m_buffer[id].value;
                priceField.text = Mathf.Max(1, Mathf.FloorToInt(mark)).ToString();
            }

            Relayout();
            Refresh();
        }

        private static void StyleTab(UIButton tab, bool active)
        {
            tab.normalBgSprite = active ? "ButtonMenuPressed" : "ButtonMenu";
            tab.focusedBgSprite = tab.normalBgSprite;
            tab.textColor = active ? (Color32)Color.white : new Color32(170, 180, 190, 255);
        }

        /// <summary>Shows the ticket only in Buy mode, and the price row only for Limit; grows the dialog to fit.</summary>
        private static void Relayout()
        {
            if (dark == null || owner == null)
            {
                return;
            }

            bool buy = IsBuyMode(owner);
            tabs.isVisible = buy;
            pricePanel.isVisible = buy && limitActive;
            float wanted = buy ? tabs.height : 0f;
            if (buy && limitActive)
            {
                // Our estimate row stands in for the vanilla Price row.
                wanted += pricePanel.height;
                if (buyStats != null)
                {
                    buyStats.isVisible = false;
                    wanted -= buyStats.height;
                }
            }
            Grow(wanted - addedHeight);
            addedHeight = wanted;
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
            foreach (string name in new[] { TabsName, PriceName })
            {
                UIComponent stale;
                while ((stale = parent.Find(name)) != null)
                {
                    stale.name = name + " (stale)";
                    UnityEngine.Object.Destroy(stale.gameObject);
                }
            }
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
#endif

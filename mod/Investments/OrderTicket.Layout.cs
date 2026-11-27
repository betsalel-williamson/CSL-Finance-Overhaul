// ColossalFramework.UI: UIButton, UIComponent, UIHorizontalAlignment, UILabel, UIPanel,
//   UITextField
using ColossalFramework.UI;
using FinanceOverhaul.Core; // IpoDialogText, Strings
using UnityEngine; // Mathf, RectOffset, Vector2, Vector3

// Game classes used without a using (global namespace, Assembly-CSharp): BuySellInvestmentPanel

namespace FinanceOverhaul
{
    /// <summary>Order ticket: building the tabs and price field inside the game's Buy and Sell dialog, and keeping the dialog laid out around them.</summary>
    public static partial class OrderTicket
    {
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

            // View headquarters, right of "Current value" (local companies only; see ShowViewHome).
            UIComponent info = root.Find("PanelInvestmentInfo");
            if (info != null)
            {
                UiCleanup.RemoveChildrenNamed(info, ViewHomeName);
                viewHome = FoButton.Add(
                    info,
                    ViewHomeName,
                    IpoDialogText.View,
                    new Vector2(170f, 26f),
                    0.8f
                );
                viewHome.relativePosition = new Vector3(info.width - 7f - 170f, 88f);
                viewHome.isVisible = false;
                viewHome.eventClick += (c, p) => OnViewHome();
            }

            // Buy | Sell switch: the side, then the order types for that side.
            sides = dark.AddUIComponent<UIPanel>();
            sides.name = SidesName;
            sides.size = new Vector2(dark.width, 42f);
            sideTabs = FoToggleTabs.AddEven(
                sides,
                TabPrefix + "Side",
                new[]
                {
                    Strings.Get("investments.buy"),
                    Strings.Get("investments.sell"),
                    Strings.Get("investments.chirps"),
                },
                new Vector2(dark.width - 52f, 34f),
                1f,
                i =>
                {
                    // Chirps keeps the side as it is; Buy or Sell brings the trade back first.
                    if (i == 2)
                    {
                        ShowChirps(true);
                        return;
                    }

                    ShowChirps(false);
                    SwitchSide(i == 0);
                }
            );
            sideTabs.Panel.relativePosition = new Vector3(26f, 6f);
            sideTabs[0].name = TabPrefix + "SideBuy";
            sideTabs[1].name = TabPrefix + "SideSell";
            sideTabs[2].name = TabPrefix + "SideChirps";
            sideTabs[2].tooltip = Strings.Get("chirps.tip");

            // Tabs row above the share amount; one button per (mode, kind).
            tabs = dark.AddUIComponent<UIPanel>();
            tabs.name = TabsName;
            tabs.size = new Vector2(dark.width, 38f);
            var labels = new string[AllTabs.Length];
            var widths = new float[AllTabs.Length];
            for (int i = 0; i < AllTabs.Length; i++)
            {
                labels[i] = AllTabs[i].Label();
                widths[i] = 124f;
            }

            typeTabs = FoToggleTabs.Add(
                tabs,
                TabPrefix + "Types",
                labels,
                widths,
                28f,
                1f,
                i => SetKind(AllTabs[i].Kind)
            );
            typeTabs.Panel.relativePosition = new Vector3(26f, 6f);
            // Under Buy | Sell: the quieter sub-group.
            typeTabs.Secondary();
            for (int i = 0; i < AllTabs.Length; i++)
            {
                Tab tab = AllTabs[i];
                tab.Button = typeTabs[i];
                tab.Button.name = TabPrefix + tab.Key + (tab.Buy ? string.Empty : "Sell");
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
            priceField.numericalOnly = false; // separators are put in for the player; digits only stay
            priceField.maxLength = PriceMaxDigits + 2; // ₡9,999,999 per share: far above any listing
            priceField.selectOnFocus = true;
            priceField.submitOnFocusLost = true;
            priceField.builtinKeyNavigation = true;
            priceField.readOnly = false;
            priceField.horizontalAlignment = UIHorizontalAlignment.Right;
            priceField.padding = new RectOffset(6, 8, 6, 4);
            priceField.normalBgSprite = FoTheme.GameFieldSprite;
            priceField.hoveredBgSprite = "TextFieldPanelHovered";
            priceField.focusedBgSprite = FoTheme.GameFieldSprite;
            priceField.selectionSprite = "EmptySprite";
            priceField.color = FoTheme.Field;
            priceField.textColor = FoTheme.GameText;
            priceField.tooltip = Strings.Get("order.price.tip");
            if (shareLabel != null && shareLabel.font != null)
            {
                priceField.font = shareLabel.font;
            }

            priceGrouping = new FoGroupedField(priceField, PriceMaxDigits);
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
            sides.zOrder = tabs.zOrder;
            pricePanel.zOrder = slider.zOrder + 1;
            BuildChirps();
            BuildInfo(root);
            ModLog.Info("order ticket: attached (buy and sell order types)");
        }

        /// <summary>
        /// A fresh open starts on the vanilla trade. Vanilla labels the button by localeID
        /// and skips re-applying an unchanged id, so always re-apply the localized label
        /// for this mode (also repairs labels left by older builds).
        /// </summary>
        private static void ResetForShow()
        {
            ShowChirps(false); // a fresh open starts on the trade
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
                label = Strings.Get(buy ? "detail.buy" : "detail.sell");
            }

            button.text = label;
        }

        private static void StyleTabs()
        {
            if (owner == null)
            {
                return;
            }

            bool buy = IsBuyMode(owner);
            if (sideTabs != null)
            {
                byte shown = (byte)IdField.GetValue(owner);
                bool canSell = Owned(shown) > 0;
                bool canBuy = Available(shown) > 0;
                sideTabs.Choose(
                    chirpsShown ? 2
                    : buy ? 0
                    : 1
                );
                sideTabs.SetEnabled(0, canBuy || buy, Strings.Get("order.side.buyNone"));
                sideTabs.SetEnabled(1, canSell || !buy, Strings.Get("order.side.sellNone"));
            }

            if (typeTabs == null)
            {
                return;
            }

            int chosen = -1;
            for (int i = 0; i < AllTabs.Length; i++)
            {
                Tab t = AllTabs[i];
                bool shown =
                    t.Buy == buy && (!t.Kind.HasValue || Mod.OrderKindEnabled(t.Kind.Value));
                typeTabs.SetShown(i, shown);
                if (shown && chosen < 0 && t.Kind == active)
                {
                    chosen = i;
                }
            }

            typeTabs.Choose(chosen);
        }

        /// <summary>
        /// Tabs in both modes; the price row only for an order type, where it stands in for
        /// the mode's vanilla stats rows. Grows the dialog to fit.
        /// </summary>
        private static void Relayout()
        {
            // While the chirps show, the trade stays folded away (Buy or Sell lays it out again).
            if (dark == null || owner == null || chirpsShown)
            {
                return;
            }

            bool buy = IsBuyMode(owner);
            UIComponent stats = buy ? buyStats : sellStats;
            // With every order type of this mode switched off there is nothing to choose:
            // no tabs row, the dialog is the vanilla one.
            tabs.isVisible = AnyOrderTab(buy);
            pricePanel.isVisible = active.HasValue;
            float wanted =
                (tabs.isVisible ? tabs.height : 0f) + (sides == null ? 0f : sides.height);
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
            CenterDialog();
        }

        /// <summary>
        /// The dialog grows with what the mod adds (stats, tabs, price fields): kept centred on
        /// the screen, as the game centres it when it opens, so it never runs off the bottom.
        /// </summary>
        private static void CenterDialog()
        {
            if (owner != null && owner.component != null && owner.component.isVisible)
            {
                owner.component.CenterToParent();
            }
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
            UiCleanup.RemoveChildrenNamed(parent, TabsName, SidesName, PriceName, ChirpsName);
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
    }
}

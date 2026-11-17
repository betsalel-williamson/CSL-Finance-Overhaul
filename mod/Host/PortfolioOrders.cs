using System;
using System.Collections.Generic;
using System.Text;
using ColossalFramework.UI;
using FinanceOverhaul.Market;
using UnityEngine;

namespace FinanceOverhaul
{
    /// <summary>
    /// Open orders as entries of Economy → Investments → Your Portfolio (Figma band 09,
    /// pins 6–7). Each order is a card in the scrolling list, placed right under its
    /// stock's holding, or in that stock's alphabetical place when not held (vanilla's own
    /// order). Vanilla re-sorts only its template items and renumbers them from 0, so
    /// <see cref="Arrange"/> runs right after that sort (postfix on
    /// EconomyPanel.UpdateInvestments). Cards are rebuilt only when orders or days left
    /// change. Lives on the overlay root (hot reload removes it); stale cards are removed
    /// by name.
    /// </summary>
    public class PortfolioOrders : MonoBehaviour
    {
        public const string RowPrefix = "FinanceOverhaulOpenOrderRow-";
        public const string TextPrefix = "FinanceOverhaulOpenOrderText-";
        public const string DetailPrefix = "FinanceOverhaulOpenOrderDetail-";
        public const string SidePrefix = "FinanceOverhaulOpenOrderSide-";
        public const string CancelPrefix = "FinanceOverhaulOpenOrderCancel-";

        // An earlier build drew a header above the list and shifted the list down.
        private const string LegacySectionName = "FinanceOverhaulOpenOrders";

        private const float CardHeight = 92f;
        private const float DetailLeft = 24f;
        private const float DetailHeight = 18f;
        private const float DetailGap = 8f;
        private const float DetailMinScale = 0.6f;
        private const float CancelRight = 10f;
        private static readonly Color32 BuyColor = new Color32(78, 205, 96, 255);
        private static readonly Color32 MutedColor = new Color32(160, 172, 182, 255);
        private static readonly Color32 SellColor = new Color32(233, 150, 66, 255);

        private EconomyPanel economy;
        private string shownSignature;
        private string shownIds;
        private float nextCheck;

        private void Update()
        {
            if (Time.unscaledTime < nextCheck)
            {
                return;
            }

            nextCheck = Time.unscaledTime + 0.5f;
            try
            {
                if (economy == null)
                {
                    economy = UnityEngine.Object.FindObjectOfType<EconomyPanel>();
                }

                if (economy == null || !economy.isInvestmentOpen)
                {
                    return;
                }

                UIComponent list = economy.component.Find("OwnedInvestmentsScrollablePanel");
                if (list == null)
                {
                    return;
                }

                TradeOrder[] orders = OrderDesk.Snapshot();
                string signature = Signature(orders, OrderDesk.Today);
                if (signature == shownSignature && CountCards(list) == orders.Length)
                {
                    return;
                }

                // Same orders, new text (days left, a trailing stop's trigger): rewrite the
                // cards where they are. Removing and re-adding them would shorten the list for
                // a moment and move a list that is scrolled to its end.
                string ids = Ids(orders);
                if (
                    ids != shownIds
                    || CountCards(list) != orders.Length
                    || !Refresh(list, orders, OrderDesk.Today)
                )
                {
                    Rebuild(list, orders, OrderDesk.Today);
                }

                shownSignature = signature;
                shownIds = ids;
            }
            catch (Exception ex)
            {
                ModLog.Warn("portfolio orders: update failed: " + ex.Message);
                nextCheck = Time.unscaledTime + 10f;
            }
        }

        /// <summary>
        /// Postfix target: EconomyPanel.UpdateInvestments. Vanilla has just sorted its
        /// holdings (renumbering them first); put each order card back in its place.
        /// </summary>
        public static void Arrange(EconomyPanel panel)
        {
            UIComponent list =
                panel == null ? null : panel.component.Find("OwnedInvestmentsScrollablePanel");
            if (list == null || CountCards(list) == 0)
            {
                return;
            }

            var items = new List<UIComponent>();
            var cards = new List<UIComponent>();
            foreach (UIComponent c in list.components)
            {
                if (c != null)
                {
                    (IsCard(c) ? cards : items).Add(c);
                }
            }

            var held = new HashSet<byte>();
            foreach (UIComponent item in items)
            {
                if (item.objectUserData is byte)
                {
                    held.Add((byte)item.objectUserData);
                }
            }

            cards.Sort((x, y) => CardId(y).CompareTo(CardId(x))); // newest first
            var placed = new HashSet<UIComponent>();
            var order = new List<UIComponent>(list.components.Count);
            foreach (UIComponent item in items)
            {
                string itemName = InvestmentName(item);
                // Orders for stocks not held go where vanilla would list that stock.
                foreach (UIComponent card in cards)
                {
                    if (
                        !placed.Contains(card)
                        && !held.Contains(CardInvestment(card))
                        && itemName != null
                        && string.Compare(InvestmentName(card), itemName) < 0
                    )
                    {
                        order.Add(card);
                        placed.Add(card);
                    }
                }

                order.Add(item);
                // Orders for this holding go right under it.
                if (item.objectUserData is byte)
                {
                    foreach (UIComponent card in cards)
                    {
                        if (
                            !placed.Contains(card)
                            && CardInvestment(card) == (byte)item.objectUserData
                        )
                        {
                            order.Add(card);
                            placed.Add(card);
                        }
                    }
                }
            }

            foreach (UIComponent card in cards)
            {
                if (!placed.Contains(card))
                {
                    order.Add(card);
                }
            }

            for (int i = 0; i < order.Count; i++)
            {
                if (order[i].zOrder != i)
                {
                    order[i].zOrder = i;
                }
            }
        }

        /// <summary>
        /// Orders were placed, filled or cancelled: build the cards again. Removing them
        /// shortens the list, and the scrolling panel then clamps a position near the end;
        /// the cards come back in the same pass, so the position from before is put back
        /// (clamped to the new length when orders really went away).
        /// </summary>
        private void Rebuild(UIComponent list, TradeOrder[] orders, int today)
        {
            var scroller = list as UIScrollablePanel;
            Vector2 scrollBefore = scroller != null ? scroller.scrollPosition : Vector2.zero;
            try
            {
                RebuildCards(list, orders, today);
            }
            finally
            {
                if (scroller != null && scroller.scrollPosition != scrollBefore)
                {
                    scroller.scrollPosition = scrollBefore;
                }
            }
        }

        private void RebuildCards(UIComponent list, TradeOrder[] orders, int today)
        {
            UiCleanup.RemoveChildrenWhere(list, IsCard);
            RestoreLegacyLayout(list);
            if (orders.Length == 0)
            {
                return;
            }

            UILabel titleStyle = list.Find<UILabel>("InvestmentName");
            UILabel bodyStyle = list.Find<UILabel>("YouInvestedLabel");
            UIButton sellStyle = list.Find<UIButton>("Sell");
            UIPanel backgroundStyle = list.Find<UIPanel>("Background");
            foreach (TradeOrder order in orders)
            {
                int id = FindInvestment(order.InvestmentKey);
                if (id >= 0)
                {
                    AddCard(
                        list,
                        order,
                        (byte)id,
                        today,
                        titleStyle,
                        bodyStyle,
                        sellStyle,
                        backgroundStyle
                    );
                }
            }

            Arrange(economy);
        }

        private static void AddCard(
            UIComponent list,
            TradeOrder order,
            byte investmentId,
            int today,
            UILabel titleStyle,
            UILabel bodyStyle,
            UIButton sellStyle,
            UIPanel backgroundStyle
        )
        {
            UIPanel card = list.AddUIComponent<UIPanel>();
            card.name = RowPrefix + order.Id;
            card.objectUserData = investmentId;
            card.size = new Vector2(list.width, CardHeight);
            card.backgroundSprite =
                backgroundStyle != null ? backgroundStyle.backgroundSprite : "GenericPanel";
            card.color =
                backgroundStyle != null ? backgroundStyle.color : new Color32(32, 38, 44, 255);

            Investment[] buffer = EconomyManager.instance.m_investments.m_buffer;
            string name = buffer[investmentId].Info.m_investmentAI.GetLocalizedName();

            UILabel title = card.AddUIComponent<UILabel>();
            title.name = TextPrefix + order.Id;
            Style(title, titleStyle, 0.9f);
            title.autoSize = false;
            title.width = card.width - 32f;
            title.height = 20f;
            title.textAlignment = UIHorizontalAlignment.Center;
            title.relativePosition = new Vector3(16f, 10f);
            title.text = name;

            UILabel side = card.AddUIComponent<UILabel>();
            Style(side, bodyStyle, 1f);
            side.name = SidePrefix + order.Id;
            side.relativePosition = new Vector3(24f, 38f);

            UILabel detail = card.AddUIComponent<UILabel>();
            detail.name = DetailPrefix + order.Id;
            detail.textColor = MutedColor;
            detail.relativePosition = new Vector3(DetailLeft, 60f);

            UIButton cancel = card.AddUIComponent<UIButton>();
            cancel.name = CancelPrefix + order.Id;
            cancel.text = "Cancel";
            cancel.size =
                sellStyle != null
                    ? new Vector2(Mathf.Max(70f, sellStyle.width + 16f), sellStyle.height)
                    : new Vector2(76f, 28f);
            Fill(card, side, detail, cancel, order, today, bodyStyle);
            if (sellStyle != null)
            {
                cancel.normalBgSprite = sellStyle.normalBgSprite;
                cancel.hoveredBgSprite = sellStyle.hoveredBgSprite;
                cancel.pressedBgSprite = sellStyle.pressedBgSprite;
                cancel.focusedBgSprite = sellStyle.normalBgSprite;
                cancel.font = sellStyle.font;
                cancel.textScale = sellStyle.textScale;
            }
            else
            {
                cancel.normalBgSprite = "ButtonMenu";
                cancel.hoveredBgSprite = "ButtonMenuHovered";
                cancel.pressedBgSprite = "ButtonMenuPressed";
            }

            int orderId = order.Id;
            cancel.eventClick += (c, p) => OrderDesk.Cancel(orderId);
        }

        /// <summary>Everything on a card that changes while its order stays open.</summary>
        private static void Fill(
            UIPanel card,
            UILabel side,
            UILabel detail,
            UIButton cancel,
            TradeOrder order,
            int today,
            UILabel bodyStyle
        )
        {
            bool paused = !Mod.OrderKindEnabled(order.Kind);
            side.textColor =
                paused ? MutedColor
                : order.IsSell ? SellColor
                : BuyColor;
            side.text = OrderText.CardSide(order.Kind, paused);

            // Up to the Cancel button: the whole line, scaled down or wrapped, never cut off.
            // The fit starts from the normal scale every time, not from the last fit.
            Style(detail, bodyStyle, 0.9f);
            FitDetail(
                detail,
                OrderText.CardDetail(order, today),
                card.width - cancel.width - CancelRight - DetailGap - DetailLeft
            );
            float height = CardHeight + Mathf.Max(0f, detail.height - DetailHeight);
            if (card.height != height)
            {
                card.height = height;
            }

            var at = new Vector3(
                card.width - cancel.width - CancelRight,
                card.height - cancel.height - 10f
            );
            if (cancel.relativePosition != at)
            {
                cancel.relativePosition = at;
            }
        }

        /// <summary>
        /// Rewrites every card in place. False when a card or one of its parts is missing
        /// (the caller then rebuilds).
        /// </summary>
        private static bool Refresh(UIComponent list, TradeOrder[] orders, int today)
        {
            UILabel bodyStyle = list.Find<UILabel>("YouInvestedLabel");
            foreach (TradeOrder order in orders)
            {
                var card = Named(list, RowPrefix + order.Id) as UIPanel;
                if (card == null)
                {
                    return false;
                }

                var side = Named(card, SidePrefix + order.Id) as UILabel;
                var detail = Named(card, DetailPrefix + order.Id) as UILabel;
                var cancel = Named(card, CancelPrefix + order.Id) as UIButton;
                if (side == null || detail == null || cancel == null)
                {
                    return false;
                }

                Fill(card, side, detail, cancel, order, today, bodyStyle);
            }

            return true;
        }

        /// <summary>A direct child by its current name (Find matches the name at creation).</summary>
        private static UIComponent Named(UIComponent parent, string name)
        {
            foreach (UIComponent c in parent.components)
            {
                if (c != null && c.name == name)
                {
                    return c;
                }
            }

            return null;
        }

        private static string Ids(TradeOrder[] orders)
        {
            var sb = new StringBuilder();
            foreach (TradeOrder o in orders)
            {
                sb.Append(o.Id).Append(';');
            }

            return sb.ToString();
        }

        private static bool IsCard(UIComponent c)
        {
            return c != null
                && c.name != null
                && c.name.StartsWith(RowPrefix, StringComparison.Ordinal);
        }

        private static int CountCards(UIComponent list)
        {
            int n = 0;
            foreach (UIComponent c in list.components)
            {
                if (IsCard(c))
                {
                    n++;
                }
            }

            return n;
        }

        private static int CardId(UIComponent card)
        {
            int id;
            return int.TryParse(card.name.Substring(RowPrefix.Length), out id) ? id : 0;
        }

        private static byte CardInvestment(UIComponent card)
        {
            return card.objectUserData is byte ? (byte)card.objectUserData : (byte)0;
        }

        /// <summary>The InvestmentInfo name vanilla sorts by.</summary>
        private static string InvestmentName(UIComponent c)
        {
            if (!(c.objectUserData is byte))
            {
                return null;
            }

            InvestmentInfo info = EconomyManager
                .instance
                .m_investments
                .m_buffer[(byte)c.objectUserData]
                .Info;
            return info == null ? null : info.m_name;
        }

        private static int FindInvestment(string key)
        {
            Investment[] buffer = EconomyManager.instance.m_investments.m_buffer;
            for (int i = 0; i < buffer.Length; i++)
            {
                InvestmentInfo info = buffer[i].Info;
                if (
                    (buffer[i].m_flags & Investment.Flags.Created) != 0
                    && info != null
                    && info.name == key
                )
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// One line in <paramref name="width"/>: scaled down when it nearly fits (like the
        /// Stock Monitor's cells, never below <see cref="DetailMinScale"/>), otherwise wrapped
        /// at its normal scale so the card grows instead of cutting text off.
        /// </summary>
        private static void FitDetail(UILabel label, string text, float width)
        {
            float baseScale = label.textScale;
            label.autoHeight = false;
            label.wordWrap = false;
            label.text = text;
            LabelFit.Result fit = LabelFit.SingleLine(
                baseScale,
                DetailMinScale,
                width,
                scale =>
                {
                    label.autoSize = false;
                    label.textScale = scale;
                    label.autoSize = true;
                    return label.width;
                }
            );
            label.autoSize = false;
            label.textScale = fit.Scale;
            if (fit.Wrap)
            {
                label.wordWrap = true;
                label.width = width;
                label.autoHeight = true;
                label.height = Mathf.Max(DetailHeight, label.height);
            }
            else
            {
                label.size = new Vector2(width, DetailHeight);
                // A scaled-down line stays centred on the row's normal line.
                label.verticalAlignment =
                    fit.Scale < baseScale ? UIVerticalAlignment.Middle : UIVerticalAlignment.Top;
            }
        }

        private static void Style(UILabel label, UILabel from, float scale)
        {
            if (from != null)
            {
                label.font = from.font;
                label.textScale = from.textScale * scale;
            }
        }

        private static string Signature(TradeOrder[] orders, int today)
        {
            var sb = new StringBuilder();
            foreach (TradeOrder o in orders)
            {
                // Anything the card shows: days left, today's trigger (a trailing stop rises),
                // and whether its type is paused in Options.
                sb.Append(o.Id)
                    .Append(':')
                    .Append(o.ExpiresDay - today)
                    .Append(':')
                    .Append(o.TriggerPrice)
                    .Append(Mod.OrderKindEnabled(o.Kind) ? string.Empty : ":paused")
                    .Append(';');
            }

            return sb.ToString();
        }

        /// <summary>Vanilla's list fills Your Portfolio from the top; undo the earlier header layout.</summary>
        private static void RestoreLegacyLayout(UIComponent list)
        {
            UIComponent panel = list.parent;
            if (panel == null)
            {
                return;
            }

            UiCleanup.RemoveChildrenNamed(panel, LegacySectionName);
            UiCleanup.RemoveChildrenNamed(list, LegacySectionName);
            UIComponent bar = panel.Find("Scrollbar");
            foreach (UIComponent c in new[] { list, bar })
            {
                if (c != null && (c.relativePosition.y != 0f || c.height != panel.height))
                {
                    c.relativePosition = new Vector3(c.relativePosition.x, 0f);
                    c.height = panel.height;
                }
            }
        }

        private void OnDestroy()
        {
            try
            {
                UIComponent list =
                    economy == null
                        ? null
                        : economy.component.Find("OwnedInvestmentsScrollablePanel");
                if (list != null)
                {
                    UiCleanup.RemoveChildrenWhere(list, IsCard);
                    RestoreLegacyLayout(list);
                }

                PortfolioAvgCost.Remove(economy);
            }
            catch
            {
                // fail soft
            }
        }
    }
}

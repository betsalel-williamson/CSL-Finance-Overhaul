using System;
using System.Collections.Generic;
using ColossalFramework.UI;
using FinanceOverhaul.Core;
using UnityEngine;

namespace FinanceOverhaul
{
    /// <summary>
    /// "Avg cost / share" on every holding card in Your Portfolio: the game's own average
    /// cost basis (You invested ÷ shares, see <see cref="CostBasis"/>). A fifth row in the
    /// card's LayoutPanel, styled like Shares; the label is right-aligned in a fixed box so
    /// placement never depends on a measured width. Refreshed right after vanilla updates
    /// the cards (postfix on EconomyPanel.UpdateInvestments). Vanilla reuses cards for
    /// other stocks, so every pass sets name and text again. Save-neutral.
    /// </summary>
    public static class PortfolioAvgCost
    {
        public const string RowPrefix = "FinanceOverhaulAvgCost-";
        public const string ValuePrefix = "FinanceOverhaulAvgCostValue-";

        // Vanilla rows end their label at x 212 and start the value at x 220.
        private const float LabelRight = 212f;
        private const float LabelBox = 200f;
        private const float ValueX = 220f;

        public static void Apply(EconomyPanel panel)
        {
            UIComponent list = OwnedList(panel);
            if (list == null)
            {
                return;
            }

            Investment[] buffer = EconomyManager.instance.m_investments.m_buffer;
            foreach (UIComponent item in list.components)
            {
                if (item == null || !(item.objectUserData is byte) || IsOrderCard(item))
                {
                    continue;
                }

                UIComponent layout = Child(item, "LayoutPanel");
                if (layout == null)
                {
                    continue;
                }

                byte id = (byte)item.objectUserData;
                InvestmentInfo info = buffer[id].Info;
                if (info == null || info.m_investmentAI == null)
                {
                    continue;
                }

                uint owned = info.m_investmentAI.OwnedCount(ref buffer[id]);
                string text = CostBasis.FormatAverage(
                    buffer[id].m_playerSpentMoney,
                    owned,
                    ColossalFramework.Globalization.LocaleManager.cultureInfo
                );
                UIPanel row = RowIn(layout);
                if (text == null)
                {
                    if (row != null)
                    {
                        row.isVisible = false;
                    }

                    continue;
                }

                if (row == null)
                {
                    row = CreateRow(layout);
                }

                string symbol = StockSymbols.For(info);
                row.name = RowPrefix + symbol;
                row.isVisible = true;
                UILabel value = row.components.Count > 1 ? row.components[1] as UILabel : null;
                if (value != null)
                {
                    value.name = ValuePrefix + symbol;
                    value.text = text;
                }
            }
        }

        /// <summary>Hot reload / disable: take the rows out of every card.</summary>
        public static void Remove(EconomyPanel panel)
        {
            UIComponent list = OwnedList(panel);
            if (list == null)
            {
                return;
            }

            foreach (UIComponent item in list.components)
            {
                UIComponent layout = item == null ? null : Child(item, "LayoutPanel");
                if (layout != null)
                {
                    UiCleanup.RemoveChildrenWhere(layout, IsRow);
                }
            }
        }

        private static UIPanel CreateRow(UIComponent layout)
        {
            UILabel labelStyle = layout.Find<UILabel>("ShareCountLabel");
            UILabel valueStyle = layout.Find<UILabel>("YouInvestedNumber");
            UIPanel row = layout.AddUIComponent<UIPanel>();
            row.size = new Vector2(layout.width, 17f);

            UILabel label = row.AddUIComponent<UILabel>();
            Copy(labelStyle, label);
            label.autoSize = false;
            label.size = new Vector2(LabelBox, 17f);
            label.textAlignment = UIHorizontalAlignment.Right;
            label.relativePosition = new Vector3(LabelRight - LabelBox, 0f);
            label.text = Strings.Get("portfolio.avgCost");

            UILabel value = row.AddUIComponent<UILabel>();
            Copy(valueStyle, value);
            value.autoSize = true;
            value.relativePosition = new Vector3(ValueX, 0f);

            var stack = layout as UIPanel;
            if (stack == null || !stack.autoLayout)
            {
                // Vanilla rows sit 23 px apart: Invested, Shares, Current value, Gain/Loss.
                row.relativePosition = new Vector3(0f, 92f);
            }

            return row;
        }

        private static void Copy(UILabel from, UILabel to)
        {
            if (from == null)
            {
                return;
            }

            to.font = from.font;
            to.textScale = from.textScale;
            to.textColor = from.textColor;
        }

        private static UIPanel RowIn(UIComponent layout)
        {
            UIPanel first = null;
            var extra = new List<UIComponent>();
            foreach (UIComponent c in layout.components)
            {
                if (!IsRow(c))
                {
                    continue;
                }

                if (first == null)
                {
                    first = c as UIPanel;
                }
                else
                {
                    extra.Add(c); // left by an older assembly
                }
            }

            foreach (UIComponent c in extra)
            {
                layout.RemoveUIComponent(c);
                UnityEngine.Object.Destroy(c.gameObject);
            }

            return first;
        }

        private static bool IsRow(UIComponent c)
        {
            return c != null
                && c.name != null
                && c.name.StartsWith(RowPrefix, StringComparison.Ordinal);
        }

        private static bool IsOrderCard(UIComponent c)
        {
            return c.name != null
                && c.name.StartsWith(PortfolioOrders.RowPrefix, StringComparison.Ordinal);
        }

        private static UIComponent Child(UIComponent parent, string name)
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

        private static UIComponent OwnedList(EconomyPanel panel)
        {
            return panel == null ? null : panel.component.Find("OwnedInvestmentsScrollablePanel");
        }
    }
}

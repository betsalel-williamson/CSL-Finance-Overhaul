using System; // Array
using System.Collections.Generic; // HashSet, KeyValuePair, List
using ColossalFramework.UI; // UIButton, UITextureSprite, UIView
// FinanceOverhaul.Core: InvestmentFilter, InvestmentList, InvestmentRow, InvestmentRowKind,
//   StockMonitor, TradeOrder
using FinanceOverhaul.Core;
using UnityEngine; // Mathf, Vector2

namespace FinanceOverhaul
{
    /// <summary>The list's data: reading the quotes, hover, scrolling, the filter and filling the pool.</summary>
    public partial class InvestmentsList
    {
        // ---------- data ----------

        private void Read()
        {
            pillsInCurrency = InCurrency;
            quotes = MarketQuotes.Read(true);
            List<InvestmentRow> next = InvestmentList.Rows(
                quotes,
                filter,
                sort,
                reversed,
                grouped,
                new List<TradeOrder>(OrderDesk.Snapshot()),
                InCurrency
            );
            offset = InvestmentList.KeepInView(rows, offset, next, view.height);
            rows = next;
            tops = InvestmentList.Tops(rows);
            foreach (KeyValuePair<InvestmentFilter, UIButton> tab in tabs)
            {
                tab.Value.text = InvestmentList.FilterLabel(tab.Key, quotes);
            }

            tabBar.Choose(Array.IndexOf(InvestmentList.Filters, filter));

            sortLabel.text = StockMonitor.SortLabel(sort, InCurrency);
            groupButton.text = InvestmentList.GroupLabel(grouped);
            FoButton.SetSelected(groupButton, grouped);
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
            if (overview != null)
            {
                overview.Refresh(quotes);
            }
        }

        private Row hovered;
        private string hoveredKey;

        /// <summary>
        /// Over a stock's 30-day line the game's stock tooltip opens with its large chart, as
        /// over the game's own cards; it closes when the pointer leaves. Reads the pointer each
        /// frame (<see cref="FoPointer"/>, which game UI tests can stand in for).
        /// </summary>
        private void Hover()
        {
            // A dialog over the panel (the game's Buy / Sell, any modal) owns the mouse: nothing
            // behind it is outlined or opens its chart.
            if (UIView.GetModalComponent() != null)
            {
                foreach (Row r in pool)
                {
                    r.Outline(false);
                }

                if (hovered != null)
                {
                    hovered = null;
                    Row.HideChart();
                }

                return;
            }

            Vector2 at = FoPointer.Position;
            Row over = null;
            foreach (Row r in pool)
            {
                // The stock under the pointer is outlined: a click on it opens the dialog.
                r.Outline(r.OverStock(at, view));
                if (r.OverLine(at, view))
                {
                    over = r;
                }
            }

            // The same pooled line can show another stock after a scroll: compare the stock too.
            string overKey = over == null ? null : over.Key;
            if (over == hovered && overKey == hoveredKey)
            {
                return;
            }

            hovered = over;
            hoveredKey = overKey;
            if (over == null)
            {
                Row.HideChart();
            }
            else
            {
                over.ShowChart();
            }
        }

        /// <summary>Scrolls so that stock's line is at the top (game UI tests); false if not listed.</summary>
        internal bool ScrollTo(string symbol)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Kind == InvestmentRowKind.Stock && rows[i].Key == symbol)
                {
                    ScrollBy(tops[i] - offset);
                    return true;
                }
            }

            return false;
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
            Top();
        }

        /// <summary>A new tab, grouping or order starts from the top of the list.</summary>
        private void Top()
        {
            offset = 0f;
            rows = new List<InvestmentRow>();
            Read();
        }

        private void Fill()
        {
            int first = InvestmentList.FirstVisible(tops, offset);
            var shown = new HashSet<UITextureSprite>();
            for (int i = 0; i < pool.Count; i++)
            {
                int k = first + i;
                bool inView = k < rows.Count && tops[k] - offset < view.height;
                pool[i].Show(inView ? rows[k] : null, rows, inView ? tops[k] - offset : 0f);
                if (pool[i].Spark != null)
                {
                    shown.Add(pool[i].Spark);
                }
            }

            // A stock scrolled out of view (or filtered out) hides its line.
            foreach (UITextureSprite sprite in sparkSprites.Values)
            {
                if (sprite != null && !shown.Contains(sprite))
                {
                    sprite.isVisible = false;
                }
            }
        }
    }
}

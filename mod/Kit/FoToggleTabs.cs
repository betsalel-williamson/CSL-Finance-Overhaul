using System; // Action
using System.Collections.Generic; // List
using ColossalFramework.UI; // UIButton, UIComponent, UIPanel
using UnityEngine; // Mathf, Vector2, Vector3

namespace FinanceOverhaul
{
    /// <summary>
    /// A tab group (Figma: FO/ToggleTabs): a one-of-several choice drawn exactly as the game's own
    /// tab strip at the top of the Economy panel (Taxes · Budget · Loans · Investments): tabs
    /// joined edge to edge in the game's tab art, white text; the chosen tab lit with dark text;
    /// the game's hover and disabled looks. For a choice (Buy | Sell, order types, filters, a chart
    /// range, a bond term), never for actions.
    /// Segments can be hidden (the bar closes up) and relabelled. Knows no feature: the caller
    /// gives the labels and widths and hears which segment was picked.
    /// </summary>
    internal sealed class FoToggleTabs
    {
        private const string Normal = "GenericTab"; // not shown: a game sprite name
        private const string ChosenSprite = "GenericTabFocused"; // not shown: a game sprite name

        private readonly List<UIButton> tabs = new List<UIButton>();
        private readonly Action<int> picked;

        /// <summary>The bar; its width follows the shown segments.</summary>
        public UIPanel Panel { get; private set; }

        /// <summary>The chosen segment's index (-1: none).</summary>
        public int Chosen { get; private set; }

        public int Count
        {
            get { return tabs.Count; }
        }

        private FoToggleTabs(Action<int> picked)
        {
            this.picked = picked;
            Chosen = -1;
        }

        /// <summary>Segments sharing <paramref name="size"/>'s width evenly; the first chosen.</summary>
        public static FoToggleTabs AddEven(
            UIComponent parent,
            string name,
            string[] labels,
            Vector2 size,
            float textScale,
            Action<int> picked
        )
        {
            var widths = new float[labels.Length];
            float w = Mathf.Floor(size.x / labels.Length);
            for (int i = 0; i < labels.Length; i++)
            {
                // The last takes what the rounding left, so the bar ends flush.
                widths[i] = i == labels.Length - 1 ? size.x - w * i : w;
            }

            return Add(parent, name, labels, widths, size.y, textScale, picked);
        }

        /// <summary>
        /// Segments of the given widths, named <c>name + index</c> (rename through the indexer);
        /// the first chosen. <paramref name="picked"/> hears a click on a segment that was not
        /// chosen, after it is shown chosen.
        /// </summary>
        public static FoToggleTabs Add(
            UIComponent parent,
            string name,
            string[] labels,
            float[] widths,
            float height,
            float textScale,
            Action<int> picked
        )
        {
            var t = new FoToggleTabs(picked);
            t.Panel = parent.AddUIComponent<UIPanel>();
            t.Panel.name = name;
            t.Panel.height = height;
            for (int i = 0; i < labels.Length; i++)
            {
                int index = i;
                UIButton b = t.Panel.AddUIComponent<UIButton>();
                b.name = name + i;
                b.text = labels[i];
                b.textScale = textScale;
                b.size = new Vector2(widths[i], height);
                b.hoveredBgSprite = "GenericTabHovered"; // not shown: a game sprite name
                b.pressedBgSprite = "GenericTabPressed"; // not shown: a game sprite name
                b.disabledBgSprite = "GenericTabDisabled"; // not shown: a game sprite name
                b.disabledTextColor = FoTheme.GameTabDisabledText;
                b.eventClick += (c, p) => t.Pick(index);
                t.tabs.Add(b);
            }

            t.Layout();
            t.Choose(0);
            return t;
        }

        public UIButton this[int index]
        {
            get { return tabs[index]; }
        }

        /// <summary>Shows that segment chosen (no event); -1 chooses none.</summary>
        public void Choose(int index)
        {
            Chosen = index;
            for (int i = 0; i < tabs.Count; i++)
            {
                bool on = i == index;
                tabs[i].normalBgSprite = on ? ChosenSprite : Normal;
                tabs[i].focusedBgSprite = tabs[i].normalBgSprite;
                // As the Economy panel's tabs: white, and dark on the lit chosen tab.
                tabs[i].textColor = on ? FoTheme.GameTabChosenText : FoTheme.GameText;
                tabs[i].hoveredTextColor = on ? FoTheme.GameTabChosenText : FoTheme.GameText;
                tabs[i].focusedTextColor = tabs[i].textColor;
                tabs[i].pressedTextColor = tabs[i].textColor;
            }
        }

        /// <summary>A segment that cannot be chosen now, and the tooltip that says why.</summary>
        public void SetEnabled(int index, bool enabled, string whyNot)
        {
            tabs[index].isEnabled = enabled;
            tabs[index].tooltip = enabled ? string.Empty : whyNot ?? string.Empty;
        }

        /// <summary>Shows or hides a segment; the bar closes up around it.</summary>
        public void SetShown(int index, bool shown)
        {
            if (tabs[index].isVisibleSelf != shown)
            {
                tabs[index].isVisible = shown;
                Layout();
            }
        }

        public void SetLabel(int index, string text)
        {
            tabs[index].text = text;
        }

        /// <summary>Shown segments side by side with no gap; the bar as wide as they are.</summary>
        private void Layout()
        {
            float x = 0f;
            foreach (UIButton b in tabs)
            {
                // Its own flag: a bar built inside a hidden panel still lays out.
                if (!b.isVisibleSelf)
                {
                    continue;
                }

                b.relativePosition = new Vector3(x, 0f);
                x += b.width;
            }

            Panel.width = Mathf.Max(1f, x);
        }

        private void Pick(int index)
        {
            if (index == Chosen || !tabs[index].isEnabled)
            {
                return;
            }

            Choose(index);
            if (picked != null)
            {
                picked(index);
            }
        }
    }
}

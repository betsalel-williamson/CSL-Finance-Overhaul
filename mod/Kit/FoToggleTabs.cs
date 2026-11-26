using System; // Action
using System.Collections.Generic; // List
using ColossalFramework.UI; // UIButton, UIComponent, UIPanel
using UnityEngine; // Mathf, Vector2, Vector3

namespace FinanceOverhaul
{
    /// <summary>
    /// Toggle tabs (Figma: FO/ToggleTabs): two or more equal tabs joined edge to edge across a
    /// strip, exactly one chosen, in the art of the game's own tab strips (the Economy panel's
    /// Taxes · Budget · Loans · Investments): the chosen tab lit, the others dark, a hover and a
    /// disabled look. For a choice that changes what the panel under it shows (Buy | Sell), not
    /// for actions. Knows no feature: the caller gives the labels and is told which was picked.
    /// </summary>
    internal sealed class FoToggleTabs
    {
        private const string Tab = "GenericTab"; // not shown: a game sprite name
        private const string TabChosen = "GenericTabFocused"; // not shown: a game sprite name

        private readonly List<UIButton> tabs = new List<UIButton>();
        private readonly Action<int> picked;

        /// <summary>The strip; every tab is named <c>name + index</c> unless renamed.</summary>
        public UIPanel Panel { get; private set; }

        /// <summary>The chosen tab's index.</summary>
        public int Chosen { get; private set; }

        private FoToggleTabs(Action<int> picked)
        {
            this.picked = picked;
        }

        /// <summary>
        /// Adds the strip to <paramref name="parent"/>: <paramref name="labels"/> share its width
        /// evenly; the first is chosen. <paramref name="picked"/> hears a click on a tab that was
        /// not chosen (after it is shown chosen).
        /// </summary>
        public static FoToggleTabs Add(
            UIComponent parent,
            string name,
            string[] labels,
            Vector2 size,
            float textScale,
            Action<int> picked
        )
        {
            var t = new FoToggleTabs(picked);
            t.Panel = parent.AddUIComponent<UIPanel>();
            t.Panel.name = name;
            t.Panel.size = size;
            float w = Mathf.Floor(size.x / labels.Length);
            for (int i = 0; i < labels.Length; i++)
            {
                int index = i;
                UIButton b = t.Panel.AddUIComponent<UIButton>();
                b.name = name + i;
                b.text = labels[i];
                b.textScale = textScale;
                // The last tab takes what the rounding left, so the strip ends flush.
                b.size = new Vector2(i == labels.Length - 1 ? size.x - w * i : w, size.y);
                b.relativePosition = new Vector3(w * i, 0f);
                b.hoveredBgSprite = "GenericTabHovered"; // not shown: a game sprite name
                b.pressedBgSprite = "GenericTabPressed"; // not shown: a game sprite name
                b.disabledBgSprite = "GenericTabDisabled"; // not shown: a game sprite name
                b.textColor = FoTheme.GameText;
                b.hoveredTextColor = FoTheme.GameText;
                b.pressedTextColor = FoTheme.GameText;
                b.focusedTextColor = FoTheme.GameText;
                b.disabledTextColor = FoTheme.GameDisabledText;
                b.eventClick += (c, p) => t.Pick(index);
                t.tabs.Add(b);
            }

            t.Choose(0);
            return t;
        }

        public UIButton this[int index]
        {
            get { return tabs[index]; }
        }

        /// <summary>Shows that tab chosen (no event).</summary>
        public void Choose(int index)
        {
            Chosen = index;
            for (int i = 0; i < tabs.Count; i++)
            {
                tabs[i].normalBgSprite = i == index ? TabChosen : Tab;
                tabs[i].focusedBgSprite = tabs[i].normalBgSprite;
            }
        }

        /// <summary>A tab that cannot be chosen now, and the tooltip that says why.</summary>
        public void SetEnabled(int index, bool enabled, string whyNot)
        {
            tabs[index].isEnabled = enabled;
            tabs[index].tooltip = enabled ? string.Empty : whyNot ?? string.Empty;
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

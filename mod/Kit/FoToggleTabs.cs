using System; // Action
using System.Collections.Generic; // List
using ColossalFramework.UI; // UIButton, UIComponent, UIPanel, UITabstrip
using UnityEngine; // Mathf, RectOffset, Vector2, Vector3

namespace FinanceOverhaul
{
    /// <summary>
    /// A tab group (Figma: FO/ToggleTabs): a one-of-several choice drawn and behaving exactly as
    /// the game's own tab strips (the Economy panel's Taxes · Budget · Loans · Investments, the
    /// policy tabs): it is a real <see cref="UITabstrip"/>, so the game's state rules apply. The
    /// chosen tab is the button's Focused state (lit art, dark text) and stays lit under the
    /// mouse and while pressed; another tab lights up on hover and darkens while pressed; a
    /// disabled tab uses the game's disabled art and grey text. Tabs are joined edge to edge.
    /// A group under another (the dialog's order types under Buy | Sell, a chart range under
    /// filters) is <see cref="Secondary"/>: the same rules, quieter art (dimmed idle tabs with
    /// grey text, the chosen tab plain with white text and a thin accent line under it). For a
    /// choice, never for actions. Knows no feature: the caller gives the labels and widths and
    /// hears which tab was clicked.
    /// </summary>
    internal sealed class FoToggleTabs
    {
        // not shown: game sprite names
        private const string Tab = "GenericTab";
        private const string TabLit = "GenericTabFocused";
        private const string TabHover = "GenericTabHovered";
        private const string TabPress = "GenericTabPressed";
        private const string TabOff = "GenericTabDisabled";

        private readonly List<UIButton> tabs = new List<UIButton>();
        private readonly Action<int> picked;
        private UITabstrip strip;
        private UIPanel underline;
        private bool secondary;

        /// <summary>The group's frame (the strip, and a secondary group's line); place this.</summary>
        public UIPanel Panel { get; private set; }

        /// <summary>The chosen tab's index (-1: none).</summary>
        public int Chosen
        {
            get { return strip.selectedIndex; }
        }

        public int Count
        {
            get { return tabs.Count; }
        }

        private FoToggleTabs(Action<int> picked)
        {
            this.picked = picked;
        }

        /// <summary>Tabs sharing <paramref name="size"/>'s width evenly; the first chosen.</summary>
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
                // The last takes what the rounding left, so the strip ends flush.
                widths[i] = i == labels.Length - 1 ? size.x - w * i : w;
            }

            return Add(parent, name, labels, widths, size.y, textScale, picked);
        }

        /// <summary>
        /// Tabs of the given widths, named <c>name + index</c> (rename through the indexer); the
        /// first chosen. <paramref name="picked"/> hears every click on an enabled tab, the
        /// chosen one included, after it is shown chosen.
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
            t.strip = t.Panel.AddUIComponent<UITabstrip>();
            t.strip.name = name + "Strip";
            t.strip.relativePosition = Vector3.zero;
            t.strip.padding = new RectOffset(0, 0, 0, 0);
            // Arrow keys would change the tab without telling the caller.
            t.strip.builtinKeyNavigation = false;
            t.strip.height = height;
            for (int i = 0; i < labels.Length; i++)
            {
                int index = i;
                UIButton b = t.strip.AddUIComponent<UIButton>();
                b.name = name + i;
                b.text = labels[i];
                b.textScale = textScale;
                b.size = new Vector2(widths[i], height);
                b.eventClick += (c, p) =>
                {
                    if (b.isEnabled)
                    {
                        t.Choose(index);
                        if (t.picked != null)
                        {
                            t.picked(index);
                        }
                    }
                };
                t.tabs.Add(b);
            }

            t.Style();
            t.Layout();
            t.Choose(0);
            return t;
        }

        public UIButton this[int index]
        {
            get { return tabs[index]; }
        }

        /// <summary>The quieter look for a group that sits under another one.</summary>
        public FoToggleTabs Secondary()
        {
            secondary = true;
            if (underline == null)
            {
                underline = Panel.AddUIComponent<UIPanel>();
                underline.name = Panel.name + "Underline";
                underline.backgroundSprite = "EmptySprite"; // not shown: a game sprite name
                underline.color = FoTheme.Accent;
                underline.isInteractive = false;
            }

            Style();
            PlaceUnderline();
            return this;
        }

        /// <summary>Shows that tab chosen (no event); -1 chooses none.</summary>
        public void Choose(int index)
        {
            if (strip.selectedIndex != index)
            {
                strip.selectedIndex = index;
            }

            PlaceUnderline();
        }

        /// <summary>A tab that cannot be chosen now, and the tooltip that says why.</summary>
        public void SetEnabled(int index, bool enabled, string whyNot)
        {
            tabs[index].isEnabled = enabled;
            tabs[index].tooltip = enabled ? string.Empty : whyNot ?? string.Empty;
        }

        /// <summary>Shows or hides a tab; the strip closes up around it.</summary>
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

        /// <summary>
        /// The art and text colour of each state, as the game's tab strips set them; the strip
        /// moves the buttons between states.
        /// </summary>
        private void Style()
        {
            foreach (UIButton b in tabs)
            {
                b.normalBgSprite = Tab;
                b.hoveredBgSprite = TabHover;
                b.pressedBgSprite = TabPress;
                b.disabledBgSprite = TabOff;
                b.disabledTextColor = FoTheme.GameTabDisabledText;
                b.pressedTextColor = FoTheme.GameText;
                b.hoveredTextColor = FoTheme.GameText;
                if (secondary)
                {
                    // Quieter: idle tabs dimmed with grey text; the chosen one plain and white.
                    b.focusedBgSprite = Tab;
                    b.color = FoTheme.SubTabIdle;
                    b.hoveredColor = FoTheme.Untinted;
                    b.pressedColor = FoTheme.Untinted;
                    b.focusedColor = FoTheme.Untinted;
                    b.textColor = FoTheme.TextMuted;
                    b.focusedTextColor = FoTheme.GameText;
                }
                else
                {
                    // As the Economy panel's tabs: white text, the chosen tab lit with dark text.
                    b.focusedBgSprite = TabLit;
                    b.color = FoTheme.GameText;
                    b.hoveredColor = FoTheme.GameText;
                    b.pressedColor = FoTheme.GameText;
                    b.focusedColor = FoTheme.GameText;
                    b.textColor = FoTheme.GameText;
                    b.focusedTextColor = FoTheme.GameTabChosenText;
                }
            }
        }

        /// <summary>Shown tabs side by side with no gap; the group as wide as they are.</summary>
        private void Layout()
        {
            float x = 0f;
            foreach (UIButton b in tabs)
            {
                // Its own flag: a group built inside a hidden panel still lays out.
                if (!b.isVisibleSelf)
                {
                    continue;
                }

                b.relativePosition = new Vector3(x, 0f);
                x += b.width;
            }

            float width = Mathf.Max(1f, x);
            strip.width = width;
            Panel.width = width;
            PlaceUnderline();
        }

        /// <summary>A secondary group's accent line under its chosen tab.</summary>
        private void PlaceUnderline()
        {
            if (underline == null)
            {
                return;
            }

            int chosen = strip.selectedIndex;
            bool shown = chosen >= 0 && chosen < tabs.Count && tabs[chosen].isVisibleSelf;
            underline.isVisible = shown;
            if (shown)
            {
                UIButton b = tabs[chosen];
                underline.size = new Vector2(b.width - 8f, 2f);
                underline.relativePosition = new Vector3(b.relativePosition.x + 4f, b.height - 3f);
                underline.BringToFront();
            }
        }
    }
}

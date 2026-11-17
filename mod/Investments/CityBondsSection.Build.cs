using System;
using System.Collections.Generic;
using System.Text;
using ColossalFramework.UI;
using FinanceOverhaul.Core;
using UnityEngine;

namespace FinanceOverhaul
{
    /// <summary>City bonds section: building its controls on the Economy panel's Loans tab.</summary>
    public partial class CityBondsSection
    {
        /// <summary>
        /// The vanilla help strip on the Loans tab: the widest panel above the loan cards. The
        /// section covers it plus the gap down to the cards.
        /// </summary>
        private bool Build()
        {
            UIComponent page = LoansPage();
            if (page == null)
            {
                return false;
            }

            UIComponent stale = page.Find(SectionName);
            if (stale != null)
            {
                Destroy(stale.gameObject);
            }

            if (strip != null)
            {
                strip.eventVisibilityChanged -= OnStripVisibility;
            }

            strip = null;
            float cardsTop = float.MaxValue;
            foreach (UIComponent c in page.components)
            {
                if (c == null)
                {
                    continue;
                }

                if (
                    c.width > page.width * 0.8f
                    && (strip == null || c.relativePosition.y < strip.relativePosition.y)
                )
                {
                    strip = c;
                }
            }

            if (strip == null)
            {
                ModLog.Warn("bonds section: no help strip on the Loans tab");
                return false;
            }

            foreach (UIComponent c in page.components)
            {
                if (c != null && c != strip && c.relativePosition.y > strip.relativePosition.y)
                {
                    cardsTop = Mathf.Min(cardsTop, c.relativePosition.y);
                }
            }

            float height =
                cardsTop < float.MaxValue ? cardsTop - strip.relativePosition.y - 8f : strip.height;
            UILabel anyLabel = page.GetComponentInChildren<UILabel>();
            font = anyLabel != null ? anyLabel.font : null;

            section = page.AddUIComponent<UIPanel>();
            section.name = SectionName;
            section.relativePosition = strip.relativePosition;
            section.size = new Vector2(strip.width, height);
            section.backgroundSprite = "GenericPanelWhite";
            section.color = Panel;
            BuildContents();
            strip.eventVisibilityChanged += OnStripVisibility;
            ModLog.Info(
                "bonds section: attached " + section.size + " in place of '" + strip.name + "'"
            );
            return true;
        }

        private UIComponent LoansPage()
        {
            // The Loans tab page holds the three loan cards; find it by a card's parent.
            UIComponent take = null;
            foreach (UIButton b in economy.component.GetComponentsInChildren<UIButton>(true))
            {
                if (
                    b.isVisibleSelf
                    && b.parent != null
                    && b.parent.parent != null
                    && IsLoanCard(b.parent)
                )
                {
                    take = b;
                    break;
                }
            }

            return take == null ? null : take.parent.parent;
        }

        private static bool IsLoanCard(UIComponent c)
        {
            return c.name != null
                && c.name.IndexOf("Loan", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void BuildContents()
        {
            float w = section.width;
            float pad = 16f;
            UILabel title = Label(
                section,
                Prefix + "Title",
                Strings.Get("bond.title"),
                1.125f,
                Gold
            );
            title.relativePosition = new Vector3(pad, 12f);
            UILabel about = Label(section, null, Strings.Get("bond.about"), 0.75f, Muted);
            about.relativePosition = new Vector3(pad + title.width + 12f, 16f);

            // Left column: sell a bond.
            float left = pad,
                colW = Mathf.Min(560f, w * 0.46f);
            float y = 44f;
            Label(section, null, Strings.Get("bond.sellTitle"), 0.875f, Text).relativePosition =
                new Vector3(left, y);
            y += 24f;
            Label(section, null, Strings.Get("bond.term"), 0.75f, Muted).relativePosition =
                new Vector3(left, y + 6f);
            float x = left + 48f;
            foreach (
                BondTerm t in new[] { BondTerm.OneYear, BondTerm.TwoYears, BondTerm.FiveYears }
            )
            {
                BondTerm captured = t;
                UIButton b = Button(section, TermPrefix + (int)t, BondText.YearsText(t), 84f);
                b.relativePosition = new Vector3(x, y);
                b.eventClick += (c, p) => SetTerm(captured);
                termButtons[t] = b;
                x += 90f;
            }

            y += 32f;
            Label(section, null, Strings.Get("bond.amount"), 0.75f, Muted).relativePosition =
                new Vector3(left, y + 6f);
            amount = section.AddUIComponent<UITextField>();
            amount.name = AmountFieldName;
            amount.size = new Vector2(140f, 27f);
            amount.relativePosition = new Vector3(left + 96f, y);
            amount.numericalOnly = false; // commas are allowed; BondText parses
            amount.maxLength = 14;
            amount.selectOnFocus = true;
            amount.submitOnFocusLost = true;
            amount.builtinKeyNavigation = true;
            amount.readOnly = false;
            amount.horizontalAlignment = UIHorizontalAlignment.Right;
            amount.padding = new RectOffset(6, 8, 6, 4);
            amount.normalBgSprite = "TextFieldPanel";
            amount.hoveredBgSprite = "TextFieldPanelHovered";
            amount.focusedBgSprite = "TextFieldPanel";
            amount.selectionSprite = "EmptySprite";
            amount.color = FoTheme.Field;
            amount.textColor = Color.white;
            if (font != null)
            {
                amount.font = font;
            }

            amount.text = "500,000";
            amount.eventTextChanged += (c, t) => Refresh(true);
            y += 30f;
            hint = Wrapped(section, HintName, colW, 0.7f, Muted);
            hint.relativePosition = new Vector3(left, y);
            y += 34f; // two lines of hint
            rate = Row(left, ref y, Strings.Get("bond.rate"), RateName);
            coupon = Row(left, ref y, Strings.Get("bond.coupon"), Prefix + "Coupon");
            interest = Row(left, ref y, Strings.Get("bond.interest"), Prefix + "Interest");
            repaid = Row(left, ref y, Strings.Get("bond.repaidOn"), Prefix + "Repaid");
            y += 4f;
            sell = Button(section, SellName, Strings.Get("bond.sell"), 260f);
            sell.relativePosition = new Vector3(left, y);
            sell.eventClick += (c, p) => OnSell();

            // Right column: bonds owed.
            float right = left + colW + 24f;
            float rightW = w - right - pad;
            owedTitle = Label(section, OwedTitleName, Strings.Get("bond.owedTitle"), 0.875f, Text);
            owedTitle.relativePosition = new Vector3(right, 44f);
            // The footer may wrap to two lines; the list ends above both.
            footer = Wrapped(section, FooterName, rightW, 0.7f, Muted);
            footer.relativePosition = new Vector3(right, section.height - 36f);
            owedList = section.AddUIComponent<UIScrollablePanel>();
            owedList.name = OwedListName;
            owedList.relativePosition = new Vector3(right, 68f);
            owedList.size = new Vector2(rightW - ScrollbarW - 4f, footer.relativePosition.y - 74f);
            owedList.autoLayout = true;
            owedList.autoLayoutDirection = LayoutDirection.Vertical;
            owedList.autoLayoutPadding = new RectOffset(0, 0, 0, 6);
            owedList.clipChildren = true;
            owedList.scrollWheelDirection = UIOrientation.Vertical;
            owedList.builtinKeyNavigation = true;
            owedList.verticalScrollbar = Scrollbar(
                section,
                new Vector3(right + rightW - ScrollbarW, 68f),
                owedList.height
            );

            // Edge fades (scrolling-list rule, option A): rows dissolve into the panel at an
            // edge with more rows past it.
            fadeTopTexture = FadeTexture(Panel, true);
            fadeBottomTexture = FadeTexture(Panel, false);
            fadeTop = Fade(FadeTopName, fadeTopTexture, owedList.relativePosition.y);
            fadeBottom = Fade(
                FadeBottomName,
                fadeBottomTexture,
                owedList.relativePosition.y + owedList.height - ScrollFade.DefaultLength
            );
            owedList.eventScrollPositionChanged += (c, p) => UpdateFades();
        }

        private UITextureSprite Fade(string name, Texture2D texture, float y)
        {
            UITextureSprite s = section.AddUIComponent<UITextureSprite>();
            s.name = name;
            s.texture = texture;
            s.size = new Vector2(owedList.width, ScrollFade.DefaultLength);
            s.relativePosition = new Vector3(owedList.relativePosition.x, y);
            s.isInteractive = false;
            s.opacity = 0f;
            return s;
        }

        /// <summary>Panel colour fading to clear; <paramref name="solidAtTop"/> for the top edge.</summary>
        private static Texture2D FadeTexture(Color32 colour, bool solidAtTop)
        {
            const int h = 32;
            var t = new Texture2D(1, h, TextureFormat.RGBA32, false);
            t.name = "FinanceOverhaulBondsFade";
            t.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < h; y++)
            {
                float f = y / (float)(h - 1); // 0 = bottom row of the texture
                float a = solidAtTop ? f : 1f - f;
                a = a * a * (3f - 2f * a); // smoothstep: a soft feather
                t.SetPixel(0, y, new Color32(colour.r, colour.g, colour.b, (byte)(255 * a)));
            }

            t.Apply();
            return t;
        }

        /// <summary>Shows each fade only while there are more rows past its edge.</summary>
        private void UpdateFades()
        {
            if (fadeTop == null || owedList == null)
            {
                return;
            }

            float top,
                bottom;
            ScrollFade.Alphas(
                owedList.scrollPosition.y,
                owedList.height,
                owedList.CalculateViewSize().y,
                ScrollFade.DefaultLength,
                out top,
                out bottom
            );
            fadeTop.opacity = top;
            fadeBottom.opacity = bottom;
            fadeTop.BringToFront();
            fadeBottom.BringToFront();
        }

        /// <summary>
        /// A vertical scrollbar in the game's style (track and thumb sprites); it hides itself
        /// while everything fits.
        /// </summary>
        private static UIScrollbar Scrollbar(UIComponent parent, Vector3 at, float height)
        {
            UIScrollbar bar = parent.AddUIComponent<UIScrollbar>();
            bar.name = OwedScrollbarName;
            bar.relativePosition = at;
            bar.size = new Vector2(ScrollbarW, height);
            bar.orientation = UIOrientation.Vertical;
            bar.pivot = UIPivotPoint.TopLeft;
            bar.minValue = 0f;
            bar.value = 0f;
            bar.incrementAmount = 50f;
            bar.autoHide = true;
            UISlicedSprite track = bar.AddUIComponent<UISlicedSprite>();
            track.relativePosition = Vector3.zero;
            track.autoSize = true;
            track.size = bar.size;
            track.fillDirection = UIFillDirection.Vertical;
            track.spriteName = "ScrollbarTrack";
            bar.trackObject = track;
            UISlicedSprite thumb = track.AddUIComponent<UISlicedSprite>();
            thumb.relativePosition = Vector3.zero;
            thumb.fillDirection = UIFillDirection.Vertical;
            thumb.autoSize = true;
            thumb.width = ScrollbarW - 4f;
            thumb.spriteName = "ScrollbarThumb";
            bar.thumbObject = thumb;
            return bar;
        }

        private UILabel Row(float left, ref float y, string label, string name)
        {
            Label(section, null, label + ":", 0.75f, Text).relativePosition = new Vector3(left, y);
            UILabel v = Label(section, name, "", 0.75f, Muted);
            v.relativePosition = new Vector3(left + 160f, y);
            y += 18f;
            return v;
        }

        private UILabel Label(
            UIComponent parent,
            string name,
            string text,
            float scale,
            Color32 colour
        )
        {
            UILabel l = parent.AddUIComponent<UILabel>();
            if (name != null)
            {
                l.name = name;
            }

            if (font != null)
            {
                l.font = font;
            }

            l.textScale = scale;
            l.textColor = colour;
            l.text = text;
            return l;
        }

        private UILabel Wrapped(
            UIComponent parent,
            string name,
            float width,
            float scale,
            Color32 colour
        )
        {
            UILabel l = Label(parent, name, "", scale, colour);
            l.autoSize = false;
            l.wordWrap = true;
            l.autoHeight = true;
            l.width = width;
            return l;
        }

        private UIButton Button(UIComponent parent, string name, string text, float width)
        {
            UIButton b = parent.AddUIComponent<UIButton>();
            b.name = name;
            b.text = text;
            b.size = new Vector2(width, 28f);
            b.textScale = 0.8f;
            if (font != null)
            {
                b.font = font;
            }

            b.normalBgSprite = "ButtonMenu";
            b.hoveredBgSprite = "ButtonMenuHovered";
            b.pressedBgSprite = "ButtonMenuPressed";
            b.focusedBgSprite = "ButtonMenu";
            b.disabledBgSprite = "ButtonMenuDisabled";
            b.textColor = new Color32(220, 228, 236, 255);
            b.disabledTextColor = new Color32(120, 128, 136, 255);
            return b;
        }
    }
}

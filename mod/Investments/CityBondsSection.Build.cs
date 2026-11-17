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
                Heading
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
                UIButton b = Button(section, TermPrefix + (int)t, BondText.WeeksText(t), 84f);
                b.relativePosition = new Vector3(x, y);
                b.eventClick += (c, p) => SetTerm(captured);
                termButtons[t] = b;
                x += 90f;
            }

            y += 30f;
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
            amount.normalBgSprite = FoTheme.GameFieldSprite;
            amount.hoveredBgSprite = "TextFieldPanelHovered";
            amount.focusedBgSprite = FoTheme.GameFieldSprite;
            amount.selectionSprite = "EmptySprite";
            amount.color = FoTheme.Field;
            amount.textColor = Color.white;
            if (font != null)
            {
                amount.font = font;
            }

            amount.text = "500,000";
            amountGrouping = new FoGroupedField(amount, AmountMaxDigits);
            amount.eventTextChanged += (c, t) => Refresh(true);
            y += 28f;
            hint = Wrapped(section, HintName, colW, 0.7f, Muted);
            hint.relativePosition = new Vector3(left, y);
            y += 34f; // two lines of hint
            rate = Row(left, ref y, Strings.Get("bond.rate"), RateName);
            coupon = Row(left, ref y, Strings.Get("bond.coupon"), Prefix + "Coupon");
            interest = Row(left, ref y, Strings.Get("bond.interest"), Prefix + "Interest");
            total = Row(left, ref y, Strings.Get("bond.total"), TotalName);
            repaid = Row(left, ref y, Strings.Get("bond.repaidOn"), Prefix + "Repaid");
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

        private const float CardHeight = 116f;
        private const float CardRowTop = 38f;
        private const float CardRowPitch = 17f; // the loan cards' row pitch
        private const float CardTextScale = 0.8f;
        private const float RepayWidth = 84f;

        /// <summary>
        /// One owed bond as a card in the look of the game's loan cards: the name on top, green
        /// labels right-aligned against pale blue values, Total (what paying
        /// now costs: the face plus interest accrued this period) and Pay Now bottom right. The
        /// fields sit in two columns because the list is short.
        /// </summary>
        private void BuildCard(IssuedBond b, int today)
        {
            int id = b.Id;
            UIPanel card = owedList.AddUIComponent<UIPanel>();
            card.name = RowPrefix + id;
            card.size = new Vector2(owedList.width - 4f, CardHeight);
            card.backgroundSprite = FoTheme.GameLoanCardSprite;
            card.color = FoTheme.BondCard;

            UITextField name = card.AddUIComponent<UITextField>();
            name.name = NamePrefix + id;
            name.size = new Vector2(Mathf.Min(320f, card.width - 24f), 26f);
            name.relativePosition = new Vector3((card.width - name.width) / 2f, 6f);
            name.maxLength = BondText.MaxNameLength;
            name.selectOnFocus = true;
            name.submitOnFocusLost = true;
            name.builtinKeyNavigation = true;
            name.horizontalAlignment = UIHorizontalAlignment.Center;
            name.padding = new RectOffset(6, 6, 5, 3);
            name.normalBgSprite = FoTheme.GameFieldSprite;
            name.hoveredBgSprite = "TextFieldPanelHovered";
            name.focusedBgSprite = FoTheme.GameFieldSprite;
            name.selectionSprite = "EmptySprite";
            name.color = FoTheme.Field;
            name.textColor = Heading;
            name.textScale = 1f;
            if (font != null)
            {
                name.font = font;
            }

            name.tooltip = Strings.Get("bond.card.nameTip");
            name.text = BondText.CardTitle(b);
            name.eventTextSubmitted += (c, t) => OnRename(id, name);
            name.eventTextCancelled += (c, t) => OnRename(id, name);

            float next = CardColumn(
                card,
                id,
                12f,
                new[]
                {
                    Strings.Get("bond.card.amount"),
                    Strings.Get("bond.card.term"),
                    Strings.Get("bond.card.interest"),
                    Strings.Get("bond.card.total"),
                },
                new[]
                {
                    BondText.CardAmount(b),
                    BondText.CardTerm(b),
                    BondText.CardInterest(b),
                    BondText.Money(BondText.RepayCents(b, today)),
                },
                new[] { "Amount", "Term", "Interest", "Total" } // not shown: control names for tests
            );

            CardColumn(
                card,
                id,
                next + 20f,
                new[]
                {
                    Strings.Get("bond.card.nextCoupon"),
                    Strings.Get("bond.card.repaidOn"),
                    Strings.Get("bond.card.couponsLeft"),
                },
                new[]
                {
                    BondText.CardNextCoupon(b, DateIn(BondText.NextCouponInDays(b, today))),
                    DateIn(b.DueDay(b.Terms.Periods) - today),
                    BondText.CardCouponsLeft(b),
                },
                new[] { "NextCoupon", "RepaidOn", "CouponsLeft" } // not shown: control names for tests
            );

            UIButton repay = Button(card, RepayPrefix + id, Strings.Get("bond.repay"), RepayWidth);
            repay.relativePosition = new Vector3(
                card.width - RepayWidth - 8f,
                CardHeight - repay.height - 6f
            );
            repay.eventClick += (c, p) => OnRepay(id);
        }

        /// <summary>
        /// A column of label and value pairs: labels end on one line, values start on the next.
        /// Returns the column's right edge.
        /// </summary>
        private float CardColumn(
            UIPanel card,
            int id,
            float left,
            string[] labels,
            string[] values,
            string[] names
        )
        {
            var made = new UILabel[labels.Length];
            float labelW = 0f;
            for (int i = 0; i < labels.Length; i++)
            {
                made[i] = Label(card, null, labels[i], CardTextScale, Heading);
                labelW = Mathf.Max(labelW, made[i].width);
            }

            float right = left + labelW;
            for (int i = 0; i < labels.Length; i++)
            {
                float y = CardRowTop + i * CardRowPitch;
                made[i].relativePosition = new Vector3(left + labelW - made[i].width, y);
                UILabel v = Label(
                    card,
                    CardValuePrefix + names[i] + "-" + id,
                    values[i],
                    CardTextScale,
                    Price
                );
                v.relativePosition = new Vector3(left + labelW + 6f, y);
                right = Mathf.Max(right, left + labelW + 6f + v.width);
            }

            return right;
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
            Label(section, null, label + ":", 0.75f, Heading).relativePosition = new Vector3(
                left,
                y
            );
            UILabel v = Label(section, name, "", 0.75f, Muted);
            v.relativePosition = new Vector3(left + 160f, y);
            y += 16f; // five quote rows fit above Sell
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
            b.textColor = FoTheme.GameText;
            b.disabledTextColor = FoTheme.GameDisabledText;
            return b;
        }
    }
}

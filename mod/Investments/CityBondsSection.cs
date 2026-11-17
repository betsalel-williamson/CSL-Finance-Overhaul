using System;
using System.Collections.Generic;
using System.Text;
using ColossalFramework.UI;
using FinanceOverhaul.Core;
using UnityEngine;

namespace FinanceOverhaul
{
    /// <summary>
    /// The City bonds section on Economy → Loans (Figma UI Map band 11): it takes the place of
    /// the vanilla help strip and the gap above the three loan cards. Left: sell a bond (term,
    /// amount, live quote, Sell). Right: bonds the city owes, each with Repay now. Shown when
    /// Options → City bonds is on, or while the city still owes a bond; otherwise the vanilla
    /// strip is back untouched. Lives on the overlay root, so hot reload removes it
    /// (<see cref="OnDestroy"/> restores the strip).
    /// </summary>
    public class CityBondsSection : MonoBehaviour
    {
        public const string Prefix = "FinanceOverhaulBonds";
        public const string SectionName = Prefix + "Section";
        public const string AmountFieldName = Prefix + "Amount";
        public const string SellName = Prefix + "Sell";
        public const string HintName = Prefix + "Hint";
        public const string RateName = Prefix + "Rate";
        public const string OwedTitleName = Prefix + "OwedTitle";
        public const string FooterName = Prefix + "Footer";
        public const string RowPrefix = Prefix + "Row-";
        public const string RepayPrefix = Prefix + "Repay-";
        public const string TermPrefix = Prefix + "Term-";
        public const string OwedListName = Prefix + "OwedList";
        public const string OwedScrollbarName = Prefix + "OwedScrollbar";
        private const float ScrollbarW = 12f;
        public const string FadeTopName = Prefix + "FadeTop";
        public const string FadeBottomName = Prefix + "FadeBottom";
        private UITextureSprite fadeTop,
            fadeBottom;
        private Texture2D fadeTopTexture,
            fadeBottomTexture;

        private static readonly Color32 Gold = new Color32(232, 214, 77, 255);
        private static readonly Color32 Text = new Color32(230, 236, 240, 255);
        private static readonly Color32 Muted = FoTheme.TextMuted;
        private static readonly Color32 Gain = FoTheme.GamePositive;
        private static readonly Color32 Price = FoTheme.GameValue;
        private static readonly Color32 Refused = FoTheme.GameNegative;
        private static readonly Color32 Panel = new Color32(24, 30, 36, 245);
        private static readonly Color32 RowColour = new Color32(36, 44, 52, 255);

        /// <summary>Game UI tests only: shows the section and allows selling with the flag off.</summary>
        public static bool ForceShow;

        private EconomyPanel economy;
        private UIComponent strip;
        private UIPanel section;
        private UIScrollablePanel owedList;
        private UITextField amount;
        private UIButton sell;
        private UILabel hint,
            rate,
            coupon,
            interest,
            repaid,
            owedTitle,
            footer;
        private readonly Dictionary<BondTerm, UIButton> termButtons =
            new Dictionary<BondTerm, UIButton>();
        private BondTerm term = BondTerm.FiveYears;
        private UIFont font;
        private string shownRows;
        private float nextCheck;
        private float nextBuild;
        private bool wanted;

        public static CityBondsSection Current { get; private set; }

        private void Awake()
        {
            Current = this;
        }

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

                wanted =
                    ForceShow
                    || (Mod.Settings != null && Mod.Settings.CityBondsEnabled)
                    || BondDesk.Snapshot().Bonds.Count > 0;
                if (economy == null || !economy.isLoansOpen)
                {
                    return;
                }

                if (!wanted)
                {
                    Hide();
                    return;
                }

                if (section != null)
                {
                    Refresh(false);
                }
            }
            catch (Exception ex)
            {
                ModLog.Warn("bonds section: update failed: " + ex);
                nextCheck = Time.unscaledTime + 10f;
            }
        }

        /// <summary>
        /// Swaps the vanilla strip for the section every frame, after the game's own UI updates and
        /// before drawing, so opening the Loans tab never shows the vanilla strip (not even for one
        /// frame). The slower Update keeps the contents fresh.
        /// </summary>
        private void LateUpdate()
        {
            try
            {
                if (!wanted || economy == null)
                {
                    return;
                }

                // Build as soon as the city is loaded, while the Economy panel is still closed, so
                // the first visit to the Loans tab has nothing left to build.
                if (section == null)
                {
                    if (Time.unscaledTime < nextBuild)
                    {
                        return;
                    }

                    if (!Build())
                    {
                        nextBuild = Time.unscaledTime + 2f;
                        return;
                    }

                    Refresh(false);
                    ShowSection();
                    return;
                }

                if (economy.isLoansOpen)
                {
                    ShowSection();
                }
            }
            catch (Exception ex)
            {
                ModLog.Warn("bonds section: swap failed: " + ex);
                wanted = false;
                nextCheck = Time.unscaledTime + 10f;
            }
        }

        private void ShowSection()
        {
            if (!section.isVisible)
            {
                section.isVisible = true;
            }

            if (strip.isVisible)
            {
                strip.isVisible = false;
            }
        }

        /// <summary>The game shows the strip again when the tab opens: hide it at once.</summary>
        private void OnStripVisibility(UIComponent c, bool visible)
        {
            if (visible && wanted && section != null)
            {
                ShowSection();
            }
        }

        /// <summary>Fills the amount field (game UI tests and the player alike).</summary>
        public void SetAmount(string text)
        {
            if (amount != null)
            {
                amount.text = text;
                Refresh(true);
            }
        }

        public void SetTerm(BondTerm t)
        {
            term = t;
            Refresh(true);
        }

        private void Hide()
        {
            if (section != null)
            {
                section.isVisible = false;
            }

            if (strip != null)
            {
                strip.isVisible = true;
            }
        }

        private void OnDestroy()
        {
            try
            {
                if (section != null)
                {
                    Destroy(section.gameObject);
                }

                if (fadeTopTexture != null)
                {
                    Destroy(fadeTopTexture);
                }

                if (fadeBottomTexture != null)
                {
                    Destroy(fadeBottomTexture);
                }

                if (strip != null)
                {
                    strip.eventVisibilityChanged -= OnStripVisibility;
                    strip.isVisible = true;
                }
            }
            catch
            {
                // fail soft on unload
            }

            if (Current == this)
            {
                Current = null;
            }
        }

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
            UILabel title = Label(section, Prefix + "Title", "City bonds", 1.125f, Gold);
            title.relativePosition = new Vector3(pad, 12f);
            UILabel about = Label(
                section,
                null,
                "You can only have three loans at once. Bonds don't use a loan slot: the city pays a coupon every 30 days and repays the face at maturity.",
                0.75f,
                Muted
            );
            about.relativePosition = new Vector3(pad + title.width + 12f, 16f);

            // Left column: sell a bond.
            float left = pad,
                colW = Mathf.Min(560f, w * 0.46f);
            float y = 44f;
            Label(section, null, "Sell a bond", 0.875f, Text).relativePosition = new Vector3(
                left,
                y
            );
            y += 24f;
            Label(section, null, "Term", 0.75f, Muted).relativePosition = new Vector3(left, y + 6f);
            float x = left + 48f;
            foreach (
                BondTerm t in new[] { BondTerm.OneYear, BondTerm.TwoYears, BondTerm.FiveYears }
            )
            {
                BondTerm captured = t;
                UIButton b = Button(
                    section,
                    TermPrefix + (int)t,
                    (int)t + ((int)t == 1 ? " year" : " years"),
                    84f
                );
                b.relativePosition = new Vector3(x, y);
                b.eventClick += (c, p) => SetTerm(captured);
                termButtons[t] = b;
                x += 90f;
            }

            y += 32f;
            Label(section, null, "Amount (₡)", 0.75f, Muted).relativePosition = new Vector3(
                left,
                y + 6f
            );
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
            rate = Row(left, ref y, "Rate", RateName);
            coupon = Row(left, ref y, "Coupon every 30 days", Prefix + "Coupon");
            interest = Row(left, ref y, "Total interest", Prefix + "Interest");
            repaid = Row(left, ref y, "Repaid on", Prefix + "Repaid");
            y += 4f;
            sell = Button(section, SellName, "Sell bond", 260f);
            sell.relativePosition = new Vector3(left, y);
            sell.eventClick += (c, p) => OnSell();

            // Right column: bonds owed.
            float right = left + colW + 24f;
            float rightW = w - right - pad;
            owedTitle = Label(section, OwedTitleName, "Bonds you owe", 0.875f, Text);
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

        /// <summary>Re-reads the city and redraws. Rows only when bonds or the day change.</summary>
        private void Refresh(bool quoteOnly)
        {
            if (section == null)
            {
                return;
            }

            foreach (KeyValuePair<BondTerm, UIButton> kv in termButtons)
            {
                bool on = kv.Key == term;
                kv.Value.normalBgSprite = on ? "ButtonMenuFocused" : "ButtonMenu";
                kv.Value.textColor = on ? (Color32)Color.white : new Color32(200, 210, 220, 255);
            }

            long debt,
                income;
            BondDesk.CityFinances(out debt, out income);
            long max = BondText.MaxRaiseCents(debt, income);
            long face = BondText.ParseAmountCents(amount.text);
            bool enabled = ForceShow || (Mod.Settings != null && Mod.Settings.CityBondsEnabled);
            BondOffer offer =
                face > 0 ? BondPricing.Offer(term, debt, face, income) : default(BondOffer);
            if (!enabled)
            {
                hint.text =
                    "Selling new bonds is off (Options → City bonds). Bonds already sold keep paying.";
                hint.textColor = Muted;
            }
            else if (face <= 0)
            {
                hint.text =
                    "Type a whole amount in ₡. Up to "
                    + BondText.WholeMoney(max)
                    + " before the city owes 3× its annual income.";
                hint.textColor = Muted;
            }
            else if (face > BondTerms.MaxFaceCents)
            {
                hint.text =
                    "Refused: one bond can raise at most "
                    + BondText.WholeMoney(BondTerms.MaxFaceCents)
                    + ".";
                hint.textColor = Refused;
            }
            else if (!offer.Allowed)
            {
                hint.text =
                    "Refused: "
                    + offer.Reason
                    + ". Most you can raise: "
                    + BondText.WholeMoney(max)
                    + ".";
                hint.textColor = Refused;
            }
            else
            {
                hint.text =
                    "Up to "
                    + BondText.WholeMoney(max)
                    + " before the city owes 3× its annual income ("
                    + BondText.WholeMoney(income)
                    + ").";
                hint.textColor = Muted;
            }

            if (face > 0 && offer.Allowed)
            {
                BondTerms t = offer.Terms(face);
                rate.text = BondText.RateLine(offer);
                rate.textColor = Price;
                coupon.text = BondText.Money(BondText.CouponCents(t));
                interest.text = BondText.Money(BondMath.TotalInterest(t));
                repaid.text = DateIn(t.TermDays) + " · " + BondText.WholeMoney(face);
                sell.text = "Sell bond · receive " + BondText.WholeMoney(face);
            }
            else
            {
                rate.text = coupon.text = interest.text = repaid.text = "—";
                rate.textColor = Muted;
                sell.text = "Sell bond";
            }

            sell.isEnabled = enabled && face > 0 && offer.Allowed;

            BondBook book = BondDesk.Snapshot();
            int today = OrderDesk.Today;
            footer.text = BondText.OwedFooter(
                debt - book.OutstandingFaceCents,
                book.OutstandingFaceCents,
                income
            );
            string sig = BondBookCodec.Format(book) + "@" + today;
            if (quoteOnly && sig == shownRows)
            {
                return;
            }

            if (sig != shownRows)
            {
                RebuildRows(book, today);
                shownRows = sig;
            }

            UpdateFades(); // rows lay out after they are added; catch up on the next refresh
        }

        private void RebuildRows(BondBook book, int today)
        {
            UiCleanup.RemoveChildrenWhere(owedList, c => true);
            owedTitle.text = "Bonds you owe (" + book.Bonds.Count + ")";
            if (book.Bonds.Count == 0)
            {
                Label(
                    owedList,
                    null,
                    "None yet. Coupons and repayments show in the budget under loans.",
                    0.7f,
                    Muted
                );
                UpdateFades();
                return;
            }

            var bonds = new List<IssuedBond>(book.Bonds);
            bonds.Sort((a, b) => a.Id.CompareTo(b.Id));
            foreach (IssuedBond b in bonds)
            {
                UIPanel row = owedList.AddUIComponent<UIPanel>();
                row.name = RowPrefix + b.Id;
                row.size = new Vector2(owedList.width - 4f, 44f);
                row.backgroundSprite = "GenericPanelWhite";
                row.color = RowColour;
                UILabel t = Label(
                    row,
                    null,
                    BondText.RowTitle(b) + " · repaid " + DateIn(b.DueDay(b.Terms.Periods) - today),
                    0.8f,
                    Text
                );
                t.relativePosition = new Vector3(10f, 5f);
                UILabel n = Label(row, null, BondText.RowNext(b, today), 0.65f, Muted);
                n.relativePosition = new Vector3(10f, 25f);
                int id = b.Id;
                UIButton repay = Button(
                    row,
                    RepayPrefix + id,
                    "Repay " + BondText.Money(BondText.RepayCents(b, today)),
                    170f
                );
                repay.relativePosition = new Vector3(row.width - 178f, 8f);
                repay.eventClick += (c, p) => OnRepay(id);
            }

            UpdateFades();
        }

        private void OnSell()
        {
            long face = BondText.ParseAmountCents(amount.text);
            BondOffer offer;
            if (face > 0 && BondDesk.Issue(term, face, out offer) != null)
            {
                shownRows = null;
                Refresh(false);
            }
        }

        private void OnRepay(int id)
        {
            if (BondDesk.Redeem(id))
            {
                ModLog.Info("bonds: repaid #" + id + " early from the Loans tab");
                shownRows = null;
                Refresh(false);
            }
        }

        private static string DateIn(int days)
        {
            return SimulationManager
                .instance.m_currentGameTime.AddDays(days)
                .ToString("dd/MM/yyyy");
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

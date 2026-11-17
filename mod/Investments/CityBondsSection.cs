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
    public partial class CityBondsSection : MonoBehaviour
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
    }
}

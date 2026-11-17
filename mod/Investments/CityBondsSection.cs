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
    /// amount, live quote, Sell). Right: bonds the city owes, each a card laid out like the game's loan cards (a name the
    /// player can change, the loan-style fields, Repay). Shown when
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
        public const string NamePrefix = Prefix + "Name-";
        public const string CardValuePrefix = Prefix + "Value-";
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

        private static readonly Color32 Heading = FoTheme.GameHeading;
        private static readonly Color32 Text = FoTheme.GameText;
        private static readonly Color32 Muted = FoTheme.TextMuted;
        private static readonly Color32 Gain = FoTheme.GamePositive;
        private static readonly Color32 Price = FoTheme.GameValue;
        private static readonly Color32 Refused = FoTheme.GameNegative;
        private static readonly Color32 Panel = FoTheme.BondsPanel;

        /// <summary>Game UI tests only: shows the section and allows selling with the flag off.</summary>
        public static bool ForceShow;

        private EconomyPanel economy;
        private UIComponent strip;
        private UIPanel section;
        private UIScrollablePanel owedList;
        private UITextField amount;

        // One more digit than the largest bond, so an amount that is too large can still be
        // typed and refused in words.
        private const int AmountMaxDigits = 10;
        private FoGroupedField amountGrouping;
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
                    if (amountGrouping != null)
                    {
                        amountGrouping.Tick();
                    }

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
                if (amountGrouping != null)
                {
                    amountGrouping.Tick();
                }

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
                hint.text = Strings.Get("bond.hint.off");
                hint.textColor = Muted;
            }
            else if (face <= 0)
            {
                hint.text = Strings.Format("bond.hint.amount", BondText.WholeMoney(max));
                hint.textColor = Muted;
            }
            else if (face > BondTerms.MaxFaceCents)
            {
                hint.text = Strings.Format(
                    "bond.refused.max",
                    BondText.WholeMoney(BondTerms.MaxFaceCents)
                );
                hint.textColor = Refused;
            }
            else if (!offer.Allowed)
            {
                hint.text = Strings.Format(
                    "bond.refused",
                    BondText.ReasonText(offer.Reason),
                    BondText.WholeMoney(max)
                );
                hint.textColor = Refused;
            }
            else
            {
                hint.text = Strings.Format(
                    "bond.hint.room",
                    BondText.WholeMoney(max),
                    BondText.WholeMoney(income)
                );
                hint.textColor = Muted;
            }

            if (face > 0 && offer.Allowed)
            {
                BondTerms t = offer.Terms(face);
                rate.text = BondText.RateLine(offer);
                rate.textColor = Price;
                coupon.text = BondText.Money(BondText.CouponCents(t));
                interest.text = BondText.Money(BondMath.TotalInterest(t));
                repaid.text = Strings.Format(
                    "bond.repaidOn.value",
                    DateIn(t.TermDays),
                    BondText.WholeMoney(face)
                );
                sell.text = Strings.Format("bond.sell.receive", BondText.WholeMoney(face));
            }
            else
            {
                rate.text = coupon.text = interest.text = repaid.text = "—";
                rate.textColor = Muted;
                sell.text = Strings.Get("bond.sell");
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

            if (sig != shownRows && !NameBeingTyped())
            {
                RebuildRows(book, today);
                shownRows = sig;
            }

            UpdateFades(); // rows lay out after they are added; catch up on the next refresh
        }

        private void RebuildRows(BondBook book, int today)
        {
            UiCleanup.RemoveChildrenWhere(owedList, c => true);
            owedTitle.text = Strings.Format("bond.owedTitle.count", book.Bonds.Count.ToString());
            if (book.Bonds.Count == 0)
            {
                Label(owedList, null, Strings.Get("bond.none"), 0.7f, Muted);
                UpdateFades();
                return;
            }

            var bonds = new List<IssuedBond>(book.Bonds);
            bonds.Sort((a, b) => a.Id.CompareTo(b.Id));
            foreach (IssuedBond b in bonds)
            {
                BuildCard(b, today);
            }

            UpdateFades();
        }

        /// <summary>A name box on a card has the keyboard: the cards wait until it lets go.</summary>
        private bool NameBeingTyped()
        {
            foreach (UITextField f in owedList.GetComponentsInChildren<UITextField>())
            {
                if (f.hasFocus)
                {
                    return true;
                }
            }

            return false;
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

        /// <summary>Keeps the typed name (tidied); an empty box goes back to "Bond #63".</summary>
        private void OnRename(int id, UITextField box)
        {
            string name = BondText.NameToKeep(box.text, id);
            if (BondDesk.Rename(id, name))
            {
                ModLog.Info("bonds: #" + id + " named '" + name + "'");
            }

            IssuedBond bond = BondDesk.Snapshot().Find(id);
            if (bond != null && box.text != BondText.CardTitle(bond))
            {
                box.text = BondText.CardTitle(bond);
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

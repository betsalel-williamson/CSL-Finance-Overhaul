using System;
using System.Globalization;

namespace FinanceOverhaul.Core
{
    /// <summary>
    /// Text and limits for the City bonds section on Economy → Loans (Figma UI Map band 11). Pure:
    /// the host passes in the offer, the book's bonds and today's day number, and formats dates.
    /// </summary>
    public static class BondText
    {
        /// <summary>Whole ₡ the player types: "500,000", "₡500000", " 1 000 " → 50,000,000 cents; -1 if not a positive whole amount.</summary>
        public static long ParseAmountCents(string text)
        {
            if (text == null)
            {
                return -1;
            }

            var digits = new System.Text.StringBuilder();
            foreach (char ch in text)
            {
                if (ch >= '0' && ch <= '9')
                {
                    digits.Append(ch);
                }
                else if (ch != ',' && ch != ' ' && ch != '₡' && ch != ' ')
                {
                    return -1;
                }
            }

            long whole;
            if (
                digits.Length == 0
                || digits.Length > 12
                || !long.TryParse(
                    digits.ToString(),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out whole
                )
                || whole <= 0
            )
            {
                return -1;
            }

            return whole * 100;
        }

        /// <summary>
        /// The most the city can raise now, in whole ₡ (as cents): 3 × annual income − debt, rounded
        /// down to a whole ₡, never below 0 nor above the bond maximum.
        /// </summary>
        public static long MaxRaiseCents(long debtCents, long annualIncomeCents)
        {
            if (annualIncomeCents <= 0 || debtCents < 0)
            {
                return 0;
            }

            decimal room = BondPricing.MaxLeverage * (decimal)annualIncomeCents - debtCents;
            if (room <= 0)
            {
                return 0;
            }

            long cents = (long)Math.Min(room, BondTerms.MaxFaceCents);
            return cents - cents % 100;
        }

        /// <summary>₡ with cents: 190833 → "₡1,908.33".</summary>
        public static string Money(long cents)
        {
            return "₡" + (cents / 100m).ToString("#,0.00", CultureInfo.InvariantCulture);
        }

        /// <summary>Whole ₡: 50000000 → "₡500,000" (cents are dropped).</summary>
        public static string WholeMoney(long cents)
        {
            return "₡" + (cents / 100).ToString("#,0", CultureInfo.InvariantCulture);
        }

        /// <summary>"4.58% a year (4.50% + 0.08%)" — the base for the term plus the debt premium.</summary>
        public static string RateLine(BondOffer offer)
        {
            int baseRate = BondPricing.BaseRate(offer.Term);
            return Strings.Format(
                "bond.rateLine",
                BondPricing.RateText(offer.RateBasisPoints),
                BondPricing.RateText(baseRate),
                BondPricing.RateText(offer.RateBasisPoints - baseRate)
            );
        }

        /// <summary>Why an offer was refused, in the player's language.</summary>
        public static string ReasonText(string reason)
        {
            if (reason == BondPricing.ReasonDebtCap)
            {
                return Strings.Get("bond.reason.debtCap");
            }

            if (reason == BondPricing.ReasonNoIncome)
            {
                return Strings.Get("bond.reason.noIncome");
            }

            return reason == BondPricing.ReasonBadInput
                ? Strings.Get("bond.reason.badInput")
                : reason ?? string.Empty;
        }

        /// <summary>The largest coupon (the first; later ones may be a cent less).</summary>
        public static long CouponCents(BondTerms terms)
        {
            return BondMath.Schedule(terms)[0].Interest;
        }

        /// <summary>
        /// Everything the bond costs over its life: the face plus every coupon (the loan cards'
        /// Total).
        /// </summary>
        public static long TotalCents(BondTerms terms)
        {
            return terms.FaceCents + BondMath.TotalInterest(terms);
        }

        /// <summary>
        /// A term the way the game's loan cards give a payment plan: in weeks, 52 to a year
        /// ("52 Weeks", "260 Weeks").
        /// </summary>
        public static string WeeksText(BondTerm term)
        {
            return Weeks((int)term);
        }

        private static string Weeks(int years)
        {
            return WeekCount(years * BondPricing.WeeksPerYear);
        }

        private static string WeekCount(int weeks)
        {
            return Strings.Plural("bond.weeks", weeks, Number(weeks));
        }

        /// <summary>Longest name a bond can be given.</summary>
        public const int MaxNameLength = 32;

        /// <summary>The card's title: the name the player gave it, else "Bond #63".</summary>
        public static string CardTitle(IssuedBond b)
        {
            return string.IsNullOrEmpty(b.Name)
                ? Strings.Format("bond.card.defaultName", Number(b.Id))
                : b.Name;
        }

        /// <summary>
        /// What to keep of a typed name: trimmed, without line breaks or the save format's
        /// separator, at most <see cref="MaxNameLength"/> characters. Empty (the bond goes back
        /// to "Bond #63") when nothing is left or the default was typed.
        /// </summary>
        public static string NameToKeep(string typed, int id)
        {
            var kept = new System.Text.StringBuilder();
            foreach (char ch in typed ?? string.Empty)
            {
                if (ch != '|' && !char.IsControl(ch))
                {
                    kept.Append(ch);
                }
            }

            string name = kept.ToString().Trim();
            if (name.Length > MaxNameLength)
            {
                name = name.Substring(0, MaxNameLength).Trim();
            }

            return name == Strings.Format("bond.card.defaultName", Number(id))
                ? string.Empty
                : name;
        }

        /// <summary>"₡500,000": what the bond raised and repays at the end.</summary>
        public static string CardAmount(IssuedBond b)
        {
            return WholeMoney(b.Terms.FaceCents);
        }

        /// <summary>"260 Weeks": the bond's whole term.</summary>
        public static string CardTerm(IssuedBond b)
        {
            return WeekCount(b.Terms.TermDays / BondMath.WeekDays);
        }

        /// <summary>"4.58%"</summary>
        public static string CardInterest(IssuedBond b)
        {
            return BondPricing.RateText(b.Terms.RateBasisPoints);
        }

        /// <summary>"₡440.39": the coupon due next, as a loan card gives its weekly cost; "—" when none is left.</summary>
        public static string CardWeeklyCost(IssuedBond b)
        {
            return b.CouponsPaid >= b.Terms.Periods
                ? "—"
                : Money(BondMath.Schedule(b.Terms)[b.CouponsPaid].Interest);
        }

        /// <summary>"258 Weeks": the coupons still to pay, one a week.</summary>
        public static string CardTimeLeft(IssuedBond b)
        {
            return WeekCount(Math.Max(0, b.Terms.Periods - b.CouponsPaid));
        }

        /// <summary>What Pay Now costs today (the card's Total): the face plus interest accrued this period.</summary>
        public static long RepayCents(IssuedBond b, int today)
        {
            return BondMath.RedemptionAmount(b.Terms, b.CouponsPaid, today - b.LastCouponDay);
        }

        /// <summary>"Owed: loans ₡218,438 + bonds ₡700,000 = ₡918,438 · 0.07× annual income (cap 3×)"</summary>
        public static string OwedFooter(long loanCents, long bondCents, long annualIncomeCents)
        {
            long total = loanCents + bondCents;
            string leverage =
                annualIncomeCents > 0
                    ? ((decimal)total / annualIncomeCents).ToString(
                        "0.00",
                        CultureInfo.InvariantCulture
                    ) + "×"
                    : "—";
            return Strings.Format(
                "bond.owed",
                WholeMoney(loanCents),
                WholeMoney(bondCents),
                WholeMoney(total),
                leverage,
                Number(BondPricing.MaxLeverage)
            );
        }

        private static string Number(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}

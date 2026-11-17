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

        public static string TermText(BondTerm term)
        {
            return Strings.Format("bond.termName", Number((int)term));
        }

        /// <summary>A term button: "1 year", "5 years".</summary>
        public static string YearsText(BondTerm term)
        {
            return Strings.Plural("bond.years", (int)term, Number((int)term));
        }

        /// <summary>"#1 · 5-year · 4.58% · ₡500,000"</summary>
        public static string RowTitle(IssuedBond b)
        {
            return Strings.Format(
                "bond.row.title",
                Number(b.Id),
                Number(b.Terms.Periods / 12),
                BondPricing.RateText(b.Terms.RateBasisPoints),
                WholeMoney(b.Terms.FaceCents)
            );
        }

        /// <summary>"#1 · 5-year · 4.58% · ₡500,000 · repaid 01/02/2031" (the host formats the date).</summary>
        public static string RowTitle(IssuedBond b, string repaidDate)
        {
            return Strings.Format(
                "bond.row.titleRepaid",
                Number(b.Id),
                Number(b.Terms.Periods / 12),
                BondPricing.RateText(b.Terms.RateBasisPoints),
                WholeMoney(b.Terms.FaceCents),
                repaidDate
            );
        }

        /// <summary>"Next coupon ₡1,908.33 in 12 days · 58 of 60 left" (the face comes with the last).</summary>
        public static string RowNext(IssuedBond b, int today)
        {
            int left = b.Terms.Periods - b.CouponsPaid;
            if (left <= 0)
            {
                return Strings.Get("bond.row.repaid");
            }

            long coupon = BondMath.Schedule(b.Terms)[b.CouponsPaid].Interest;
            int days = Math.Max(0, b.DueDay(b.CouponsPaid + 1) - today);
            return Strings.Plural(
                left == 1 ? "bond.row.nextFace" : "bond.row.next",
                days,
                Money(coupon),
                Number(days),
                Number(left),
                Number(b.Terms.Periods)
            );
        }

        /// <summary>What "Repay now" costs today: the face plus interest accrued this period.</summary>
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

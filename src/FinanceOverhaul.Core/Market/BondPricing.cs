using System;

namespace FinanceOverhaul.Core
{
    /// <summary>The terms a city can issue bonds for.</summary>
    public enum BondTerm
    {
        OneYear = 1,
        TwoYears = 2,
        FiveYears = 5,
    }

    /// <summary>What the market offers for a city bond: a rate, or why not.</summary>
    public struct BondOffer
    {
        public bool Allowed;
        public int RateBasisPoints;
        public BondTerm Term;
        public string Reason;

        /// <summary>The bond's terms for this face value (monthly coupons, 30/360).</summary>
        public BondTerms Terms(long faceCents)
        {
            return new BondTerms(
                faceCents,
                RateBasisPoints,
                BondPricing.Periods(Term),
                BondPricing.PeriodDays
            );
        }
    }

    /// <summary>
    /// City bond pricing, "rate by debt": a base rate by term (1y 3.00%, 2y 3.50%, 5y 4.50%) plus
    /// a premium of 1.50% per 1× of leverage — (debt already owed + this bond) ÷ annual income —
    /// capped at +6.00%; an issue that would put leverage above 3× is refused. Integer basis
    /// points and cents only (see BondPricingTests for the proved bounds and monotonicity).
    /// </summary>
    public static class BondPricing
    {
        public const int PeriodDays = 30;
        public const int PremiumBasisPointsPerLeverage = 150;
        public const int MaxPremiumBasisPoints = 600;

        /// <summary>Leverage cap: debt + new bond ≤ 3 × annual income.</summary>
        public const int MaxLeverage = 3;

        public const string ReasonDebtCap = "the city would owe more than 3× its annual income";
        public const string ReasonNoIncome = "the city has no income to borrow against";
        public const string ReasonBadInput = "invalid bond request";

        public static int BaseRate(BondTerm term)
        {
            switch (term)
            {
                case BondTerm.OneYear:
                    return 300;
                case BondTerm.TwoYears:
                    return 350;
                case BondTerm.FiveYears:
                    return 450;
                default:
                    return -1;
            }
        }

        /// <summary>Basis points as a rate, e.g. 450 → "4.50%".</summary>
        public static string RateText(int basisPoints)
        {
            return (basisPoints / 100m).ToString(
                    "0.00",
                    System.Globalization.CultureInfo.InvariantCulture
                ) + "%";
        }

        public static int Periods(BondTerm term)
        {
            return (int)term * 12;
        }

        /// <param name="existingDebtCents">Owed already: vanilla loans left + outstanding bond faces.</param>
        /// <param name="faceCents">The new bond.</param>
        /// <param name="annualIncomeCents">The city's income over a game year.</param>
        public static BondOffer Offer(
            BondTerm term,
            long existingDebtCents,
            long faceCents,
            long annualIncomeCents
        )
        {
            int baseRate = BaseRate(term);
            if (
                baseRate < 0
                || existingDebtCents < 0
                || faceCents <= 0
                || faceCents > BondTerms.MaxFaceCents
            )
            {
                return Refuse(term, ReasonBadInput);
            }

            if (annualIncomeCents <= 0)
            {
                return Refuse(term, ReasonNoIncome);
            }

            // Exact decimal arithmetic: leverage = (debt + face) / income.
            decimal owed = (decimal)existingDebtCents + faceCents;
            if (owed > MaxLeverage * (decimal)annualIncomeCents)
            {
                return Refuse(term, ReasonDebtCap);
            }

            decimal leverage = owed / annualIncomeCents;
            int premium = (int)
                Math.Min(
                    MaxPremiumBasisPoints,
                    Math.Round(
                        leverage * PremiumBasisPointsPerLeverage,
                        MidpointRounding.AwayFromZero
                    )
                );
            return new BondOffer
            {
                Allowed = true,
                RateBasisPoints = Math.Min(BondTerms.MaxRateBasisPoints, baseRate + premium),
                Term = term,
            };
        }

        private static BondOffer Refuse(BondTerm term, string reason)
        {
            return new BondOffer
            {
                Allowed = false,
                Term = term,
                Reason = reason,
            };
        }
    }
}

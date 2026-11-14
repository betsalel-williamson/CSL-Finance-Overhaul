using System;
using Xunit;

namespace FinanceOverhaul.Market.Tests
{
    /// <summary>
    /// Formal properties of the city-bond rate (rate by debt), over seeded random cities
    /// (5,000 trials) plus exact examples:
    /// <list type="bullet">
    /// <item>Bounds: base(term) ≤ rate ≤ base(term) + 6.00%, never above the bond maximum.</item>
    /// <item>Monotone: more existing debt, a bigger bond, a longer term, or less income never
    /// gives a cheaper rate.</item>
    /// <item>Cap: an issue that would put debt above 3× annual income is refused; at or below it
    /// is offered.</item>
    /// <item>Safety: no income, negative or out-of-range inputs → refused, never a rate.</item>
    /// </list>
    /// </summary>
    public class BondPricingTests
    {
        private const long Million = 100_000_000; // ₡1,000,000 in cents

        [Fact]
        public void BaseRatesWithNoDebt()
        {
            // A tiny bond against a big income: leverage ≈ 0, so the base rate.
            Assert.Equal(300, Rate(BondTerm.OneYear, 0, 100, 100 * Million));
            Assert.Equal(350, Rate(BondTerm.TwoYears, 0, 100, 100 * Million));
            Assert.Equal(450, Rate(BondTerm.FiveYears, 0, 100, 100 * Million));
        }

        [Fact]
        public void PremiumIsOneAndAHalfPercentPerTimesAnnualIncome()
        {
            // Debt + bond = 1× annual income → +150 bp; 2× → +300 bp.
            Assert.Equal(450 + 150, Rate(BondTerm.FiveYears, 0, 10 * Million, 10 * Million));
            Assert.Equal(
                350 + 300,
                Rate(BondTerm.TwoYears, 15 * Million, 5 * Million, 10 * Million)
            );
        }

        [Fact]
        public void TheCapRefusesAboveThreeTimesIncome()
        {
            Assert.Equal(
                300 + 450,
                Rate(BondTerm.OneYear, 20 * Million, 10 * Million, 10 * Million)
            ); // exactly 3×
            BondOffer over = BondPricing.Offer(
                BondTerm.OneYear,
                20 * Million,
                10 * Million + 1,
                10 * Million
            );
            Assert.False(over.Allowed);
            Assert.Equal(BondPricing.ReasonDebtCap, over.Reason);
        }

        [Fact]
        public void BadInputsAreRefused()
        {
            Assert.False(BondPricing.Offer(BondTerm.OneYear, 0, Million, 0).Allowed);
            Assert.False(BondPricing.Offer(BondTerm.OneYear, -1, Million, Million).Allowed);
            Assert.False(BondPricing.Offer(BondTerm.OneYear, 0, 0, Million).Allowed);
            Assert.False(
                BondPricing.Offer(BondTerm.OneYear, 0, BondTerms.MaxFaceCents + 1, Million).Allowed
            );
            Assert.False(BondPricing.Offer((BondTerm)7, 0, Million, Million).Allowed);
        }

        [Fact]
        public void TermsAreMonthlyCouponsOverOneTwoOrFiveYears()
        {
            BondTerms t = BondPricing
                .Offer(BondTerm.FiveYears, 0, Million, 100 * Million)
                .Terms(Million);
            Assert.Equal(60, t.Periods);
            Assert.Equal(30, t.PeriodDays);
            Assert.Equal(12, BondPricing.Periods(BondTerm.OneYear));
            Assert.Equal(24, BondPricing.Periods(BondTerm.TwoYears));
        }

        [Fact]
        public void BoundsAndMonotonicity()
        {
            var rng = new Random(29052079);
            BondTerm[] terms = { BondTerm.OneYear, BondTerm.TwoYears, BondTerm.FiveYears };
            for (int k = 0; k < 5000; k++)
            {
                long income = 1 + (long)(rng.NextDouble() * 100 * Million);
                long debt = (long)(rng.NextDouble() * 4 * income);
                long face =
                    1 + (long)(rng.NextDouble() * Math.Min(BondTerms.MaxFaceCents - 1, 3 * income));
                BondTerm term = terms[rng.Next(3)];
                BondOffer o = BondPricing.Offer(term, debt, face, income);
                bool withinCap = (decimal)debt + face <= 3m * income;
                Assert.Equal(withinCap, o.Allowed);
                if (!o.Allowed)
                {
                    continue;
                }

                int baseRate = BondPricing.BaseRate(term);
                Assert.InRange(
                    o.RateBasisPoints,
                    baseRate,
                    baseRate + BondPricing.MaxPremiumBasisPoints
                );
                Assert.True(o.RateBasisPoints <= BondTerms.MaxRateBasisPoints);

                // more debt, bigger bond, less income → never cheaper (when still offered)
                AssertNotCheaper(o, BondPricing.Offer(term, debt + 1 + debt / 10, face, income));
                AssertNotCheaper(o, BondPricing.Offer(term, debt, face + 1 + face / 10, income));
                AssertNotCheaper(
                    o,
                    BondPricing.Offer(term, debt, face, Math.Max(1, income - 1 - income / 10))
                );
                // longer term → never cheaper
                for (int j = Array.IndexOf(terms, term) + 1; j < terms.Length; j++)
                {
                    AssertNotCheaper(o, BondPricing.Offer(terms[j], debt, face, income));
                }
            }
        }

        private static void AssertNotCheaper(BondOffer before, BondOffer after)
        {
            if (after.Allowed)
            {
                Assert.True(after.RateBasisPoints >= before.RateBasisPoints);
            }
        }

        private static int Rate(BondTerm term, long debt, long face, long income)
        {
            BondOffer o = BondPricing.Offer(term, debt, face, income);
            Assert.True(o.Allowed, o.Reason);
            return o.RateBasisPoints;
        }
    }
}

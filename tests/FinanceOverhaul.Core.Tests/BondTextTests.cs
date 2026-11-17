using System;
using Xunit;

namespace FinanceOverhaul.Core.Tests
{
    /// <summary>
    /// The City bonds section's text and limits. Properties over 5,000 seeded cities: the shown
    /// maximum is exactly where the market stops saying yes (that amount is offered, ₡1 more is
    /// refused, unless the bond maximum binds first); repay-now equals the proved redemption amount.
    /// </summary>
    public class BondTextTests
    {
        [Theory]
        [InlineData("500,000", 50_000_000)]
        [InlineData("₡500000", 50_000_000)]
        [InlineData(" 1 000 ", 100_000)]
        [InlineData("0", -1)]
        [InlineData("", -1)]
        [InlineData("12.5", -1)]
        [InlineData("-5", -1)]
        [InlineData("abc", -1)]
        [InlineData("1234567890123", -1)]
        public void ParseAmount(string text, long cents)
        {
            Assert.Equal(cents, BondText.ParseAmountCents(text));
        }

        [Fact]
        public void TheShownMaximumIsTheMarketsLimit()
        {
            var rng = new Random(311);
            BondTerm[] terms = { BondTerm.OneYear, BondTerm.TwoYears, BondTerm.FiveYears };
            for (int k = 0; k < 5000; k++)
            {
                long income = 1 + (long)(rng.NextDouble() * 5_000_000_000L);
                long debt = (long)(rng.NextDouble() * 4 * income);
                BondTerm term = terms[rng.Next(3)];
                long max = BondText.MaxRaiseCents(debt, income);
                Assert.Equal(0, max % 100);
                Assert.InRange(max, 0, BondTerms.MaxFaceCents);
                if (max > 0)
                {
                    Assert.True(BondPricing.Offer(term, debt, max, income).Allowed);
                }

                if (max + 100 <= BondTerms.MaxFaceCents)
                {
                    Assert.False(BondPricing.Offer(term, debt, max + 100, income).Allowed);
                }
            }
        }

        [Fact]
        public void MaxRaiseExamples()
        {
            // 3 × ₡13,753,838.80 − ₡218,437.82 = ₡41,043,078.58 → ₡41,043,078
            Assert.Equal(4_104_307_800, BondText.MaxRaiseCents(21_843_782, 1_375_383_880));
            Assert.Equal(0, BondText.MaxRaiseCents(400, 100)); // already over the cap
            Assert.Equal(0, BondText.MaxRaiseCents(0, 0)); // no income
        }

        [Fact]
        public void QuoteAndRowText()
        {
            // The Figma example: ₡500,000 for 5 years at 4.58%.
            BondOffer offer = BondPricing.Offer(
                BondTerm.FiveYears,
                21_843_782,
                50_000_000,
                1_375_383_880
            );
            Assert.Equal(458, offer.RateBasisPoints);
            Assert.Equal("4.58% a year (4.50% + 0.08%)", BondText.RateLine(offer));
            BondTerms t = offer.Terms(50_000_000);
            Assert.Equal(190_834, BondText.CouponCents(t)); // ₡114,500 over 60: 20 coupons a cent more
            Assert.Equal("₡1,908.34", BondText.Money(BondText.CouponCents(t)));

            var book = new BondBook();
            IssuedBond b;
            book.Issue(t, 1000, out b);
            Assert.Equal("#1 · 5-year · 4.58% · ₡500,000", BondText.RowTitle(b));
            Assert.Equal(
                "Next coupon ₡1,908.34 in 30 days · 60 of 60 left",
                BondText.RowNext(b, 1000)
            );
            book.Due(1030 + 18); // one coupon paid, 18 days into the next period
            Assert.Equal(
                "Next coupon ₡1,908.34 in 12 days · 59 of 60 left",
                BondText.RowNext(b, 1048)
            );
            Assert.Equal(BondMath.RedemptionAmount(t, 1, 18), BondText.RepayCents(b, 1048));
            Assert.Equal(50_000_000 + 190_834 * 18 / 30, BondText.RepayCents(b, 1048));
        }

        [Fact]
        public void LastCouponMentionsTheFace()
        {
            var book = new BondBook();
            IssuedBond b;
            book.Issue(new BondTerms(10_000, 300, 12, 30), 0, out b);
            book.Due(330);
            Assert.StartsWith(
                "Next coupon ₡0.25 + the face in 30 days · 1 of 12 left",
                BondText.RowNext(b, 330)
            );
        }

        [Fact]
        public void Footer()
        {
            Assert.Equal(
                "Owed: loans ₡218,437 + bonds ₡700,000 = ₡918,437 · 0.07× annual income (cap 3×)",
                BondText.OwedFooter(21_843_782, 70_000_000, 1_375_383_880)
            );
        }
    }
}

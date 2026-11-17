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
            Assert.Equal(260, t.Periods); // a coupon a week for five years of 52 weeks
            Assert.Equal(7, t.PeriodDays);
            Assert.Equal(44_039, BondText.CouponCents(t)); // ₡114,500 over 260: 120 coupons a cent more
            Assert.Equal("₡440.39", BondText.Money(BondText.CouponCents(t)));
            Assert.Equal(50_000_000 + 11_450_000, BondText.TotalCents(t)); // face + ₡114,500 interest

            var book = new BondBook();
            IssuedBond b;
            book.Issue(t, 1000, out b);
            Assert.Equal("Bond #1", BondText.CardTitle(b));
            Assert.Equal("₡500,000", BondText.CardAmount(b));
            Assert.Equal("260 Weeks", BondText.CardTerm(b));
            Assert.Equal("4.58%", BondText.CardInterest(b));
            Assert.Equal("₡440.39", BondText.CardWeeklyCost(b));
            Assert.Equal("260 Weeks", BondText.CardTimeLeft(b));
            book.Due(1007 + 3); // one coupon paid, 3 days into the next week
            Assert.Equal("259 Weeks", BondText.CardTimeLeft(b));
            Assert.Equal(BondMath.RedemptionAmount(t, 1, 3), BondText.RepayCents(b, 1010));
            Assert.Equal(50_000_000 + 44_039 * 3 / 7, BondText.RepayCents(b, 1010));
        }

        [Fact]
        public void ANamedBondShowsItsName()
        {
            var book = new BondBook();
            IssuedBond b;
            book.Issue(new BondTerms(10_000, 300, 52, 7), 0, out b);
            Assert.True(book.Rename(b.Id, "New hospital"));
            Assert.False(book.Rename(b.Id, "New hospital")); // unchanged
            Assert.False(book.Rename(99, "x")); // unknown
            Assert.Equal("New hospital", BondText.CardTitle(b));
            Assert.Equal("52 Weeks", BondText.CardTerm(b));
            Assert.True(book.Rename(b.Id, ""));
            Assert.Equal("Bond #1", BondText.CardTitle(b));
        }

        [Theory]
        [InlineData("  New hospital ", "New hospital")]
        [InlineData("Bond #7", "")] // the default typed back: no name
        [InlineData("   ", "")]
        [InlineData(null, "")]
        [InlineData("a|b\nc", "abc")] // the save format's separators are dropped
        [InlineData("12345678901234567890123456789012345", "12345678901234567890123456789012")]
        public void TypedNamesAreTidied(string typed, string kept)
        {
            Assert.Equal(kept, BondText.NameToKeep(typed, 7));
        }

        [Fact]
        public void NamesSurviveTheSaveAndOlderBuildsStillReadTheBonds()
        {
            var book = new BondBook();
            IssuedBond a,
                b;
            book.Issue(new BondTerms(10_000, 300, 12, 30), 0, out a);
            book.Issue(new BondTerms(20_000, 300, 12, 30), 0, out b);
            book.Rename(b.Id, "Metro line 2");
            string saved = BondBookCodec.Format(book);
            BondBook back = BondBookCodec.Parse(saved);
            Assert.Null(back.Find(a.Id).Name);
            Assert.Equal("Metro line 2", back.Find(b.Id).Name);
            Assert.Equal(saved, BondBookCodec.Format(back));

            // Every bond line keeps the eight fields a build from before names expects.
            foreach (string line in saved.Split('\n'))
            {
                if (line.StartsWith("issued|"))
                {
                    Assert.Equal(8, line.Split('|').Length);
                }
            }

            // A name for a bond that is gone is dropped.
            Assert.Empty(BondBookCodec.Parse("v1\nnext=3\nname|2|Ghost\n").Bonds);
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

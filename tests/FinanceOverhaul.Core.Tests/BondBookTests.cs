using System; // Math, Random
using System.Collections.Generic; // IEnumerable, List
using System.Linq; // Last(), Select(), Sum(), Take(), ToArray(), Where()
using Xunit; // Assert, FactAttribute

namespace FinanceOverhaul.Core.Tests
{
    /// <summary>
    /// Formal properties of the city's bond book, over seeded random books and check-day paths
    /// (500 trials) plus exact examples:
    /// <list type="bullet">
    /// <item>Lifetime conservation: everything paid over a bond's life is face + total interest,
    /// to the cent, however the days are checked (daily, in jumps, or once at the end).</item>
    /// <item>Path independence: what is paid by day D depends only on D.</item>
    /// <item>Idempotent: checking the same day twice pays nothing the second time.</item>
    /// <item>Never early: no payment is made before its due day, and each carries its due day.</item>
    /// <item>Early redemption: paid so far + redemption lies between face + coupons paid and
    /// face + total interest, and removes only that bond.</item>
    /// <item>Codec: format → parse is the identity; unknown versions, malformed and out-of-range
    /// lines are skipped, never thrown.</item>
    /// </list>
    /// </summary>
    public class BondBookTests
    {
        private const int Trials = 500;

        private static BondTerms RandomTerms(Random rng)
        {
            long face = 100 + (long)(rng.NextDouble() * 1_000_000_000L);
            BondTerm[] terms = { BondTerm.OneYear, BondTerm.TwoYears, BondTerm.FiveYears };
            BondTerm term = terms[rng.Next(3)];
            return new BondTerms(
                face,
                rng.Next(0, 2001),
                BondPricing.Periods(term),
                BondPricing.PeriodDays
            );
        }

        private static long Out(IEnumerable<BondCash> cash)
        {
            return cash.Where(c => c.Kind != BondCashKind.Proceeds).Sum(c => c.Cents);
        }

        [Fact]
        public void ExampleOneYearBond()
        {
            // ₡1,000,000 at 3.00% for 1 year: ₡2,500 a month (250,000 cents), face on day 360.
            var book = new BondBook();
            IssuedBond bond;
            BondCash proceeds = book.Issue(new BondTerms(100_000_000, 300, 12, 30), 1000, out bond);
            Assert.Equal(100_000_000, proceeds.Cents);
            Assert.Equal(BondCashKind.Proceeds, proceeds.Kind);
            Assert.Empty(book.Due(1029));
            List<BondCash> first = book.Due(1030);
            Assert.Single(first);
            Assert.Equal(250_000, first[0].Cents);
            Assert.Equal(BondCashKind.Coupon, first[0].Kind);
            List<BondCash> rest = book.Due(1360);
            Assert.Equal(12, rest.Count); // 11 coupons + the face
            Assert.Equal(100_000_000, rest.Last().Cents);
            Assert.Equal(BondCashKind.Principal, rest.Last().Kind);
            Assert.Empty(book.Bonds);
            Assert.Equal(0, book.OutstandingFaceCents);
        }

        [Fact]
        public void LifetimeConservationOnAnyPath()
        {
            var rng = new Random(14112026);
            for (int k = 0; k < Trials; k++)
            {
                BondTerms t = RandomTerms(rng);
                var book = new BondBook();
                IssuedBond bond;
                int start = rng.Next(0, 100_000);
                book.Issue(t, start, out bond);
                var paid = new List<BondCash>();
                int day = start;
                int end = start + t.TermDays + 50;
                while (day < end)
                {
                    day += rng.Next(3) == 0 ? rng.Next(1, 400) : 1;
                    List<BondCash> due = book.Due(day);
                    foreach (BondCash c in due)
                    {
                        Assert.True(c.Day <= day, "paid before its due day");
                        Assert.True(c.Cents > 0);
                    }

                    paid.AddRange(due);
                    Assert.Empty(book.Due(day)); // idempotent
                }

                Assert.Equal(t.FaceCents + BondMath.TotalInterest(t), Out(paid));
                Assert.Empty(book.Bonds);
            }
        }

        [Fact]
        public void WhatIsPaidByADayDependsOnlyOnTheDay()
        {
            var rng = new Random(360);
            for (int k = 0; k < Trials; k++)
            {
                BondTerms t = RandomTerms(rng);
                int target = rng.Next(0, t.TermDays + 60);
                var daily = new BondBook();
                var once = new BondBook();
                IssuedBond b1,
                    b2;
                daily.Issue(t, 0, out b1);
                once.Issue(t, 0, out b2);
                long dailyPaid = 0;
                for (int d = 1; d <= target; d++)
                {
                    dailyPaid += Out(daily.Due(d));
                }

                Assert.Equal(dailyPaid, Out(once.Due(target)));
                Assert.Equal(b1.CouponsPaid, b2.CouponsPaid);
                Assert.Equal(Math.Min(t.Periods, target / t.PeriodDays), b1.CouponsPaid);
            }
        }

        [Fact]
        public void EarlyRedemptionBounds()
        {
            var rng = new Random(77);
            for (int k = 0; k < Trials; k++)
            {
                BondTerms t = RandomTerms(rng);
                var book = new BondBook();
                IssuedBond mine,
                    other;
                book.Issue(t, 0, out mine);
                book.Issue(RandomTerms(rng), 0, out other);
                int day = rng.Next(0, t.TermDays);
                long before = Out(book.Due(day).Where(c => c.BondId == mine.Id));
                int paidCoupons = mine.CouponsPaid;
                bool otherLive = book.Find(other.Id) != null;
                BondCash r;
                List<BondCash> due;
                Assert.True(book.Redeem(mine.Id, day, out r, out due));
                long total = before + Out(due) + r.Cents;
                long couponsSoFar = BondMath.Schedule(t).Take(paidCoupons).Sum(p => p.Interest);
                Assert.InRange(
                    total,
                    t.FaceCents + couponsSoFar,
                    t.FaceCents + BondMath.TotalInterest(t)
                );
                Assert.Null(book.Find(mine.Id));
                Assert.Equal(otherLive, book.Find(other.Id) != null); // only that bond
                Assert.False(book.Redeem(mine.Id, day, out r, out due));
            }
        }

        [Fact]
        public void RedeemingAfterMaturityPaysTheScheduleOnly()
        {
            var book = new BondBook();
            IssuedBond b;
            var t = new BondTerms(10_000, 1200, 12, 30);
            book.Issue(t, 0, out b);
            BondCash r;
            List<BondCash> due;
            Assert.True(book.Redeem(b.Id, 9999, out r, out due));
            Assert.Equal(0, r.Cents);
            Assert.Equal(t.FaceCents + BondMath.TotalInterest(t), Out(due));
        }

        [Fact]
        public void OutstandingFaceIsTheSumOfLiveBonds()
        {
            var book = new BondBook();
            IssuedBond a,
                b;
            book.Issue(new BondTerms(5_000, 300, 12, 30), 0, out a);
            book.Issue(new BondTerms(7_000, 300, 24, 30), 0, out b);
            Assert.Equal(12_000, book.OutstandingFaceCents);
            book.Due(360);
            Assert.Equal(7_000, book.OutstandingFaceCents);
        }

        [Fact]
        public void CodecRoundTrip()
        {
            var rng = new Random(5);
            for (int k = 0; k < Trials; k++)
            {
                var book = new BondBook();
                int n = rng.Next(0, 6);
                for (int i = 0; i < n; i++)
                {
                    IssuedBond b;
                    book.Issue(RandomTerms(rng), rng.Next(-1000, 100_000), out b);
                }

                book.Due(rng.Next(0, 200_000));
                BondBook back = BondBookCodec.Parse(BondBookCodec.Format(book));
                Assert.Equal(BondBookCodec.Format(book), BondBookCodec.Format(back));
                Assert.Equal(book.NextId, back.NextId);
                Assert.Equal(book.Bonds.Count, back.Bonds.Count);
            }
        }

        [Fact]
        public void CodecSkipsWhatItCannotRead()
        {
            Assert.Empty(BondBookCodec.Parse(null).Bonds);
            Assert.Empty(BondBookCodec.Parse("v9\nissued|1|100|300|12|30|0|0\n").Bonds);
            BondBook b = BondBookCodec.Parse(
                "v1\nnext=4\n"
                    + "issued|1|100|300|12|30|0|0\n" // ok
                    + "issued|1|100|300|12|30|0|0\n" // duplicate id
                    + "issued|2|0|300|12|30|0|0\n" // zero face
                    + "issued|3|100|99999|12|30|0|0\n" // rate out of range
                    + "issued|5|100|300|12|30|0|12\n" // already repaid
                    + "issued|6|abc|300|12|30|0|0\n" // malformed
                    + "future-kind|7|1|2\n" // a newer build's line
                    + "issued|8|100|300|12|30|5|3\r\n" // ok, CRLF
            );
            Assert.Equal(new[] { 1, 8 }, b.Bonds.Select(x => x.Id).ToArray());
            Assert.Equal(9, b.NextId);
            Assert.Equal(3, b.Find(8).CouponsPaid);
        }
    }
}

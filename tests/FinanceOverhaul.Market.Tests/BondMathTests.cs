using System;
using System.Linq;
using Xunit;

namespace FinanceOverhaul.Market.Tests
{
    /// <summary>
    /// Formal properties of the bond cash-flow math, in integer cents, over seeded random terms
    /// (1,000 trials: face ₡1–₡100,000,000, rate 0–20%, 1–360 periods) plus exact examples:
    /// <list type="bullet">
    /// <item>Conservation: coupons + principal = face + total interest, to the cent.</item>
    /// <item>Total interest = round(face × rate × term), half up.</item>
    /// <item>Fair split: every coupon is the floor or the ceiling of the ideal coupon.</item>
    /// <item>Shape: no negative payment; principal only at maturity (bullet bond).</item>
    /// <item>Monotone: more rate or more periods never means less interest.</item>
    /// <item>Early redemption lies between the principal and principal + next coupon, never
    /// falls as days pass within a period, and equals them at the period's ends.</item>
    /// <item>No overflow anywhere in the domain.</item>
    /// </list>
    /// </summary>
    public class BondMathTests
    {
        private const int Trials = 1000;

        private static BondTerms Random(Random rng)
        {
            long face = (long)Math.Exp(rng.NextDouble() * Math.Log(10_000_000_000d)) + 100; // cents
            int bp = rng.Next(0, 2001);
            int periods = rng.Next(1, 361);
            return new BondTerms(face, bp, periods, 30);
        }

        private static void Trial(Action<BondTerms> check)
        {
            var rng = new Random(20791114);
            for (int k = 0; k < Trials; k++)
            {
                check(Random(rng));
            }
        }

        [Fact]
        public void ExampleMunicipalBond()
        {
            // ₡1,000,000 at 4.5% for 2 years, monthly coupons: ₡90,000 interest, ₡3,750 a month.
            var t = new BondTerms(100_000_000, 450, 24, 30);
            Assert.Equal(9_000_000, BondMath.TotalInterest(t));
            BondPayment[] s = BondMath.Schedule(t);
            Assert.Equal(24, s.Length);
            Assert.All(s, p => Assert.Equal(375_000, p.Interest));
            Assert.Equal(100_000_000, s[23].Principal);
            Assert.Equal(0, s[0].Principal);
        }

        [Fact]
        public void UnevenCouponsAreSpreadByLargestRemainder()
        {
            // ₡100.00 at 10% for 7 periods of 30 days (30/360): 10_000 × 10% × 210/360 = 583.3
            // → 583 cents over 7 coupons: 83 each, the first 2 carry the 2 leftover cents.
            var t = new BondTerms(10_000, 1000, 7, 30);
            Assert.Equal(583, BondMath.TotalInterest(t));
            BondPayment[] s = BondMath.Schedule(t);
            Assert.Equal(
                new long[] { 84, 84, 83, 83, 83, 83, 83 },
                s.Select(p => p.Interest).ToArray()
            );
        }

        [Fact]
        public void Conservation()
        {
            Trial(t =>
            {
                BondPayment[] s = BondMath.Schedule(t);
                Assert.Equal(
                    t.FaceCents + BondMath.TotalInterest(t),
                    s.Sum(p => p.Interest + p.Principal)
                );
            });
        }

        [Fact]
        public void TotalInterestIsTheRoundedExactValue()
        {
            Trial(t =>
            {
                decimal exact =
                    t.FaceCents
                    * (decimal)t.RateBasisPoints
                    / 10000m
                    * t.Periods
                    * t.PeriodDays
                    / 360m;
                long expected = (long)Math.Floor(exact + 0.5m);
                Assert.Equal(expected, BondMath.TotalInterest(t));
            });
        }

        [Fact]
        public void EveryCouponIsTheFloorOrCeilingOfTheIdeal()
        {
            Trial(t =>
            {
                long total = BondMath.TotalInterest(t);
                long floor = total / t.Periods;
                long ceil = floor + (total % t.Periods == 0 ? 0 : 1);
                Assert.All(BondMath.Schedule(t), p => Assert.InRange(p.Interest, floor, ceil));
            });
        }

        [Fact]
        public void BulletShape()
        {
            Trial(t =>
            {
                BondPayment[] s = BondMath.Schedule(t);
                Assert.Equal(t.Periods, s.Length);
                for (int i = 0; i < s.Length; i++)
                {
                    Assert.True(s[i].Interest >= 0 && s[i].Principal >= 0);
                    Assert.Equal(i + 1, s[i].Period);
                    Assert.Equal(i == s.Length - 1 ? t.FaceCents : 0, s[i].Principal);
                }
            });
        }

        [Fact]
        public void MoreRateOrMorePeriodsNeverMeansLessInterest()
        {
            Trial(t =>
            {
                var moreRate = new BondTerms(
                    t.FaceCents,
                    Math.Min(2000, t.RateBasisPoints + 1),
                    t.Periods,
                    t.PeriodDays
                );
                var moreTime = new BondTerms(
                    t.FaceCents,
                    t.RateBasisPoints,
                    Math.Min(BondTerms.MaxPeriods, t.Periods + 1),
                    t.PeriodDays
                );
                Assert.True(BondMath.TotalInterest(moreRate) >= BondMath.TotalInterest(t));
                Assert.True(BondMath.TotalInterest(moreTime) >= BondMath.TotalInterest(t));
            });
        }

        [Fact]
        public void ZeroRatePaysNoInterest()
        {
            var t = new BondTerms(123_456, 0, 12, 30);
            Assert.Equal(0, BondMath.TotalInterest(t));
            Assert.All(BondMath.Schedule(t), p => Assert.Equal(0, p.Interest));
        }

        [Fact]
        public void EarlyRedemptionIsPrincipalPlusAccruedInterest()
        {
            var rng = new Random(7);
            Trial(t =>
            {
                BondPayment[] s = BondMath.Schedule(t);
                int paid = rng.Next(0, t.Periods); // coupons already paid
                long next = s[paid].Interest;
                long atStart = BondMath.RedemptionAmount(t, paid, 0);
                long atEnd = BondMath.RedemptionAmount(t, paid, t.PeriodDays);
                Assert.Equal(t.FaceCents, atStart);
                Assert.Equal(t.FaceCents + next, atEnd);
                long last = atStart;
                for (int d = 1; d <= t.PeriodDays; d += Math.Max(1, t.PeriodDays / 7))
                {
                    long v = BondMath.RedemptionAmount(t, paid, d);
                    Assert.InRange(v, t.FaceCents, t.FaceCents + next);
                    Assert.True(v >= last);
                    last = v;
                }
            });
        }

        [Fact]
        public void RedemptionClampsOutOfRangeDays()
        {
            var t = new BondTerms(100_000, 500, 12, 30);
            Assert.Equal(BondMath.RedemptionAmount(t, 3, 0), BondMath.RedemptionAmount(t, 3, -5));
            Assert.Equal(BondMath.RedemptionAmount(t, 3, 30), BondMath.RedemptionAmount(t, 3, 999));
            Assert.Equal(0, BondMath.RedemptionAmount(t, 12, 0)); // fully repaid
        }

        [Fact]
        public void LargestTermsDoNotOverflow()
        {
            var t = new BondTerms(10_000_000_000, 2000, 360, 365); // ₡100M, 20%, 360 years of periods
            long total = BondMath.TotalInterest(t);
            Assert.Equal(730_000_000_000, total); // 1e10 × 20% × 131,400 / 360
            Assert.Equal(
                t.FaceCents + total,
                BondMath.Schedule(t).Sum(p => p.Interest + p.Principal)
            );
        }

        [Fact]
        public void InvalidTermsAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new BondTerms(0, 100, 12, 30));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BondTerms(100, -1, 12, 30));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BondTerms(100, 100, 0, 30));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BondTerms(100, 100, 12, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BondTerms(100, 2001, 12, 30));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new BondTerms(10_000_000_001, 100, 12, 30)
            );
        }
    }
}

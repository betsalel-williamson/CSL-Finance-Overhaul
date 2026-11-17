using System;

namespace FinanceOverhaul.Core
{
    /// <summary>
    /// A fixed-rate bullet bond: <see cref="FaceCents"/> borrowed (or lent), a coupon every
    /// <see cref="PeriodDays"/> game days for <see cref="Periods"/> periods, the face repaid with
    /// the last coupon. Money is in integer cents throughout; rates in basis points a year
    /// (450 = 4.50%). Bounds keep every product inside a 64-bit integer.
    /// </summary>
    public sealed class BondTerms
    {
        public const long MaxFaceCents = 10_000_000_000; // ₡100,000,000
        public const int MaxRateBasisPoints = 2000; // 20%
        public const int MaxPeriods = 360;
        public const int MaxPeriodDays = 365;

        public BondTerms(long faceCents, int rateBasisPoints, int periods, int periodDays)
        {
            if (faceCents <= 0 || faceCents > MaxFaceCents)
            {
                throw new ArgumentOutOfRangeException("faceCents");
            }

            if (rateBasisPoints < 0 || rateBasisPoints > MaxRateBasisPoints)
            {
                throw new ArgumentOutOfRangeException("rateBasisPoints");
            }

            if (periods < 1 || periods > MaxPeriods)
            {
                throw new ArgumentOutOfRangeException("periods");
            }

            if (periodDays < 1 || periodDays > MaxPeriodDays)
            {
                throw new ArgumentOutOfRangeException("periodDays");
            }

            FaceCents = faceCents;
            RateBasisPoints = rateBasisPoints;
            Periods = periods;
            PeriodDays = periodDays;
        }

        public long FaceCents { get; private set; }
        public int RateBasisPoints { get; private set; }
        public int Periods { get; private set; }
        public int PeriodDays { get; private set; }

        public int TermDays
        {
            get { return Periods * PeriodDays; }
        }
    }

    /// <summary>One scheduled payment (period 1 is the first coupon).</summary>
    public struct BondPayment
    {
        public int Period;
        public long Interest;
        public long Principal;

        public long Total
        {
            get { return Interest + Principal; }
        }
    }

    /// <summary>
    /// Bond cash flows in integer cents, exact to the cent (see BondMathTests for the proved
    /// properties): simple annual interest, rounded half up once for the whole bond. A year is
    /// 52 weeks for a bond with weekly coupons (a 7-day period = 1/52 year, as the game's loans
    /// count) and 360 days otherwise (30/360: a 30-day period = 1/12 year, the bonds sold before
    /// coupons became weekly). The interest is then split over the coupons by largest remainder so they add up exactly and differ
    /// by at most one cent. Issuer and holder use the same schedule, so what one pays the other
    /// receives.
    /// </summary>
    public static class BondMath
    {
        /// <summary>Days in a week: the period of a bond with weekly coupons.</summary>
        public const int WeekDays = 7;

        /// <summary>
        /// Days in the bond's year: 364 (52 weeks) with weekly coupons, else 360 (30/360, a
        /// common bond convention: a 30-day period is exactly 1/12 of a year).
        /// </summary>
        public static long YearDays(BondTerms t)
        {
            return t.PeriodDays == WeekDays ? 364 : 360;
        }

        private const long BasisPoints = 10_000;

        /// <summary>round(face × rate × termDays / year days), half up. Max product 1e10 × 2000 × 131,400 ≈ 2.6e18 &lt; 2^63.</summary>
        public static long TotalInterest(BondTerms t)
        {
            long numerator = t.FaceCents * t.RateBasisPoints * t.TermDays; // ≤ 2.6e18
            long denominator = BasisPoints * YearDays(t);
            return (numerator + denominator / 2) / denominator;
        }

        /// <summary>The payments, period 1..n: coupons split by largest remainder, face at the end.</summary>
        public static BondPayment[] Schedule(BondTerms t)
        {
            long total = TotalInterest(t);
            long floor = total / t.Periods;
            long extra = total % t.Periods; // the first `extra` coupons carry one more cent
            var s = new BondPayment[t.Periods];
            for (int i = 0; i < t.Periods; i++)
            {
                s[i] = new BondPayment
                {
                    Period = i + 1,
                    Interest = floor + (i < extra ? 1 : 0),
                    Principal = i == t.Periods - 1 ? t.FaceCents : 0,
                };
            }

            return s;
        }

        /// <summary>
        /// What repays the bond now: the face plus interest accrued in the current period —
        /// the next coupon × daysIntoPeriod / periodDays, rounded down (in the payer's favour by
        /// under a cent). 0 once every period is paid.
        /// </summary>
        /// <param name="couponsPaid">Coupons already paid (0..periods).</param>
        /// <param name="daysIntoPeriod">Days since the last coupon (clamped to 0..periodDays).</param>
        public static long RedemptionAmount(BondTerms t, int couponsPaid, int daysIntoPeriod)
        {
            if (couponsPaid >= t.Periods)
            {
                return 0;
            }

            int paid = Math.Max(0, couponsPaid);
            int days = Math.Max(0, Math.Min(t.PeriodDays, daysIntoPeriod));
            long next = Schedule(t)[paid].Interest;
            return t.FaceCents + next * days / t.PeriodDays;
        }
    }
}

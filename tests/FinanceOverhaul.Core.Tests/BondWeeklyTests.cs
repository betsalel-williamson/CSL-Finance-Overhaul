using System.Linq;
using FinanceOverhaul.Core;
using Xunit;

namespace FinanceOverhaul.Core.Tests
{
    public class BondWeeklyTests
    {
        [Fact]
        public void AYearOfWeeklyCouponsPaysExactlyTheYearlyRate()
        {
            // ₡1,000 at 3% for 52 weeks: ₡30.00 of interest, as a year of the old monthly
            // coupons paid.
            var weekly = new BondTerms(100_000, 300, 52, 7);
            var monthly = new BondTerms(100_000, 300, 12, 30);
            Assert.Equal(3_000, BondMath.TotalInterest(weekly));
            Assert.Equal(BondMath.TotalInterest(monthly), BondMath.TotalInterest(weekly));
            BondPayment[] schedule = BondMath.Schedule(weekly);
            Assert.Equal(52, schedule.Length);
            Assert.Equal(3_000, schedule.Sum(p => p.Interest));
            Assert.Equal(58, schedule[0].Interest); // ₡0.5769: 36 coupons of 58 cents, 16 of 57
            Assert.Equal(57, schedule[51].Interest);
            Assert.Equal(100_000, schedule[51].Principal);
            Assert.All(schedule.Take(51), p => Assert.Equal(0, p.Principal));
        }

        [Fact]
        public void FiveYearsIs260CouponsAndFiveTimesTheRate()
        {
            var t = new BondTerms(150_000_000, 463, 260, 7);
            Assert.Equal(150_000_000L * 463 * 5 / 10_000, BondMath.TotalInterest(t));
            Assert.Equal(1820, t.TermDays);
        }

        [Fact]
        public void AnOldMonthlyBondBecomesWeeklyFromItsLastCoupon()
        {
            // The player's bond: ₡1,500,000 at 4.63% for 5 years, 53 of 60 coupons paid.
            var book = new BondBook();
            IssuedBond b;
            book.Issue(new BondTerms(150_000_000, 463, 60, 30), 1000, out b);
            book.Rename(b.Id, "Hospital");
            book.Due(1000 + 53 * 30 + 12); // 12 days into the 54th month
            Assert.Equal(53, b.CouponsPaid);
            int repaidBefore = b.DueDay(60);
            long owedBefore = BondMath.Schedule(b.Terms).Skip(53).Sum(p => p.Interest);

            Assert.Equal(1, BondMigration.ToWeekly(book));

            Assert.Equal(7, b.Terms.PeriodDays);
            Assert.Equal(30, b.Terms.Periods); // 7 months of 30 days = 210 days = 30 weeks
            Assert.Equal(0, b.CouponsPaid);
            Assert.Equal(150_000_000, b.Terms.FaceCents);
            Assert.Equal(463, b.Terms.RateBasisPoints);
            Assert.Equal("Hospital", b.Name);
            Assert.Equal(repaidBefore, b.DueDay(b.Terms.Periods)); // repaid on the same day
            long owedAfter = BondMath.TotalInterest(b.Terms);
            Assert.True(owedAfter <= owedBefore, "never more interest than before");
            Assert.True(owedAfter >= owedBefore * 98 / 100, "and within 2% of it");

            // The 12 days already into the month: one weekly coupon is due at once, no more.
            Assert.Single(book.Due(1000 + 53 * 30 + 12));
            Assert.Equal(1, b.CouponsPaid);

            // Converting again changes nothing.
            Assert.Equal(0, BondMigration.ToWeekly(book));
            Assert.Equal(30, b.Terms.Periods);
        }

        [Theory]
        [InlineData(12, 0)]
        [InlineData(12, 11)]
        [InlineData(24, 5)]
        [InlineData(60, 0)]
        [InlineData(60, 59)]
        public void TheRepaymentDayMovesByAtMostThreeDaysAndTheFaceIsRepaidOnce(
            int months,
            int paid
        )
        {
            var book = new BondBook();
            IssuedBond b;
            book.Issue(new BondTerms(50_000_000, 458, months, 30), 0, out b);
            book.Due(paid * 30);
            int repaidBefore = b.DueDay(months);
            BondMigration.ToWeekly(book);
            Assert.InRange(b.DueDay(b.Terms.Periods) - repaidBefore, -3, 3);
            Assert.True(b.Terms.Periods >= 1);
            var all = book.Due(repaidBefore + 10);
            Assert.Equal(
                50_000_000,
                all.Where(c => c.Kind == BondCashKind.Principal).Sum(c => c.Cents)
            );
            Assert.Empty(book.Bonds); // matured and gone
        }

        [Fact]
        public void WeeklyBondsAreLeftAlone()
        {
            var book = new BondBook();
            IssuedBond b;
            book.Issue(new BondTerms(100_000, 300, 52, 7), 0, out b);
            book.Due(21);
            Assert.Equal(0, BondMigration.ToWeekly(book));
            Assert.Equal(3, b.CouponsPaid);
            Assert.Equal(52, b.Terms.Periods);
        }
    }
}

using System; // Math
using FinanceOverhaul.Core; // IpoBuyout, IpoDice, IpoRules, PeriodDraw
using Xunit; // Assert, FactAttribute, InlineDataAttribute, TheoryAttribute

namespace FinanceOverhaul.Core.Tests
{
    public class IpoBuyoutTests
    {
        [Fact]
        public void ACompanyIsBoughtAboutFourTimesInAHundredYears()
        {
            var rules = new IpoRules();
            // A year of monthly chances (365 ÷ 30 of them) makes exactly the yearly chance.
            double year = 1d - Math.Pow(1d - IpoBuyout.PeriodChance(rules), 365d / 30d);
            Assert.Equal(0.04, year, 10);
            Assert.InRange(IpoBuyout.ExpectedYearsBetween(1, rules), 24d, 25d);
            Assert.InRange(IpoBuyout.ExpectedYearsBetween(12, rules), 2.0d, 2.1d);
            Assert.True(double.IsPositiveInfinity(IpoBuyout.ExpectedYearsBetween(0, rules)));
        }

        [Theory]
        [InlineData(1, 24.5d)]
        [InlineData(6, 4.08d)]
        [InlineData(12, 2.04d)]
        public void SimulatedYearsMatch(int listed, double years)
        {
            var rules = new IpoRules();
            var dice = new IpoDice(4UL);
            const int Periods = 12 * 600_000;
            int bought = 0;
            for (int period = 0; period < Periods; period++)
            {
                if (IpoBuyout.DrawPeriod(listed, rules, dice).Happens)
                {
                    bought++;
                }
            }

            Assert.InRange(Periods * 30d / 365d / bought, years * 0.95, years * 1.05);
        }

        [Fact]
        public void NoBuyoutInTheFirstYearOrWhenSwitchedOffOrWithNothingListed()
        {
            var rules = new IpoRules();
            Assert.False(IpoBuyout.Eligible(364, rules));
            Assert.True(IpoBuyout.Eligible(365, rules));

            var dice = new IpoDice(8UL);
            ulong before = dice.State;
            Assert.False(IpoBuyout.DrawPeriod(0, rules, dice).Happens);
            Assert.Equal(before, dice.State);

            var off = new IpoRules { BuyoutPercentPerYear = 0 };
            for (int week = 0; week < 100_000; week++)
            {
                Assert.False(IpoBuyout.DrawPeriod(12, off, dice).Happens);
            }
        }

        [Fact]
        public void EveryEligibleCompanyIsEquallyLikely()
        {
            var rules = new IpoRules { BuyoutPercentPerYear = 90 }; // frequent, to count
            var dice = new IpoDice(21UL);
            var picks = new int[6];
            int total = 0;
            for (int week = 0; week < 1_000_000; week++)
            {
                int pick = IpoBuyout.DrawPeriod(picks.Length, rules, dice).Index;
                if (pick >= 0)
                {
                    picks[pick]++;
                    total++;
                }
            }

            foreach (int count in picks)
            {
                Assert.InRange(count, total / 6 * 0.93, total / 6 * 1.07);
            }
        }

        [Fact]
        public void ThePremiumAveragesFortyPercentAndIsNeverSmallOrAbsurd()
        {
            var rules = new IpoRules();
            var dice = new IpoDice(40UL);
            const int N = 300_000;
            double sum = 0;
            int big = 0;
            for (int i = 0; i < N; i++)
            {
                double premium = IpoBuyout.Premium(rules, dice);
                Assert.InRange(premium, IpoBuyout.MinPremium, IpoBuyout.MaxPremium);
                sum += premium;
                if (premium > 0.6)
                {
                    big++;
                }
            }

            Assert.InRange(sum / N, 0.39, 0.41);
            Assert.InRange(big / (double)N, 0.05, 0.15); // now and then a much better offer
        }

        [Theory]
        [InlineData(1000u, 5_200L, 0.40, 7_300_000L)] // ₡52 + 40% = ₡72.80 → ₡73 × 1,000
        [InlineData(250u, 52_000L, 0.25, 16_250_000L)] // ₡520 + 25% = ₡650 × 250
        [InlineData(0u, 52_000L, 0.40, 0L)] // no shares: nothing paid
        [InlineData(10u, 10_000L, 0.0, 110_000L)] // never less than 10% above
        [InlineData(10u, 10_000L, 9.0, 250_000L)] // never more than 150% above
        public void TheCityIsPaidThePricePlusThePremiumForEveryShare(
            uint shares,
            long price,
            double premium,
            long paid
        )
        {
            Assert.Equal(paid, IpoBuyout.PayoutCents(shares, price, premium));
            Assert.True(IpoBuyout.PricePerShareCents(price, premium) > price);
        }

        [Fact]
        public void TheSameSeedBuysTheSameCompanyAtTheSamePremium()
        {
            var rules = new IpoRules { BuyoutPercentPerYear = 50 };
            var a = new IpoDice(5UL);
            var b = new IpoDice(5UL);
            for (int day = 0; day < 20_000; day++)
            {
                PeriodDraw first = IpoBuyout.DrawPeriod(12, rules, a);
                PeriodDraw second = IpoBuyout.DrawPeriod(12, rules, b);
                Assert.Equal(first.Index, second.Index);
                Assert.Equal(first.DayOffset, second.DayOffset);
                if (first.Happens)
                {
                    Assert.Equal(IpoBuyout.Premium(rules, a), IpoBuyout.Premium(rules, b));
                }
            }
        }
    }
}

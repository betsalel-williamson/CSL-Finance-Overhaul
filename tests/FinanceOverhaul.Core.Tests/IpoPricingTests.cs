using System;
using FinanceOverhaul.Core;
using Xunit;

namespace FinanceOverhaul.Core.Tests
{
    public class IpoPricingTests
    {
        [Fact]
        public void TheFullHoldingIsWorthTheCompany()
        {
            // ₡26,000 a year × 20 = ₡520,000; over the 1,000 shares the city may hold: ₡520.
            var rules = new IpoRules();
            long price = IpoPricing.OfferingPriceCents(2_600_000, rules);
            Assert.Equal(52_000, price);
            Assert.Equal(2_600_000L * 20, price * IpoPricing.HoldingCap);
        }

        [Theory]
        [InlineData(0L, 100L)] // no earnings: the floor
        [InlineData(-500L, 100L)]
        [InlineData(4_999L, 100L)] // under ₡1 a share: the floor
        [InlineData(10_000L, 200L)] // ₡100 a year → ₡2
        [InlineData(1_234_567L, 24_600L)] // ₡246.91 → whole ₡, rounded down
        [InlineData(900_000_000_000L, 10_000_000L)] // the ceiling
        public void PricesAreWholeAndInsideTheGamesRange(long earnings, long price)
        {
            Assert.Equal(price, IpoPricing.OfferingPriceCents(earnings, new IpoRules()));
        }

        [Fact]
        public void MoreEarningsNeverListLowerAndAHigherMultipleNeverListsLower()
        {
            var rules = new IpoRules();
            var richer = new IpoRules { PriceToEarnings = 30 };
            long last = 0;
            for (long earnings = 0; earnings < 80_000_000; earnings += 37_337)
            {
                long price = IpoPricing.OfferingPriceCents(earnings, rules);
                Assert.True(price >= last);
                Assert.Equal(0, price % 100);
                Assert.InRange(price, IpoPricing.MinPriceCents, IpoPricing.MaxPriceCents);
                Assert.True(IpoPricing.OfferingPriceCents(earnings, richer) >= price);
                last = price;
            }
        }

        [Fact]
        public void ThePopAveragesFifteenPercentWithMostRisingAndAFewFalling()
        {
            var rules = new IpoRules();
            var dice = new IpoDice(15UL);
            const int N = 400_000;
            double sum = 0,
                squares = 0;
            int fell = 0,
                soared = 0,
                modest = 0;
            for (int i = 0; i < N; i++)
            {
                double pop = IpoPricing.FirstDayPop(rules, dice);
                Assert.InRange(pop, IpoPricing.MinPop, IpoPricing.MaxPop);
                sum += pop;
                squares += pop * pop;
                if (pop < 0)
                {
                    fell++;
                }
                else if (pop > 0.5)
                {
                    soared++;
                }
                else
                {
                    modest++;
                }
            }

            double mean = sum / N;
            double deviation = Math.Sqrt(squares / N - mean * mean);
            Assert.InRange(mean, 0.145, 0.155);
            Assert.InRange(deviation, 0.24, 0.26);
            Assert.InRange(fell / (double)N, 0.25, 0.33); // a few fall (real IPOs: about 3 in 10)
            Assert.InRange(soared / (double)N, 0.03, 0.10); // a few soar past +50%
            Assert.True(modest > N / 2); // most rise modestly
        }

        [Fact]
        public void NoSpreadMeansExactlyTheAverageAndTheDiceAdvanceTwice()
        {
            var rules = new IpoRules { PopSpreadPercent = 0 };
            var dice = new IpoDice(3UL);
            var twice = new IpoDice(3UL);
            Assert.Equal(0.15, IpoPricing.FirstDayPop(rules, dice), 9);
            twice.Next();
            twice.Next();
            Assert.Equal(twice.State, dice.State);
        }

        [Fact]
        public void TheSameSeedOpensAtTheSamePrice()
        {
            var rules = new IpoRules();
            var a = new IpoDice(99UL);
            var b = new IpoDice(99UL);
            for (int i = 0; i < 1000; i++)
            {
                Assert.Equal(IpoPricing.FirstDayPop(rules, a), IpoPricing.FirstDayPop(rules, b));
            }
        }

        [Theory]
        [InlineData(52_000L, 0.15, 59_800L)] // ₡520 + 15% = ₡598
        [InlineData(52_000L, -0.2, 41_600L)]
        [InlineData(52_000L, 0.0049, 52_300L)] // ₡522.55 rounds to ₡523
        [InlineData(100L, -0.5, 100L)] // never under ₡1
        [InlineData(10_000_000L, 3.0, 10_000_000L)] // never over the ceiling
        [InlineData(52_000L, 9.0, 208_000L)] // a pop beyond the cut-off is cut off (4×)
        public void TheFirstDayOpensAtAWholePriceInRange(long offer, double pop, long opens)
        {
            Assert.Equal(opens, IpoPricing.FirstDayPriceCents(offer, pop));
        }
    }
}

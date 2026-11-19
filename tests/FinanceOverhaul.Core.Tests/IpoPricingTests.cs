using System; // Math
using FinanceOverhaul.Core; // IpoDice, IpoPricing, IpoRules
using Xunit; // Assert, FactAttribute, InlineDataAttribute, TheoryAttribute

namespace FinanceOverhaul.Core.Tests
{
    public class IpoPricingTests
    {
        [Fact]
        public void TheFullHoldingIsATenPercentStake()
        {
            // ₡26,000 a year × 20 = ₡520,000; a 10% stake is ₡52,000 over the 1,000 shares the
            // city may hold: ₡52 a share.
            var rules = new IpoRules();
            long price = IpoPricing.OfferingPriceCents(2_600_000, rules);
            Assert.Equal(5_200, price);
            Assert.Equal(2_600_000L * 20 / 10, price * IpoPricing.HoldingCap);
            Assert.Equal(2_600_000L * 20, IpoPricing.CompanyValueCents(price, rules));
        }

        [Fact]
        public void ABiggerStakeMeansADearerShare()
        {
            var whole = new IpoRules { CityStakePercent = 100 };
            Assert.Equal(52_000, IpoPricing.OfferingPriceCents(2_600_000, whole));
            var over = new IpoRules { CityStakePercent = 250 }; // more than all of it: all of it
            Assert.Equal(52_000, IpoPricing.OfferingPriceCents(2_600_000, over));
            var none = new IpoRules { CityStakePercent = 0 };
            Assert.Equal(IpoPricing.MinPriceCents, IpoPricing.OfferingPriceCents(2_600_000, none));
        }

        [Theory]
        [InlineData(0L, 100L)] // no earnings: the floor
        [InlineData(-500L, 100L)]
        [InlineData(49_999L, 100L)] // under ₡1 a share: the floor
        [InlineData(100_000L, 200L)] // ₡1,000 a year → ₡2
        [InlineData(12_345_678L, 24_600L)] // ₡246.91 → whole ₡, rounded down
        [InlineData(9_000_000_000_000L, 10_000_000L)] // the ceiling
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
            for (long earnings = 0; earnings < 800_000_000; earnings += 373_337)
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

        [Fact]
        public void APriceMovesEveryDayAndStaysNearWhatTheBusinessIsWorth()
        {
            var rules = new IpoRules();
            var dice = new IpoDice(7);
            long price = 5_200,
                low = price,
                high = price;
            int moved = 0;
            for (int day = 0; day < 3650; day++)
            {
                long next = IpoPricing.NextDayPriceCents(price, 5_200, rules, dice);
                Assert.InRange(next, price * 0.85 - 1, price * 1.15 + 1); // never past the cap
                moved += next != price ? 1 : 0;
                price = next;
                low = Math.Min(low, price);
                high = Math.Max(high, price);
            }

            Assert.True(moved > 3500, "moved on " + moved + " of 3650 days");
            // Ten years of swings never take it far from its worth (the pull holds it).
            Assert.InRange(low, 2_600, 5_200);
            Assert.InRange(high, 5_200, 10_400);
        }

        [Fact]
        public void APriceFollowsTheBusinessWhenItsEarningsChange()
        {
            var rules = new IpoRules { DailySwingPercent = 0 }; // the pull alone
            var dice = new IpoDice(1);
            long price = 5_000;
            for (int day = 0; day < 365; day++)
            {
                price = IpoPricing.NextDayPriceCents(price, 10_000, rules, dice);
            }

            Assert.InRange(price, 9_500, 10_000); // a year closes nearly all of the gap
            for (int day = 0; day < 365; day++)
            {
                price = IpoPricing.NextDayPriceCents(price, 2_000, rules, dice);
            }

            Assert.InRange(price, 2_000, 2_300);
        }

        [Fact]
        public void UnknownEarningsPullNowhereAndACheapShareStillMoves()
        {
            var still = new IpoRules { DailySwingPercent = 0 };
            Assert.Equal(5_000, IpoPricing.NextDayPriceCents(5_000, 0, still, new IpoDice(3)));

            var rules = new IpoRules();
            var dice = new IpoDice(3);
            long price = 200; // ₡2
            int moved = 0;
            for (int day = 0; day < 100; day++)
            {
                long next = IpoPricing.NextDayPriceCents(price, 200, rules, dice);
                Assert.True(next >= IpoPricing.MinPriceCents);
                moved += next != price ? 1 : 0;
                price = next;
            }

            Assert.True(moved > 50, "a ₡2 share moved on " + moved + " of 100 days");
        }

        [Fact]
        public void TheSameDiceGiveTheSamePrices()
        {
            var rules = new IpoRules();
            IpoDice a = new IpoDice(42),
                b = new IpoDice(42);
            long pa = 8_100,
                pb = 8_100;
            for (int day = 0; day < 50; day++)
            {
                pa = IpoPricing.NextDayPriceCents(pa, 8_100, rules, a);
                pb = IpoPricing.NextDayPriceCents(pb, 8_100, rules, b);
            }

            Assert.Equal(pa, pb);
            Assert.Equal(a.State, b.State);
        }
    }
}

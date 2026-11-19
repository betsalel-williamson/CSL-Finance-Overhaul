using FinanceOverhaul.Core; // IpoEarnings, IpoPricing, IpoRules
using Xunit; // Assert, FactAttribute, InlineDataAttribute, TheoryAttribute

namespace FinanceOverhaul.Core.Tests
{
    public class IpoEarningsTests
    {
        [Fact]
        public void ABusinessIsCreditedWithItsShareOfTheZonesRealIncome()
        {
            // The zone brings in ₡67,000 a week. This business is 1/1,000 of the formula's
            // total: ₡67 a week of tax, ₡3,484 a year, levied at 10% on ₡34,840 of earnings.
            long earnings = IpoEarnings.AnnualCents(1_000, 1_000_000, 6_700_000, 10);
            Assert.Equal(3_484_000, earnings);
            // …which lists at ₡69 a share (× 20 × 10% ÷ 1,000 shares).
            Assert.Equal(6_900, IpoPricing.OfferingPriceCents(earnings, new IpoRules()));
        }

        [Fact]
        public void TheSharesOfAZoneAddUpToWhatTheGameReports()
        {
            long[] formula = { 5, 120, 999, 40_000, 7, 7, 300_000 };
            long total = 0;
            foreach (long f in formula)
            {
                total += f;
            }

            long weekly = 6_704_316;
            decimal taxBack = 0;
            foreach (long f in formula)
            {
                taxBack += IpoEarnings.AnnualCents(f, total, weekly, 9) * 9m / 100m / 52m;
            }

            Assert.InRange(taxBack, weekly - formula.Length, weekly); // rounding down only
        }

        [Fact]
        public void ABiggerShareOrALowerTaxRateMeansMoreEarnings()
        {
            long small = IpoEarnings.AnnualCents(10, 1000, 1_000_000, 10);
            Assert.True(IpoEarnings.AnnualCents(20, 1000, 1_000_000, 10) > small);
            Assert.True(IpoEarnings.AnnualCents(10, 1000, 2_000_000, 10) > small);
            Assert.True(IpoEarnings.AnnualCents(10, 1000, 1_000_000, 5) > small);
            // Never more than the whole zone.
            Assert.Equal(
                IpoEarnings.AnnualCents(1000, 1000, 1_000_000, 10),
                IpoEarnings.AnnualCents(5000, 1000, 1_000_000, 10)
            );
        }

        [Theory]
        [InlineData(0L, 1000L, 1_000_000L, 10)]
        [InlineData(10L, 0L, 1_000_000L, 10)]
        [InlineData(10L, 1000L, 0L, 10)] // the zone earns nothing
        [InlineData(10L, 1000L, 1_000_000L, 0)] // untaxed: nothing to measure by
        [InlineData(-5L, 1000L, 1_000_000L, 10)]
        public void NothingToShareOutMeansNoEarnings(long formula, long zone, long weekly, int tax)
        {
            Assert.Equal(0, IpoEarnings.AnnualCents(formula, zone, weekly, tax));
            Assert.Equal(
                IpoPricing.MinPriceCents,
                IpoPricing.OfferingPriceCents(0, new IpoRules())
            );
        }
    }
}

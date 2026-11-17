using System;

namespace FinanceOverhaul.Core
{
    /// <summary>
    /// What a local company lists at and where it opens. The game has no share count or volume:
    /// a stock is a price and the city's own holding, at most
    /// <see cref="HoldingCap"/> shares. So the offering price is set so that the full holding is
    /// worth the whole company (annual earnings × the price-to-earnings multiple): the cap reads
    /// as "the city can own the company". Prices are whole ₡ a share, in cents, kept inside the
    /// range of the game's own stocks.
    /// </summary>
    public static class IpoPricing
    {
        /// <summary>The most shares of one stock the game lets the city hold.</summary>
        public const int HoldingCap = 1000;

        public const long MinPriceCents = 100; // ₡1
        public const long MaxPriceCents = 10000000; // ₡100,000: the top of the game's own stocks

        /// <summary>The first trading day never opens below half the offering price…</summary>
        public const double MinPop = -0.5;

        /// <summary>…nor above four times it.</summary>
        public const double MaxPop = 3.0;

        /// <summary>
        /// Offering price a share: annual earnings × multiple ÷ <see cref="HoldingCap"/>, rounded
        /// down to a whole ₡, never outside ₡1 to ₡100,000. A business with no earnings lists at
        /// the floor.
        /// </summary>
        public static long OfferingPriceCents(long annualEarningsCents, IpoRules rules)
        {
            if (annualEarningsCents <= 0 || rules == null || rules.PriceToEarnings <= 0)
            {
                return MinPriceCents;
            }

            decimal value = (decimal)annualEarningsCents * rules.PriceToEarnings;
            decimal share = Math.Floor(value / HoldingCap / 100m) * 100m; // whole ₡
            if (share <= MinPriceCents)
            {
                return MinPriceCents;
            }

            return share >= MaxPriceCents ? MaxPriceCents : (long)share;
        }

        /// <summary>
        /// The first-day pop as a fraction of the offering price (0.15 = opens 15% up), modelled
        /// on real IPOs: most rise modestly, a few soar, a few fall. A log-normal jump whose
        /// average and standard deviation are the two options, cut off at
        /// <see cref="MinPop"/> and <see cref="MaxPop"/>. Takes two numbers from the dice.
        /// </summary>
        public static double FirstDayPop(IpoRules rules, IpoDice dice)
        {
            double mean = 1d + rules.PopAveragePercent / 100d;
            double spread = Math.Max(0, rules.PopSpreadPercent) / 100d;
            double u1 = 1d - dice.Next(); // (0, 1]: the logarithm below is finite
            double u2 = dice.Next();
            if (mean <= 0d)
            {
                return MinPop;
            }

            // Log-normal with the wanted mean and standard deviation.
            double sigma2 = Math.Log(1d + spread * spread / (mean * mean));
            double mu = Math.Log(mean) - sigma2 / 2d;
            double normal = Math.Sqrt(-2d * Math.Log(u1)) * Math.Cos(2d * Math.PI * u2);
            double pop = Math.Exp(mu + Math.Sqrt(sigma2) * normal) - 1d;
            return Math.Max(MinPop, Math.Min(MaxPop, pop));
        }

        /// <summary>The opening price on the first trading day: whole ₡, inside the same range.</summary>
        public static long FirstDayPriceCents(long offeringPriceCents, double pop)
        {
            double opened = offeringPriceCents * (1d + Math.Max(MinPop, Math.Min(MaxPop, pop)));
            long whole = (long)Math.Round(opened / 100d, MidpointRounding.AwayFromZero) * 100;
            return Math.Max(MinPriceCents, Math.Min(MaxPriceCents, whole));
        }
    }
}

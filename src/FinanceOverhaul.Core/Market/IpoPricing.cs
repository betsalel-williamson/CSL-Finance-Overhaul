using System; // Math, MidpointRounding

namespace FinanceOverhaul.Core
{
    /// <summary>
    /// What a local company lists at and where it opens. The game has no share count or volume:
    /// a stock is a price and the city's own holding, at most <see cref="HoldingCap"/> shares.
    /// The city is a passive investor with no say in the company, so it may hold only a stake
    /// (<see cref="IpoRules.CityStakePercent"/>, 10%): the offering price is set so that the full
    /// holding is worth that stake of the company (annual earnings × the price-to-earnings
    /// multiple). Prices are whole ₡ a share, in cents, kept inside the range of the game's own
    /// stocks.
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
        /// Offering price a share: annual earnings × multiple × the city's stake ÷
        /// <see cref="HoldingCap"/>, rounded down to a whole ₡, never outside ₡1 to ₡100,000. A
        /// business with no earnings lists at the floor.
        /// </summary>
        public static long OfferingPriceCents(long annualEarningsCents, IpoRules rules)
        {
            if (
                annualEarningsCents <= 0
                || rules == null
                || rules.PriceToEarnings <= 0
                || rules.CityStakePercent <= 0
            )
            {
                return MinPriceCents;
            }

            decimal stake =
                (decimal)annualEarningsCents
                * rules.PriceToEarnings
                * Math.Min(100, rules.CityStakePercent)
                / 100m;
            decimal share = Math.Floor(stake / HoldingCap / 100m) * 100m; // whole ₡
            if (share <= MinPriceCents)
            {
                return MinPriceCents;
            }

            return share >= MaxPriceCents ? MaxPriceCents : (long)share;
        }

        /// <summary>The company's whole value at a share price: what a 100% stake would cost.</summary>
        public static long CompanyValueCents(long sharePriceCents, IpoRules rules)
        {
            int stake = Math.Max(1, Math.Min(100, rules.CityStakePercent));
            return sharePriceCents * HoldingCap * 100 / stake;
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
            double jump = dice.LogNormal(mean, spread);
            if (mean <= 0d)
            {
                return MinPop;
            }

            return Math.Max(MinPop, Math.Min(MaxPop, jump - 1d));
        }

        /// <summary>
        /// A trading company's price tomorrow: today's price, pulled a little towards what the
        /// business is worth now and moved by the day's swing, never more than
        /// <see cref="IpoRules.MaxDailyMovePercent"/> in a day and never outside the range of
        /// the game's stocks. Kept to the cent, so a ₡2 share moves too. Takes two numbers
        /// from the dice.
        /// </summary>
        /// <param name="fairPriceCents">
        /// What the business would list at today (<see cref="OfferingPriceCents"/> of its
        /// earnings now); 0 or less when that is unknown, and then nothing pulls.
        /// </param>
        public static long NextDayPriceCents(
            long priceCents,
            long fairPriceCents,
            IpoRules rules,
            IpoDice dice
        )
        {
            double swing = Math.Max(0, rules.DailySwingPercent) / 100d * dice.Normal();
            double pull =
                fairPriceCents <= 0 || priceCents <= 0
                    ? 0d
                    : Math.Max(0, Math.Min(100, rules.FairPullPercent))
                        / 100d
                        * (fairPriceCents - priceCents)
                        / priceCents;
            double most = Math.Max(0, rules.MaxDailyMovePercent) / 100d;
            double move = Math.Max(-most, Math.Min(most, swing + pull));
            long next = (long)Math.Round(priceCents * (1d + move), MidpointRounding.AwayFromZero);
            return Math.Max(MinPriceCents, Math.Min(MaxPriceCents, next));
        }

        /// <summary>
        /// The price tomorrow of a company in its grace period (headquarters lost, nowhere to
        /// move): <see cref="IpoRules.HomelessFallPercent"/> lower, no swing, never under ₡1.
        /// About a sixth of the price is left after 90 days at 2%.
        /// </summary>
        public static long HomelessDayPriceCents(long priceCents, IpoRules rules)
        {
            double fall = Math.Max(0, Math.Min(100, rules.HomelessFallPercent)) / 100d;
            long next = (long)Math.Round(priceCents * (1d - fall), MidpointRounding.AwayFromZero);
            return Math.Max(MinPriceCents, Math.Min(MaxPriceCents, next));
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

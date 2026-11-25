using System; // Math, MidpointRounding

namespace FinanceOverhaul.Core
{
    /// <summary>
    /// A buy-out: now and then a listed local company is bought and leaves the exchange, and the
    /// city is paid for its shares at a premium. Mergers are the most common reason a real
    /// company delists (about 4 to 6 in 100 a year), paid well above the market price. Here it is
    /// a windfall the player cannot see coming: once a period (a month by default) it is decided
    /// whether one of the companies listed long enough is bought in the coming period (each has the same small
    /// chance), and on which day, and the premium is drawn afresh each
    /// time.
    /// </summary>
    public static class IpoBuyout
    {
        /// <summary>A buyer never pays less than this above the price…</summary>
        public const double MinPremium = 0.10;

        /// <summary>…nor more than this.</summary>
        public const double MaxPremium = 1.5;

        /// <summary>Listed long enough to be bought.</summary>
        public static bool Eligible(int daysListed, IpoRules rules)
        {
            return daysListed >= rules.BuyoutMinDaysListed;
        }

        /// <summary>One company's chance in one period, from its chance in a year.</summary>
        public static double PeriodChance(IpoRules rules)
        {
            if (rules == null || rules.BuyoutPercentPerYear <= 0)
            {
                return 0d;
            }

            double year = Math.Min(100, rules.BuyoutPercentPerYear) / 100d;
            return 1d
                - Math.Pow(1d - year, (double)IpoCalendar.PeriodDays(rules) / IpoRules.DaysPerYear);
        }

        /// <summary>The chance that some company is bought this period, with this many eligible.</summary>
        public static double ChanceThisPeriod(int eligible, IpoRules rules)
        {
            return eligible <= 0 ? 0d : 1d - Math.Pow(1d - PeriodChance(rules), eligible);
        }

        /// <summary>Average years between buy-outs with this many eligible companies (infinite with none).</summary>
        public static double ExpectedYearsBetween(int eligible, IpoRules rules)
        {
            double chance = ChanceThisPeriod(eligible, rules);
            return chance <= 0d
                ? double.PositiveInfinity
                : IpoCalendar.PeriodDays(rules) / chance / IpoRules.DaysPerYear;
        }

        /// <summary>
        /// This period's draw: whether an eligible company is bought in the coming period,
        /// which one, and on which day. Takes one number from the dice whenever a company is
        /// eligible, and a second for the day when a buy-out happens. Every eligible company is
        /// equally likely.
        /// </summary>
        public static PeriodDraw DrawPeriod(int eligible, IpoRules rules, IpoDice dice)
        {
            return eligible <= 0
                ? PeriodDraw.Nothing
                : IpoCalendar.Roll(
                    eligible,
                    ChanceThisPeriod(eligible, rules),
                    IpoCalendar.PeriodDays(rules),
                    dice
                );
        }

        /// <summary>
        /// What the buyer pays above the price, as a fraction (0.4 = 40% above): most near the
        /// average, now and then far more, between <see cref="IpoRules.BuyoutPremiumMinPercent"/>
        /// and <see cref="IpoRules.BuyoutPremiumMaxPercent"/> (by default <see cref="MinPremium"/>
        /// and <see cref="MaxPremium"/>). Takes two numbers from the dice.
        /// </summary>
        public static double Premium(IpoRules rules, IpoDice dice)
        {
            double mean = 1d + Math.Max(0, rules.BuyoutPremiumAveragePercent) / 100d;
            double spread = Math.Max(0, rules.BuyoutPremiumSpreadPercent) / 100d;
            double paid = dice.LogNormal(mean, spread) - 1d;
            double low = Math.Max(0d, rules.BuyoutPremiumMinPercent / 100d);
            double high = Math.Max(low, rules.BuyoutPremiumMaxPercent / 100d);
            return Math.Max(low, Math.Min(high, paid));
        }

        /// <summary>
        /// The buyer's price a share: the price plus the premium (kept inside the rules' range,
        /// the defaults without rules), a whole ₡.
        /// </summary>
        public static long PricePerShareCents(
            long priceCents,
            double premium,
            IpoRules rules = null
        )
        {
            rules = rules ?? new IpoRules();
            double low = Math.Max(0d, rules.BuyoutPremiumMinPercent / 100d);
            double high = Math.Max(low, rules.BuyoutPremiumMaxPercent / 100d);
            double bounded = Math.Max(low, Math.Min(high, premium));
            return (long)
                    Math.Round(priceCents * (1d + bounded) / 100d, MidpointRounding.AwayFromZero)
                * 100;
        }

        /// <summary>What the city receives for its shares.</summary>
        public static long PayoutCents(uint shares, long priceCents, double premium)
        {
            return shares * PricePerShareCents(priceCents, premium);
        }
    }
}

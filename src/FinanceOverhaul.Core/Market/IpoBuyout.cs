using System;

namespace FinanceOverhaul.Core
{
    /// <summary>
    /// A buy-out: now and then a listed local company is bought and leaves the exchange, and the
    /// city is paid for its shares at a premium. Mergers are the most common reason a real
    /// company delists (about 4 to 6 in 100 a year), paid well above the market price. Here it is
    /// a windfall the player cannot see coming: every company listed long enough has the same
    /// small chance each day, at most one is bought a day, and the premium is drawn afresh each
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

        /// <summary>One company's chance on one day, from its chance in a year.</summary>
        public static double DailyChance(IpoRules rules)
        {
            if (rules == null || rules.BuyoutPercentPerYear <= 0)
            {
                return 0d;
            }

            double year = Math.Min(100, rules.BuyoutPercentPerYear) / 100d;
            return 1d - Math.Pow(1d - year, 1d / IpoRules.DaysPerYear);
        }

        /// <summary>The chance that some company is bought today, with this many eligible.</summary>
        public static double ChanceToday(int eligible, IpoRules rules)
        {
            return eligible <= 0 ? 0d : 1d - Math.Pow(1d - DailyChance(rules), eligible);
        }

        /// <summary>Average years between buy-outs with this many eligible companies (infinite with none).</summary>
        public static double ExpectedYearsBetween(int eligible, IpoRules rules)
        {
            double chance = ChanceToday(eligible, rules);
            return chance <= 0d ? double.PositiveInfinity : 1d / chance / IpoRules.DaysPerYear;
        }

        /// <summary>
        /// Today's draw: the index of the eligible company that is bought, or −1 for none. Takes
        /// one number from the dice whenever a company is eligible. Every eligible company is
        /// equally likely.
        /// </summary>
        public static int Draw(int eligible, IpoRules rules, IpoDice dice)
        {
            if (eligible <= 0)
            {
                return -1;
            }

            double chance = ChanceToday(eligible, rules);
            double roll = dice.Next();
            if (chance <= 0d || roll >= chance)
            {
                return -1;
            }

            return Math.Min(eligible - 1, (int)(roll / chance * eligible));
        }

        /// <summary>
        /// What the buyer pays above the price, as a fraction (0.4 = 40% above): most near the
        /// average, now and then far more, between <see cref="MinPremium"/> and
        /// <see cref="MaxPremium"/>. Takes two numbers from the dice.
        /// </summary>
        public static double Premium(IpoRules rules, IpoDice dice)
        {
            double mean = 1d + Math.Max(0, rules.BuyoutPremiumAveragePercent) / 100d;
            double spread = Math.Max(0, rules.BuyoutPremiumSpreadPercent) / 100d;
            double paid = dice.LogNormal(mean, spread) - 1d;
            return Math.Max(MinPremium, Math.Min(MaxPremium, paid));
        }

        /// <summary>The buyer's price a share: the price plus the premium, a whole ₡.</summary>
        public static long PricePerShareCents(long priceCents, double premium)
        {
            double bounded = Math.Max(MinPremium, Math.Min(MaxPremium, premium));
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

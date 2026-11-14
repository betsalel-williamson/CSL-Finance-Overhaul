using System;
using System.Globalization;

namespace FinanceOverhaul.Market
{
    /// <summary>
    /// Average cost basis, as the game keeps it: every buy adds its cost to "You invested"
    /// (Investment.m_playerSpentMoney) and every sell removes the sold shares' pro-rata
    /// share of it. Average cost per share is therefore invested ÷ shares held.
    /// </summary>
    public static class CostBasis
    {
        /// <summary>Average cost per share, or null when no shares are held.</summary>
        public static double? Average(long invested, uint shares)
        {
            if (shares == 0)
            {
                return null;
            }

            return (double)invested / shares;
        }

        /// <summary>"₡90.00": two decimals, because the basis is rarely a whole ₡.</summary>
        public static string FormatAverage(
            long invested,
            uint shares,
            IFormatProvider culture = null
        )
        {
            double? average = Average(invested, shares);
            if (!average.HasValue)
            {
                return null;
            }

            double rounded = Math.Round(average.Value, 2, MidpointRounding.AwayFromZero);
            return "₡" + rounded.ToString("N2", culture ?? CultureInfo.InvariantCulture);
        }
    }
}

using System; // Math
using System.Globalization; // CultureInfo

namespace FinanceOverhaul.Core
{
    /// <summary>
    /// The one rule for a change in ₡ as the mod shows it: cents up to ₡9.99, whole ₡ from ₡10
    /// (decided on the rounded value, so 9.996 is "₡10", never "₡10.00"), a "+" or "−" sign,
    /// "₡0" for no change, "--" when unknown. Used by every ₡ pill (list, Stock Monitor,
    /// overview, heatmap).
    /// </summary>
    public static class MoneyText
    {
        /// <param name="amount">The change in ₡.</param>
        /// <param name="keepTiny">
        /// A change that rounds to less than a cent still shows as ₡0.01 (the pills: they agree
        /// with a % pill that is not 0.00%).
        /// </param>
        public static string Change(double amount, bool keepTiny = false)
        {
            if (double.IsNaN(amount) || double.IsInfinity(amount))
            {
                return "--";
            }

            double cents = Math.Round(Math.Abs(amount), 2);
            if (keepTiny && amount != 0d)
            {
                cents = Math.Max(0.01, cents);
            }

            if (cents == 0d)
            {
                return "₡0";
            }

            string digits =
                cents < 10.0
                    ? cents.ToString("0.00", CultureInfo.InvariantCulture)
                    : Math.Round(Math.Abs(amount)).ToString("N0", CultureInfo.InvariantCulture);
            return (amount > 0 ? "+₡" : "−₡") + digits;
        }
    }
}

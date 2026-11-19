using System; // Math

namespace FinanceOverhaul.Core
{
    /// <summary>
    /// What a business earns in a game year, for its offering price. The game stores no income
    /// per building, and its income formula alone cannot be scaled reliably (measured: what it
    /// predicts is 5 to 16 times a zone's real income, differently per zone, because the real
    /// amount depends on each building's activity). So the formula is only used to share out
    /// what the game itself reports: a business is credited with its share of its zone's real
    /// weekly tax income, and its earnings are the income that tax was levied on. The share is
    /// by weight: the formula's income times the business's jobs, so a larger business takes
    /// more (the formula alone hardly differs between businesses of one zone).
    /// </summary>
    public static class IpoEarnings
    {
        public const int WeeksPerYear = 52;

        /// <param name="formulaIncome">This business's weight (the game formula's income × its jobs).</param>
        /// <param name="zoneFormulaIncome">The weights of every business of its zone, summed.</param>
        /// <param name="zoneWeeklyTaxCents">The weekly income the game reports for that zone.</param>
        /// <param name="taxRatePercent">The tax rate on this business (9 = 9%).</param>
        /// <returns>Cents a year; 0 when there is nothing to share out or the zone is untaxed.</returns>
        public static long AnnualCents(
            long formulaIncome,
            long zoneFormulaIncome,
            long zoneWeeklyTaxCents,
            int taxRatePercent
        )
        {
            if (
                formulaIncome <= 0
                || zoneFormulaIncome <= 0
                || zoneWeeklyTaxCents <= 0
                || taxRatePercent <= 0
            )
            {
                return 0;
            }

            decimal share = (decimal)Math.Min(formulaIncome, zoneFormulaIncome) / zoneFormulaIncome;
            decimal taxAYear = zoneWeeklyTaxCents * share * WeeksPerYear;
            decimal earnings = taxAYear * 100m / taxRatePercent;
            return earnings >= long.MaxValue ? long.MaxValue : (long)Math.Floor(earnings);
        }
    }
}

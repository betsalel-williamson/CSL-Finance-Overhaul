using System; // Math
using System.Collections.Generic; // IList, List

namespace FinanceOverhaul.Core
{
    /// <summary>
    /// The bond desk's decisions that are not the book itself: what the city owes and earns for
    /// pricing, which notices a day's payments deserve, and how a cash movement is handed to the
    /// game's budget (whose calls take an int). The mod reads the game, moves the cash and saves.
    /// </summary>
    public static class BondDay
    {
        /// <summary>Annual income is the game's weekly income figure × 52.</summary>
        public const int WeeksPerYear = 52;

        /// <summary>
        /// Debt already owed (vanilla loans left + outstanding bond faces) and annual income, in
        /// cents, as bond pricing wants them. Loans with nothing left and a negative weekly income
        /// count as zero.
        /// </summary>
        public static void Finances(
            IList<long> loansLeftCents,
            long bondFaceCents,
            long weeklyIncomeCents,
            out long debtCents,
            out long annualIncomeCents
        )
        {
            long loans = 0;
            if (loansLeftCents != null)
            {
                foreach (long left in loansLeftCents)
                {
                    if (left > 0)
                    {
                        loans += left;
                    }
                }
            }

            debtCents = loans + Math.Max(0, bondFaceCents);
            annualIncomeCents = Math.Max(0, weeklyIncomeCents) * WeeksPerYear;
        }

        /// <summary>
        /// Notices for a day's payments: a bond repaid at maturity (face plus its last coupon),
        /// and each coupon only when the player asked for those in Options.
        /// </summary>
        /// <param name="book">The book after the payments were taken (matured bonds are gone).</param>
        public static List<Notice> Notices(BondBook book, IList<BondCash> due, bool chirpCoupons)
        {
            var notices = new List<Notice>();
            if (due == null)
            {
                return notices;
            }

            foreach (BondCash c in due)
            {
                if (c.Kind == BondCashKind.Principal)
                {
                    long last = 0;
                    foreach (BondCash x in due)
                    {
                        if (x.BondId == c.BondId && x.Day == c.Day && x.Kind == BondCashKind.Coupon)
                        {
                            last = x.Cents;
                        }
                    }

                    notices.Add(NoticeText.BondMatured(c.BondId, c.Cents, last));
                }
                else if (c.Kind == BondCashKind.Coupon && chirpCoupons)
                {
                    IssuedBond bond = book == null ? null : book.Find(c.BondId);
                    if (bond != null) // matured bonds get the maturity notice instead
                    {
                        notices.Add(
                            NoticeText.BondCoupon(
                                c.BondId,
                                c.Cents,
                                bond.Terms.Periods - bond.CouponsPaid,
                                bond.Terms.Periods
                            )
                        );
                    }
                }
            }

            return notices;
        }

        /// <summary>Money comes in (a bond sold); every other movement goes out.</summary>
        public static bool IsCashIn(BondCash c)
        {
            return c.Kind == BondCashKind.Proceeds;
        }

        /// <summary>
        /// A movement cut into pieces the game's budget calls accept (each at most
        /// <see cref="int.MaxValue"/> cents), adding up to the whole.
        /// </summary>
        public static int[] Pieces(long cents)
        {
            var pieces = new List<int>();
            long left = cents;
            while (left > 0)
            {
                int piece = (int)Math.Min(left, int.MaxValue);
                pieces.Add(piece);
                left -= piece;
            }

            return pieces.ToArray();
        }
    }

    /// <summary>Lines about bonds for the mod's log file (English, never shown to the player).</summary>
    public static class BondLog
    {
        public static string Sold(IssuedBond bond, BondTerm term, int rateBasisPoints)
        {
            return "bonds: sold #"
                + bond.Id
                + " "
                + BondText.Money(bond.Terms.FaceCents)
                + " for "
                + (int)term
                + "y at "
                + BondPricing.RateText(rateBasisPoints);
        }

        /// <param name="movedCents">What the game reports it moved.</param>
        /// <param name="shortfall">True when the game moved a different amount (log as a warning).</param>
        public static string Moved(BondCash c, long movedCents, out bool shortfall)
        {
            string line =
                "bonds: #"
                + c.BondId
                + " "
                + c.Kind.ToString().ToLowerInvariant()
                + " "
                + BondText.Money(c.Cents)
                + " ("
                + c.Cents
                + " cents, due day "
                + c.Day
                + ")";
            shortfall = movedCents != c.Cents;
            return shortfall ? line + " — the game moved only " + movedCents + " cents" : line;
        }

        public static string Missed(MissedPayments missed)
        {
            return "bonds: mod was off for "
                + missed.DaysAway
                + " day(s); "
                + missed.Payments.Count
                + " payment(s) missed ("
                + BondText.Money(missed.TotalCents)
                + "), waiting for the player";
        }

        public static string Settled(MissedPayments missed, bool paid, int paymentsMoved)
        {
            return "bonds: welcome back after "
                + missed.DaysAway
                + " day(s) away: "
                + (paid ? "paid what was missed" : "started fresh")
                + " ("
                + paymentsMoved
                + " payment(s) moved)";
        }
    }
}

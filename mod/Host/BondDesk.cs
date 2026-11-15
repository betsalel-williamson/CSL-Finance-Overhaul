#if HAS_CITIES
using System;
using System.Collections.Generic;
using System.Text;
using FinanceOverhaul.Market;

namespace FinanceOverhaul
{
    /// <summary>
    /// City bonds in the running game (docs/features/bonds.md). The book lives in the mod's own
    /// save data (<see cref="BondBookCodec.DataId"/>), which vanilla ignores. Once per in-game day,
    /// right after the vanilla investment update, every coupon and face that has fallen due is paid
    /// through the game's loan-payment budget line — even if that takes cash below zero, like a
    /// vanilla loan. Selling a bond adds the face through the loan-taken line. Budget calls are in
    /// cents (measured: game UI scenario 36), the same unit as the book.
    /// </summary>
    public static class BondDesk
    {
        private static readonly object Sync = new object();
        private static BondBook book;
        private static int lastPaidDay = -1;

        /// <summary>Annual income is the game's weekly income figure × 52.</summary>
        public const int WeeksPerYear = 52;

        public static BondBook Snapshot()
        {
            lock (Sync)
            {
                return BondBookCodec.Parse(BondBookCodec.Format(Book()));
            }
        }

        /// <summary>What the market offers this city now for a bond of this size.</summary>
        public static BondOffer Quote(BondTerm term, long faceCents)
        {
            long debt,
                income;
            CityFinances(out debt, out income);
            return BondPricing.Offer(term, debt, faceCents, income);
        }

        /// <summary>
        /// Debt already owed (vanilla loans left + outstanding bond faces) and annual income, in
        /// cents.
        /// </summary>
        public static void CityFinances(out long debtCents, out long annualIncomeCents)
        {
            EconomyManager em = EconomyManager.instance;
            long loans = 0;
            for (int i = 0; i < EconomyManager.MAX_LOANS; i++)
            {
                EconomyManager.Loan loan;
                if (em.GetLoan(i, out loan) && loan.m_amountLeft > 0)
                {
                    loans += loan.m_amountLeft;
                }
            }

            long bonds;
            lock (Sync)
            {
                bonds = Book().OutstandingFaceCents;
            }

            long weeklyIncome,
                weeklyExpenses;
            em.GetIncomeAndExpenses(
                ItemClass.Service.None,
                ItemClass.SubService.None,
                ItemClass.Level.None,
                out weeklyIncome,
                out weeklyExpenses
            );
            debtCents = loans + bonds;
            annualIncomeCents = Math.Max(0, weeklyIncome) * WeeksPerYear;
        }

        /// <summary>Sells a bond at the offered rate; null (with the reason logged) if refused.</summary>
        public static IssuedBond Issue(BondTerm term, long faceCents, out BondOffer offer)
        {
            offer = Quote(term, faceCents);
            if (!offer.Allowed)
            {
                ModLog.Info("bonds: issue refused: " + offer.Reason);
                return null;
            }

            IssuedBond bond;
            lock (Sync)
            {
                BondBook b = Book();
                BondCash proceeds = b.Issue(offer.Terms(faceCents), OrderDesk.Today, out bond);
                Move(proceeds);
                Save(b);
            }

            ModLog.Info(
                "bonds: sold #"
                    + bond.Id
                    + " "
                    + Money(faceCents)
                    + " for "
                    + (int)term
                    + "y at "
                    + BondPricing.RateText(offer.RateBasisPoints)
            );
            return bond;
        }

        /// <summary>Repays a bond early (face + interest accrued this period). False if unknown.</summary>
        public static bool Redeem(int id)
        {
            lock (Sync)
            {
                BondBook b = Book();
                BondCash r;
                List<BondCash> due;
                if (!b.Redeem(id, OrderDesk.Today, out r, out due))
                {
                    return false;
                }

                foreach (BondCash c in due)
                {
                    Move(c);
                }

                if (r.Cents > 0)
                {
                    Move(r);
                }

                Save(b);
                return true;
            }
        }

        /// <summary>Postfix target: EconomyManager.SimulationStepInvestments (simulation thread).</summary>
        public static void OnInvestmentsStepped()
        {
            int today = OrderDesk.Today;
            if (today == lastPaidDay)
            {
                return;
            }

            lastPaidDay = today;
            lock (Sync)
            {
                BondBook b = Book();
                if (b.Bonds.Count == 0)
                {
                    return;
                }

                List<BondCash> due = b.Due(today);
                if (due.Count == 0)
                {
                    return;
                }

                foreach (BondCash c in due)
                {
                    Move(c);
                }

                Save(b);
                ChirpNotices.PostDay(DayNotices(b, due));
            }
        }

        /// <summary>
        /// Notices for today's payments: a bond repaid at maturity (face plus its last coupon),
        /// and each coupon only when Options → "Also chirp every bond coupon" is on.
        /// </summary>
        private static List<Notice> DayNotices(BondBook book, List<BondCash> due)
        {
            var notices = new List<Notice>();
            bool coupons = Mod.Settings != null && Mod.Settings.NoticesBondCoupons;
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
                else if (c.Kind == BondCashKind.Coupon && coupons)
                {
                    IssuedBond bond = book.Find(c.BondId);
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

        /// <summary>Game UI tests: moves a bond's schedule this many days earlier.</summary>
        public static bool Backdate(int id, int days)
        {
            lock (Sync)
            {
                BondBook b = Book();
                IssuedBond bond = b.Find(id);
                if (bond == null)
                {
                    return false;
                }

                bond.IssueDay -= days;
                Save(b);
                lastPaidDay = -1;
                return true;
            }
        }

        /// <summary>Level load / unload: the next city reads its own book.</summary>
        public static void Reset()
        {
            lock (Sync)
            {
                book = null;
                lastPaidDay = -1;
            }
        }

        /// <summary>One cash movement through the loan budget lines, in int-sized pieces.</summary>
        private static void Move(BondCash c)
        {
            EconomyManager em = EconomyManager.instance;
            bool cashIn = c.Kind == BondCashKind.Proceeds;
            long left = c.Cents;
            long moved = 0;
            while (left > 0)
            {
                int piece = (int)Math.Min(left, int.MaxValue);
                moved += cashIn
                    ? em.AddResource(
                        EconomyManager.Resource.LoanAmount,
                        piece,
                        ItemClass.Service.None,
                        ItemClass.SubService.None,
                        ItemClass.Level.None
                    )
                    : em.FetchResource(
                        EconomyManager.Resource.LoanPayment,
                        piece,
                        ItemClass.Service.None,
                        ItemClass.SubService.None,
                        ItemClass.Level.None
                    );
                left -= piece;
            }

            string line =
                "bonds: #"
                + c.BondId
                + " "
                + c.Kind.ToString().ToLowerInvariant()
                + " "
                + Money(c.Cents)
                + " ("
                + c.Cents
                + " cents, due day "
                + c.Day
                + ")";
            if (moved != c.Cents)
            {
                ModLog.Warn(line + " — the game moved only " + moved + " cents");
            }
            else
            {
                ModLog.Info(line);
            }
        }

        private static string Money(long cents)
        {
            return "₡"
                + (cents / 100m).ToString(
                    "#,0.00",
                    System.Globalization.CultureInfo.InvariantCulture
                );
        }

        private static BondBook Book()
        {
            if (book != null)
            {
                return book;
            }

            book = new BondBook();
            try
            {
                byte[] data = SimulationManager.instance.m_SerializableDataWrapper.LoadData(
                    BondBookCodec.DataId
                );
                if (data != null && data.Length > 0)
                {
                    book = BondBookCodec.Parse(Encoding.UTF8.GetString(data));
                    ModLog.Info("bonds: loaded " + book.Bonds.Count + " outstanding bond(s)");
                }
            }
            catch (Exception ex)
            {
                ModLog.Warn("bonds: load failed, starting empty: " + ex.Message);
            }

            return book;
        }

        private static void Save(BondBook b)
        {
            try
            {
                SimulationManager.instance.m_SerializableDataWrapper.SaveData(
                    BondBookCodec.DataId,
                    Encoding.UTF8.GetBytes(BondBookCodec.Format(b))
                );
            }
            catch (Exception ex)
            {
                ModLog.Warn("bonds: save failed: " + ex.Message);
            }
        }
    }
}
#endif

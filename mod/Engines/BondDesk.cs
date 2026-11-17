using System;
using System.Collections.Generic;
using System.Text;
using FinanceOverhaul.Core;

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

        // Welcome back: payments that fell due while the mod was off. While set, the daily step
        // pays nothing and leaves the "last seen" bookmark alone until the player chooses.
        private static MissedPayments pending;

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

        // Game UI tests (dev builds): fixed finances, so a scenario's rates and debt cap do not
        // depend on the loaded city. The bond counterpart of the test stock.
        private static bool testFinances;
        private static long testDebtCents;
        private static long testIncomeCents;

        internal static void SetTestFinances(long debtCents, long annualIncomeCents)
        {
            testDebtCents = Math.Max(0, debtCents);
            testIncomeCents = Math.Max(0, annualIncomeCents);
            testFinances = BuildInfo.ShowDevBuildIdentity;
        }

        internal static void ClearTestFinances()
        {
            testFinances = false;
        }

        /// <summary>
        /// Debt already owed (vanilla loans left + outstanding bond faces) and annual income, in
        /// cents.
        /// </summary>
        public static void CityFinances(out long debtCents, out long annualIncomeCents)
        {
            if (testFinances)
            {
                debtCents = testDebtCents;
                annualIncomeCents = testIncomeCents;
                return;
            }

            EconomyManager em = EconomyManager.instance;
            var loans = new List<long>();
            for (int i = 0; i < EconomyManager.MAX_LOANS; i++)
            {
                EconomyManager.Loan loan;
                if (em.GetLoan(i, out loan))
                {
                    loans.Add(loan.m_amountLeft);
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
            BondDay.Finances(loans, bonds, weeklyIncome, out debtCents, out annualIncomeCents);
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

            ModLog.Info(BondLog.Sold(bond, term, offer.RateBasisPoints));
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

            lock (Sync)
            {
                BondBook b = Book();
                if (pending != null)
                {
                    return; // waiting for the player's choice in the Welcome back dialog
                }

                lastPaidDay = today;
                List<BondCash> due = b.Bonds.Count == 0 ? null : b.Due(today);
                if (due != null && due.Count > 0)
                {
                    foreach (BondCash c in due)
                    {
                        Move(c);
                    }

                    Save(b);
                    ChirpNotices.PostDay(
                        BondDay.Notices(
                            b,
                            due,
                            Mod.Settings != null && Mod.Settings.NoticesBondCoupons
                        )
                    );
                }

                WriteSeen(today);
            }
        }

        /// <summary>Payments missed while the mod was off, waiting for the player; null if none.</summary>
        public static MissedPayments Pending
        {
            get
            {
                lock (Sync)
                {
                    Book();
                    return pending;
                }
            }
        }

        /// <summary>
        /// The player's Welcome back choice: pay every missed payment today, or skip the missed
        /// coupons (a face that fell due is still repaid). Then payments carry on as normal.
        /// </summary>
        public static void SettleMissed(bool pay)
        {
            lock (Sync)
            {
                BondBook b = Book();
                if (pending == null)
                {
                    return;
                }

                int today = OrderDesk.Today;
                List<BondCash> cash = WelcomeBack.Settle(b, today, pay);
                foreach (BondCash c in cash)
                {
                    Move(c);
                }

                ModLog.Info(BondLog.Settled(pending, pay, cash.Count));
                pending = null;
                Save(b);
                WriteSeen(today);
                lastPaidDay = -1;
            }
        }

        /// <summary>
        /// Game UI tests: pretends the mod was off for <paramref name="days"/> days. The given
        /// bonds' schedules move that many days earlier (so those payments are now due and unpaid)
        /// and the bookmark goes back as far; the next look at the book finds them missed. The
        /// player's own bonds are untouched (their past payments were made).
        /// </summary>
        public static void SimulateAway(int days, IEnumerable<int> bondIds)
        {
            lock (Sync)
            {
                BondBook b = Book();
                foreach (int id in bondIds)
                {
                    b.Backdate(id, days);
                }

                Save(b);
                WriteSeen(OrderDesk.Today - days);
                book = null;
                pending = null;
                lastPaidDay = -1;
            }
        }

        private static void WriteSeen(int day)
        {
            try
            {
                SimulationManager.instance.m_SerializableDataWrapper.SaveData(
                    SeenCodec.DataId,
                    Encoding.UTF8.GetBytes(SeenCodec.Format(day))
                );
            }
            catch (Exception ex)
            {
                ModLog.Warn("bonds: bookmark save failed: " + ex.Message);
            }
        }

        /// <summary>Names a bond (empty: back to its number). False if unknown or unchanged.</summary>
        public static bool Rename(int id, string name)
        {
            lock (Sync)
            {
                BondBook b = Book();
                if (!b.Rename(id, name))
                {
                    return false;
                }

                Save(b);
                return true;
            }
        }

        /// <summary>Game UI tests: moves a bond's schedule this many days earlier.</summary>
        public static bool Backdate(int id, int days)
        {
            lock (Sync)
            {
                BondBook b = Book();
                if (!b.Backdate(id, days))
                {
                    return false;
                }

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
                pending = null;
                lastPaidDay = -1;
            }
        }

        /// <summary>One cash movement through the loan budget lines, in int-sized pieces.</summary>
        private static void Move(BondCash c)
        {
            EconomyManager em = EconomyManager.instance;
            bool cashIn = BondDay.IsCashIn(c);
            long moved = 0;
            foreach (int piece in BondDay.Pieces(c.Cents))
            {
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
            }

            bool shortfall;
            string line = BondLog.Moved(c, moved, out shortfall);
            if (shortfall)
            {
                ModLog.Warn(line);
            }
            else
            {
                ModLog.Info(line);
            }
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
                    int weekly = BondMigration.ToWeekly(book);
                    if (weekly > 0)
                    {
                        // Sold when coupons were paid every 30 days: now weekly, like new ones.
                        ModLog.Info("bonds: converted " + weekly + " bond(s) to weekly coupons");
                        Save(book);
                    }
                }
            }
            catch (Exception ex)
            {
                ModLog.Warn("bonds: load failed, starting empty: " + ex.Message);
            }

            pending = null;
            try
            {
                byte[] seen = SimulationManager.instance.m_SerializableDataWrapper.LoadData(
                    SeenCodec.DataId
                );
                int lastSeen =
                    seen == null ? SeenCodec.None : SeenCodec.Parse(Encoding.UTF8.GetString(seen));
                MissedPayments m = WelcomeBack.Missed(book, lastSeen, OrderDesk.Today);
                if (m.Any)
                {
                    pending = m;
                    ModLog.Info(BondLog.Missed(m));
                }
            }
            catch (Exception ex)
            {
                ModLog.Warn("bonds: bookmark read failed: " + ex.Message);
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

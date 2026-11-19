using System; // ArgumentNullException, ArgumentOutOfRangeException, Math, StringComparison
using System.Collections.Generic; // Dictionary, KeyValuePair, List
using System.Globalization; // CultureInfo, NumberStyles
using System.Text; // StringBuilder

namespace FinanceOverhaul.Core
{
    /// <summary>What a bond cash movement is, from the city's side.</summary>
    public enum BondCashKind
    {
        /// <summary>The face received when the bond is sold (cash in).</summary>
        Proceeds,

        /// <summary>A scheduled coupon (cash out).</summary>
        Coupon,

        /// <summary>The face repaid at maturity (cash out).</summary>
        Principal,

        /// <summary>Early repayment: face plus accrued interest (cash out).</summary>
        Redemption,
    }

    /// <summary>One cash movement for one bond, in cents (always positive; the kind gives the direction).</summary>
    public struct BondCash
    {
        public int BondId;
        public BondCashKind Kind;
        public long Cents;
        public int Day;
    }

    /// <summary>A bond the city has sold and not yet fully repaid.</summary>
    public sealed class IssuedBond
    {
        public int Id;
        public BondTerms Terms;
        public int IssueDay;
        public int CouponsPaid;

        /// <summary>The name the player gave it (the project it pays for); null or empty for none.</summary>
        public string Name;

        /// <summary>The day coupon <paramref name="period"/> (1-based) falls due.</summary>
        public int DueDay(int period)
        {
            return IssueDay + period * Terms.PeriodDays;
        }

        public int LastCouponDay
        {
            get { return DueDay(CouponsPaid); }
        }
    }

    /// <summary>
    /// The city's bond book: what was sold and how much of each schedule is paid. Pure and
    /// day-driven — <see cref="Due"/> pays every coupon whose day has come, catching up any days
    /// that were skipped, so the total paid by a day never depends on how often it is called (see
    /// BondBookTests for the proofs). The host moves the cash and saves the book.
    /// </summary>
    public sealed class BondBook
    {
        public readonly List<IssuedBond> Bonds = new List<IssuedBond>();
        public int NextId = 1;

        public long OutstandingFaceCents
        {
            get
            {
                long sum = 0;
                foreach (IssuedBond b in Bonds)
                {
                    sum += b.Terms.FaceCents;
                }

                return sum;
            }
        }

        /// <summary>Records a sale; the proceeds (the face) go to the city today.</summary>
        public BondCash Issue(BondTerms terms, int today, out IssuedBond bond)
        {
            if (terms == null)
            {
                throw new ArgumentNullException("terms");
            }

            bond = new IssuedBond
            {
                Id = NextId++,
                Terms = terms,
                IssueDay = today,
            };
            Bonds.Add(bond);
            return new BondCash
            {
                BondId = bond.Id,
                Kind = BondCashKind.Proceeds,
                Cents = terms.FaceCents,
                Day = today,
            };
        }

        /// <summary>
        /// Every payment due on or before <paramref name="today"/> that is not yet paid, in day
        /// order per bond; marks them paid and drops bonds that matured. Calling it again the same
        /// day returns nothing.
        /// </summary>
        public List<BondCash> Due(int today)
        {
            var paid = new List<BondCash>();
            for (int i = Bonds.Count - 1; i >= 0; i--)
            {
                IssuedBond b = Bonds[i];
                BondPayment[] schedule = null;
                while (b.CouponsPaid < b.Terms.Periods && b.DueDay(b.CouponsPaid + 1) <= today)
                {
                    if (schedule == null)
                    {
                        schedule = BondMath.Schedule(b.Terms);
                    }

                    BondPayment p = schedule[b.CouponsPaid];
                    int day = b.DueDay(b.CouponsPaid + 1);
                    b.CouponsPaid++;
                    if (p.Interest > 0)
                    {
                        paid.Add(Cash(b, BondCashKind.Coupon, p.Interest, day));
                    }

                    if (p.Principal > 0)
                    {
                        paid.Add(Cash(b, BondCashKind.Principal, p.Principal, day));
                    }
                }

                if (b.CouponsPaid >= b.Terms.Periods)
                {
                    Bonds.RemoveAt(i);
                }
            }

            paid.Sort(
                (x, y) => x.Day != y.Day ? x.Day.CompareTo(y.Day) : x.BondId.CompareTo(y.BondId)
            );
            return paid;
        }

        /// <summary>
        /// Repays a bond early: face plus interest accrued since the last coupon. Pays anything
        /// already due first (returned in <paramref name="duePaid"/>), then removes the bond. False
        /// if there is no such bond.
        /// </summary>
        public bool Redeem(int id, int today, out BondCash redemption, out List<BondCash> duePaid)
        {
            duePaid = new List<BondCash>();
            redemption = default(BondCash);
            IssuedBond b = Find(id);
            if (b == null)
            {
                return false;
            }

            duePaid = DueFor(b, today);
            if (Find(id) == null)
            {
                return true; // it matured on the way: nothing left to redeem
            }

            long amount = BondMath.RedemptionAmount(
                b.Terms,
                b.CouponsPaid,
                today - b.LastCouponDay
            );
            Bonds.Remove(b);
            redemption = Cash(b, BondCashKind.Redemption, amount, today);
            return true;
        }

        /// <summary>
        /// Moves a bond's whole schedule this many days earlier, as if it had been sold sooner
        /// (game UI tests: makes payments fall due without waiting). False if unknown.
        /// </summary>
        public bool Backdate(int id, int days)
        {
            IssuedBond bond = Find(id);
            if (bond == null)
            {
                return false;
            }

            bond.IssueDay -= days;
            return true;
        }

        /// <summary>Names a bond (null or empty: no name). False if unknown or unchanged.</summary>
        public bool Rename(int id, string name)
        {
            IssuedBond bond = Find(id);
            if (bond == null || (bond.Name ?? string.Empty) == (name ?? string.Empty))
            {
                return false;
            }

            bond.Name = string.IsNullOrEmpty(name) ? null : name;
            return true;
        }

        public IssuedBond Find(int id)
        {
            foreach (IssuedBond b in Bonds)
            {
                if (b.Id == id)
                {
                    return b;
                }
            }

            return null;
        }

        private List<BondCash> DueFor(IssuedBond only, int today)
        {
            // Run Due on a book holding just this bond, so the others stay untouched.
            var solo = new BondBook();
            solo.Bonds.Add(only);
            List<BondCash> paid = solo.Due(today);
            if (solo.Bonds.Count == 0)
            {
                Bonds.Remove(only);
            }

            return paid;
        }

        private static BondCash Cash(IssuedBond b, BondCashKind kind, long cents, int day)
        {
            return new BondCash
            {
                BondId = b.Id,
                Kind = kind,
                Cents = cents,
                Day = day,
            };
        }
    }

    /// <summary>
    /// Bonds sold before coupons became weekly paid every 30 days. They are converted once, when
    /// the city loads: the same bond (number, name, face, rate) with what is left of its term
    /// in weekly coupons, counted from its last paid coupon so no interest is skipped or paid
    /// twice. The repayment day moves by at most three days (the remaining days rounded to whole
    /// weeks), and the interest still to pay is never more than before (a year of 52 weeks is
    /// 364 days, against 360 for the old count).
    /// </summary>
    public static class BondMigration
    {
        /// <summary>Converts every bond that is not weekly; returns how many were converted.</summary>
        public static int ToWeekly(BondBook book)
        {
            int converted = 0;
            foreach (IssuedBond b in book.Bonds)
            {
                if (b.Terms.PeriodDays == BondMath.WeekDays)
                {
                    continue;
                }

                int last = b.LastCouponDay;
                int daysLeft = b.DueDay(b.Terms.Periods) - last;
                int weeks = (daysLeft + BondMath.WeekDays / 2) / BondMath.WeekDays;
                weeks = Math.Max(1, Math.Min(BondTerms.MaxPeriods, weeks));
                b.Terms = new BondTerms(
                    b.Terms.FaceCents,
                    b.Terms.RateBasisPoints,
                    weeks,
                    BondMath.WeekDays
                );
                b.IssueDay = last;
                b.CouponsPaid = 0;
                converted++;
            }

            return converted;
        }
    }

    /// <summary>
    /// Versioned text format for the bond book in the save (mod data id <see cref="DataId"/>).
    /// Unknown versions and malformed or out-of-range lines are skipped, so a newer or damaged
    /// blob never breaks loading; vanilla ignores mod data entirely.
    /// </summary>
    public static class BondBookCodec
    {
        public const string DataId = "FinanceOverhaul.Bonds";
        private const string Version = "v1";
        private const string IssuedTag = "issued";

        // A name is a line of its own, after the bonds: a build from before names skips it and
        // still reads every bond.
        private const string NameTag = "name";

        public static string Format(BondBook book)
        {
            var sb = new StringBuilder();
            sb.Append(Version).Append('\n');
            sb.Append("next=").Append(Int(book.NextId)).Append('\n');
            foreach (IssuedBond b in book.Bonds)
            {
                sb.Append(IssuedTag)
                    .Append('|')
                    .Append(Int(b.Id))
                    .Append('|')
                    .Append(b.Terms.FaceCents.ToString(CultureInfo.InvariantCulture))
                    .Append('|')
                    .Append(Int(b.Terms.RateBasisPoints))
                    .Append('|')
                    .Append(Int(b.Terms.Periods))
                    .Append('|')
                    .Append(Int(b.Terms.PeriodDays))
                    .Append('|')
                    .Append(Int(b.IssueDay))
                    .Append('|')
                    .Append(Int(b.CouponsPaid))
                    .Append('\n');
            }

            foreach (IssuedBond b in book.Bonds)
            {
                if (!string.IsNullOrEmpty(b.Name))
                {
                    sb.Append(NameTag)
                        .Append('|')
                        .Append(Int(b.Id))
                        .Append('|')
                        .Append(b.Name.Replace('\n', ' ').Replace('\r', ' '))
                        .Append('\n');
                }
            }

            return sb.ToString();
        }

        public static BondBook Parse(string text)
        {
            var book = new BondBook();
            if (string.IsNullOrEmpty(text))
            {
                return book;
            }

            string[] lines = text.Split('\n');
            if (lines[0].Trim('\r') != Version)
            {
                return book;
            }

            var names = new Dictionary<int, string>();

            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim('\r');
                if (line.StartsWith("next=", StringComparison.Ordinal))
                {
                    int next;
                    if (TryInt(line.Substring(5), out next) && next > 0)
                    {
                        book.NextId = Math.Max(book.NextId, next);
                    }

                    continue;
                }

                if (line.StartsWith(NameTag + "|", StringComparison.Ordinal))
                {
                    // name|ID|TEXT: the text is everything after the second separator.
                    string[] n = line.Split(new[] { '|' }, 3);
                    int named;
                    if (n.Length == 3 && TryInt(n[1], out named) && n[2].Length > 0)
                    {
                        names[named] = n[2];
                    }

                    continue;
                }

                string[] f = line.Split('|');
                long face;
                int id,
                    bp,
                    periods,
                    periodDays,
                    issueDay,
                    paid;
                if (
                    f.Length != 8
                    || f[0] != IssuedTag
                    || !TryInt(f[1], out id)
                    || !long.TryParse(
                        f[2],
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out face
                    )
                    || !TryInt(f[3], out bp)
                    || !TryInt(f[4], out periods)
                    || !TryInt(f[5], out periodDays)
                    || !TryInt(f[6], out issueDay)
                    || !TryInt(f[7], out paid)
                    || id <= 0
                    || paid < 0
                    || book.Find(id) != null
                )
                {
                    continue;
                }

                BondTerms terms;
                try
                {
                    terms = new BondTerms(face, bp, periods, periodDays);
                }
                catch (ArgumentOutOfRangeException)
                {
                    continue;
                }

                if (paid >= terms.Periods)
                {
                    continue; // fully repaid: nothing owed
                }

                book.Bonds.Add(
                    new IssuedBond
                    {
                        Id = id,
                        Terms = terms,
                        IssueDay = issueDay,
                        CouponsPaid = paid,
                    }
                );
                book.NextId = Math.Max(book.NextId, id + 1);
            }

            foreach (KeyValuePair<int, string> n in names)
            {
                IssuedBond bond = book.Find(n.Key);
                if (bond != null)
                {
                    bond.Name = n.Value;
                }
            }

            return book;
        }

        private static string Int(int v)
        {
            return v.ToString(CultureInfo.InvariantCulture);
        }

        private static bool TryInt(string s, out int v)
        {
            return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v);
        }
    }
}

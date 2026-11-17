using System;
using System.Collections.Generic;
using System.Globalization;

namespace FinanceOverhaul.Core
{
    /// <summary>
    /// The "last seen" bookmark (docs/features/save-compatibility.md, Welcome back). The mod writes
    /// the game day into its own save data once per game day. A city saved without the mod keeps
    /// the block unchanged (the game writes every mod's data back), so on the next load with the
    /// mod, payments that fell due after that day were missed.
    /// </summary>
    public static class SeenCodec
    {
        public const string DataId = "FinanceOverhaul.Seen";
        private const string Version = "v1";

        /// <summary>No bookmark (an older save, or a damaged or newer block).</summary>
        public const int None = -1;

        public static string Format(int day)
        {
            return Version + "\nday=" + day.ToString(CultureInfo.InvariantCulture) + "\n";
        }

        public static int Parse(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return None;
            }

            string[] lines = text.Split('\n');
            if (lines[0].Trim('\r') != Version)
            {
                return None;
            }

            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim('\r');
                int day;
                if (
                    line.StartsWith("day=", StringComparison.Ordinal)
                    && int.TryParse(
                        line.Substring(4),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out day
                    )
                    && day >= 0
                )
                {
                    return day;
                }
            }

            return None;
        }
    }

    /// <summary>What the city missed while the mod was off, and the player's two ways to settle it.</summary>
    public sealed class MissedPayments
    {
        public int LastSeenDay;
        public int Today;
        public readonly List<BondCash> Payments = new List<BondCash>();

        public int DaysAway
        {
            get { return Math.Max(0, Today - LastSeenDay); }
        }

        /// <summary>Everything missed: coupons plus any face repaid at maturity.</summary>
        public long TotalCents
        {
            get { return Sum(false); }
        }

        /// <summary>Faces of bonds that matured while the mod was off: repaid under either choice.</summary>
        public long MaturedCents
        {
            get { return Sum(true); }
        }

        public bool Any
        {
            get { return Payments.Count > 0; }
        }

        private long Sum(bool principalOnly)
        {
            long s = 0;
            foreach (BondCash c in Payments)
            {
                if (!principalOnly || c.Kind == BondCashKind.Principal)
                {
                    s += c.Cents;
                }
            }

            return s;
        }
    }

    public static class WelcomeBack
    {
        /// <summary>
        /// A bookmark one day behind is normal (a hot reload, or a save made before that day's
        /// payments ran): the mod pays that day itself. Only longer gaps count as time away.
        /// </summary>
        public const int MinDaysAway = 2;

        /// <summary>
        /// Payments that fell due after <paramref name="lastSeenDay"/> and up to today, without
        /// touching the book. Empty when there is no bookmark or no time has passed.
        /// </summary>
        public static MissedPayments Missed(BondBook book, int lastSeenDay, int today)
        {
            var m = new MissedPayments { LastSeenDay = lastSeenDay, Today = today };
            if (book == null || lastSeenDay == SeenCodec.None || today - lastSeenDay < MinDaysAway)
            {
                return m;
            }

            BondBook copy = BondBookCodec.Parse(BondBookCodec.Format(book));
            foreach (BondCash c in copy.Due(today))
            {
                if (c.Day > lastSeenDay)
                {
                    m.Payments.Add(c);
                }
            }

            return m;
        }

        /// <summary>
        /// Settles the missed payments on the book. "Pay" returns every payment due (the city pays
        /// them all today). "Start fresh" marks the missed coupons as done without paying them; a
        /// face that fell due is still returned, so a bond can never end up as free money.
        /// </summary>
        public static List<BondCash> Settle(BondBook book, int today, bool pay)
        {
            List<BondCash> due = book.Due(today);
            if (pay)
            {
                return due;
            }

            var cash = new List<BondCash>();
            foreach (BondCash c in due)
            {
                if (c.Kind == BondCashKind.Principal)
                {
                    cash.Add(c);
                }
            }

            return cash;
        }
    }

    /// <summary>The Welcome back dialog's words (Figma band 14). Kept short and plain.</summary>
    public static class WelcomeText
    {
        public static string Title
        {
            get { return Strings.Get("welcome.title"); }
        }

        public static string Skip
        {
            get { return Strings.Get("welcome.skip"); }
        }

        public static string SkipHelp
        {
            get { return Strings.Get("welcome.skipHelp"); }
        }

        public static string Footer
        {
            get { return Strings.Get("welcome.footer"); }
        }

        /// <summary>Tooltip on ×: closing picks the default.</summary>
        public static string CloseTip
        {
            get { return Strings.Get("welcome.close.tip"); }
        }

        public static string Ask
        {
            get { return Strings.Get("welcome.ask"); }
        }

        public static string Lead(int daysAway)
        {
            return Strings.Plural("welcome.lead", daysAway, Days(daysAway));
        }

        public static string Pay(long cents)
        {
            return Strings.Format("welcome.pay", Amount(cents));
        }

        public static string PayHelp(int daysAway)
        {
            return Strings.Plural("welcome.payHelp", daysAway, Days(daysAway));
        }

        /// <summary>Only when a bond matured while the mod was off.</summary>
        public static string Matured(long cents)
        {
            return Strings.Format("welcome.matured", Amount(cents));
        }

        public static string IndexBack(long shares)
        {
            return Strings.Format(
                "welcome.indexBack",
                shares.ToString("#,0", CultureInfo.InvariantCulture)
            );
        }

        /// <summary>Cents up to ₡9.99, whole ₡ from ₡10 (the mod's one ₡ rule).</summary>
        private static string Amount(long cents)
        {
            return cents < 1000 ? BondText.Money(cents) : BondText.WholeMoney(cents);
        }

        private static string Days(int n)
        {
            return n.ToString(CultureInfo.InvariantCulture);
        }
    }
}

namespace FinanceOverhaul.Core
{
    /// <summary>The city's Index holding as last saved with the mod (mod data <see cref="IndexCopyCodec.DataId"/>).</summary>
    public struct IndexHolding
    {
        public long Shares;
        public long SpentCents;
    }

    /// <summary>
    /// The Index copy (save compatibility P3/P7). The Index lives in a vanilla investment slot that
    /// the game drops when it loads a city without the mod (or with the mod's Index flag off). The
    /// mod keeps a copy of the holding in its own save data, written at every save (and quit or hot
    /// reload) while the Index is listed, and puts it back when an Index slot is created again.
    /// </summary>
    public static class IndexCopyCodec
    {
        public const string DataId = "FinanceOverhaul.IndexCopy";
        private const string Version = "v1";

        public static string Format(IndexHolding h)
        {
            return Version
                + "\nshares="
                + h.Shares.ToString(CultureInfo.InvariantCulture)
                + "\nspent="
                + h.SpentCents.ToString(CultureInfo.InvariantCulture)
                + "\n";
        }

        /// <summary>Null for no copy, another version, or a damaged block (never throws).</summary>
        public static IndexHolding? Parse(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            string[] lines = text.Split('\n');
            if (lines[0].Trim('\r') != Version)
            {
                return null;
            }

            long shares = -1,
                spent = -1;
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim('\r');
                int eq = line.IndexOf('=');
                if (eq <= 0)
                {
                    continue;
                }

                long v;
                if (
                    !long.TryParse(
                        line.Substring(eq + 1),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out v
                    )
                    || v < 0
                )
                {
                    continue;
                }

                string key = line.Substring(0, eq);
                if (key == "shares")
                {
                    shares = v;
                }
                else if (key == "spent")
                {
                    spent = v;
                }
            }

            if (shares < 0 || spent < 0)
            {
                return null;
            }

            return new IndexHolding { Shares = shares, SpentCents = spent };
        }

        /// <summary>
        /// What to put back into a newly created Index slot: the copy, when the slot is empty and
        /// the copy holds shares. Never touches a slot that already holds shares.
        /// </summary>
        public static IndexHolding? ToRestore(bool slotEmpty, IndexHolding? copy)
        {
            if (!slotEmpty || copy == null || copy.Value.Shares <= 0)
            {
                return null;
            }

            return copy;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace FinanceOverhaul.Market.Tests
{
    /// <summary>
    /// Welcome back (save compatibility): the "last seen" bookmark and settling payments missed
    /// while the mod was off. Properties over 500 seeded books and gaps:
    /// <list type="bullet">
    /// <item>Missed never changes the book, and lists exactly the payments due after the bookmark.</item>
    /// <item>Pay settles the same total that Missed showed; nothing is due again afterwards.</item>
    /// <item>Start fresh pays only faces of bonds that matured (never free money), skips every
    /// missed coupon, and leaves nothing due today; later payments are unchanged.</item>
    /// <item>A city that never left, or whose bookmark is one day behind, has nothing missed.</item>
    /// </list>
    /// </summary>
    public class WelcomeBackTests
    {
        private const int Trials = 500;

        [Fact]
        public void SeenCodecRoundTripAndBadInput()
        {
            Assert.Equal(759828, SeenCodec.Parse(SeenCodec.Format(759828)));
            Assert.Equal(SeenCodec.None, SeenCodec.Parse(null));
            Assert.Equal(SeenCodec.None, SeenCodec.Parse(""));
            Assert.Equal(SeenCodec.None, SeenCodec.Parse("v9\nday=5\n"));
            Assert.Equal(SeenCodec.None, SeenCodec.Parse("v1\nday=abc\n"));
            Assert.Equal(SeenCodec.None, SeenCodec.Parse("v1\nday=-4\n"));
            Assert.Equal(12, SeenCodec.Parse("v1\r\nother=1\r\nday=12\r\n"));
        }

        [Fact]
        public void Wording()
        {
            Assert.Equal(
                "Finance Overhaul was off for 23 days in this city.",
                WelcomeText.Lead(23)
            );
            Assert.Equal("Pay what was missed (₡44,000)", WelcomeText.Pay(4_400_000));
            Assert.Equal("Pay what was missed (₡5.00)", WelcomeText.Pay(500));
            Assert.Equal("Pay what was missed (₡9.99)", WelcomeText.Pay(999));
            Assert.Equal("Pay what was missed (₡10)", WelcomeText.Pay(1000));
            Assert.Equal(
                "Pays the 1 day of bond payments today, as if the mod had been on.",
                WelcomeText.PayHelp(1)
            );
            Assert.Equal("Your Index shares (1,200) are back.", WelcomeText.IndexBack(1200));
        }

        [Fact]
        public void Example()
        {
            // ₡1,000,000 at 3% for 1 year, sold day 1000: ₡2,500 a month, face on day 1360.
            var book = new BondBook();
            IssuedBond bond;
            book.Issue(new BondTerms(100_000_000, 300, 12, 30), 1000, out bond);
            book.Due(1040); // first coupon paid on day 1030 while the mod ran
            MissedPayments m = WelcomeBack.Missed(book, 1040, 1100); // off for 60 days
            Assert.Equal(60, m.DaysAway);
            Assert.Equal(2, m.Payments.Count); // days 1060 and 1090
            Assert.Equal(500_000, m.TotalCents);
            Assert.Equal(0, m.MaturedCents);
            Assert.Equal(1, book.Find(bond.Id).CouponsPaid); // Missed did not touch the book

            List<BondCash> cash = WelcomeBack.Settle(book, 1100, pay: false);
            Assert.Empty(cash);
            Assert.Equal(3, book.Find(bond.Id).CouponsPaid);
            Assert.Empty(book.Due(1100));
            Assert.Single(book.Due(1120)); // next coupon on its normal day
        }

        [Fact]
        public void NoBookmarkOrNoTimeMeansNothingMissed()
        {
            var book = new BondBook();
            IssuedBond bond;
            book.Issue(new BondTerms(100_000_000, 300, 12, 30), 1000, out bond);
            Assert.False(WelcomeBack.Missed(book, SeenCodec.None, 1500).Any);
            Assert.False(WelcomeBack.Missed(book, 1500, 1500).Any);
            Assert.False(WelcomeBack.Missed(book, 1500, 1400).Any);
        }

        [Fact]
        public void Properties()
        {
            var rng = new Random(2026);
            for (int t = 0; t < Trials; t++)
            {
                var book = new BondBook();
                int start = 700_000 + rng.Next(1000);
                int n = rng.Next(1, 6);
                for (int i = 0; i < n; i++)
                {
                    BondTerm term = new[] { BondTerm.OneYear, BondTerm.TwoYears }[rng.Next(2)];
                    IssuedBond b;
                    book.Issue(
                        new BondTerms(
                            100 + rng.Next(1, 1_000_000_000),
                            rng.Next(0, 2001),
                            BondPricing.Periods(term),
                            BondPricing.PeriodDays
                        ),
                        start + rng.Next(200),
                        out b
                    );
                }

                // The mod ran daily up to the bookmark, then was off for a while.
                int seen = start + 200 + rng.Next(400);
                for (int d = start; d <= seen; d += 1 + rng.Next(5))
                {
                    book.Due(d);
                }

                book.Due(seen);
                int today = seen + WelcomeBack.MinDaysAway + rng.Next(0, 800);
                string before = BondBookCodec.Format(book);

                // A city that never left, or a bookmark one day behind: nothing missed.
                Assert.False(WelcomeBack.Missed(book, seen, seen).Any);
                Assert.False(WelcomeBack.Missed(book, seen, seen + 1).Any);

                MissedPayments m = WelcomeBack.Missed(book, seen, today);
                Assert.Equal(before, BondBookCodec.Format(book));
                Assert.All(m.Payments, c => Assert.True(c.Day > seen && c.Day <= today));

                BondBook payBook = BondBookCodec.Parse(before);
                BondBook skipBook = BondBookCodec.Parse(before);
                List<BondCash> paid = WelcomeBack.Settle(payBook, today, pay: true);
                List<BondCash> skipped = WelcomeBack.Settle(skipBook, today, pay: false);

                Assert.Equal(m.TotalCents, paid.Sum(c => c.Cents));
                Assert.All(skipped, c => Assert.Equal(BondCashKind.Principal, c.Kind));
                Assert.Equal(m.MaturedCents, skipped.Sum(c => c.Cents));
                Assert.Empty(payBook.Due(today));
                Assert.Empty(skipBook.Due(today));

                // Both books carry on identically from today: same bonds, same future payments.
                Assert.Equal(BondBookCodec.Format(payBook), BondBookCodec.Format(skipBook));
                int later = today + 1 + rng.Next(800);
                Assert.Equal(
                    payBook.Due(later).Select(c => c.Cents + ":" + c.Day),
                    skipBook.Due(later).Select(c => c.Cents + ":" + c.Day)
                );
            }
        }
    }
}

namespace FinanceOverhaul.Market.Tests
{
    /// <summary>The Index copy: codec round trip, damaged input, and when it may be restored.</summary>
    public class IndexCopyTests
    {
        [Fact]
        public void CodecRoundTripAndBadInput()
        {
            var h = new IndexHolding { Shares = 120, SpentCents = 17_500_000 };
            IndexHolding? back = IndexCopyCodec.Parse(IndexCopyCodec.Format(h));
            Assert.True(back.HasValue);
            Assert.Equal(120, back.Value.Shares);
            Assert.Equal(17_500_000, back.Value.SpentCents);
            Assert.Null(IndexCopyCodec.Parse(null));
            Assert.Null(IndexCopyCodec.Parse("v2\nshares=1\nspent=1\n"));
            Assert.Null(IndexCopyCodec.Parse("v1\nshares=x\nspent=1\n"));
            Assert.Null(IndexCopyCodec.Parse("v1\nshares=-5\nspent=1\n"));
            Assert.Null(IndexCopyCodec.Parse("v1\nshares=5\n"));
        }

        [Fact]
        public void RestoreOnlyIntoAnEmptySlot()
        {
            var held = new IndexHolding { Shares = 120, SpentCents = 1 };
            Assert.Equal(120, IndexCopyCodec.ToRestore(true, held).Value.Shares);
            Assert.Null(IndexCopyCodec.ToRestore(false, held)); // a slot holding shares is never touched
            Assert.Null(IndexCopyCodec.ToRestore(true, null)); // no copy (older save)
            Assert.Null(
                IndexCopyCodec.ToRestore(true, new IndexHolding { Shares = 0, SpentCents = 0 })
            );
        }

        [Fact]
        public void RestoredNotice()
        {
            Notice n = NoticeText.IndexRestored(120);
            Assert.Equal(NoticeText.Brokerage, n.Sender);
            Assert.False(n.IsOrder);
            Assert.Equal(
                "Your Index shares (120) are back. They were kept safe while Finance Overhaul was off. #orders",
                n.Text
            );
        }
    }
}

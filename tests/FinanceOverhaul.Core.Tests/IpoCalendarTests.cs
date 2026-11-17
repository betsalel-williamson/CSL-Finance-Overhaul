using System.Collections.Generic;
using FinanceOverhaul.Core;
using Xunit;

namespace FinanceOverhaul.Core.Tests
{
    public class IpoCalendarTests
    {
        private static readonly BusinessFacts Good = new BusinessFacts
        {
            TopLevel = true,
            FullyStaffed = true,
            Jobs = 40,
            LandValue = 80,
        };

        private static BuildingKey B(ushort id)
        {
            return new BuildingKey(id, 1000u + id);
        }

        [Fact]
        public void TheReferencePoolListsOnceInTheTargetYears()
        {
            var rules = new IpoRules();
            Assert.Equal(
                10d * 365d,
                IpoCalendar.ExpectedDaysBetween(rules.ReferencePool, rules),
                6
            );
            Assert.Equal(1d / 3650d, IpoCalendar.ChanceToday(20, rules), 12);
        }

        [Fact]
        public void FewerCandidatesAreRarerMoreAreMoreCommonNoneNever()
        {
            var rules = new IpoRules();
            Assert.Equal(0d, IpoCalendar.ChanceToday(0, rules));
            Assert.True(double.IsPositiveInfinity(IpoCalendar.ExpectedDaysBetween(0, rules)));
            double last = 0d;
            for (int n = 1; n <= 500; n++)
            {
                double chance = IpoCalendar.ChanceToday(n, rules);
                Assert.True(chance > last, "more candidates, higher chance (n=" + n + ")");
                Assert.True(chance < 1d);
                last = chance;
            }

            // One candidate waits about twenty times as long as twenty do.
            Assert.InRange(IpoCalendar.ExpectedDaysBetween(1, rules), 72_000d, 74_000d);
        }

        [Theory]
        [InlineData(20, 10d)]
        [InlineData(5, 40d)]
        [InlineData(80, 2.5d)]
        public void SixtyThousandCityYearsAverageTheTarget(int candidates, double years)
        {
            // Simulated days, not the formula: counts the IPOs the dice actually deal.
            var rules = new IpoRules();
            var dice = new IpoDice(20261117UL);
            const int Days = 365 * 60_000;
            int ipos = 0;
            for (int day = 0; day < Days; day++)
            {
                if (IpoCalendar.Draw(candidates, 0, false, rules, dice) >= 0)
                {
                    ipos++;
                }
            }

            double averageYears = Days / 365d / ipos;
            Assert.InRange(averageYears, years * 0.95, years * 1.05);
        }

        [Fact]
        public void EveryCandidateIsEquallyLikely()
        {
            var rules = new IpoRules { TargetYears = 1, ReferencePool = 1 }; // frequent, to count
            var dice = new IpoDice(7UL);
            var picks = new int[8];
            int total = 0;
            for (int day = 0; day < 2_000_000; day++)
            {
                int pick = IpoCalendar.Draw(picks.Length, 0, false, rules, dice);
                if (pick >= 0)
                {
                    picks[pick]++;
                    total++;
                }
            }

            foreach (int count in picks)
            {
                Assert.InRange(count, total / 8 * 0.9, total / 8 * 1.1);
            }
        }

        [Fact]
        public void NoDrawWhileAnIpoRunsOrAtTheCap()
        {
            var rules = new IpoRules { TargetYears = 1, ReferencePool = 1 };
            var dice = new IpoDice(1UL);
            ulong before = dice.State;
            for (int day = 0; day < 10_000; day++)
            {
                Assert.Equal(-1, IpoCalendar.Draw(50, 0, true, rules, dice));
                Assert.Equal(-1, IpoCalendar.Draw(50, rules.ListingCap, false, rules, dice));
                Assert.Equal(-1, IpoCalendar.Draw(0, 0, false, rules, dice));
            }

            Assert.Equal(before, dice.State); // and the dice were not touched
        }

        [Fact]
        public void TheSameSeedDealsTheSameDays()
        {
            var rules = new IpoRules { TargetYears = 1 };
            var a = new IpoDice(42UL);
            var b = new IpoDice(42UL);
            for (int day = 0; day < 50_000; day++)
            {
                Assert.Equal(
                    IpoCalendar.Draw(20, 0, false, rules, a),
                    IpoCalendar.Draw(20, 0, false, rules, b)
                );
            }

            // Saving the state and carrying on gives the same numbers as never stopping.
            var resumed = new IpoDice(a.State);
            Assert.Equal(a.Next(), resumed.Next());
        }

        [Fact]
        public void ABusinessBecomesACandidateAfterEnoughHealthyDaysInARow()
        {
            var rules = new IpoRules { HealthyDays = 3 };
            var book = new CandidateBook();
            for (int day = 1; day <= 2; day++)
            {
                book.Observe(B(1), Good, rules);
                book.EndDay();
                Assert.Empty(book.Candidates(rules));
            }

            book.Observe(B(1), Good, rules);
            book.EndDay();
            Assert.Equal(new List<BuildingKey> { B(1) }, book.Candidates(rules));
            Assert.Equal(3, book.HealthyDaysOf(B(1)));
        }

        [Fact]
        public void LosingAnyConditionStartsTheCountAgain()
        {
            var rules = new IpoRules { HealthyDays = 2 };
            var bad = new List<BusinessFacts>();
            BusinessFacts f = Good;
            f.TopLevel = false;
            bad.Add(f);
            f = Good;
            f.HasProblems = true;
            bad.Add(f);
            f = Good;
            f.FullyStaffed = false;
            bad.Add(f);
            f = Good;
            f.Jobs = rules.MinJobs - 1;
            bad.Add(f);
            f = Good;
            f.LandValue = rules.LandValueFloor - 1;
            bad.Add(f);
            foreach (BusinessFacts unhealthy in bad)
            {
                Assert.False(CandidateBook.Healthy(unhealthy, rules));
                var book = new CandidateBook();
                book.Observe(B(1), Good, rules);
                book.EndDay();
                book.Observe(B(1), Good, rules);
                book.EndDay();
                Assert.Single(book.Candidates(rules));
                book.Observe(B(1), unhealthy, rules);
                book.EndDay();
                Assert.Empty(book.Candidates(rules));
                Assert.Equal(0, book.HealthyDaysOf(B(1)));
                book.Observe(B(1), Good, rules);
                book.EndDay();
                Assert.Equal(1, book.HealthyDaysOf(B(1)));
            }

            // Exactly at the floors counts as healthy.
            f = Good;
            f.Jobs = rules.MinJobs;
            f.LandValue = rules.LandValueFloor;
            Assert.True(CandidateBook.Healthy(f, rules));
        }

        [Fact]
        public void ABuildingThatIsGoneOrReplacedIsForgotten()
        {
            var rules = new IpoRules { HealthyDays = 1 };
            var book = new CandidateBook();
            book.Observe(B(1), Good, rules);
            book.Observe(B(2), Good, rules);
            book.EndDay();
            Assert.Equal(2, book.Candidates(rules).Count);

            // Day 2: building 1 is demolished and a new building takes its id.
            var reused = new BuildingKey(1, 9999u);
            book.Observe(reused, Good, rules);
            book.Observe(B(2), Good, rules);
            book.EndDay();
            Assert.Equal(0, book.HealthyDaysOf(B(1)));
            Assert.Equal(1, book.HealthyDaysOf(reused));
            Assert.Equal(2, book.HealthyDaysOf(B(2)));
        }

        [Fact]
        public void CandidatesComeInAFixedOrderAndCountsRestoreFromTheSave()
        {
            var rules = new IpoRules { HealthyDays = 10 };
            var book = new CandidateBook();
            book.Restore(B(7), 10);
            book.Restore(B(3), 400);
            book.Restore(B(5), 9); // one day short
            book.Restore(B(9), 0); // nothing to keep
            Assert.Equal(new List<BuildingKey> { B(3), B(7) }, book.Candidates(rules));
            int counted = 0;
            foreach (KeyValuePair<BuildingKey, int> kv in book.Counted)
            {
                counted++;
            }

            Assert.Equal(3, counted);
        }
    }
}

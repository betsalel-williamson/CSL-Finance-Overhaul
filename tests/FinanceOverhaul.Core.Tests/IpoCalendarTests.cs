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
        public void TheSameSeedDealsTheSameNumbers()
        {
            var a = new IpoDice(42UL);
            var b = new IpoDice(42UL);
            for (int i = 0; i < 1000; i++)
            {
                Assert.Equal(a.Next(), b.Next());
            }

            // Saving the state and carrying on gives the same numbers as never stopping.
            var resumed = new IpoDice(a.State);
            Assert.Equal(a.Next(), resumed.Next());
        }

        [Fact]
        public void CandidatesAreListedByKind()
        {
            var rules = new IpoRules { HealthyDays = 1 };
            var book = new CandidateBook();
            BusinessFacts office = Good;
            office.Kind = BusinessKind.Office;
            book.Observe(B(1), Good, rules);
            book.Observe(B(2), office, rules);
            book.Observe(B(3), office, rules);
            book.EndDay();
            Assert.Equal(
                new List<BuildingKey> { B(1) },
                book.Candidates(rules, BusinessKind.Commercial)
            );
            Assert.Equal(
                new List<BuildingKey> { B(2), B(3) },
                book.Candidates(rules, BusinessKind.Office)
            );
            Assert.Empty(book.Candidates(rules, BusinessKind.Oil));
            Assert.Equal(BusinessKind.Office, book.KindOf(B(3)));
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

using System.Collections.Generic; // HashSet, List
using System.Linq; // Count(), Distinct(), Select()
// FinanceOverhaul.Core: BuildingKey, BusinessFacts, BusinessKind, CandidateBook, CompanyRegistry,
//   CompanyStatus, IBusinessSource, IpoBuyout, IpoDice, IpoEngine, IpoEvent, IpoEventKind,
//   IpoPricing, IpoRules, KindFacts, LocalCompany
using FinanceOverhaul.Core;
using Xunit; // Assert, FactAttribute

namespace FinanceOverhaul.Core.Tests
{
    public class IpoEngineTests
    {
        private sealed class City : IBusinessSource
        {
            public long Earnings = 2_600_000; // ₡26,000 a year → ₡52 a share
            private readonly HashSet<string> used = new HashSet<string>();

            public string NameOf(BuildingKey building)
            {
                return "Business " + building.Id;
            }

            public string SymbolFor(string name)
            {
                string symbol = "B" + name.Substring(name.LastIndexOf(' ') + 1);
                Assert.True(used.Add(symbol), "symbol handed out twice: " + symbol);
                return symbol;
            }

            public long AnnualEarningsCents(BuildingKey building)
            {
                return Earnings;
            }
        }

        private static readonly BusinessFacts Healthy = new BusinessFacts
        {
            Kind = BusinessKind.Office,
            TopLevel = true,
            FullyStaffed = true,
            Jobs = 100,
            JobsFloor = 50,
            LandValue = 60,
        };

        /// <summary>A city with offices that have all been healthy long enough.</summary>
        private static CandidateBook Offices(int count, IpoRules rules)
        {
            var book = new CandidateBook();
            for (ushort id = 1; id <= count; id++)
            {
                book.Restore(new BuildingKey(id, id), rules.HealthyDays, BusinessKind.Office);
            }

            return book;
        }

        private static List<KindFacts> OfficeKind(int topLevel)
        {
            return new List<KindFacts>
            {
                new KindFacts
                {
                    Kind = BusinessKind.Office,
                    Squares = 900,
                    TopLevelBuildings = topLevel,
                },
            };
        }

        /// <summary>Rules under which the first draw is certain to schedule an IPO.</summary>
        private static IpoRules Certain()
        {
            // One bad day costs the whole count here, so a test can take a business out of the
            // running in a day.
            return new IpoRules
            {
                SlotFillDays = 1,
                BuyoutPercentPerYear = 0,
                BadDayCost = 365,
            };
        }

        [Fact]
        public void AnIpoIsScheduledAnnouncedOpensAndThenTrades()
        {
            IpoRules rules = Certain();
            var registry = new CompanyRegistry();
            CandidateBook book = Offices(5, rules);
            var dice = new IpoDice(11UL);
            var city = new City();
            var seen = new List<IpoEvent>();
            int announced = -1;
            for (int day = 100; day < 100 + 60 && registry.Companies.Count < 1; day++)
            {
                seen.AddRange(
                    IpoEngine.Step(registry, book, OfficeKind(10), 6, day, rules, dice, city)
                );
                if (registry.Companies.Count == 1)
                {
                    announced = day;
                }
            }

            LocalCompany company = Assert.Single(registry.Companies);
            Assert.Equal(IpoEventKind.Announced, Assert.Single(seen).Kind);
            Assert.InRange(announced, 100, 129); // on a day of the first period
            Assert.Equal(CompanyStatus.Offering, company.Status);
            Assert.Equal(5_200, company.OfferPriceCents);
            Assert.Equal(5_200, company.PriceCents); // the offering price holds for the window
            Assert.Equal(announced + 7, company.FirstTradingDay);
            Assert.Equal(BusinessKind.Office, company.Kind);
            Assert.StartsWith("Business ", company.Name);
            Assert.Equal(new List<int> { announced }, registry.IpoDays);

            // During the window nothing moves and no second IPO is drawn.
            for (int day = announced + 1; day < announced + 7; day++)
            {
                Assert.Empty(
                    IpoEngine.Step(registry, book, OfficeKind(10), 6, day, rules, dice, city)
                );
                Assert.Equal(5_200, company.PriceCents);
            }

            IpoEvent first = Assert.Single(
                IpoEngine.Step(registry, book, OfficeKind(10), 6, announced + 7, rules, dice, city)
            );
            Assert.Equal(IpoEventKind.FirstTradingDay, first.Kind);
            Assert.Equal(CompanyStatus.Trading, company.Status);
            Assert.Equal(IpoPricing.FirstDayPriceCents(5_200, first.Fraction), company.PriceCents);
            Assert.InRange(first.Fraction, IpoPricing.MinPop, IpoPricing.MaxPop);
        }

        [Fact]
        public void WhenThePickedBusinessStopsQualifyingTheNextCandidateTakesItsPlace()
        {
            IpoRules rules = Certain();
            var registry = new CompanyRegistry();
            CandidateBook book = Offices(3, rules);
            var dice = new IpoDice(3UL);
            var city = new City();
            Assert.Empty(IpoEngine.Step(registry, book, OfficeKind(3), 6, 0, rules, dice, city));
            // With these dice the draw schedules the IPO for a later day of the period.
            Assert.True(registry.IpoScheduled);
            Assert.True(registry.IpoDay > 0);

            BuildingKey picked = registry.IpoBusiness;
            List<BuildingKey> queue = book.Candidates(rules, BusinessKind.Office);
            BuildingKey expected = queue[(queue.IndexOf(picked) + 1) % queue.Count];

            // The picked business gets a problem icon and loses its streak; the others stay healthy.
            BusinessFacts sick = Healthy;
            sick.HasProblems = true;
            foreach (BuildingKey b in queue)
            {
                book.Observe(b, b.Equals(picked) ? sick : Healthy, rules);
            }

            book.EndDay();
            var events = new List<IpoEvent>();
            for (int day = 1; day <= registry.IpoDay; day++)
            {
                events.AddRange(
                    IpoEngine.Step(registry, book, OfficeKind(3), 6, day, rules, dice, city)
                );
            }

            IpoEvent announced = Assert.Single(events);
            Assert.Equal(IpoEventKind.Announced, announced.Kind);
            Assert.True(announced.StoodIn);
            Assert.Equal(expected, announced.Company.Home); // the next in the queue, not a new roll
            Assert.Single(registry.IpoDays); // it counts towards the year's two like any IPO
        }

        [Fact]
        public void WithNoOtherCandidateOfItsKindTheIpoFallsThrough()
        {
            IpoRules rules = Certain();
            var registry = new CompanyRegistry();
            CandidateBook book = Offices(1, rules);
            var dice = new IpoDice(3UL);
            var city = new City();
            // The draw schedules the only candidate for a day of the period.
            Assert.Empty(IpoEngine.Step(registry, book, OfficeKind(1), 6, 0, rules, dice, city));
            if (!registry.IpoScheduled)
            {
                // It was scheduled for day 0 and announced at once: nothing to cancel.
                Assert.Single(registry.Companies);
                return;
            }

            // It gets a problem icon the next day and loses its streak.
            BusinessFacts sick = Healthy;
            sick.HasProblems = true;
            book.Observe(registry.IpoBusiness, sick, rules);
            book.EndDay();
            var events = new List<IpoEvent>();
            for (int day = 1; day <= registry.IpoDay; day++)
            {
                events.AddRange(
                    IpoEngine.Step(registry, book, OfficeKind(1), 6, day, rules, dice, city)
                );
            }

            Assert.Equal(IpoEventKind.Cancelled, Assert.Single(events).Kind);
            Assert.Empty(registry.Companies);
            Assert.Empty(registry.IpoDays); // a cancelled IPO does not use up the year's two
            Assert.False(registry.IpoScheduled);
        }

        [Fact]
        public void TwoAYearOneAtATimeNeverTheSameBusinessTwiceAndNoMoreThanTheSlots()
        {
            IpoRules rules = Certain();
            var registry = new CompanyRegistry();
            CandidateBook book = Offices(40, rules);
            var dice = new IpoDice(2112UL);
            var city = new City();
            // 45 top-level offices: three slots (the gate, 20, 40).
            for (int day = 0; day < 365 * 6; day++)
            {
                IpoEngine.Step(registry, book, OfficeKind(45), 9, day, rules, dice, city);
                Assert.True(registry.Companies.Count(c => c.Status == CompanyStatus.Offering) <= 1);
            }

            Assert.Equal(3, registry.Companies.Count);
            Assert.Equal(3, registry.Companies.Select(c => c.Home).Distinct().Count());
            Assert.Equal(3, registry.Companies.Select(c => c.Symbol).Distinct().Count());
            for (int i = 2; i < registry.IpoDays.Count; i++)
            {
                Assert.True(registry.IpoDays[i] - registry.IpoDays[i - 2] >= 365);
            }
        }

        [Fact]
        public void ABuyoutPaysThePremiumDelistsAndFreesTheSlot()
        {
            IpoRules rules = Certain();
            rules.BuyoutPercentPerYear = 100; // certain, once eligible
            var registry = new CompanyRegistry();
            CandidateBook book = Offices(6, rules);
            var dice = new IpoDice(40UL);
            var city = new City();
            IpoEvent? bought = null;
            int day = 0;
            for (; day < 365 * 4 && bought == null; day++)
            {
                foreach (
                    IpoEvent e in IpoEngine.Step(
                        registry,
                        book,
                        OfficeKind(5),
                        6,
                        day,
                        rules,
                        dice,
                        city
                    )
                )
                {
                    if (e.Kind == IpoEventKind.BoughtOut)
                    {
                        bought = e;
                    }
                }
            }

            Assert.NotNull(bought);
            LocalCompany gone = bought.Value.Company;
            Assert.True(day - gone.FirstTradingDay >= rules.BuyoutMinDaysListed); // not in its first year
            Assert.InRange(bought.Value.Fraction, IpoBuyout.MinPremium, IpoBuyout.MaxPremium);
            Assert.Equal(
                IpoBuyout.PricePerShareCents(gone.PriceCents, bought.Value.Fraction),
                bought.Value.PricePerShareCents
            );
            Assert.True(bought.Value.PricePerShareCents > gone.PriceCents);
            Assert.DoesNotContain(gone, registry.Companies);
            Assert.Null(registry.Find(gone.Id));

            // The slot is free: with one slot (5 top-level offices), another company lists later.
            int before = registry.NextId;
            for (int more = 0; more < 365 * 3 && registry.NextId == before; more++, day++)
            {
                IpoEngine.Step(registry, book, OfficeKind(5), 6, day, rules, dice, city);
            }

            Assert.True(registry.NextId > before, "the freed slot was never filled again");
        }

        [Fact]
        public void TheSameSaveAndDicePlayTheSameYears()
        {
            IpoRules rules = new IpoRules { SlotFillDays = 200 };
            var logs = new List<string>();
            for (int run = 0; run < 2; run++)
            {
                var registry = new CompanyRegistry();
                CandidateBook book = Offices(30, rules);
                var dice = new IpoDice(99UL);
                var city = new City();
                var log = new System.Text.StringBuilder();
                for (int day = 0; day < 365 * 8; day++)
                {
                    foreach (
                        IpoEvent e in IpoEngine.Step(
                            registry,
                            book,
                            OfficeKind(45),
                            9,
                            day,
                            rules,
                            dice,
                            city
                        )
                    )
                    {
                        log.Append(day)
                            .Append(':')
                            .Append(e.Kind)
                            .Append(':')
                            .Append(
                                e.Company == null
                                    ? "-"
                                    : e.Company.Symbol + "@" + e.Company.PriceCents
                            )
                            .Append(' ');
                    }
                }

                logs.Add(log.ToString());
            }

            Assert.Equal(logs[0], logs[1]);
            Assert.Contains("Announced", logs[0]);
        }

        [Fact]
        public void ATradingCompanyMovesOnceADayFromTheDayAfterItOpens()
        {
            var rules = new IpoRules { MaxIposPerYear = 0, BuyoutPercentPerYear = 0 };
            var registry = new CompanyRegistry { NextDrawDay = int.MaxValue };
            var city = new City();
            var dice = new IpoDice(11);
            var book = new CandidateBook();
            IpoEvent listed = IpoEngine.List(
                registry,
                new BuildingKey(1, 1),
                BusinessKind.Office,
                100,
                rules,
                city
            );
            LocalCompany c = listed.Company;

            // The offering window: the price is the offering price every day.
            for (int day = 101; day < c.FirstTradingDay; day++)
            {
                IpoEngine.Step(registry, book, OfficeKind(0), 9, day, rules, dice, city);
                Assert.Equal(c.OfferPriceCents, c.PriceCents);
            }

            // The first trading day is the pop alone.
            IpoEngine.Step(registry, book, OfficeKind(0), 9, c.FirstTradingDay, rules, dice, city);
            long opened = c.PriceCents;
            IpoEngine.Step(registry, book, OfficeKind(0), 9, c.FirstTradingDay, rules, dice, city);
            Assert.Equal(opened, c.PriceCents);

            // Then it moves, once a day however often the day is stepped.
            int moved = 0;
            long last = opened;
            for (int day = c.FirstTradingDay + 1; day <= c.FirstTradingDay + 30; day++)
            {
                IpoEngine.Step(registry, book, OfficeKind(0), 9, day, rules, dice, city);
                long once = c.PriceCents;
                IpoEngine.Step(registry, book, OfficeKind(0), 9, day, rules, dice, city);
                Assert.Equal(once, c.PriceCents);
                moved += once != last ? 1 : 0;
                last = once;
            }

            Assert.True(moved >= 25, "moved on " + moved + " of 30 days");
        }
    }
}

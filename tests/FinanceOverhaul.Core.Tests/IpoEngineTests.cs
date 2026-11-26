using System.Collections.Generic; // Dictionary, HashSet, List
using System.Linq; // Count(), Distinct(), Select()
// FinanceOverhaul.Core: BuildingKey, BusinessFacts, BusinessKind, BusinessSite, CandidateBook,
//   CompanyRegistry, CompanyStatus, IBusinessSource, IpoBuyout, IpoDice, IpoEngine, IpoEvent,
//   IpoEventKind, IpoPricing, IpoRules, KindFacts, LocalCompany
using FinanceOverhaul.Core;
using Xunit; // Assert, FactAttribute, InlineDataAttribute, TheoryAttribute

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

            /// <summary>Earnings by building id, where a test sets them; else <see cref="Earnings"/>.</summary>
            public readonly Dictionary<ushort, long> EarningsOf = new Dictionary<ushort, long>();

            /// <summary>Buildings demolished, abandoned or collapsed.</summary>
            public readonly HashSet<BuildingKey> Lost = new HashSet<BuildingKey>();

            public long AnnualEarningsCents(BuildingKey building)
            {
                long e;
                return EarningsOf.TryGetValue(building.Id, out e) ? e : Earnings;
            }

            public bool HomeStands(BuildingKey building)
            {
                return !Lost.Contains(building);
            }

            /// <summary>An area's or factory's rebuilt headquarters, by its site note.</summary>
            public readonly Dictionary<string, BuildingKey> Rebuilt =
                new Dictionary<string, BuildingKey>();

            public bool TrySuccessor(LocalCompany company, out BuildingKey home)
            {
                return Rebuilt.TryGetValue(company.SiteRef ?? string.Empty, out home);
            }
        }

        private static readonly BusinessFacts Healthy = new BusinessFacts
        {
            Kind = BusinessKind.Office,
            TopLevel = true,
            FullyStaffed = true,
            Jobs = 100,
            Workers = 100,
            WorkersFloor = 50,
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

        [Theory]
        [InlineData(0)] // no Stock Exchange
        [InlineData(1)]
        public void NoIpoIsDrawnBelowTheStockExchangeLevel(int exchangeLevel)
        {
            IpoRules rules = Certain();
            var registry = new CompanyRegistry();
            CandidateBook book = Offices(5, rules);
            var dice = new IpoDice(11UL);
            var city = new City();
            for (int day = 0; day < 3 * 365; day++)
            {
                Assert.Empty(
                    IpoEngine.Step(
                        registry,
                        book,
                        OfficeKind(10),
                        6,
                        day,
                        rules,
                        dice,
                        city,
                        exchangeLevel
                    )
                );
            }

            Assert.Empty(registry.Companies);
            Assert.False(registry.IpoScheduled);
            Assert.Empty(registry.IpoDays); // pacing is not used up while waiting

            // Once the exchange reaches level 2 the waiting candidates list.
            for (int day = 3 * 365; day < 3 * 365 + 60 && registry.Companies.Count == 0; day++)
            {
                IpoEngine.Step(registry, book, OfficeKind(10), 6, day, rules, dice, city, 2);
            }

            Assert.Single(registry.Companies);
        }

        [Fact]
        public void AnIpoScheduledBeforeTheStockExchangeWasLostIsCancelled()
        {
            IpoRules rules = Certain();
            var registry = new CompanyRegistry();
            CandidateBook book = Offices(3, rules);
            var dice = new IpoDice(3UL);
            var city = new City();
            Assert.Empty(IpoEngine.Step(registry, book, OfficeKind(3), 6, 0, rules, dice, city, 2));
            Assert.True(registry.IpoScheduled);
            int ipoDay = registry.IpoDay;
            for (int day = 1; day < ipoDay; day++)
            {
                IpoEngine.Step(registry, book, OfficeKind(3), 6, day, rules, dice, city, 0);
            }

            IpoEvent cancelled = Assert.Single(
                IpoEngine.Step(registry, book, OfficeKind(3), 6, ipoDay, rules, dice, city, 0)
            );
            Assert.Equal(IpoEventKind.Cancelled, cancelled.Kind);
            Assert.Empty(registry.Companies);
            Assert.False(registry.IpoScheduled);
        }

        [Fact]
        public void ListedCompaniesKeepTradingWithoutAStockExchange()
        {
            IpoRules rules = Certain();
            var registry = new CompanyRegistry();
            CandidateBook book = Offices(5, rules);
            var dice = new IpoDice(11UL);
            var city = new City();
            for (int day = 0; day < 60 && registry.Companies.Count == 0; day++)
            {
                IpoEngine.Step(registry, book, OfficeKind(10), 6, day, rules, dice, city, 5);
            }

            LocalCompany company = Assert.Single(registry.Companies);
            for (int day = 60; day < 200; day++)
            {
                IpoEngine.Step(registry, book, OfficeKind(10), 6, day, rules, dice, city, 0);
            }

            Assert.Equal(CompanyStatus.Trading, company.Status);
            Assert.Single(registry.Companies);
        }

        [Fact]
        public void WithLocalIposSwitchedOffListedCompaniesKeepTradingAndNoneIsDrawn()
        {
            // A company already trading; then Local IPOs is switched off in Options.
            IpoRules rules = Certain();
            var registry = new CompanyRegistry();
            CandidateBook book = Offices(5, rules);
            var dice = new IpoDice(11UL);
            var city = new City();
            int day = 100;
            for (; day < 200 && registry.Companies.Count < 1; day++)
            {
                IpoEngine.Step(registry, book, OfficeKind(10), 6, day, rules, dice, city);
            }

            LocalCompany company = Assert.Single(registry.Companies);
            for (; company.Status != CompanyStatus.Trading; day++)
            {
                IpoEngine.Step(registry, book, OfficeKind(10), 6, day, rules, dice, city);
            }

            // Thirty days with new listings off: the price moves (not one flat line), and no
            // second company is drawn although every slot is open and the draw is certain.
            var prices = new HashSet<long>();
            for (int end = day + 30; day < end; day++)
            {
                List<IpoEvent> events = IpoEngine.Step(
                    registry,
                    book,
                    OfficeKind(10),
                    6,
                    day,
                    rules,
                    dice,
                    city,
                    IpoEngine.MaxExchangeLevel,
                    newListings: false
                );
                Assert.DoesNotContain(events, e => e.Kind == IpoEventKind.Announced);
                prices.Add(company.PriceCents);
            }

            Assert.Single(registry.Companies);
            Assert.False(registry.IpoScheduled);
            Assert.True(prices.Count > 20, "the price moved on " + prices.Count + " of 30 days");
        }

        [Fact]
        public void OnlyTheLargestEmployersAreDrawnAndTheBiggestMostOften()
        {
            // Fifteen offices of 10 to 150 workers: the draw picks from the ten largest only
            // (60 workers and up), and the largest about two and a half times as often as the
            // tenth.
            var picks = new Dictionary<int, int>();
            for (ulong seed = 1; seed <= 400; seed++)
            {
                IpoRules rules = Certain();
                CandidateBook book = Offices(15, rules);
                for (ushort id = 1; id <= 15; id++)
                {
                    book.SetWorkers(new BuildingKey(id, id), id * 10);
                }

                var registry = new CompanyRegistry();
                IpoEngine.Step(
                    registry,
                    book,
                    OfficeKind(10),
                    6,
                    0,
                    rules,
                    new IpoDice(seed),
                    new City()
                );
                // Scheduled for later in the period, or announced at once.
                int id0 = registry.IpoScheduled
                    ? registry.IpoBusiness.Id
                    : Assert.Single(registry.Companies).Home.Id;
                Assert.True(id0 >= 6, "a business outside the ten largest was drawn: " + id0);
                int n;
                picks.TryGetValue(id0, out n);
                picks[id0] = n + 1;
            }

            int largest,
                tenth;
            picks.TryGetValue(15, out largest);
            picks.TryGetValue(6, out tenth);
            Assert.True(largest > tenth * 3 / 2, "largest " + largest + ", tenth " + tenth);
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
            // The largest other candidate takes its place (all the same size here: the older
            // building).
            BuildingKey expected = queue.Find(b => !b.Equals(picked));

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
            Assert.Equal(expected, announced.Company.Home); // the largest other, not a new roll
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

        /// <summary>A company on building 1 of an office city, past its first trading day, on day 200.</summary>
        private static LocalCompany Trading(CompanyRegistry registry, IpoRules rules, City city)
        {
            LocalCompany c = IpoEngine
                .List(registry, new BuildingKey(1, 1), BusinessKind.Office, 100, rules, city)
                .Company;
            c.Status = CompanyStatus.Trading;
            c.FirstTradingDay = 107;
            return c;
        }

        private static List<IpoEvent> Day(
            CompanyRegistry registry,
            CandidateBook book,
            int day,
            IpoRules rules,
            IpoDice dice,
            City city
        )
        {
            // No new IPO can be drawn: the kind has no top-level buildings, so no slot.
            return IpoEngine.Step(registry, book, OfficeKind(0), 9, day, rules, dice, city);
        }

        [Fact]
        public void ACompanyThatLosesItsHeadquartersMovesToTheCandidateThatEarnsTheMost()
        {
            IpoRules rules = Certain();
            var registry = new CompanyRegistry { NextDrawDay = int.MaxValue };
            CandidateBook book = Offices(4, rules);
            var dice = new IpoDice(5UL);
            var city = new City();
            LocalCompany c = Trading(registry, rules, city);
            string symbol = c.Symbol;
            city.EarningsOf[2] = 1_000_000;
            city.EarningsOf[3] = 9_000_000;
            city.EarningsOf[4] = 9_000_000; // a tie: the first in the queue wins

            Assert.Empty(Day(registry, book, 200, rules, dice, city));
            city.Lost.Add(new BuildingKey(1, 1));
            IpoEvent moved = Assert.Single(Day(registry, book, 201, rules, dice, city));

            Assert.Equal(IpoEventKind.Moved, moved.Kind);
            Assert.Same(c, moved.Company);
            Assert.Equal(new BuildingKey(1, 1), moved.From);
            Assert.Equal(new BuildingKey(3, 3), c.Home);
            Assert.Equal(-1, c.HomeLostDay);
            Assert.Equal(symbol, c.Symbol);
            Assert.Single(registry.Companies);
            Assert.Empty(Day(registry, book, 202, rules, dice, city)); // settled in its new home
        }

        [Fact]
        public void AnAreaCompanyNeverMovesIntoAZonedBusinessAndWaitsToBeRebuilt()
        {
            IpoRules rules = Certain();
            var registry = new CompanyRegistry { NextDrawDay = int.MaxValue };
            CandidateBook book = Offices(4, rules); // offices 1 to 4, all candidates
            var dice = new IpoDice(5UL);
            var city = new City();
            var gate = new BuildingKey(1, 1);
            LocalCompany c = IpoEngine
                .List(
                    registry,
                    gate,
                    BusinessKind.Office,
                    100,
                    rules,
                    city,
                    BusinessSite.Area,
                    "area 7"
                )
                .Company;
            c.Status = CompanyStatus.Trading;
            Assert.Equal(BusinessSite.Area, c.Site);

            city.Lost.Add(gate); // demolished, or switched off
            IpoEvent lost = Assert.Single(Day(registry, book, 201, rules, dice, city));
            Assert.Equal(IpoEventKind.HomeLost, lost.Kind);
            Assert.Equal(gate, c.Home); // the offices 2 to 4 were not an option
            Assert.Equal(201, c.HomeLostDay);
            Assert.Empty(Day(registry, book, 202, rules, dice, city));

            // The main building is rebuilt: the company takes it back.
            var rebuilt = new BuildingKey(90, 91);
            city.Rebuilt["area 7"] = rebuilt;
            IpoEvent back = Assert.Single(Day(registry, book, 220, rules, dice, city));
            Assert.Equal(IpoEventKind.HomeRestored, back.Kind);
            Assert.Equal(gate, back.From);
            Assert.Equal(rebuilt, c.Home);
            Assert.Equal(-1, c.HomeLostDay);
        }

        [Fact]
        public void AHeadquartersSwitchedBackOnEndsTheGracePeriod()
        {
            IpoRules rules = Certain();
            var registry = new CompanyRegistry { NextDrawDay = int.MaxValue };
            CandidateBook book = Offices(1, rules);
            var dice = new IpoDice(5UL);
            var city = new City();
            var factory = new BuildingKey(1, 1);
            LocalCompany c = IpoEngine
                .List(
                    registry,
                    factory,
                    BusinessKind.UniqueFactory,
                    100,
                    rules,
                    city,
                    BusinessSite.Factory,
                    "Bakery"
                )
                .Company;
            c.Status = CompanyStatus.Trading;

            city.Lost.Add(factory);
            Assert.Equal(
                IpoEventKind.HomeLost,
                Assert.Single(Day(registry, book, 201, rules, dice, city)).Kind
            );
            long falling = c.PriceCents;
            city.Lost.Remove(factory); // switched back on within the grace period
            IpoEvent back = Assert.Single(Day(registry, book, 230, rules, dice, city));
            Assert.Equal(IpoEventKind.HomeRestored, back.Kind);
            Assert.Equal(default(BuildingKey), back.From); // the same building
            Assert.Equal(factory, c.Home);
            Assert.Equal(-1, c.HomeLostDay);
            for (int day = 231; day < 231 + rules.HomeGraceDays + 5; day++)
            {
                Day(registry, book, day, rules, dice, city);
            }

            Assert.Single(registry.Companies); // never closed
            Assert.True(falling > 0);
        }

        [Fact]
        public void AZonedCompanyDoesNotMoveIntoAnAreasMainBuilding()
        {
            IpoRules rules = Certain();
            var registry = new CompanyRegistry { NextDrawDay = int.MaxValue };
            CandidateBook book = Offices(1, rules); // the company's own office
            var gate = new BuildingKey(5, 5);
            book.Observe(gate, BusinessKind.Office, true, rules, BusinessSite.Area, "area 1");
            book.Restore(gate, rules.HealthyDays, BusinessKind.Office, BusinessSite.Area, "area 1");
            var dice = new IpoDice(5UL);
            var city = new City();
            LocalCompany c = Trading(registry, rules, city);
            Assert.Contains(gate, book.Candidates(rules, BusinessKind.Office));

            city.Lost.Add(new BuildingKey(1, 1));
            IpoEvent lost = Assert.Single(Day(registry, book, 201, rules, dice, city));
            Assert.Equal(IpoEventKind.HomeLost, lost.Kind);
            Assert.NotEqual(gate, c.Home);
        }

        [Fact]
        public void WithNowhereToMoveItsPriceFallsForNinetyDaysThenItClosesAtTheLastPrice()
        {
            IpoRules rules = Certain();
            var registry = new CompanyRegistry { NextDrawDay = int.MaxValue };
            CandidateBook book = Offices(1, rules); // its own building is the only office
            var dice = new IpoDice(5UL);
            var city = new City();
            LocalCompany c = Trading(registry, rules, city);
            c.PriceCents = 10_000;
            city.Lost.Add(new BuildingKey(1, 1));

            IpoEvent lost = Assert.Single(Day(registry, book, 300, rules, dice, city));
            Assert.Equal(IpoEventKind.HomeLost, lost.Kind);
            Assert.Equal(300, c.HomeLostDay);
            Assert.Equal(9_800, c.PriceCents); // 2% lower, no swing

            long expected = 9_800;
            for (int day = 301; day < 390; day++)
            {
                Assert.Empty(Day(registry, book, day, rules, dice, city)); // one chirp, not one a day
                expected = (long)
                    System.Math.Round(expected * 0.98, System.MidpointRounding.AwayFromZero);
                Assert.Equal(expected, c.PriceCents);
            }

            IpoEvent closed = Assert.Single(Day(registry, book, 390, rules, dice, city));
            Assert.Equal(IpoEventKind.Defunct, closed.Kind);
            Assert.Same(c, closed.Company);
            Assert.Equal(c.PriceCents, closed.PricePerShareCents);
            Assert.InRange(closed.PricePerShareCents, 1_500, 1_700); // about a sixth of ₡100
            Assert.Empty(registry.Companies);
        }

        [Fact]
        public void ABusinessThatQualifiesDuringTheGracePeriodTakesTheCompanyIn()
        {
            IpoRules rules = Certain();
            var registry = new CompanyRegistry { NextDrawDay = int.MaxValue };
            CandidateBook book = Offices(1, rules);
            var dice = new IpoDice(5UL);
            var city = new City();
            LocalCompany c = Trading(registry, rules, city);
            city.Lost.Add(new BuildingKey(1, 1));
            Assert.Equal(
                IpoEventKind.HomeLost,
                Assert.Single(Day(registry, book, 300, rules, dice, city)).Kind
            );

            book.Restore(new BuildingKey(7, 7), rules.HealthyDays, BusinessKind.Office);
            IpoEvent moved = Assert.Single(Day(registry, book, 340, rules, dice, city));

            Assert.Equal(IpoEventKind.Moved, moved.Kind);
            Assert.Equal(new BuildingKey(7, 7), c.Home);
            Assert.Equal(-1, c.HomeLostDay);
            Assert.Empty(Day(registry, book, 400, rules, dice, city)); // no longer closing
            Assert.Single(registry.Companies);
        }

        [Fact]
        public void ACandidateWhoseBuildingIsAlsoLostIsNotAHome()
        {
            IpoRules rules = Certain();
            var registry = new CompanyRegistry { NextDrawDay = int.MaxValue };
            CandidateBook book = Offices(2, rules);
            var dice = new IpoDice(5UL);
            var city = new City();
            LocalCompany c = Trading(registry, rules, city);
            city.Lost.Add(new BuildingKey(1, 1));
            city.Lost.Add(new BuildingKey(2, 2)); // abandoned the same day, its count not yet reset

            Assert.Equal(
                IpoEventKind.HomeLost,
                Assert.Single(Day(registry, book, 300, rules, dice, city)).Kind
            );
            Assert.Equal(new BuildingKey(1, 1), c.Home);
        }

        [Fact]
        public void ACompanyWithNoHeadquartersIsNeverBoughtOut()
        {
            IpoRules rules = new IpoRules { BuyoutPercentPerYear = 100, DrawEveryDays = 365 };
            var registry = new CompanyRegistry();
            CandidateBook book = Offices(1, rules);
            var dice = new IpoDice(5UL);
            var city = new City();
            LocalCompany c = Trading(registry, rules, city);
            city.Lost.Add(new BuildingKey(1, 1));
            registry.NextDrawDay = 1_000; // the draw comes during the grace period

            for (int day = 990; day < 990 + 80; day++)
            {
                foreach (IpoEvent e in Day(registry, book, day, rules, dice, city))
                {
                    Assert.NotEqual(IpoEventKind.BoughtOut, e.Kind);
                }
            }

            Assert.False(registry.BuyoutScheduled);
            Assert.Same(c, Assert.Single(registry.Companies));
        }
    }
}

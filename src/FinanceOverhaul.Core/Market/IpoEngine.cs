using System;
using System.Collections.Generic;

namespace FinanceOverhaul.Core
{
    /// <summary>Where a local company stands.</summary>
    public enum CompanyStatus
    {
        /// <summary>Announced: the offering window is open and the price is the offering price.</summary>
        Offering,

        /// <summary>Past its first trading day.</summary>
        Trading,
    }

    /// <summary>A business of the city that went public.</summary>
    public sealed class LocalCompany
    {
        public int Id;
        public string Name;
        public string Symbol;
        public BusinessKind Kind;

        /// <summary>Its headquarters.</summary>
        public BuildingKey Home;

        public int AnnouncedDay;
        public int FirstTradingDay;
        public long OfferPriceCents;

        /// <summary>The price now: the offering price until the first trading day.</summary>
        public long PriceCents;

        public CompanyStatus Status;
    }

    /// <summary>What happened to local companies on a day, for the game side to act on.</summary>
    public enum IpoEventKind
    {
        /// <summary>A company was announced: list it, mark its building, tell the player.</summary>
        Announced,

        /// <summary>The IPO planned for today fell through (the business no longer qualifies).</summary>
        Cancelled,

        /// <summary>The offering window closed: the price moved to its first-day price.</summary>
        FirstTradingDay,

        /// <summary>A company was bought: pay the city for its shares and delist it.</summary>
        BoughtOut,
    }

    public struct IpoEvent
    {
        public IpoEventKind Kind;
        public LocalCompany Company;

        /// <summary>FirstTradingDay: the pop. BoughtOut: the premium.</summary>
        public double Fraction;

        /// <summary>BoughtOut: what the buyer pays a share.</summary>
        public long PricePerShareCents;
    }

    /// <summary>
    /// The city's local companies and what is planned for them, saved with the city: the
    /// companies listed, the days of past IPOs (for pacing), the next draw day, and an IPO or a
    /// buy-out the dice have already scheduled for a coming day.
    /// </summary>
    public sealed class CompanyRegistry
    {
        public readonly List<LocalCompany> Companies = new List<LocalCompany>();
        public readonly List<int> IpoDays = new List<int>();
        public int NextId = 1;

        /// <summary>The day of the next draw (0: draw on the first day the engine runs).</summary>
        public int NextDrawDay;

        public bool IpoScheduled;
        public BuildingKey IpoBusiness;
        public BusinessKind IpoKind;
        public int IpoDay;

        public bool BuyoutScheduled;
        public int BuyoutCompanyId;
        public int BuyoutDay;

        public LocalCompany Find(int id)
        {
            foreach (LocalCompany c in Companies)
            {
                if (c.Id == id)
                {
                    return c;
                }
            }

            return null;
        }

        public int ListedOf(BusinessKind kind)
        {
            int n = 0;
            foreach (LocalCompany c in Companies)
            {
                if (c.Kind == kind)
                {
                    n++;
                }
            }

            return n;
        }

        public bool OfferingOpen
        {
            get
            {
                foreach (LocalCompany c in Companies)
                {
                    if (c.Status == CompanyStatus.Offering)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public bool IsHome(BuildingKey building)
        {
            foreach (LocalCompany c in Companies)
            {
                if (c.Home.Equals(building))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>What the game side supplies about a business when it is about to list.</summary>
    public interface IBusinessSource
    {
        /// <summary>The business's name as the player sees it.</summary>
        string NameOf(BuildingKey building);

        /// <summary>A ticker symbol for that name, unique among the listed stocks.</summary>
        string SymbolFor(string name);

        /// <summary>What the business earns in a game year, in cents.</summary>
        long AnnualEarningsCents(BuildingKey building);
    }

    /// <summary>
    /// One day of Local IPOs, in order:
    /// 1. On a draw day (once a period) the dice decide whether an IPO and whether a buy-out
    ///    happen in the coming period, and on which day; both are only scheduled.
    /// 2. A scheduled IPO whose day has come is announced if its business still qualifies:
    ///    the company is listed at its offering price and the offering window opens. Otherwise
    ///    it is cancelled.
    /// 3. A company whose offering window has closed opens at its first-day price (the pop).
    /// 4. A scheduled buy-out whose day has come pays the premium and delists the company.
    /// Pure: the game side lists, pays and announces from the returned events.
    /// </summary>
    public static class IpoEngine
    {
        /// <summary>Days the city can buy at the offering price.</summary>
        public const int OfferDays = 7;

        public static List<IpoEvent> Step(
            CompanyRegistry registry,
            CandidateBook candidates,
            IList<KindFacts> kinds,
            int tilesOwned,
            int today,
            IpoRules rules,
            IpoDice dice,
            IBusinessSource source
        )
        {
            var events = new List<IpoEvent>();
            if (today >= registry.NextDrawDay)
            {
                registry.NextDrawDay = today + IpoCalendar.PeriodDays(rules);
                DrawIpo(registry, candidates, kinds, tilesOwned, today, rules, dice);
                DrawBuyout(registry, today, rules, dice);
            }

            if (registry.IpoScheduled && today >= registry.IpoDay)
            {
                registry.IpoScheduled = false;
                events.Add(Announce(registry, candidates, today, rules, source));
            }

            foreach (LocalCompany c in registry.Companies)
            {
                if (c.Status == CompanyStatus.Offering && today >= c.FirstTradingDay)
                {
                    double pop = IpoPricing.FirstDayPop(rules, dice);
                    c.PriceCents = IpoPricing.FirstDayPriceCents(c.OfferPriceCents, pop);
                    c.Status = CompanyStatus.Trading;
                    events.Add(
                        new IpoEvent
                        {
                            Kind = IpoEventKind.FirstTradingDay,
                            Company = c,
                            Fraction = pop,
                        }
                    );
                }
            }

            if (registry.BuyoutScheduled && today >= registry.BuyoutDay)
            {
                registry.BuyoutScheduled = false;
                LocalCompany bought = registry.Find(registry.BuyoutCompanyId);
                if (bought != null && bought.Status == CompanyStatus.Trading)
                {
                    double premium = IpoBuyout.Premium(rules, dice);
                    registry.Companies.Remove(bought);
                    events.Add(
                        new IpoEvent
                        {
                            Kind = IpoEventKind.BoughtOut,
                            Company = bought,
                            Fraction = premium,
                            PricePerShareCents = IpoBuyout.PricePerShareCents(
                                bought.PriceCents,
                                premium
                            ),
                        }
                    );
                }
            }

            return events;
        }

        private static void DrawIpo(
            CompanyRegistry registry,
            CandidateBook candidates,
            IList<KindFacts> kinds,
            int tilesOwned,
            int today,
            IpoRules rules,
            IpoDice dice
        )
        {
            // The kinds as the board sees them: what is listed comes from the registry, and a
            // business that is already a company's home is not a candidate again.
            var board = new List<KindFacts>();
            var pools = new List<List<BuildingKey>>();
            foreach (KindFacts k in kinds)
            {
                List<BuildingKey> pool = candidates.Candidates(rules, k.Kind);
                pool.RemoveAll(registry.IsHome);
                KindFacts seen = k;
                seen.Listed = registry.ListedOf(k.Kind);
                seen.Candidates = pool.Count;
                board.Add(seen);
                pools.Add(pool);
            }

            IpoPick pick = IpoBoard.DrawPeriod(
                board,
                registry.Companies.Count,
                tilesOwned,
                registry.IpoScheduled || registry.OfferingOpen,
                registry.IpoDays,
                today,
                rules,
                dice
            );
            if (!pick.Happens)
            {
                return;
            }

            registry.IpoScheduled = true;
            registry.IpoBusiness = pools[pick.KindIndex][pick.CandidateIndex];
            registry.IpoKind = board[pick.KindIndex].Kind;
            registry.IpoDay = today + pick.DayOffset;
        }

        private static void DrawBuyout(
            CompanyRegistry registry,
            int today,
            IpoRules rules,
            IpoDice dice
        )
        {
            if (registry.BuyoutScheduled)
            {
                return;
            }

            var eligible = new List<LocalCompany>();
            foreach (LocalCompany c in registry.Companies)
            {
                if (
                    c.Status == CompanyStatus.Trading
                    && IpoBuyout.Eligible(today - c.FirstTradingDay, rules)
                )
                {
                    eligible.Add(c);
                }
            }

            PeriodDraw draw = IpoBuyout.DrawPeriod(eligible.Count, rules, dice);
            if (draw.Happens)
            {
                registry.BuyoutScheduled = true;
                registry.BuyoutCompanyId = eligible[draw.Index].Id;
                registry.BuyoutDay = today + draw.DayOffset;
            }
        }

        private static IpoEvent Announce(
            CompanyRegistry registry,
            CandidateBook candidates,
            int today,
            IpoRules rules,
            IBusinessSource source
        )
        {
            BuildingKey business = registry.IpoBusiness;
            bool stillQualifies =
                candidates.HealthyDaysOf(business) >= rules.HealthyDays
                && candidates.KindOf(business) == registry.IpoKind
                && !registry.IsHome(business);
            if (!stillQualifies)
            {
                return new IpoEvent { Kind = IpoEventKind.Cancelled };
            }

            string name = source.NameOf(business) ?? string.Empty;
            long offer = IpoPricing.OfferingPriceCents(source.AnnualEarningsCents(business), rules);
            var company = new LocalCompany
            {
                Id = registry.NextId++,
                Name = name,
                Symbol = source.SymbolFor(name),
                Kind = registry.IpoKind,
                Home = business,
                AnnouncedDay = today,
                FirstTradingDay = today + OfferDays,
                OfferPriceCents = offer,
                PriceCents = offer,
                Status = CompanyStatus.Offering,
            };
            registry.Companies.Add(company);
            registry.IpoDays.Add(today);
            return new IpoEvent { Kind = IpoEventKind.Announced, Company = company };
        }
    }
}

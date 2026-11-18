using System;
using System.Collections.Generic;

namespace FinanceOverhaul.Core
{
    /// <summary>
    /// The tuning values of Local IPOs (Options, while the feel is tuned). Defaults are the
    /// design's: a kind of business unlocks at 200 squares, earns listing slots as it grows, an
    /// open slot fills in about a year and a half, and at most two companies list in a year.
    /// </summary>
    public sealed class IpoRules
    {
        public const int DaysPerYear = 365;

        /// <summary>Squares of a kind of business the city needs before that kind can list (its first slot).</summary>
        public int GateSquares = 200;

        /// <summary>
        /// Top-level buildings of a kind at which its second slot comes; every doubling after
        /// that adds another (10 → 20, 40, 80…), so slots grow slowly in a city of any size.
        /// </summary>
        public int SlotDoublingBase = 10;

        /// <summary>Days an open slot waits for its IPO on average (548 = a year and a half).</summary>
        public int SlotFillDays = 548;

        /// <summary>Most IPOs in any 365 days, so a large city is not flooded.</summary>
        public int MaxIposPerYear = 2;

        /// <summary>Local listings the board holds for each map tile the city owns (up to <see cref="ListingCap"/>).</summary>
        public int ListingsPerTile = 2;

        /// <summary>Days in a row a business must stay healthy to become a candidate.</summary>
        public int HealthyDays = 365;

        /// <summary>
        /// A business must be among the larger employers of its kind in this city: in the top
        /// this-many percent by jobs (33 = the top third). Relative, so it holds in any city and
        /// for kinds whose buildings are all small (farming, oil).
        /// </summary>
        public int TopEmployersPercent = 33;

        /// <summary>
        /// Land value at a commercial or office building must be at least this: 41, the game's
        /// own threshold for a level-3 building. Not asked of industry, which lowers land value.
        /// </summary>
        public int LandValueFloor = 41;

        /// <summary>A business counts as fully staffed with at most this percent of its jobs unfilled.</summary>
        public int MaxVacancyPercent = 10;

        /// <summary>Most local companies listed at once.</summary>
        public int ListingCap = 12;

        /// <summary>
        /// How often the dice are rolled, in days: once a period it is decided whether an IPO
        /// (and, separately, a buy-out) happens in the coming period. 30 is once a month.
        /// </summary>
        public int DrawEveryDays = 30;

        /// <summary>Company value = annual earnings × this multiple.</summary>
        public int PriceToEarnings = 20;

        /// <summary>
        /// The most of a local company the city may hold, in percent: a passive public investor,
        /// like a sovereign fund's 10% limit. The game's full holding is this stake.
        /// </summary>
        public int CityStakePercent = 10;

        /// <summary>The first-day jump on average, in percent.</summary>
        public int PopAveragePercent = 15;

        /// <summary>How varied the first-day jump is (its standard deviation), in percent.</summary>
        public int PopSpreadPercent = 25;

        /// <summary>A listed company's chance of being bought out in a year, in percent.</summary>
        public int BuyoutPercentPerYear = 4;

        /// <summary>No buy-out until a company has been listed this many days.</summary>
        public int BuyoutMinDaysListed = 365;

        /// <summary>What a buyer pays above the price on average, in percent.</summary>
        public int BuyoutPremiumAveragePercent = 40;

        /// <summary>How varied that premium is (its standard deviation), in percent.</summary>
        public int BuyoutPremiumSpreadPercent = 15;
    }

    /// <summary>The kinds of business that list separately: the three zones and each specialisation.</summary>
    public enum BusinessKind
    {
        Commercial,
        Office,
        Industry,
        ItCluster,
        Leisure,
        Tourism,
        Organic,
        Farming,
        Forestry,
        Oil,
        Ore,
    }

    /// <summary>What the mod reads from the game about one zoned business on one day.</summary>
    public struct BusinessFacts
    {
        public BusinessKind Kind;

        /// <summary>At the top level of its kind (level 3, or the only level of a specialisation).</summary>
        public bool TopLevel;

        /// <summary>Any problem icon on the building.</summary>
        public bool HasProblems;

        /// <summary>Every job filled.</summary>
        public bool FullyStaffed;

        public int Jobs;

        /// <summary>The jobs a business of this kind needs today to count among its larger employers.</summary>
        public int JobsFloor;

        public int LandValue;
    }

    /// <summary>A building, told apart from a later one that reuses its id by its build index.</summary>
    public struct BuildingKey : IEquatable<BuildingKey>
    {
        public readonly ushort Id;
        public readonly uint BuildIndex;

        public BuildingKey(ushort id, uint buildIndex)
        {
            Id = id;
            BuildIndex = buildIndex;
        }

        public bool Equals(BuildingKey other)
        {
            return Id == other.Id && BuildIndex == other.BuildIndex;
        }

        public override bool Equals(object obj)
        {
            return obj is BuildingKey && Equals((BuildingKey)obj);
        }

        public override int GetHashCode()
        {
            return unchecked((int)(BuildIndex * 397) ^ Id);
        }
    }

    /// <summary>
    /// Which businesses could go public: the game does not count how long a business has been
    /// healthy, so this does. A business seen healthy today has one more healthy day; one seen
    /// unhealthy, or not seen at all (demolished, replaced), starts again from nothing. It is a
    /// candidate once it has <see cref="IpoRules.HealthyDays"/> healthy days in a row.
    /// </summary>
    public sealed class CandidateBook
    {
        private readonly Dictionary<BuildingKey, int> healthyDays =
            new Dictionary<BuildingKey, int>();
        private readonly HashSet<BuildingKey> seenToday = new HashSet<BuildingKey>();
        private readonly Dictionary<BuildingKey, BusinessKind> kinds =
            new Dictionary<BuildingKey, BusinessKind>();

        /// <summary>
        /// Healthy enough to count today: top level, staffed, no problems, among the larger
        /// employers of its kind, and (shops and offices) on
        /// land that still supports its level.
        /// </summary>
        public static bool Healthy(BusinessFacts facts, IpoRules rules)
        {
            return facts.TopLevel
                && !facts.HasProblems
                && facts.FullyStaffed
                && facts.Jobs >= facts.JobsFloor
                && (
                    !BusinessKinds.LandValueMatters(facts.Kind)
                    || facts.LandValue >= rules.LandValueFloor
                );
        }

        /// <summary>One business as the game shows it today. Call once per business, then <see cref="EndDay"/>.</summary>
        public void Observe(BuildingKey building, BusinessFacts facts, IpoRules rules)
        {
            if (!Healthy(facts, rules))
            {
                healthyDays.Remove(building);
                kinds.Remove(building);
                return;
            }

            kinds[building] = facts.Kind;

            int days;
            healthyDays.TryGetValue(building, out days);
            healthyDays[building] = days == int.MaxValue ? days : days + 1;
            seenToday.Add(building);
        }

        /// <summary>Closes the day: a business that was not observed healthy today is forgotten.</summary>
        public void EndDay()
        {
            var gone = new List<BuildingKey>();
            foreach (BuildingKey building in healthyDays.Keys)
            {
                if (!seenToday.Contains(building))
                {
                    gone.Add(building);
                }
            }

            foreach (BuildingKey building in gone)
            {
                healthyDays.Remove(building);
                kinds.Remove(building);
            }

            seenToday.Clear();
        }

        /// <summary>Healthy days in a row so far (0 if unknown).</summary>
        public int HealthyDaysOf(BuildingKey building)
        {
            int days;
            return healthyDays.TryGetValue(building, out days) ? days : 0;
        }

        /// <summary>Sets a count read from the save.</summary>
        public void Restore(
            BuildingKey building,
            int days,
            BusinessKind kind = BusinessKind.Commercial
        )
        {
            if (days > 0)
            {
                healthyDays[building] = days;
                kinds[building] = kind;
            }
        }

        /// <summary>The kind a counted business was last seen as.</summary>
        public BusinessKind KindOf(BuildingKey building)
        {
            BusinessKind kind;
            return kinds.TryGetValue(building, out kind) ? kind : BusinessKind.Commercial;
        }

        /// <summary>The candidates of one kind, in the same fixed order.</summary>
        public List<BuildingKey> Candidates(IpoRules rules, BusinessKind kind)
        {
            List<BuildingKey> all = Candidates(rules);
            all.RemoveAll(b => KindOf(b) != kind);
            return all;
        }

        /// <summary>Every business being counted, with its healthy days (for the save).</summary>
        public IEnumerable<KeyValuePair<BuildingKey, int>> Counted
        {
            get { return healthyDays; }
        }

        /// <summary>
        /// The candidates, in a fixed order (by building id, then build index) so the same day
        /// with the same dice picks the same business.
        /// </summary>
        public List<BuildingKey> Candidates(IpoRules rules)
        {
            var list = new List<BuildingKey>();
            foreach (KeyValuePair<BuildingKey, int> kv in healthyDays)
            {
                if (kv.Value >= rules.HealthyDays)
                {
                    list.Add(kv.Key);
                }
            }

            list.Sort(
                (a, b) => a.Id != b.Id ? a.Id.CompareTo(b.Id) : a.BuildIndex.CompareTo(b.BuildIndex)
            );
            return list;
        }
    }

    /// <summary>
    /// A small repeatable random source (SplitMix64). Its state is one number saved with the
    /// city, so loading a save and playing the same days draws the same numbers.
    /// </summary>
    public sealed class IpoDice
    {
        public ulong State;

        public IpoDice(ulong seed)
        {
            State = seed;
        }

        /// <summary>
        /// A log-normal number with this mean and standard deviation (most near the mean, a few
        /// far above it, never below zero). Takes two numbers from the dice.
        /// </summary>
        public double LogNormal(double mean, double deviation)
        {
            double u1 = 1d - Next(); // (0, 1]: the logarithm below is finite
            double u2 = Next();
            if (mean <= 0d)
            {
                return 0d;
            }

            double sigma2 = Math.Log(1d + deviation * deviation / (mean * mean));
            double mu = Math.Log(mean) - sigma2 / 2d;
            double normal = Math.Sqrt(-2d * Math.Log(u1)) * Math.Cos(2d * Math.PI * u2);
            return Math.Exp(mu + Math.Sqrt(sigma2) * normal);
        }

        /// <summary>The next number in [0, 1).</summary>
        public double Next()
        {
            unchecked
            {
                State += 0x9E3779B97F4A7C15UL;
                ulong z = State;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                z ^= z >> 31;
                return (z >> 11) * (1.0 / 9007199254740992.0); // 53 bits
            }
        }
    }

    /// <summary>What a draw decided: nothing, or who and on which day of the coming period.</summary>
    public struct PeriodDraw
    {
        /// <summary>Index of the one picked, or −1 for nothing this period.</summary>
        public int Index;

        /// <summary>Days from the draw to the event: 0 (the day of the draw) to the period's last day.</summary>
        public int DayOffset;

        public bool Happens
        {
            get { return Index >= 0; }
        }

        public static readonly PeriodDraw Nothing = new PeriodDraw { Index = -1 };
    }

    /// <summary>
    /// The dice cycle shared by IPOs and buy-outs: once a period (a month by default,
    /// <see cref="IpoRules.DrawEveryDays"/>) it is decided whether one happens in the coming
    /// period, which one, and on which day. What may list is <see cref="IpoBoard"/>'s rule.
    /// </summary>
    public static class IpoCalendar
    {
        /// <summary>Days between draws: at least 1.</summary>
        public static int PeriodDays(IpoRules rules)
        {
            return rules == null ? 1 : Math.Max(1, rules.DrawEveryDays);
        }

        /// <summary>The shared roll: happens with this chance, one of <paramref name="count"/>, on a day of the period.</summary>
        internal static PeriodDraw Roll(int count, double chance, int periodDays, IpoDice dice)
        {
            double roll = dice.Next();
            if (chance <= 0d || roll >= chance)
            {
                return PeriodDraw.Nothing;
            }

            // roll is uniform in [0, chance): spread it evenly over the choices.
            return new PeriodDraw
            {
                Index = Math.Min(count - 1, (int)(roll / chance * count)),
                DayOffset = Math.Min(periodDays - 1, (int)(dice.Next() * periodDays)),
            };
        }
    }
}

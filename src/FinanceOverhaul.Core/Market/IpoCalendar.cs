using System;
using System.Collections.Generic;

namespace FinanceOverhaul.Core
{
    /// <summary>
    /// The tuning values of Local IPOs (Options, while the feel is tuned). Defaults are the
    /// design's: about one IPO every 10 game years in a city with 20 candidates.
    /// </summary>
    public sealed class IpoRules
    {
        public const int DaysPerYear = 365;

        /// <summary>Expected years between IPOs when the city has <see cref="ReferencePool"/> candidates.</summary>
        public int TargetYears = 10;

        /// <summary>The number of candidates at which <see cref="TargetYears"/> applies.</summary>
        public int ReferencePool = 20;

        /// <summary>Days in a row a business must stay healthy to become a candidate.</summary>
        public int HealthyDays = 365;

        /// <summary>Fewer jobs than this never lists (keeps corner shops out).</summary>
        public int MinJobs = 20;

        /// <summary>Land value at the building must be at least this.</summary>
        public int LandValueFloor = 50;

        /// <summary>Most local companies listed at once.</summary>
        public int ListingCap = 12;

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

    /// <summary>What the mod reads from the game about one zoned business on one day.</summary>
    public struct BusinessFacts
    {
        /// <summary>At the top level of its kind (level 3, or the only level of a specialisation).</summary>
        public bool TopLevel;

        /// <summary>Any problem icon on the building.</summary>
        public bool HasProblems;

        /// <summary>Every job filled.</summary>
        public bool FullyStaffed;

        public int Jobs;
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

        /// <summary>Healthy enough to count today: top level, staffed, no problems, big and valuable enough.</summary>
        public static bool Healthy(BusinessFacts facts, IpoRules rules)
        {
            return facts.TopLevel
                && !facts.HasProblems
                && facts.FullyStaffed
                && facts.Jobs >= rules.MinJobs
                && facts.LandValue >= rules.LandValueFloor;
        }

        /// <summary>One business as the game shows it today. Call once per business, then <see cref="EndDay"/>.</summary>
        public void Observe(BuildingKey building, BusinessFacts facts, IpoRules rules)
        {
            if (!Healthy(facts, rules))
            {
                healthyDays.Remove(building);
                return;
            }

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
        public void Restore(BuildingKey building, int days)
        {
            if (days > 0)
            {
                healthyDays[building] = days;
            }
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

    /// <summary>
    /// When a business goes public: rare by design. Every candidate has the same small chance
    /// each day, set so that a city with the reference number of candidates sees one IPO every
    /// target years on average. Fewer candidates make it rarer, more make it more common, and
    /// a city with none never gets one. At most one business is picked a day, and none while an
    /// IPO is in progress or the listing cap is reached.
    /// </summary>
    public static class IpoCalendar
    {
        /// <summary>
        /// One candidate's chance on one day: p with 1 − (1 − p)^pool = 1 ÷ target days, so the
        /// chance that any of a reference pool lists on a day is one in the target days.
        /// </summary>
        public static double DailyChance(IpoRules rules)
        {
            if (rules == null || rules.TargetYears <= 0 || rules.ReferencePool <= 0)
            {
                return 0d;
            }

            double anyInPool = 1d / ((double)rules.TargetYears * IpoRules.DaysPerYear);
            return 1d - Math.Pow(1d - anyInPool, 1d / rules.ReferencePool);
        }

        /// <summary>The chance that some candidate lists today, with this many candidates.</summary>
        public static double ChanceToday(int candidates, IpoRules rules)
        {
            return candidates <= 0 ? 0d : 1d - Math.Pow(1d - DailyChance(rules), candidates);
        }

        /// <summary>Average days between IPOs with this many candidates (infinite with none).</summary>
        public static double ExpectedDaysBetween(int candidates, IpoRules rules)
        {
            double chance = ChanceToday(candidates, rules);
            return chance <= 0d ? double.PositiveInfinity : 1d / chance;
        }

        /// <summary>
        /// Today's draw: the index of the candidate that goes public, or −1 for none. One number
        /// is taken from the dice whenever a draw is possible, whatever the outcome, so the
        /// sequence does not depend on how many candidates there were. Every candidate is
        /// equally likely to be the one.
        /// </summary>
        public static int Draw(
            int candidates,
            int listed,
            bool ipoInProgress,
            IpoRules rules,
            IpoDice dice
        )
        {
            if (candidates <= 0 || ipoInProgress || listed >= rules.ListingCap)
            {
                return -1;
            }

            double chance = ChanceToday(candidates, rules);
            double roll = dice.Next();
            if (chance <= 0d || roll >= chance)
            {
                return -1;
            }

            // roll is uniform in [0, chance): spread it evenly over the candidates.
            return Math.Min(candidates - 1, (int)(roll / chance * candidates));
        }
    }
}

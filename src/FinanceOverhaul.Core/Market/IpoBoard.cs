using System; // Math
using System.Collections.Generic; // IList, List

namespace FinanceOverhaul.Core
{
    /// <summary>What the city has of one kind of business today.</summary>
    public struct KindFacts
    {
        public BusinessKind Kind;

        /// <summary>Squares its buildings cover.</summary>
        public int Squares;

        /// <summary>Its buildings at the top level of their kind.</summary>
        public int TopLevelBuildings;

        /// <summary>Businesses of this kind that could go public (healthy long enough).</summary>
        public int Candidates;

        /// <summary>Local companies of this kind listed now.</summary>
        public int Listed;

        /// <summary>
        /// Slots beside the zoned ones: one per industry area of this kind at the level areas
        /// need, or (for <see cref="BusinessKind.UniqueFactory"/>) one per unique factory.
        /// </summary>
        public int ExtraSlots;
    }

    /// <summary>Where a kind of business stands on the board, for its card in Economy → Investments.</summary>
    public enum KindState
    {
        /// <summary>Not enough of it yet: the bar counts squares towards the gate.</summary>
        Locked,

        /// <summary>A slot is open and a candidate exists: a listing can happen.</summary>
        Open,

        /// <summary>A slot is open but no business has been healthy long enough yet.</summary>
        Waiting,

        /// <summary>Every slot is used: the bar counts buildings towards the next slot.</summary>
        Full,

        /// <summary>A slot is open, but the city has had its IPOs for the year or the board is full.</summary>
        Paused,
    }

    /// <summary>One kind's card: its state and the bar under it.</summary>
    public struct KindStatus
    {
        public BusinessKind Kind;
        public KindState State;
        public int Slots;
        public int Listed;
        public int OpenSlots;
        public int Candidates;

        /// <summary>The bar: squares towards the gate while locked, else buildings towards the next slot.</summary>
        public int BarHave;
        public int BarNeed;
    }

    /// <summary>What a period's draw decided for IPOs.</summary>
    public struct IpoPick
    {
        public bool Happens;

        /// <summary>Index into the kinds passed to the draw.</summary>
        public int KindIndex;

        /// <summary>Which of that kind's candidates (in the candidate book's fixed order).</summary>
        public int CandidateIndex;

        /// <summary>Days from the draw to the announcement.</summary>
        public int DayOffset;

        public static readonly IpoPick Nothing = new IpoPick();
    }

    /// <summary>
    /// Which local companies may list, and when: the player builds towards it and is never
    /// flooded.
    /// 1. Gate: a kind of business can list once the city has enough squares of it; passing the
    ///    gate gives the kind its first listing slot.
    /// 2. Slots: each doubling of the kind's top-level buildings adds a slot, so slots grow
    ///    slowly in a city of any size. A listing uses one; a buy-out or a delisting gives it
    ///    back.
    /// 3. Board: the whole city holds so many local listings per map tile it owns, up to a cap.
    /// 4. Pacing: at most so many IPOs in any 365 days, and one at a time.
    /// 5. Chance: once a period, every open slot that has a candidate has the same chance of
    ///    being filled, set so a slot waits about a year and a half; the slot picked decides the
    ///    kind, and each of that kind's candidates is equally likely.
    /// </summary>
    public static class IpoBoard
    {
        /// <summary>
        /// Top-level buildings a kind needs for one more slot than it has: the base for the
        /// second, double that for the third, and so on.
        /// </summary>
        public static int NextSlotAt(int slots, IpoRules rules)
        {
            long need = Math.Max(1, rules.SlotDoublingBase);
            for (int s = 1; s < Math.Max(1, slots) && need < int.MaxValue; s++)
            {
                need *= 2;
            }

            return (int)Math.Min(int.MaxValue, need * 2);
        }

        /// <summary>The kind can list: enough squares of zoned buildings, or an area or factory that gives a slot.</summary>
        public static bool GateOpen(KindFacts facts, IpoRules rules)
        {
            return ZonedGateOpen(facts, rules) || facts.ExtraSlots > 0;
        }

        /// <summary>Enough squares of the kind's zoned buildings for their first slot.</summary>
        public static bool ZonedGateOpen(KindFacts facts, IpoRules rules)
        {
            return facts.Kind != BusinessKind.UniqueFactory && facts.Squares >= rules.GateSquares;
        }

        /// <summary>
        /// Listing slots the kind has earned: its zoned slots and its
        /// <see cref="KindFacts.ExtraSlots"/> (areas, factories) together.
        /// </summary>
        public static int Slots(KindFacts facts, IpoRules rules)
        {
            return ZonedSlots(facts, rules) + Math.Max(0, facts.ExtraSlots);
        }

        /// <summary>
        /// Slots its zoned buildings earn: none before the gate; the gate gives one; then one
        /// more each time its top-level buildings double (20, 40, 80… with the default base).
        /// Slow growth at any size: 1,280 buildings earn 8.
        /// </summary>
        public static int ZonedSlots(KindFacts facts, IpoRules rules)
        {
            if (!ZonedGateOpen(facts, rules))
            {
                return 0;
            }

            int slots = 1;
            while (slots < 31 && facts.TopLevelBuildings >= NextSlotAt(slots, rules))
            {
                slots++;
            }

            return slots;
        }

        /// <summary>Slots not used by a listing.</summary>
        public static int OpenSlots(KindFacts facts, IpoRules rules)
        {
            return Math.Max(0, Slots(facts, rules) - Math.Max(0, facts.Listed));
        }

        /// <summary>Local listings the whole board holds: so many per map tile owned, up to the cap.</summary>
        public static int BoardCap(int tilesOwned, IpoRules rules)
        {
            return Math.Max(
                0,
                Math.Min(rules.ListingCap, rules.ListingsPerTile * Math.Max(0, tilesOwned))
            );
        }

        /// <summary>IPOs in the 365 days up to and including today.</summary>
        public static int IposInLastYear(IList<int> ipoDays, int today)
        {
            int count = 0;
            if (ipoDays != null)
            {
                foreach (int day in ipoDays)
                {
                    if (day <= today && day > today - IpoRules.DaysPerYear)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        /// <summary>The city has not had its IPOs for the year.</summary>
        /// <summary>
        /// The city's Stock Exchange is grown enough for IPOs: at <see cref="IpoRules.ExchangeLevel"/>
        /// or higher (0 = no Stock Exchange).
        /// </summary>
        public static bool ExchangeAllows(int exchangeLevel, IpoRules rules)
        {
            return exchangeLevel >= rules.ExchangeLevel;
        }

        public static bool PacingAllows(IList<int> ipoDays, int today, IpoRules rules)
        {
            return IposInLastYear(ipoDays, today) < rules.MaxIposPerYear;
        }

        /// <summary>
        /// The first day another IPO is allowed: today if pacing allows it now, else the day the
        /// oldest of the year's IPOs is a year old. <see cref="int.MaxValue"/> if IPOs are
        /// switched off (a maximum of none).
        /// </summary>
        public static int NextAllowedDay(IList<int> ipoDays, int today, IpoRules rules)
        {
            if (rules.MaxIposPerYear <= 0)
            {
                return int.MaxValue;
            }

            if (PacingAllows(ipoDays, today, rules))
            {
                return today;
            }

            var recent = new List<int>();
            foreach (int day in ipoDays)
            {
                if (day <= today && day > today - IpoRules.DaysPerYear)
                {
                    recent.Add(day);
                }
            }

            recent.Sort();
            // Once enough of the oldest have aged out, one fewer than the maximum remain.
            int mustAgeOut = recent.Count - rules.MaxIposPerYear + 1;
            return recent[mustAgeOut - 1] + IpoRules.DaysPerYear;
        }

        /// <summary>One kind's card.</summary>
        /// <param name="boardHasRoom">The board is below its cap and pacing allows an IPO.</param>
        public static KindStatus Status(KindFacts facts, bool boardHasRoom, IpoRules rules)
        {
            var status = new KindStatus
            {
                Kind = facts.Kind,
                Slots = Slots(facts, rules),
                Listed = Math.Max(0, facts.Listed),
                OpenSlots = OpenSlots(facts, rules),
                Candidates = Math.Max(0, facts.Candidates),
            };
            if (!GateOpen(facts, rules))
            {
                status.State = KindState.Locked;
                status.BarHave = Math.Max(0, facts.Squares);
                status.BarNeed = rules.GateSquares;
                return status;
            }

            if (ZonedGateOpen(facts, rules))
            {
                status.BarHave = Math.Max(0, facts.TopLevelBuildings);
                status.BarNeed = NextSlotAt(ZonedSlots(facts, rules), rules);
            }
            else if (facts.Kind != BusinessKind.UniqueFactory)
            {
                // Open through its areas alone: zoned buildings can still earn a slot.
                status.BarHave = Math.Max(0, facts.Squares);
                status.BarNeed = rules.GateSquares;
            }
            // A unique factory's slots come one per factory: no bar (BarNeed stays 0).

            status.State =
                status.OpenSlots == 0 ? KindState.Full
                : status.Candidates == 0 ? KindState.Waiting
                : boardHasRoom ? KindState.Open
                : KindState.Paused;
            return status;
        }

        /// <summary>One open slot's chance of being filled in one period.</summary>
        public static double SlotChance(IpoRules rules)
        {
            return rules.SlotFillDays <= 0
                ? 0d
                : Math.Min(1d, (double)IpoCalendar.PeriodDays(rules) / rules.SlotFillDays);
        }

        /// <summary>
        /// Slots that could be filled this period: per kind its open slots (none without a
        /// candidate), in total no more than the board has room for.
        /// </summary>
        public static int FillableSlots(
            IList<KindFacts> kinds,
            int totalListed,
            int tilesOwned,
            IpoRules rules
        )
        {
            int open = 0;
            foreach (KindFacts k in kinds)
            {
                if (k.Candidates > 0)
                {
                    open += OpenSlots(k, rules);
                }
            }

            return Math.Max(0, Math.Min(open, BoardCap(tilesOwned, rules) - totalListed));
        }

        /// <summary>The chance that some company lists this period.</summary>
        public static double ChanceThisPeriod(
            IList<KindFacts> kinds,
            int totalListed,
            int tilesOwned,
            IpoRules rules
        )
        {
            int slots = FillableSlots(kinds, totalListed, tilesOwned, rules);
            return slots <= 0 ? 0d : 1d - Math.Pow(1d - SlotChance(rules), slots);
        }

        /// <summary>
        /// This period's draw. Nothing, and no dice taken, while an IPO is in progress, when
        /// pacing forbids one, or when no slot can be filled. Otherwise one number decides
        /// whether an IPO happens and which open slot it fills (so a kind with more open slots
        /// is likelier), a second sets its day, a third picks the candidate.
        /// </summary>
        public static IpoPick DrawPeriod(
            IList<KindFacts> kinds,
            int totalListed,
            int tilesOwned,
            bool ipoInProgress,
            IList<int> ipoDays,
            int today,
            IpoRules rules,
            IpoDice dice
        )
        {
            if (ipoInProgress || !PacingAllows(ipoDays, today, rules))
            {
                return IpoPick.Nothing;
            }

            int slots = FillableSlots(kinds, totalListed, tilesOwned, rules);
            if (slots <= 0)
            {
                return IpoPick.Nothing;
            }

            double chance = 1d - Math.Pow(1d - SlotChance(rules), slots);
            PeriodDraw draw = IpoCalendar.Roll(slots, chance, IpoCalendar.PeriodDays(rules), dice);
            if (!draw.Happens)
            {
                return IpoPick.Nothing;
            }

            // Walk the kinds' open slots to the one picked. The board's room may be less than
            // the open slots; the walk still covers them all in proportion.
            int total = 0;
            foreach (KindFacts k in kinds)
            {
                total += k.Candidates > 0 ? OpenSlots(k, rules) : 0;
            }

            int target = (int)((long)draw.Index * total / slots);
            for (int i = 0; i < kinds.Count; i++)
            {
                int open = kinds[i].Candidates > 0 ? OpenSlots(kinds[i], rules) : 0;
                if (target < open)
                {
                    return new IpoPick
                    {
                        Happens = true,
                        KindIndex = i,
                        CandidateIndex = Math.Min(
                            kinds[i].Candidates - 1,
                            (int)(dice.Next() * kinds[i].Candidates)
                        ),
                        DayOffset = draw.DayOffset,
                    };
                }

                target -= open;
            }

            return IpoPick.Nothing; // not reached: target is below the total
        }
    }
}

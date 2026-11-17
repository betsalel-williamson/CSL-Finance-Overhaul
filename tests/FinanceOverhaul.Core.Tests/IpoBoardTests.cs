using System.Collections.Generic;
using FinanceOverhaul.Core;
using Xunit;

namespace FinanceOverhaul.Core.Tests
{
    public class IpoBoardTests
    {
        private static KindFacts Kind(
            BusinessKind kind,
            int squares,
            int topLevel,
            int candidates,
            int listed = 0
        )
        {
            return new KindFacts
            {
                Kind = kind,
                Squares = squares,
                TopLevelBuildings = topLevel,
                Candidates = candidates,
                Listed = listed,
            };
        }

        [Fact]
        public void TheGateOpensAt200SquaresAndGivesTheFirstSlot()
        {
            var rules = new IpoRules();
            Assert.False(IpoBoard.GateOpen(Kind(BusinessKind.Office, 199, 80, 5), rules));
            Assert.Equal(0, IpoBoard.Slots(Kind(BusinessKind.Office, 199, 80, 5), rules));
            Assert.True(IpoBoard.GateOpen(Kind(BusinessKind.Office, 200, 0, 0), rules));
            Assert.Equal(1, IpoBoard.Slots(Kind(BusinessKind.Office, 200, 0, 0), rules));
        }

        [Theory]
        [InlineData(BusinessKind.Office, 24, 1)]
        [InlineData(BusinessKind.Office, 25, 2)]
        [InlineData(BusinessKind.Office, 74, 3)]
        [InlineData(BusinessKind.Commercial, 39, 1)]
        [InlineData(BusinessKind.Commercial, 80, 3)]
        [InlineData(BusinessKind.Industry, 30, 2)]
        [InlineData(BusinessKind.ItCluster, 15, 2)]
        [InlineData(BusinessKind.Oil, 44, 3)]
        public void MoreTopLevelBuildingsEarnMoreSlots(BusinessKind kind, int topLevel, int slots)
        {
            var rules = new IpoRules();
            Assert.Equal(slots, IpoBoard.Slots(Kind(kind, 500, topLevel, 3), rules));
        }

        [Fact]
        public void AListingUsesASlotAndLeavingGivesItBack()
        {
            var rules = new IpoRules();
            Assert.Equal(2, IpoBoard.OpenSlots(Kind(BusinessKind.Office, 500, 30, 4, 0), rules));
            Assert.Equal(1, IpoBoard.OpenSlots(Kind(BusinessKind.Office, 500, 30, 4, 1), rules));
            Assert.Equal(0, IpoBoard.OpenSlots(Kind(BusinessKind.Office, 500, 30, 4, 2), rules));
            // The sector shrank below its listings: no open slot, nothing negative.
            Assert.Equal(0, IpoBoard.OpenSlots(Kind(BusinessKind.Office, 500, 0, 4, 2), rules));
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(1, 2)]
        [InlineData(3, 6)]
        [InlineData(6, 12)]
        [InlineData(25, 12)] // never past the cap
        public void TheBoardHoldsTwoListingsPerTileUpToTwelve(int tiles, int cap)
        {
            Assert.Equal(cap, IpoBoard.BoardCap(tiles, new IpoRules()));
        }

        [Fact]
        public void AtMostTwoIposInAnyYear()
        {
            var rules = new IpoRules();
            var days = new List<int>();
            Assert.True(IpoBoard.PacingAllows(days, 1000, rules));
            Assert.Equal(1000, IpoBoard.NextAllowedDay(days, 1000, rules));
            days.Add(900);
            Assert.True(IpoBoard.PacingAllows(days, 1000, rules));
            days.Add(950);
            Assert.False(IpoBoard.PacingAllows(days, 1000, rules));
            // Allowed again the day the older one is a year old.
            Assert.Equal(900 + 365, IpoBoard.NextAllowedDay(days, 1000, rules));
            Assert.False(IpoBoard.PacingAllows(days, 900 + 364, rules));
            Assert.True(IpoBoard.PacingAllows(days, 900 + 365, rules));
            // An IPO still to come this period does not count yet.
            Assert.Equal(1, IpoBoard.IposInLastYear(new List<int> { 990, 1010 }, 1000));

            var off = new IpoRules { MaxIposPerYear = 0 };
            Assert.False(IpoBoard.PacingAllows(new List<int>(), 1000, off));
            Assert.Equal(int.MaxValue, IpoBoard.NextAllowedDay(new List<int>(), 1000, off));
        }

        [Fact]
        public void TheCardStates()
        {
            var rules = new IpoRules();
            KindStatus locked = IpoBoard.Status(Kind(BusinessKind.Office, 132, 9, 2), true, rules);
            Assert.Equal(KindState.Locked, locked.State);
            Assert.Equal(132, locked.BarHave);
            Assert.Equal(200, locked.BarNeed);

            KindStatus open = IpoBoard.Status(
                Kind(BusinessKind.ItCluster, 400, 6, 14),
                true,
                rules
            );
            Assert.Equal(KindState.Open, open.State);
            Assert.Equal(1, open.OpenSlots);
            Assert.Equal(14, open.Candidates);
            Assert.Equal(6, open.BarHave); // towards the next slot
            Assert.Equal(15, open.BarNeed);

            KindStatus waiting = IpoBoard.Status(
                Kind(BusinessKind.Leisure, 400, 6, 0),
                true,
                rules
            );
            Assert.Equal(KindState.Waiting, waiting.State);

            KindStatus full = IpoBoard.Status(
                Kind(BusinessKind.Commercial, 900, 46, 8, 2),
                true,
                rules
            );
            Assert.Equal(KindState.Full, full.State);
            Assert.Equal(2, full.Slots);
            Assert.Equal(6, full.BarHave);
            Assert.Equal(40, full.BarNeed);

            KindStatus paused = IpoBoard.Status(
                Kind(BusinessKind.Industry, 900, 9, 6),
                false,
                rules
            );
            Assert.Equal(KindState.Paused, paused.State);
        }

        [Fact]
        public void AnOpenSlotWaitsAboutAYearAndAHalf()
        {
            var rules = new IpoRules();
            var kinds = new List<KindFacts> { Kind(BusinessKind.Office, 500, 0, 7) };
            Assert.Equal(30d / 548d, IpoBoard.ChanceThisPeriod(kinds, 0, 6, rules), 12);

            var dice = new IpoDice(548UL);
            var none = new List<int>();
            long periods = 0;
            const int Fills = 50_000;
            for (int filled = 0; filled < Fills; )
            {
                periods++;
                if (IpoBoard.DrawPeriod(kinds, 0, 6, false, none, 0, rules, dice).Happens)
                {
                    filled++;
                }
            }

            Assert.InRange(periods * 30d / Fills, 548d * 0.97, 548d * 1.03);
        }

        [Fact]
        public void MoreOpenSlotsFillSoonerAndTheKindWithMoreIsLikelier()
        {
            var rules = new IpoRules();
            var kinds = new List<KindFacts>
            {
                Kind(BusinessKind.Office, 500, 50, 5), // 3 slots
                Kind(BusinessKind.Oil, 500, 0, 2), // 1 slot
                Kind(BusinessKind.Leisure, 500, 90, 0), // slots, but no candidate
                Kind(BusinessKind.Farming, 100, 90, 9), // behind the gate
            };
            Assert.Equal(4, IpoBoard.FillableSlots(kinds, 0, 6, rules));
            Assert.True(
                IpoBoard.ChanceThisPeriod(kinds, 0, 6, rules)
                    > IpoBoard.ChanceThisPeriod(kinds.GetRange(1, 3), 0, 6, rules)
            );

            var dice = new IpoDice(4UL);
            var none = new List<int>();
            var byKind = new int[4];
            var byCandidate = new int[5];
            var byDay = new int[30];
            int total = 0;
            for (int period = 0; period < 2_000_000; period++)
            {
                IpoPick pick = IpoBoard.DrawPeriod(kinds, 0, 6, false, none, 0, rules, dice);
                if (pick.Happens)
                {
                    total++;
                    byKind[pick.KindIndex]++;
                    byDay[pick.DayOffset]++;
                    if (pick.KindIndex == 0)
                    {
                        byCandidate[pick.CandidateIndex]++;
                    }
                    else
                    {
                        Assert.InRange(pick.CandidateIndex, 0, 1);
                    }
                }
            }

            Assert.Equal(0, byKind[2]);
            Assert.Equal(0, byKind[3]);
            Assert.InRange(byKind[0] / (double)total, 0.74, 0.76); // 3 of the 4 open slots
            foreach (int count in byCandidate)
            {
                Assert.InRange(count, byKind[0] / 5 * 0.95, byKind[0] / 5 * 1.05);
            }

            foreach (int count in byDay)
            {
                Assert.InRange(count, total / 30 * 0.93, total / 30 * 1.07);
            }
        }

        [Fact]
        public void NothingAndNoDiceWhenItCannotHappen()
        {
            var rules = new IpoRules();
            var kinds = new List<KindFacts> { Kind(BusinessKind.Office, 500, 50, 5) };
            var none = new List<int>();
            var twoThisYear = new List<int> { 900, 950 };
            var dice = new IpoDice(1UL);
            ulong before = dice.State;
            for (int i = 0; i < 1000; i++)
            {
                // an IPO in progress
                Assert.False(
                    IpoBoard.DrawPeriod(kinds, 0, 6, true, none, 1000, rules, dice).Happens
                );
                // the year's IPOs already happened
                Assert.False(
                    IpoBoard.DrawPeriod(kinds, 0, 6, false, twoThisYear, 1000, rules, dice).Happens
                );
                // the board is full (3 tiles hold 6)
                Assert.False(
                    IpoBoard.DrawPeriod(kinds, 6, 3, false, none, 1000, rules, dice).Happens
                );
                // no tiles, no board
                Assert.False(
                    IpoBoard.DrawPeriod(kinds, 0, 0, false, none, 1000, rules, dice).Happens
                );
                // nothing to list
                Assert.False(
                    IpoBoard
                        .DrawPeriod(new List<KindFacts>(), 0, 6, false, none, 1000, rules, dice)
                        .Happens
                );
            }

            Assert.Equal(before, dice.State);
        }

        [Fact]
        public void TheBoardsRoomLimitsHowManySlotsCanFill()
        {
            var rules = new IpoRules();
            var kinds = new List<KindFacts> { Kind(BusinessKind.Office, 500, 250, 40) }; // 11 slots
            Assert.Equal(11, IpoBoard.FillableSlots(kinds, 0, 6, rules));
            Assert.Equal(2, IpoBoard.FillableSlots(kinds, 0, 1, rules)); // one tile: two listings
            Assert.Equal(1, IpoBoard.FillableSlots(kinds, 5, 3, rules)); // 6 allowed, 5 used
        }

        [Fact]
        public void ALargeCitySwitchingItOnFillsTheBoardOverYearsNotAtOnce()
        {
            // 12 open slots from day one: the board fills two a year, never three in a year.
            var rules = new IpoRules();
            var dice = new IpoDice(2112UL);
            var days = new List<int>();
            int listed = 0;
            for (int today = 0; today < 365 * 12 && listed < 12; today += 30)
            {
                var kinds = new List<KindFacts> { Kind(BusinessKind.Office, 900, 300, 60, listed) };
                IpoPick pick = IpoBoard.DrawPeriod(
                    kinds,
                    listed,
                    9,
                    false,
                    days,
                    today,
                    rules,
                    dice
                );
                if (pick.Happens)
                {
                    days.Add(today + pick.DayOffset);
                    listed++;
                }
            }

            Assert.Equal(12, listed);
            Assert.InRange(days[11] / 365d, 5.5d, 9d); // six years at two a year, plus the waits
            for (int i = 2; i < days.Count; i++)
            {
                Assert.True(
                    days[i] - days[i - 2] >= 365 - 30,
                    "three IPOs inside a year at " + days[i]
                );
            }
        }

        [Fact]
        public void TheSameSeedPicksTheSameCompanies()
        {
            var rules = new IpoRules { SlotFillDays = 60 };
            var kinds = new List<KindFacts>
            {
                Kind(BusinessKind.Office, 500, 50, 5),
                Kind(BusinessKind.Oil, 500, 0, 2),
            };
            var none = new List<int>();
            var a = new IpoDice(7UL);
            var b = new IpoDice(7UL);
            for (int i = 0; i < 20_000; i++)
            {
                IpoPick first = IpoBoard.DrawPeriod(kinds, 0, 6, false, none, 0, rules, a);
                IpoPick second = IpoBoard.DrawPeriod(kinds, 0, 6, false, none, 0, rules, b);
                Assert.Equal(first.Happens, second.Happens);
                Assert.Equal(first.KindIndex, second.KindIndex);
                Assert.Equal(first.CandidateIndex, second.CandidateIndex);
                Assert.Equal(first.DayOffset, second.DayOffset);
            }
        }
    }
}

using System.Collections.Generic; // IList, List
using System.Globalization; // CultureInfo

namespace FinanceOverhaul.Core
{
    /// <summary>The whole local board on a day, for Economy → Investments.</summary>
    public sealed class BoardView
    {
        /// <summary>One entry per kind of business the city has (or has listed).</summary>
        public readonly List<KindStatus> Kinds = new List<KindStatus>();

        public int Listed;
        public int Cap;
        public int Tiles;

        /// <summary>The city has not had its IPOs for the year.</summary>
        public bool PacingAllows;

        /// <summary>The first day another IPO is allowed.</summary>
        public int NextAllowedDay;

        /// <summary>The city's Stock Exchange is at the level IPOs need.</summary>
        public bool ExchangeAllows = true;

        /// <summary>The Stock Exchange level IPOs need.</summary>
        public int ExchangeNeeded;

        /// <summary>The board is below its cap and pacing allows an IPO.</summary>
        public bool HasRoom
        {
            get { return Listed < Cap && PacingAllows; }
        }
    }

    /// <summary>
    /// The local company cards in Economy → Investments (Figma UI Map band 16): one card per
    /// kind of business, in the style of the game's locked stocks: a name, a line or two on
    /// where it stands, and a bar with "N / target". The host formats dates.
    /// </summary>
    public static class IpoCardText
    {
        /// <summary>What the city's board looks like today.</summary>
        public static BoardView View(
            IList<KindFacts> kinds,
            CompanyRegistry registry,
            int tilesOwned,
            int today,
            IpoRules rules,
            int exchangeLevel = IpoEngine.MaxExchangeLevel
        )
        {
            var view = new BoardView
            {
                ExchangeAllows = IpoBoard.ExchangeAllows(exchangeLevel, rules),
                ExchangeNeeded = rules.ExchangeLevel,
                Listed = registry.Companies.Count,
                Cap = IpoBoard.BoardCap(tilesOwned, rules),
                Tiles = tilesOwned,
                PacingAllows = IpoBoard.PacingAllows(registry.IpoDays, today, rules),
                NextAllowedDay = IpoBoard.NextAllowedDay(registry.IpoDays, today, rules),
            };
            foreach (KindFacts k in kinds)
            {
                KindFacts seen = k;
                seen.Listed = registry.ListedOf(k.Kind);
                if (seen.Squares <= 0 && seen.Listed == 0 && seen.ExtraSlots <= 0)
                {
                    continue; // the city has none of this kind: no card
                }

                view.Kinds.Add(IpoBoard.Status(seen, view.HasRoom, rules));
            }

            return view;
        }

        /// <summary>"Office companies"</summary>
        public static string Title(BusinessKind kind)
        {
            return Strings.Get("ipo.card.title." + kind);
        }

        /// <summary>The section label above the cards.</summary>
        public static string Section
        {
            get { return Strings.Get("ipo.card.section"); }
        }

        /// <summary>
        /// The first line: the unlock requirement while locked, else the slots.
        /// </summary>
        public static string Line(KindStatus status)
        {
            switch (status.State)
            {
                case KindState.Locked:
                    return Strings.Format("ipo.card.locked", Number(status.BarNeed));
                case KindState.Full:
                    return Strings.Format(
                        "ipo.card.full",
                        Number(status.Listed),
                        Number(status.Slots)
                    );
                default:
                    return Strings.Format(
                        "ipo.card.joined",
                        Strings.Plural(
                            "ipo.card.slotsOpen",
                            status.OpenSlots,
                            Number(status.OpenSlots)
                        ),
                        Strings.Plural(
                            "ipo.card.candidates",
                            status.Candidates,
                            Number(status.Candidates)
                        )
                    );
            }
        }

        /// <summary>
        /// The second line: what happens next. Empty while locked or full (the bar says it).
        /// With a slot open and the Stock Exchange below the level IPOs need, that comes first.
        /// </summary>
        /// <param name="nextAllowedDate">The day pacing allows another IPO, formatted by the host.</param>
        /// <param name="rules">For the jobs a specialised-industry business needs (null: the defaults).</param>
        public static string Note(
            KindStatus status,
            BoardView board,
            string nextAllowedDate,
            IpoRules rules = null
        )
        {
            if (
                !board.ExchangeAllows
                && status.State != KindState.Locked
                && status.State != KindState.Full
            )
            {
                return Strings.Format("ipo.card.note.exchange", Number(board.ExchangeNeeded));
            }

            switch (status.State)
            {
                case KindState.Open:
                    return Strings.Get("ipo.card.note.open");
                case KindState.Waiting:
                    if (status.Kind == BusinessKind.UniqueFactory)
                    {
                        return Strings.Format(
                            "ipo.card.note.waitingFactory",
                            Number((rules ?? new IpoRules()).MinWorkers)
                        );
                    }

                    if (!BusinessKinds.IsSpecialisedIndustry(status.Kind))
                    {
                        return Strings.Get("ipo.card.note.waiting");
                    }

                    // Farm, forest, oil and ore plots are small: say what would list.
                    return status.ExtraSlots > 0
                        ? Strings.Get("ipo.card.note.waitingArea")
                        : Strings.Format(
                            "ipo.card.note.large",
                            Number((rules ?? new IpoRules()).SpecialisedMinWorkers)
                        );
                case KindState.Paused:
                    return board.Listed >= board.Cap
                        ? Strings.Format(
                            "ipo.card.note.boardFull",
                            Number(board.Listed),
                            Number(board.Cap)
                        )
                        : Strings.Format("ipo.card.note.pacing", nextAllowedDate);
                default:
                    return string.Empty;
            }
        }

        /// <summary>The caption left of the bar: what it counts towards.</summary>
        public static string BarCaption(KindStatus status)
        {
            return Strings.Get(
                status.State == KindState.Locked ? "ipo.card.bar.unlock" : "ipo.card.bar.nextSlot"
            );
        }

        /// <summary>The card has a bar: not for unique factories (one slot per factory, nothing to count towards).</summary>
        public static bool HasBar(KindStatus status)
        {
            return status.BarNeed > 0;
        }

        /// <summary>"132 / 200" (squares while locked, else top-level buildings).</summary>
        public static string BarText(KindStatus status)
        {
            return Strings.Format(
                "ipo.card.bar.value",
                Number(status.BarHave),
                Number(status.BarNeed)
            );
        }

        /// <summary>How full the bar is, 0 to 1.</summary>
        public static float BarFraction(KindStatus status)
        {
            if (status.BarNeed <= 0)
            {
                return 0f;
            }

            float f = (float)status.BarHave / status.BarNeed;
            return f < 0f ? 0f
                : f > 1f ? 1f
                : f;
        }

        /// <summary>"Local listings 3 of 6 · 2 per map tile (3 owned) · at most 2 IPOs a year"</summary>
        public static string Footer(BoardView board, IpoRules rules)
        {
            return Strings.Format(
                "ipo.card.footer",
                Number(board.Listed),
                Number(board.Cap),
                Number(rules.ListingsPerTile),
                Number(board.Tiles),
                Number(rules.MaxIposPerYear)
            );
        }

        private static string Number(int value)
        {
            return value.ToString("#,0", CultureInfo.InvariantCulture);
        }
    }
}

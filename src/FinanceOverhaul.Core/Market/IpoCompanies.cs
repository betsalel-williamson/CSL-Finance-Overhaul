using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace FinanceOverhaul.Core
{
    /// <summary>
    /// Which kind of business a zoned building is, from the game's own names for its service and
    /// sub-service (so this needs no game types and can be tested without the game).
    /// </summary>
    public static class BusinessKinds
    {
        /// <summary>The game's top level for the zones that have levels (0-based: level 3).</summary>
        public const int TopLevelIndex = 2;

        /// <summary>False for anything that is not a commercial, office or industrial zone building.</summary>
        public static bool TryFromGame(string service, string subService, out BusinessKind kind)
        {
            kind = BusinessKind.Commercial;
            switch (subService)
            {
                case "CommercialLow":
                case "CommercialHigh":
                case "CommercialWallToWall":
                    kind = BusinessKind.Commercial;
                    return true;
                case "CommercialLeisure":
                    kind = BusinessKind.Leisure;
                    return true;
                case "CommercialTourist":
                    kind = BusinessKind.Tourism;
                    return true;
                case "CommercialEco":
                    kind = BusinessKind.Organic;
                    return true;
                case "OfficeGeneric":
                case "OfficeFinancial":
                case "OfficeWallToWall":
                    kind = BusinessKind.Office;
                    return true;
                case "OfficeHightech":
                    kind = BusinessKind.ItCluster;
                    return true;
                case "IndustrialGeneric":
                    kind = BusinessKind.Industry;
                    return true;
                case "IndustrialFarming":
                    kind = BusinessKind.Farming;
                    return true;
                case "IndustrialForestry":
                    kind = BusinessKind.Forestry;
                    return true;
                case "IndustrialOil":
                    kind = BusinessKind.Oil;
                    return true;
                case "IndustrialOre":
                    kind = BusinessKind.Ore;
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>The three zones level up to level 3; a specialisation has one level.</summary>
        public static bool HasLevels(BusinessKind kind)
        {
            return kind == BusinessKind.Commercial
                || kind == BusinessKind.Office
                || kind == BusinessKind.Industry;
        }

        /// <summary>
        /// Land value says something about shops and offices (the game levels them by it); an
        /// industrial area lowers land value by nature, so it is not asked of industry.
        /// </summary>
        public static bool LandValueMatters(BusinessKind kind)
        {
            switch (kind)
            {
                case BusinessKind.Commercial:
                case BusinessKind.Leisure:
                case BusinessKind.Tourism:
                case BusinessKind.Organic:
                case BusinessKind.Office:
                case BusinessKind.ItCluster:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// The jobs a business needs to be among the larger employers of its kind: the job
        /// count of the business that closes the top <paramref name="topPercent"/> percent
        /// (ties with it count too). 0 for an empty list.
        /// </summary>
        /// <param name="jobsOfKind">The jobs of every business of the kind in the city.</param>
        public static int JobsFloor(IList<int> jobsOfKind, int topPercent)
        {
            if (jobsOfKind == null || jobsOfKind.Count == 0)
            {
                return 0;
            }

            var sorted = new List<int>(jobsOfKind);
            sorted.Sort();
            sorted.Reverse();
            int percent = Math.Max(1, Math.Min(100, topPercent));
            int keep = (sorted.Count * percent + 99) / 100; // rounded up: at least one
            return sorted[Math.Max(1, keep) - 1];
        }

        /// <summary>At the top level of its kind.</summary>
        /// <param name="levelIndex">The building's level, 0-based as the game stores it.</param>
        public static bool IsTopLevel(BusinessKind kind, int levelIndex)
        {
            return !HasLevels(kind) || levelIndex >= TopLevelIndex;
        }
    }

    /// <summary>
    /// What Local IPOs keeps in the save (mod data id <see cref="DataId"/>, versioned text like
    /// the bond book): the dice, how long each business has stayed healthy, the companies
    /// listed, the days of past IPOs, and what the dice have scheduled. Unknown versions
    /// and bad lines are skipped, so a newer or damaged blob never breaks loading; vanilla
    /// ignores mod data.
    /// </summary>
    public static class CompaniesCodec
    {
        public const string DataId = "FinanceOverhaul.Companies";
        private const string Version = "v1";
        private const string CandidateTag = "healthy";
        private const string CompanyTag = "company";
        private const string IpoDayTag = "ipoday";
        private const string IpoTag = "ipo";
        private const string BuyoutTag = "buyout";
        private const string HoldingTag = "holding";

        public static string Format(CandidateBook book, IpoDice dice, CompanyRegistry registry)
        {
            var sb = new StringBuilder();
            sb.Append(Version).Append('\n');
            sb.Append("dice=")
                .Append(dice.State.ToString(CultureInfo.InvariantCulture))
                .Append('\n');
            var counted = new List<KeyValuePair<BuildingKey, int>>(book.Counted);
            counted.Sort(
                (a, b) =>
                    a.Key.Id != b.Key.Id
                        ? a.Key.Id.CompareTo(b.Key.Id)
                        : a.Key.BuildIndex.CompareTo(b.Key.BuildIndex)
            );
            foreach (KeyValuePair<BuildingKey, int> kv in counted)
            {
                sb.Append(CandidateTag)
                    .Append('|')
                    .Append(kv.Key.Id.ToString(CultureInfo.InvariantCulture))
                    .Append('|')
                    .Append(kv.Key.BuildIndex.ToString(CultureInfo.InvariantCulture))
                    .Append('|')
                    .Append(kv.Value.ToString(CultureInfo.InvariantCulture))
                    .Append('|')
                    .Append(book.KindOf(kv.Key).ToString())
                    .Append('\n');
            }

            sb.Append("next=").Append(Int(registry.NextId)).Append('\n');
            sb.Append("draw=").Append(Int(registry.NextDrawDay)).Append('\n');
            foreach (int day in registry.IpoDays)
            {
                sb.Append(IpoDayTag).Append('|').Append(Int(day)).Append('\n');
            }

            if (registry.IpoScheduled)
            {
                sb.Append(IpoTag)
                    .Append('|')
                    .Append(Int(registry.IpoBusiness.Id))
                    .Append('|')
                    .Append(registry.IpoBusiness.BuildIndex.ToString(CultureInfo.InvariantCulture))
                    .Append('|')
                    .Append(registry.IpoKind.ToString())
                    .Append('|')
                    .Append(Int(registry.IpoDay))
                    .Append('\n');
            }

            if (registry.BuyoutScheduled)
            {
                sb.Append(BuyoutTag)
                    .Append('|')
                    .Append(Int(registry.BuyoutCompanyId))
                    .Append('|')
                    .Append(Int(registry.BuyoutDay))
                    .Append('\n');
            }

            foreach (LocalCompany c in registry.Companies)
            {
                // The name comes last: it is the player's text and may hold anything but a
                // line break.
                sb.Append(CompanyTag)
                    .Append('|')
                    .Append(Int(c.Id))
                    .Append('|')
                    .Append(c.Kind.ToString())
                    .Append('|')
                    .Append(Int(c.Home.Id))
                    .Append('|')
                    .Append(c.Home.BuildIndex.ToString(CultureInfo.InvariantCulture))
                    .Append('|')
                    .Append(Int(c.AnnouncedDay))
                    .Append('|')
                    .Append(Int(c.FirstTradingDay))
                    .Append('|')
                    .Append(c.OfferPriceCents.ToString(CultureInfo.InvariantCulture))
                    .Append('|')
                    .Append(c.PriceCents.ToString(CultureInfo.InvariantCulture))
                    .Append('|')
                    .Append(c.Status.ToString())
                    .Append('|')
                    .Append(Clean(c.Symbol).Replace("|", string.Empty))
                    .Append('|')
                    .Append(Clean(c.Name))
                    .Append('\n');
                if (
                    c.HeldShares > 0
                    || c.HeldSpent != 0
                    || (c.History != null && c.History.Length > 0)
                )
                {
                    sb.Append(HoldingTag)
                        .Append('|')
                        .Append(Int(c.Id))
                        .Append('|')
                        .Append(c.HeldShares.ToString(CultureInfo.InvariantCulture))
                        .Append('|')
                        .Append(Int(c.HeldSpent))
                        .Append('|');
                    for (int h = 0; c.History != null && h < c.History.Length; h++)
                    {
                        sb.Append(h == 0 ? string.Empty : ",")
                            .Append(c.History[h].ToString("R", CultureInfo.InvariantCulture));
                    }

                    sb.Append('\n');
                }
            }

            return sb.ToString();
        }

        private static string Int(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string Clean(string text)
        {
            return (text ?? string.Empty).Replace('\n', ' ').Replace('\r', ' ');
        }

        private static bool TryInt(string text, out int value)
        {
            return int.TryParse(
                text,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out value
            );
        }

        private static bool TryBuilding(string id, string buildIndex, out BuildingKey building)
        {
            ushort i;
            uint b;
            bool ok =
                ushort.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out i)
                & uint.TryParse(buildIndex, NumberStyles.None, CultureInfo.InvariantCulture, out b);
            building = new BuildingKey(i, b);
            return ok;
        }

        private static void ReadRegistryLine(string line, CompanyRegistry registry)
        {
            int number;
            if (line.StartsWith("next=", StringComparison.Ordinal))
            {
                if (TryInt(line.Substring(5), out number) && number > 0)
                {
                    registry.NextId = Math.Max(registry.NextId, number);
                }

                return;
            }

            if (line.StartsWith("draw=", StringComparison.Ordinal))
            {
                if (TryInt(line.Substring(5), out number))
                {
                    registry.NextDrawDay = number;
                }

                return;
            }

            string[] f = line.Split(new[] { '|' }, 12);
            BuildingKey building;
            int day;
            if (f[0] == IpoDayTag && f.Length == 2 && TryInt(f[1], out day))
            {
                registry.IpoDays.Add(day);
            }
            else if (
                f[0] == IpoTag
                && f.Length == 5
                && TryBuilding(f[1], f[2], out building)
                && Enum.IsDefined(typeof(BusinessKind), f[3])
                && TryInt(f[4], out day)
            )
            {
                registry.IpoScheduled = true;
                registry.IpoBusiness = building;
                registry.IpoKind = (BusinessKind)Enum.Parse(typeof(BusinessKind), f[3]);
                registry.IpoDay = day;
            }
            else if (
                f[0] == BuyoutTag
                && f.Length == 3
                && TryInt(f[1], out number)
                && TryInt(f[2], out day)
            )
            {
                registry.BuyoutScheduled = true;
                registry.BuyoutCompanyId = number;
                registry.BuyoutDay = day;
            }
            else if (f[0] == HoldingTag && f.Length == 5 && TryInt(f[1], out number))
            {
                // Follows its company's line.
                LocalCompany held = registry.Find(number);
                uint shares;
                int spent;
                if (
                    held == null
                    || !uint.TryParse(
                        f[2],
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out shares
                    )
                    || !TryInt(f[3], out spent)
                )
                {
                    return;
                }

                var history = new List<float>();
                foreach (string one in f[4].Split(','))
                {
                    float price;
                    if (
                        one.Length > 0
                        && float.TryParse(
                            one,
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out price
                        )
                    )
                    {
                        history.Add(price);
                    }
                }

                held.HeldShares = shares;
                held.HeldSpent = spent;
                held.History = history.ToArray();
            }
            else if (f[0] == CompanyTag && f.Length == 12)
            {
                int announced,
                    first;
                long offer,
                    price;
                if (
                    !TryInt(f[1], out number)
                    || number <= 0
                    || registry.Find(number) != null
                    || !Enum.IsDefined(typeof(BusinessKind), f[2])
                    || !TryBuilding(f[3], f[4], out building)
                    || !TryInt(f[5], out announced)
                    || !TryInt(f[6], out first)
                    || !long.TryParse(
                        f[7],
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out offer
                    )
                    || !long.TryParse(
                        f[8],
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out price
                    )
                    || !Enum.IsDefined(typeof(CompanyStatus), f[9])
                )
                {
                    return;
                }

                registry.Companies.Add(
                    new LocalCompany
                    {
                        Id = number,
                        Kind = (BusinessKind)Enum.Parse(typeof(BusinessKind), f[2]),
                        Home = building,
                        AnnouncedDay = announced,
                        FirstTradingDay = first,
                        OfferPriceCents = offer,
                        PriceCents = price,
                        Status = (CompanyStatus)Enum.Parse(typeof(CompanyStatus), f[9]),
                        Symbol = f[10],
                        Name = f[11],
                    }
                );
                registry.NextId = Math.Max(registry.NextId, number + 1);
            }
        }

        /// <summary>
        /// Reads the save. <paramref name="dice"/> keeps its state if the save has none;
        /// <paramref name="registry"/> is filled with the companies and what is scheduled.
        /// </summary>
        public static CandidateBook Parse(string text, IpoDice dice, CompanyRegistry registry)
        {
            var book = new CandidateBook();
            if (string.IsNullOrEmpty(text))
            {
                return book;
            }

            string[] lines = text.Split('\n');
            if (lines[0].Trim('\r') != Version)
            {
                return book;
            }

            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim('\r');
                if (line.StartsWith("dice=", StringComparison.Ordinal))
                {
                    ulong state;
                    if (
                        ulong.TryParse(
                            line.Substring(5),
                            NumberStyles.None,
                            CultureInfo.InvariantCulture,
                            out state
                        )
                    )
                    {
                        dice.State = state;
                    }

                    continue;
                }

                if (!line.StartsWith(CandidateTag + "|", StringComparison.Ordinal))
                {
                    if (line.Length > 0)
                    {
                        ReadRegistryLine(line, registry);
                    }

                    continue;
                }

                string[] f = line.Split('|');
                ushort id;
                uint buildIndex;
                int days;
                if (
                    f.Length != 5
                    || f[0] != CandidateTag
                    || !ushort.TryParse(
                        f[1],
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out id
                    )
                    || !uint.TryParse(
                        f[2],
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out buildIndex
                    )
                    || !int.TryParse(
                        f[3],
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out days
                    )
                    || days <= 0
                    || !Enum.IsDefined(typeof(BusinessKind), f[4])
                )
                {
                    continue;
                }

                book.Restore(
                    new BuildingKey(id, buildIndex),
                    days,
                    (BusinessKind)Enum.Parse(typeof(BusinessKind), f[4])
                );
            }

            return book;
        }
    }
}

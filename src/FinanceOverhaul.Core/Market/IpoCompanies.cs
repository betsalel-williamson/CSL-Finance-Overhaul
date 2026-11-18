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

        /// <summary>At the top level of its kind.</summary>
        /// <param name="levelIndex">The building's level, 0-based as the game stores it.</param>
        public static bool IsTopLevel(BusinessKind kind, int levelIndex)
        {
            return !HasLevels(kind) || levelIndex >= TopLevelIndex;
        }
    }

    /// <summary>
    /// What Local IPOs keeps in the save (mod data id <see cref="DataId"/>, versioned text like
    /// the bond book): the dice, and how long each business has stayed healthy. Unknown versions
    /// and bad lines are skipped, so a newer or damaged blob never breaks loading; vanilla
    /// ignores mod data.
    /// </summary>
    public static class CompaniesCodec
    {
        public const string DataId = "FinanceOverhaul.Companies";
        private const string Version = "v1";
        private const string CandidateTag = "healthy";

        public static string Format(CandidateBook book, IpoDice dice)
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

            return sb.ToString();
        }

        /// <summary>Reads the save. <paramref name="dice"/> keeps its state if the save has none.</summary>
        public static CandidateBook Parse(string text, IpoDice dice)
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

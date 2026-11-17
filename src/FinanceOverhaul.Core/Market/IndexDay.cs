using System;
using System.Collections.Generic;
using System.Globalization;

namespace FinanceOverhaul.Core
{
    /// <summary>How today's Index level was arrived at.</summary>
    public enum IndexMarkMode
    {
        /// <summary>Nothing readable and no level yet: leave the game's own value.</summary>
        None,

        /// <summary>Yesterday's level moved by the members' average return.</summary>
        Chain,

        /// <summary>No level yet: start at the members' average price.</summary>
        Seed,

        /// <summary>No member readable today: keep yesterday's level.</summary>
        Hold,
    }

    public struct IndexMark
    {
        public IndexMarkMode Mode;
        public float Value;
    }

    /// <summary>A prefab the Index could be cloned from, as plain facts.</summary>
    public struct IndexSource
    {
        public string Name;

        /// <summary>Priced per share (not per coin, like crypto).</summary>
        public bool SharePriced;

        /// <summary>Locked behind a milestone.</summary>
        public bool HasMilestone;
    }

    /// <summary>
    /// The Index listing's daily and one-off decisions on top of <see cref="CityIndexBasket"/>:
    /// which rule gives today's level, which stocks are members, which prefab to clone, how a
    /// rebuilt history is put back, and how often the daily update is logged.
    /// </summary>
    public static class IndexDay
    {
        /// <summary>
        /// Today's level: chain from yesterday's when there is one and a member moved; seed
        /// from the members' average price when the Index has no level yet; hold yesterday's
        /// when no member is readable; otherwise none.
        /// </summary>
        /// <param name="previous">Yesterday's level, or 0 when the Index has no history.</param>
        public static IndexMark Mark(
            float previous,
            IList<string> names,
            IList<float> lastValues,
            IList<float> previousValues,
            IList<bool> eligible
        )
        {
            float mark;
            if (
                CityIndexBasket.TryComputeChainedMark(
                    previous,
                    names,
                    lastValues,
                    previousValues,
                    eligible,
                    out mark
                )
            )
            {
                return new IndexMark { Mode = IndexMarkMode.Chain, Value = mark };
            }

            if (
                !(previous > 0f)
                && CityIndexBasket.TryComputeMark(names, lastValues, eligible, out mark)
            )
            {
                return new IndexMark { Mode = IndexMarkMode.Seed, Value = mark };
            }

            return previous > 0f
                ? new IndexMark { Mode = IndexMarkMode.Hold, Value = previous }
                : new IndexMark { Mode = IndexMarkMode.None };
        }

        /// <summary>
        /// Whether a share-priced stock with this prefab name may be a member: the Index
        /// itself and the dev test and stress listings never are.
        /// </summary>
        public static bool IsMemberName(string name)
        {
            return !CityIndexBasket.IsIndexName(name)
                && !TestStock.IsTestName(name)
                && !StressStock.IsStressName(name);
        }

        /// <summary>How many members are counted today.</summary>
        public static int CountMembers(IList<string> names, IList<bool> eligible)
        {
            int n = 0;
            if (names == null || eligible == null)
            {
                return n;
            }

            for (int i = 0; i < Math.Min(names.Count, eligible.Count); i++)
            {
                if (eligible[i] && !CityIndexBasket.IsIndexName(names[i]))
                {
                    n++;
                }
            }

            return n;
        }

        /// <summary>
        /// Which prefab to clone the Index from: the first share-priced member with no unlock
        /// milestone, else the first share-priced member; -1 when there is none.
        /// </summary>
        public static int PickSource(IList<IndexSource> candidates)
        {
            int fallback = -1;
            if (candidates == null)
            {
                return fallback;
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                IndexSource c = candidates[i];
                if (c.Name == null || !c.SharePriced || !IsMemberName(c.Name))
                {
                    continue;
                }

                if (!c.HasMilestone)
                {
                    return i;
                }

                if (fallback < 0)
                {
                    fallback = i;
                }
            }

            return fallback;
        }

        /// <summary>
        /// A history with rebuilt older days put in front of the real points, oldest first (the
        /// order the game's history is refilled in). Both inputs are newest first.
        /// </summary>
        public static float[] WithOlderDays(float[] ownNewestFirst, float[] olderNewestFirst)
        {
            int own = ownNewestFirst == null ? 0 : ownNewestFirst.Length;
            int older = olderNewestFirst == null ? 0 : olderNewestFirst.Length;
            var all = new float[own + older];
            int k = 0;
            for (int j = older - 1; j >= 0; j--)
            {
                all[k++] = olderNewestFirst[j];
            }

            for (int j = own - 1; j >= 0; j--)
            {
                all[k++] = ownNewestFirst[j];
            }

            return all;
        }

        /// <summary>First 20 updates after a load, then every 30th, to keep the log small.</summary>
        public static bool LogsUpdate(int updateCount)
        {
            return updateCount <= 20 || updateCount % 30 == 0;
        }

        /// <summary>The log line for a daily update (English, never shown to the player).</summary>
        public static string UpdateLine(
            int updateCount,
            IndexMarkMode mode,
            float previous,
            float mark,
            int members
        )
        {
            float change = previous > 0f ? (mark / previous - 1f) * 100f : 0f;
            return "index: update #"
                + updateCount
                + " mode="
                + mode.ToString().ToLowerInvariant()
                + " "
                + previous.ToString("0.##", CultureInfo.InvariantCulture)
                + " -> "
                + mark.ToString("0.##", CultureInfo.InvariantCulture)
                + " ("
                + change.ToString("+0.00;-0.00", CultureInfo.InvariantCulture)
                + "%) members="
                + members;
        }
    }
}

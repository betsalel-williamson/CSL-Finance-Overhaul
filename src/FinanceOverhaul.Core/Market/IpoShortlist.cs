using System; // Func, Math
using System.Collections.Generic; // IList, List

namespace FinanceOverhaul.Core
{
    /// <summary>
    /// Which candidate of a kind goes public: only the largest employers of the kind are in the
    /// draw (<see cref="IpoRules.ShortlistSize"/>), and a bigger one is likelier
    /// (<see cref="IpoRules.SizeWeightPercent"/>). Sizes are workers today.
    /// </summary>
    public static class IpoShortlist
    {
        /// <summary>
        /// The <paramref name="size"/> candidates with the most workers, largest first; ties go
        /// to the older building (lower id, then build index), so the same day always gives the
        /// same list.
        /// </summary>
        public static List<BuildingKey> Rank(
            IList<BuildingKey> pool,
            Func<BuildingKey, int> workers,
            int size
        )
        {
            var ranked = new List<BuildingKey>(pool ?? new BuildingKey[0]);
            ranked.Sort(
                (a, b) =>
                {
                    int byWorkers = workers(b).CompareTo(workers(a));
                    if (byWorkers != 0)
                    {
                        return byWorkers;
                    }

                    return a.Id != b.Id
                        ? a.Id.CompareTo(b.Id)
                        : a.BuildIndex.CompareTo(b.BuildIndex);
                }
            );
            int keep = Math.Max(1, size);
            if (ranked.Count > keep)
            {
                ranked.RemoveRange(keep, ranked.Count - keep);
            }

            return ranked;
        }

        /// <summary>
        /// Each shortlisted candidate's share of the draw, from its workers: at 100 percent in
        /// proportion to them, at 0 the same for all, and in between a mix of the two. A
        /// candidate with no workers read still has a small chance (it counts as one worker).
        /// </summary>
        public static double[] Chances(IList<int> workers, int weightPercent)
        {
            int n = workers == null ? 0 : workers.Count;
            var chances = new double[n];
            if (n == 0)
            {
                return chances;
            }

            double weight = Math.Max(0, Math.Min(100, weightPercent)) / 100.0;
            double total = 0;
            foreach (int w in workers)
            {
                total += Math.Max(1, w);
            }

            for (int i = 0; i < n; i++)
            {
                chances[i] = (1 - weight) / n + weight * Math.Max(1, workers[i]) / total;
            }

            return chances;
        }

        /// <summary>
        /// The candidate a roll in [0, 1) lands on, by <see cref="Chances"/>; -1 for an empty
        /// list.
        /// </summary>
        public static int Pick(IList<int> workers, int weightPercent, double roll)
        {
            double[] chances = Chances(workers, weightPercent);
            double sum = 0;
            for (int i = 0; i < chances.Length; i++)
            {
                sum += chances[i];
                if (roll < sum)
                {
                    return i;
                }
            }

            return chances.Length - 1;
        }
    }
}

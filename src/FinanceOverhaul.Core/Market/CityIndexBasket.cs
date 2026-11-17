using System;
using System.Collections.Generic;

namespace FinanceOverhaul.Core
{
    /// <summary>
    /// Builds the Index stock mark from a snapshot of listed investments.
    /// The Host supplies parallel name / value / eligibility lists read from
    /// Cities; this type owns the basket rules (exclude Index, skip non-share
    /// and non-finite marks, equal weight).
    /// </summary>
    public static class CityIndexBasket
    {
        /// <summary>Exact display and prefab name of the city index stock.</summary>
        public const string IndexName = "Index";

        public static bool IsIndexName(string name)
        {
            return name == IndexName;
        }

        /// <summary>
        /// Equal-weight mark over eligible, finite, positive values whose name is
        /// not <see cref="IndexName"/>. Returns false (mark 0) when the basket is
        /// empty so callers can keep the vanilla value.
        /// </summary>
        public static bool TryComputeMark(
            IList<string> names,
            IList<float> values,
            IList<bool> eligible,
            out float mark
        )
        {
            mark = 0f;
            if (names == null || values == null || eligible == null)
            {
                return false;
            }

            int n = names.Count;
            if (values.Count < n)
            {
                n = values.Count;
            }

            if (eligible.Count < n)
            {
                n = eligible.Count;
            }

            float sum = 0f;
            int count = 0;
            for (int i = 0; i < n; i++)
            {
                if (!eligible[i] || IsIndexName(names[i]))
                {
                    continue;
                }

                float v = values[i];
                if (float.IsNaN(v) || float.IsInfinity(v) || v <= 0f)
                {
                    continue;
                }

                sum += v;
                count++;
            }

            if (count == 0)
            {
                return false;
            }

            mark = sum / count;
            return true;
        }

        /// <summary>
        /// Largest single-day move one member can contribute: halving or doubling. The cap
        /// is applied to the log of the price ratio, so it is symmetric (a jump and its
        /// reversal cancel exactly).
        /// </summary>
        public const float MinMemberReturn = -0.5f;
        public const float MaxMemberReturn = 1.0f;
        private static readonly double MaxLogMove = Math.Log(2.0);

        /// <summary>
        /// Chain-linked equal-weight **geometric** index (Jevons):
        /// <c>mark = previous × exp(mean ln(last_i / prev_i))</c> over eligible members
        /// (Index excluded) with finite, positive history, each ln-ratio capped to ±ln 2.
        /// For a fixed basket this telescopes to <c>I0 · Π (p_T / p_0)^(1/n)</c>: path
        /// independent and time reversible, so stocks that only bounce around do not move
        /// it. (The earlier mean of returns — Carli — is ≥ this every day by the AM–GM
        /// inequality and drifted up without bound; see CityIndexPropertyTests.) Members
        /// joining or leaving only change which ratios are averaged that day, so the level
        /// never jumps. Returns false when there is no previous level or no usable member,
        /// so callers can seed or hold.
        /// </summary>
        public static bool TryComputeChainedMark(
            float previousIndex,
            IList<string> names,
            IList<float> lastValues,
            IList<float> previousValues,
            IList<bool> eligible,
            out float mark
        )
        {
            mark = 0f;
            if (
                float.IsNaN(previousIndex)
                || float.IsInfinity(previousIndex)
                || previousIndex <= 0f
                || names == null
                || lastValues == null
                || previousValues == null
                || eligible == null
            )
            {
                return false;
            }

            int n = names.Count;
            n = Math.Min(n, lastValues.Count);
            n = Math.Min(n, previousValues.Count);
            n = Math.Min(n, eligible.Count);

            double sum = 0d;
            int count = 0;
            for (int i = 0; i < n; i++)
            {
                if (!eligible[i] || IsIndexName(names[i]))
                {
                    continue;
                }

                float last = lastValues[i];
                float prev = previousValues[i];
                if (!IsUsable(last) || !IsUsable(prev))
                {
                    continue;
                }

                sum += CappedLogMove(last, prev);
                count++;
            }

            if (count == 0)
            {
                return false;
            }

            mark = (float)(previousIndex * Math.Exp(sum / count));
            return true;
        }

        /// <summary>
        /// Rebuilds Index levels older than its own history from the members' vanilla
        /// value histories (newest-first, like <c>Queue.GetFromTop</c>), running the
        /// chain-linked geometric rule backwards (its exact inverse):
        /// <c>L(j+1) = L(j) / exp(mean_i ln(h_i[j] / h_i[j+1]))</c>.
        /// </summary>
        /// <param name="oldestOwnLevel">Index's oldest real value (step ownCount − 1).</param>
        /// <param name="ownCount">How many real Index points exist (steps 0..ownCount−1).</param>
        /// <param name="maxSteps">History capacity (own + backfilled points).</param>
        /// <returns>Levels for steps ownCount, ownCount+1, … (newest-first); empty when
        /// there is nothing usable to rebuild.</returns>
        public static float[] BackfillOlder(
            float oldestOwnLevel,
            int ownCount,
            IList<string> names,
            IList<float[]> newestFirstHistories,
            IList<bool> eligible,
            int maxSteps
        )
        {
            if (
                !IsUsable(oldestOwnLevel)
                || ownCount < 1
                || names == null
                || newestFirstHistories == null
                || eligible == null
            )
            {
                return new float[0];
            }

            int n = Math.Min(names.Count, Math.Min(newestFirstHistories.Count, eligible.Count));
            int maxLen = 0;
            for (int i = 0; i < n; i++)
            {
                if (IsMember(names[i], eligible[i], newestFirstHistories[i]))
                {
                    maxLen = Math.Max(maxLen, newestFirstHistories[i].Length);
                }
            }

            int last = Math.Min(maxLen, maxSteps) - 1;
            if (last < ownCount)
            {
                return new float[0];
            }

            var result = new float[last - ownCount + 1];
            double level = oldestOwnLevel;
            for (int step = ownCount; step <= last; step++)
            {
                // Return from (step) to (step − 1), i.e. how the newer day moved.
                double sum = 0d;
                int count = 0;
                for (int i = 0; i < n; i++)
                {
                    float[] h = newestFirstHistories[i];
                    if (!IsMember(names[i], eligible[i], h) || h.Length <= step)
                    {
                        continue;
                    }

                    float newer = h[step - 1];
                    float older = h[step];
                    if (!IsUsable(newer) || !IsUsable(older))
                    {
                        continue;
                    }

                    sum += CappedLogMove(newer, older);
                    count++;
                }

                if (count > 0)
                {
                    level /= Math.Exp(sum / count);
                }

                result[step - ownCount] = (float)level;
            }

            return result;
        }

        /// <summary>ln(newer / older), capped to ±ln 2 (at most a halving or a doubling).</summary>
        private static double CappedLogMove(float newer, float older)
        {
            double m = Math.Log(newer / (double)older);
            return Math.Max(-MaxLogMove, Math.Min(MaxLogMove, m));
        }

        private static bool IsMember(string name, bool eligible, float[] history)
        {
            return eligible && history != null && !IsIndexName(name);
        }

        private static bool IsUsable(float v)
        {
            return !float.IsNaN(v) && !float.IsInfinity(v) && v > 0f;
        }
    }
}

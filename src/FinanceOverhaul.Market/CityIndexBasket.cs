using System;
using System.Collections.Generic;

namespace FinanceOverhaul.Market
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

        /// <summary>Largest single-update loss / gain one member can contribute.</summary>
        public const float MinMemberReturn = -0.5f;
        public const float MaxMemberReturn = 1.0f;

        /// <summary>
        /// Chain-linked equal-weight return index:
        /// <c>mark = previous × (1 + mean(last_i / prev_i − 1))</c> over eligible members
        /// (Index excluded) with finite, positive history. Each member's return is
        /// clamped to [<see cref="MinMemberReturn"/>, <see cref="MaxMemberReturn"/>] so
        /// one stock unlocking from ₡1 cannot spike the index. Returns false when there
        /// is no previous level or no usable member, so callers can seed or hold.
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

                double r = last / (double)prev - 1d;
                if (r < MinMemberReturn)
                {
                    r = MinMemberReturn;
                }
                else if (r > MaxMemberReturn)
                {
                    r = MaxMemberReturn;
                }

                sum += r;
                count++;
            }

            if (count == 0)
            {
                return false;
            }

            mark = (float)(previousIndex * (1d + sum / count));
            return true;
        }

        private static bool IsUsable(float v)
        {
            return !float.IsNaN(v) && !float.IsInfinity(v) && v > 0f;
        }
    }
}

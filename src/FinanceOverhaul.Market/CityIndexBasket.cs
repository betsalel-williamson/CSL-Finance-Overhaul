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
    }
}

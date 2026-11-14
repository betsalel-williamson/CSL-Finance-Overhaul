using System;

namespace FinanceOverhaul.Market
{
    /// <summary>
    /// One-time correction for Index levels inflated by the old chained mean-of-returns rule
    /// (see <see cref="CityIndexBasket.TryComputeChainedMark"/>). The Index and its whole
    /// history are scaled by one factor so the level matches the members' average price — the
    /// level a fresh Index seeds at — and the chart keeps its shape. Runs at most once per
    /// city (a marker in the mod's save data), only when the level is more than ×2 off, and
    /// only when the city holds no Index shares and has no open Index orders (rescaling
    /// would otherwise change a holding's value or fire orders set at the old level).
    /// </summary>
    public static class IndexRebase
    {
        /// <summary>Mod save data id of the done-marker.</summary>
        public const string DataId = "FinanceOverhaul.IndexRebase";
        public const string Marker = "v1";

        /// <summary>Levels within this factor of the members' average are left alone.</summary>
        public const float Tolerance = 2f;

        public enum Decision
        {
            /// <summary>Level is fine (or unreadable): mark done, change nothing.</summary>
            Skip,

            /// <summary>Rescale by <see cref="Plan.Factor"/>, then mark done.</summary>
            Rescale,

            /// <summary>Holdings or open orders: try again another day.</summary>
            Wait,
        }

        public struct Plan
        {
            public Decision Decision;
            public double Factor;
            public string Reason;
        }

        public static Plan Decide(
            float level,
            float membersAverage,
            uint indexSharesHeld,
            int openIndexOrders
        )
        {
            if (!Usable(level) || !Usable(membersAverage))
            {
                return new Plan
                {
                    Decision = Decision.Skip,
                    Factor = 1d,
                    Reason = "no usable level",
                };
            }

            double factor = membersAverage / (double)level;
            if (factor >= 1d / Tolerance && factor <= Tolerance)
            {
                return new Plan
                {
                    Decision = Decision.Skip,
                    Factor = 1d,
                    Reason = "level within x2 of members",
                };
            }

            if (indexSharesHeld > 0 || openIndexOrders > 0)
            {
                return new Plan
                {
                    Decision = Decision.Wait,
                    Factor = factor,
                    Reason =
                        indexSharesHeld > 0 ? "the city holds Index shares" : "open Index orders",
                };
            }

            return new Plan
            {
                Decision = Decision.Rescale,
                Factor = factor,
                Reason = "level off by more than x2",
            };
        }

        /// <summary>Scales every finite, positive value; anything else is left as it was.</summary>
        public static float[] Scale(float[] values, double factor)
        {
            var result = new float[values == null ? 0 : values.Length];
            for (int i = 0; i < result.Length; i++)
            {
                float v = values[i];
                result[i] = Usable(v) ? (float)(v * factor) : v;
            }

            return result;
        }

        private static bool Usable(float v)
        {
            return !float.IsNaN(v) && !float.IsInfinity(v) && v > 0f;
        }
    }
}

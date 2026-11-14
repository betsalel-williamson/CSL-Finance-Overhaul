namespace FinanceOverhaul.Market
{
    /// <summary>
    /// Dev-only artificial listing for game UI tests ("Test Stock (dev)", symbol TST).
    /// Its price never moves on its own: it stays flat until a test pins a new price,
    /// which applies at the next daily update. Never part of the Index basket.
    /// </summary>
    public static class TestStock
    {
        public const string PrefabName = "FinanceOverhaul Test Stock";
        public const string DisplayName = "Test Stock (dev)";
        public const float BasePrice = 100f;

        public static bool IsTestName(string name)
        {
            return name == PrefabName;
        }

        /// <param name="pinned">Price set by a test (0 or less = none).</param>
        /// <param name="current">Today's mark (0 or non-finite = no history yet).</param>
        public static float NextMark(float pinned, float current)
        {
            if (IsPrice(pinned))
            {
                return pinned;
            }

            return IsPrice(current) ? current : BasePrice;
        }

        private static bool IsPrice(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }
    }
}

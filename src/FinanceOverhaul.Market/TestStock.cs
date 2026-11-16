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

namespace FinanceOverhaul.Market
{
    /// <summary>
    /// Dev-only stress listings ("Stress 001 (dev)" …) for measuring how many stocks the game
    /// handles (docs/developer/testing.md, many-stocks limit). Never part of the Index basket.
    /// </summary>
    public static class StressStock
    {
        public const string Prefix = "FinanceOverhaul Stress Stock ";

        /// <summary>The game's investment table has 255 slots; slot 0 is a placeholder, so 254 investments.</summary>
        public const int GameSlotLimit = 254;

        public static string PrefabName(int n)
        {
            return Prefix + n.ToString("000", System.Globalization.CultureInfo.InvariantCulture);
        }

        public static bool IsStressName(string name)
        {
            return name != null && name.StartsWith(Prefix, System.StringComparison.Ordinal);
        }

        /// <summary>"FinanceOverhaul Stress Stock 007" → "Stress 007 (dev)".</summary>
        public static string DisplayName(string prefabName)
        {
            return "Stress " + prefabName.Substring(Prefix.Length) + " (dev)";
        }
    }
}

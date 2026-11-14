using System;
using System.Text;

namespace FinanceOverhaul.Market
{
    /// <summary>
    /// Plain <c>key=value</c> settings file so Options survive hot reload and game
    /// restarts. Unknown keys, comments and malformed lines are ignored; anything
    /// missing falls back to the save-safe defaults of <see cref="ModSettings"/>. The retired
    /// <c>probeBiasEnabled</c> key from older versions is ignored like any unknown key.
    /// </summary>
    public static class SettingsFile
    {
        public const string FileName = "FinanceOverhaul.settings.txt";

        private const string IndexStockKey = "indexStockEnabled";
        private const string LiveTickerKey = "liveTickerEnabled";
        private const string DevTestStockKey = "devTestStockEnabled";

        /// <summary>Settings key of an order type's flag, e.g. <c>ordersStopLossEnabled</c>.</summary>
        public static string OrderKey(OrderKind kind)
        {
            return "orders" + kind.ToString().Replace("Sell", string.Empty) + "Enabled";
        }

        public static ModSettings Parse(string text)
        {
            var settings = new ModSettings();
            if (string.IsNullOrEmpty(text))
            {
                return settings;
            }

            string[] lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line[0] == '#')
                {
                    continue;
                }

                int eq = line.IndexOf('=');
                if (eq <= 0)
                {
                    continue;
                }

                string key = line.Substring(0, eq).Trim();
                string value = line.Substring(eq + 1).Trim();
                bool flag;
                if (!TryParseBool(value, out flag))
                {
                    continue;
                }

                if (string.Equals(key, IndexStockKey, StringComparison.OrdinalIgnoreCase))
                {
                    settings.IndexStockEnabled = flag;
                }
                else if (string.Equals(key, LiveTickerKey, StringComparison.OrdinalIgnoreCase))
                {
                    settings.LiveTickerEnabled = flag;
                }
                else if (string.Equals(key, DevTestStockKey, StringComparison.OrdinalIgnoreCase))
                {
                    settings.DevTestStockEnabled = flag;
                }
                else
                {
                    foreach (OrderKind kind in ModSettings.OrderKinds)
                    {
                        if (string.Equals(key, OrderKey(kind), StringComparison.OrdinalIgnoreCase))
                        {
                            settings.SetEnabled(kind, flag);
                        }
                    }
                }
            }

            return settings;
        }

        public static string Format(ModSettings settings)
        {
            settings = settings ?? new ModSettings();
            var sb = new StringBuilder();
            sb.Append("# Finance Overhaul settings (written by the mod)\n");
            sb.Append(IndexStockKey)
                .Append('=')
                .Append(settings.IndexStockEnabled ? "true" : "false")
                .Append('\n');
            sb.Append(LiveTickerKey)
                .Append('=')
                .Append(settings.LiveTickerEnabled ? "true" : "false")
                .Append('\n');
            sb.Append(DevTestStockKey)
                .Append('=')
                .Append(settings.DevTestStockEnabled ? "true" : "false")
                .Append('\n');
            foreach (OrderKind kind in ModSettings.OrderKinds)
            {
                sb.Append(OrderKey(kind))
                    .Append('=')
                    .Append(settings.IsEnabled(kind) ? "true" : "false")
                    .Append('\n');
            }

            return sb.ToString();
        }

        private static bool TryParseBool(string value, out bool result)
        {
            if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || value == "1")
            {
                result = true;
                return true;
            }

            if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase) || value == "0")
            {
                result = false;
                return true;
            }

            result = false;
            return false;
        }
    }
}

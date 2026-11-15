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
        private const string ShareChartsKey = "shareChartsEnabled";
        private const string CityBondsKey = "cityBondsEnabled";
        private const string TickerShowsKey = "tickerShows";
        private const string TickerBrightnessKey = "tickerBrightness";
        private const string TickerInInfoViewsKey = "tickerInInfoViews";
        private const string TickerPercent = "changePercent";
        private const string TickerPrice = "priceAndChange";
        private const string OrderLifetimeKey = "ordersLifetimeDays";

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
                if (settings.Monitor.TryRead(key, value))
                {
                    continue;
                }

                if (string.Equals(key, TickerBrightnessKey, StringComparison.OrdinalIgnoreCase))
                {
                    // classic | bright | brightest; unknown values keep the default.
                    foreach (
                        LedBrightness level in new[]
                        {
                            LedBrightness.Classic,
                            LedBrightness.Bright,
                            LedBrightness.Brightest,
                        }
                    )
                    {
                        if (
                            string.Equals(
                                value,
                                level.ToString(),
                                StringComparison.OrdinalIgnoreCase
                            )
                        )
                        {
                            settings.TickerBrightness = level;
                        }
                    }

                    continue;
                }

                if (string.Equals(key, TickerShowsKey, StringComparison.OrdinalIgnoreCase))
                {
                    // Unknown values keep the default (a newer build's choice).
                    if (string.Equals(value, TickerPrice, StringComparison.OrdinalIgnoreCase))
                    {
                        settings.TickerShows = TickerDisplay.PriceAndChange;
                    }
                    else if (
                        string.Equals(value, TickerPercent, StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        settings.TickerShows = TickerDisplay.ChangePercent;
                    }

                    continue;
                }

                if (string.Equals(key, OrderLifetimeKey, StringComparison.OrdinalIgnoreCase))
                {
                    int days;
                    if (
                        int.TryParse(
                            value,
                            System.Globalization.NumberStyles.Integer,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out days
                        )
                    )
                    {
                        settings.OrderLifetimeDays = days;
                    }

                    continue;
                }

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
                else if (string.Equals(key, ShareChartsKey, StringComparison.OrdinalIgnoreCase))
                {
                    settings.ShareChartsEnabled = flag;
                }
                else if (
                    string.Equals(key, TickerInInfoViewsKey, StringComparison.OrdinalIgnoreCase)
                )
                {
                    settings.TickerInInfoViews = flag;
                }
                else if (string.Equals(key, CityBondsKey, StringComparison.OrdinalIgnoreCase))
                {
                    settings.CityBondsEnabled = flag;
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
            sb.Append(OrderLifetimeKey)
                .Append('=')
                .Append(
                    settings.OrderLifetimeDays.ToString(
                        System.Globalization.CultureInfo.InvariantCulture
                    )
                )
                .Append('\n');
            sb.Append(ShareChartsKey)
                .Append('=')
                .Append(settings.ShareChartsEnabled ? "true" : "false")
                .Append('\n');
            sb.Append(TickerInInfoViewsKey)
                .Append('=')
                .Append(settings.TickerInInfoViews ? "true" : "false")
                .Append('\n');
            sb.Append(TickerBrightnessKey)
                .Append('=')
                .Append(settings.TickerBrightness.ToString().ToLowerInvariant())
                .Append('\n');
            sb.Append(TickerShowsKey)
                .Append('=')
                .Append(
                    settings.TickerShows == TickerDisplay.PriceAndChange
                        ? TickerPrice
                        : TickerPercent
                )
                .Append('\n');
            sb.Append(CityBondsKey)
                .Append('=')
                .Append(settings.CityBondsEnabled ? "true" : "false")
                .Append('\n');
            foreach (OrderKind kind in ModSettings.OrderKinds)
            {
                sb.Append(OrderKey(kind))
                    .Append('=')
                    .Append(settings.IsEnabled(kind) ? "true" : "false")
                    .Append('\n');
            }

            settings.Monitor.Write(sb);
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

using System; // StringComparison
using System.Text; // StringBuilder

namespace FinanceOverhaul.Core
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
        private const string LocalIposKey = "localIposEnabled";
        private const string InvestmentListKey = "investmentListEnabled";
        private const string IpoDialogKey = "ipoDialogEnabled";

        // Linked by default since 2026-11-27: a new key, so the "false" every earlier build
        // wrote (its default, never a choice) starts linked; the old key is ignored.
        private const string ListSortSyncedKey = "investmentListLinked";
        private const string ListSortKey = "investmentListSort";
        private const string ListSortReversedKey = "investmentListSortReversed";
        private const string ListCurrencyKey = "investmentListChangeCurrency";
        private const string TickerShowsKey = "tickerShows";
        private const string TickerBrightnessKey = "tickerBrightness";
        private const string TickerInInfoViewsKey = "tickerInInfoViews";
        private const string NoticesKey = "noticesEnabled";
        private const string NoticesCouponsKey = "noticesBondCoupons";
        private const string TickerPercent = "changePercent";
        private const string TickerPercentOnly = "changePercentOnly";
        private const string TickerPrice = "priceAndChange";
        private const string TickerPills = "followPills";
        private const string OrderLifetimeKey = "ordersLifetimeDays";
        private const string IpoPresetKey = "ipoPreset";

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

                if (string.Equals(key, IpoPresetKey, StringComparison.OrdinalIgnoreCase))
                {
                    IpoUserPreset preset = IpoPresets.Parse(value);
                    if (preset != null)
                    {
                        IpoPresets.Delete(settings.IpoUserPresets, preset.Name); // the last wins
                        settings.IpoUserPresets.Add(preset);
                    }

                    continue;
                }

                if (
                    key.StartsWith(IpoTuning.KeyPrefix, StringComparison.OrdinalIgnoreCase)
                    && IpoTuning.TryRead(settings.Ipo, key, value)
                )
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
                        string.Equals(value, TickerPercentOnly, StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        settings.TickerShows = TickerDisplay.ChangePercent;
                    }
                    else if (
                        string.Equals(value, TickerPills, StringComparison.OrdinalIgnoreCase)
                        // Older builds always wrote "changePercent" (their default): it reads as
                        // Follow the pills, which looks the same until a pill is flipped.
                        || string.Equals(value, TickerPercent, StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        settings.TickerShows = TickerDisplay.FollowPills;
                    }

                    continue;
                }

                if (string.Equals(key, ListSortKey, StringComparison.OrdinalIgnoreCase))
                {
                    // Unknown values keep the default (a newer build's sort).
                    foreach (MonitorSort sort in StockMonitor.SortCycle)
                    {
                        if (
                            string.Equals(
                                value,
                                sort.ToString(),
                                StringComparison.OrdinalIgnoreCase
                            )
                        )
                        {
                            settings.InvestmentListSort = sort;
                        }
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
                else if (string.Equals(key, NoticesKey, StringComparison.OrdinalIgnoreCase))
                {
                    settings.NoticesEnabled = flag;
                }
                else if (string.Equals(key, NoticesCouponsKey, StringComparison.OrdinalIgnoreCase))
                {
                    settings.NoticesBondCoupons = flag;
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
                else if (string.Equals(key, LocalIposKey, StringComparison.OrdinalIgnoreCase))
                {
                    settings.LocalIposEnabled = flag;
                }
                else if (string.Equals(key, InvestmentListKey, StringComparison.OrdinalIgnoreCase))
                {
                    settings.InvestmentListEnabled = flag;
                }
                else if (string.Equals(key, IpoDialogKey, StringComparison.OrdinalIgnoreCase))
                {
                    settings.IpoDialogEnabled = flag;
                }
                else if (string.Equals(key, ListSortSyncedKey, StringComparison.OrdinalIgnoreCase))
                {
                    settings.InvestmentListSortSynced = flag;
                }
                else if (
                    string.Equals(key, ListSortReversedKey, StringComparison.OrdinalIgnoreCase)
                )
                {
                    settings.InvestmentListSortReversed = flag;
                }
                else if (string.Equals(key, ListCurrencyKey, StringComparison.OrdinalIgnoreCase))
                {
                    settings.InvestmentListChangeAsCurrency = flag;
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
            sb.Append(NoticesKey)
                .Append('=')
                .Append(settings.NoticesEnabled ? "true" : "false")
                .Append('\n');
            sb.Append(NoticesCouponsKey)
                .Append('=')
                .Append(settings.NoticesBondCoupons ? "true" : "false")
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
                    settings.TickerShows == TickerDisplay.PriceAndChange ? TickerPrice
                    : settings.TickerShows == TickerDisplay.ChangePercent ? TickerPercentOnly
                    : TickerPills
                )
                .Append('\n');
            sb.Append(CityBondsKey)
                .Append('=')
                .Append(settings.CityBondsEnabled ? "true" : "false")
                .Append('\n');
            sb.Append(LocalIposKey)
                .Append('=')
                .Append(settings.LocalIposEnabled ? "true" : "false")
                .Append('\n');
            sb.Append(InvestmentListKey)
                .Append('=')
                .Append(settings.InvestmentListEnabled ? "true" : "false")
                .Append('\n');
            sb.Append(ListSortSyncedKey)
                .Append('=')
                .Append(settings.InvestmentListSortSynced ? "true" : "false")
                .Append('\n');
            sb.Append(ListSortKey).Append('=').Append(settings.InvestmentListSort).Append('\n');
            sb.Append(ListSortReversedKey)
                .Append('=')
                .Append(settings.InvestmentListSortReversed ? "true" : "false")
                .Append('\n');
            sb.Append(ListCurrencyKey)
                .Append('=')
                .Append(settings.InvestmentListChangeAsCurrency ? "true" : "false")
                .Append('\n');
            sb.Append(IpoDialogKey)
                .Append('=')
                .Append(settings.IpoDialogEnabled ? "true" : "false")
                .Append('\n');
            foreach (OrderKind kind in ModSettings.OrderKinds)
            {
                sb.Append(OrderKey(kind))
                    .Append('=')
                    .Append(settings.IsEnabled(kind) ? "true" : "false")
                    .Append('\n');
            }

            settings.Monitor.Write(sb);
            foreach (string line in IpoTuning.Lines(settings.Ipo))
            {
                sb.Append(line).Append('\n');
            }

            foreach (IpoUserPreset preset in settings.IpoUserPresets)
            {
                sb.Append(IpoPresetKey).Append('=').Append(IpoPresets.Format(preset)).Append('\n');
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

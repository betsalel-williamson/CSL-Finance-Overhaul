#if HAS_CITIES
using System;
using FinanceOverhaul.Market;
using ICities;

namespace FinanceOverhaul
{
    /// <summary>
    /// The Options page (Options → Finance Overhaul) and what each control does when changed.
    /// Preferences show in every build; feature flags only in dev builds (release builds use
    /// <see cref="ModSettings.PinReleaseFlags"/>).
    /// </summary>
    internal static class ModOptions
    {
        public static void Build(UIHelperBase helper)
        {
            ModSettings Settings = Mod.EnsureSettings();

            // Preferences: how shipped features look and behave. Every build.
            UIHelperBase group = helper.AddGroup(ModBuildInfo.OptionsTitle);
            group.AddCheckbox(
                "Live Stock Exchange ticker (visual only)",
                Settings.LiveTickerEnabled,
                OnLiveTickerChanged
            );
            group.AddDropdown(
                "Stock Exchange ticker shows",
                new[] { "Change % (+3.22%)", "Price and ₡ change (₡951 +₡30)" },
                Settings.TickerShows == TickerDisplay.PriceAndChange ? 1 : 0,
                OnTickerShowsChanged
            );
            group.AddCheckbox(
                "Show the LED ticker in info views (traffic, population…)",
                Settings.TickerInInfoViews,
                OnTickerInInfoViewsChanged
            );
            group.AddDropdown(
                "LED ticker brightness",
                new[] { "Classic", "Bright", "Brightest (daylight)" },
                (int)Settings.TickerBrightness,
                OnTickerBrightnessChanged
            );

            // How long new orders stay open (a game, so the player chooses; default 90 days).
            int[] lifetimes = TradeOrder.LifetimeChoices;
            var lifetimeLabels = new string[lifetimes.Length];
            int selected = Array.IndexOf(lifetimes, TradeOrder.DefaultLifetimeDays);
            for (int i = 0; i < lifetimes.Length; i++)
            {
                lifetimeLabels[i] = OrderText.LifetimeLabel(lifetimes[i]);
                if (lifetimes[i] == Settings.OrderLifetimeDays)
                {
                    selected = i;
                }
            }

            group.AddDropdown(
                "Orders stay open for (new orders)",
                lifetimeLabels,
                selected,
                OnOrderLifetimeChanged
            );
            group.AddCheckbox(
                "Order and bond notices in Chirper",
                Settings.NoticesEnabled,
                v =>
                {
                    Settings.NoticesEnabled = v;
                    Mod.SaveSettings();
                    ModLog.Info("notices enabled=" + v);
                }
            );
            if (BuildInfo.ShowDevBuildIdentity || Settings.CityBondsEnabled)
            {
                group.AddCheckbox(
                    "Also chirp every bond coupon",
                    Settings.NoticesBondCoupons,
                    v =>
                    {
                        Settings.NoticesBondCoupons = v;
                        Mod.SaveSettings();
                    }
                );
            }

            // Feature flags: whether a feature exists at all. Dev builds only; release builds
            // use ModSettings.PinReleaseFlags.
            if (BuildInfo.ShowDevBuildIdentity)
            {
                UIHelperBase flags = helper.AddGroup("Feature flags (dev builds only)");
                foreach (OrderKind kind in ModSettings.OrderKinds)
                {
                    OrderKind k = kind;
                    flags.AddCheckbox(
                        OrderText.OptionLabel(k),
                        Settings.IsEnabled(k),
                        enabled => OnOrderKindChanged(k, enabled)
                    );
                }

                flags.AddCheckbox(
                    "Stock Monitor window",
                    Settings.Monitor.Enabled,
                    OnStockMonitorChanged
                );
                flags.AddCheckbox(
                    "Share value charts in the Stock Monitor style",
                    Settings.ShareChartsEnabled,
                    OnShareChartsChanged
                );
                flags.AddCheckbox(
                    "Index stock (adds an investment to saves)",
                    Settings.IndexStockEnabled,
                    OnIndexStockChanged
                );
                flags.AddCheckbox(
                    "City bonds (sell bonds to raise money; adds mod save data)",
                    Settings.CityBondsEnabled,
                    OnCityBondsChanged
                );
                flags.AddCheckbox(
                    "Dev: Test Stock (TST) for game UI tests (adds a listing; use a test save)",
                    Settings.DevTestStockEnabled,
                    OnDevTestStockChanged
                );
            }

            // Build info goes on the Options page itself after the group, as plain labels (not
            // nested groups, which each draw a rule): the group's own separator rule then sits
            // between the toggles and the build info.
            AddFooterLabel(helper, ModBuildInfo.GetBuildInfoFooterDisplay());
            AddFooterLabel(helper, ModBuildInfo.GetBuildInfoLocalDisplay());

            // Always available (dev and release): one click puts version, assembly identity
            // and build time on the clipboard for bug reports.
            copyBuildInfoButton =
                helper.AddButton(CopyBuildInfoLabel, OnCopyBuildInfo)
                as ColossalFramework.UI.UIButton;
        }

        private const string CopyBuildInfoLabel = "Copy build info";

        private static ColossalFramework.UI.UIButton copyBuildInfoButton;

        private static void OnCopyBuildInfo()
        {
            try
            {
                string text = ModBuildInfo.GetBuildInfoText();
                UnityEngine.GUIUtility.systemCopyBuffer = text;
                ModLog.Info("build info copied: " + text.Replace("\n", " | "));
                if (copyBuildInfoButton != null)
                {
                    copyBuildInfoButton.text = "Copied build info";
                }
            }
            catch (Exception ex)
            {
                ModLog.Warn("build info copy failed: " + ex.Message);
            }
        }

        private static void AddFooterLabel(UIHelperBase group, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            var helper = group as UIHelper;
            var panel = helper == null ? null : helper.self as ColossalFramework.UI.UIComponent;
            if (panel == null)
            {
                group.AddGroup(text); // fallback: still visible, with an extra rule
                return;
            }

            ColossalFramework.UI.UILabel label =
                panel.AddUIComponent<ColossalFramework.UI.UILabel>();
            label.text = text;
            label.textScale = 0.875f;
        }

        private static void OnIndexStockChanged(bool enabled)
        {
            ModSettings Settings = Mod.EnsureSettings();
            Settings.IndexStockEnabled = enabled;
            Mod.SaveSettings();
            ModLog.Info("index stock enabled=" + enabled);
            if (enabled)
            {
                // Mid-game: list Index now instead of waiting for the next city load.
                IndexListing.EnsureInLoadedCity();
            }
            else
            {
                ModLog.Info("index: disabling keeps an existing listing until the next city load");
            }
        }

        private static void OnDevTestStockChanged(bool enabled)
        {
            ModSettings Settings = Mod.EnsureSettings();
            Settings.DevTestStockEnabled = enabled;
            Mod.SaveSettings();
            ModLog.Info("dev test stock enabled=" + enabled);
            if (enabled)
            {
                TestStockListing.EnsureInLoadedCity();
            }
        }

        internal static void OnOrderKindChanged(OrderKind kind, bool enabled)
        {
            ModSettings Settings = Mod.EnsureSettings();
            Settings.SetEnabled(kind, enabled);
            Mod.SaveSettings();
            ModLog.Info("orders: " + SettingsFile.OrderKey(kind) + "=" + enabled);
            OrderTicket.OnFlagsChanged();
        }

        private static void OnTickerInInfoViewsChanged(bool shown)
        {
            ModSettings Settings = Mod.EnsureSettings();
            Settings.TickerInInfoViews = shown;
            Mod.SaveSettings();
            ModLog.Info("ticker: in info views=" + shown);
        }

        private static void OnTickerBrightnessChanged(int index)
        {
            ModSettings Settings = Mod.EnsureSettings();
            Settings.TickerBrightness = (LedBrightness)Math.Max(0, Math.Min(2, index));
            Mod.SaveSettings();
            ModLog.Info("ticker: brightness " + Settings.TickerBrightness);
        }

        private static void OnTickerShowsChanged(int index)
        {
            ModSettings Settings = Mod.EnsureSettings();
            Settings.TickerShows =
                index == 1 ? TickerDisplay.PriceAndChange : TickerDisplay.ChangePercent;
            Mod.SaveSettings();
            ModLog.Info("ticker: shows " + Settings.TickerShows);
        }

        private static void OnOrderLifetimeChanged(int index)
        {
            ModSettings Settings = Mod.EnsureSettings();
            int[] lifetimes = TradeOrder.LifetimeChoices;
            if (index < 0 || index >= lifetimes.Length)
            {
                return;
            }

            Settings.OrderLifetimeDays = lifetimes[index];
            Mod.SaveSettings();
            ModLog.Info("orders: new orders stay open " + Settings.OrderLifetimeDays + " days");
        }

        private static void OnShareChartsChanged(bool enabled)
        {
            ModSettings Settings = Mod.EnsureSettings();
            Settings.ShareChartsEnabled = enabled;
            Mod.SaveSettings();
            ModLog.Info("share charts enabled=" + enabled);
            if (!enabled)
            {
                ShareChartOverlay.RemoveAll();
            }
        }

        private static void OnCityBondsChanged(bool enabled)
        {
            ModSettings Settings = Mod.EnsureSettings();
            Settings.CityBondsEnabled = enabled;
            Mod.SaveSettings();
            ModLog.Info("city bonds enabled=" + enabled);
        }

        private static void OnStockMonitorChanged(bool enabled)
        {
            ModSettings Settings = Mod.EnsureSettings();
            Settings.Monitor.Enabled = enabled;
            Mod.SaveSettings();
            ModLog.Info("stock monitor enabled=" + enabled);
            if (StockMonitorHost.Current != null)
            {
                StockMonitorHost.Current.ApplyFlag();
            }
        }

        private static void OnLiveTickerChanged(bool enabled)
        {
            ModSettings Settings = Mod.EnsureSettings();
            Settings.LiveTickerEnabled = enabled;
            Mod.SaveSettings();
            ModLog.Info("live ticker enabled=" + enabled);
        }
    }
}
#endif

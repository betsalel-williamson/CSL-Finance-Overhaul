using System;
using FinanceOverhaul.Core;
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
                Strings.Get("options.ticker.live"),
                Settings.LiveTickerEnabled,
                OnLiveTickerChanged
            );
            group.AddDropdown(
                Strings.Get("options.ticker.shows"),
                new[]
                {
                    Strings.Get("options.ticker.shows.percent"),
                    Strings.Get("options.ticker.shows.price"),
                },
                Settings.TickerShows == TickerDisplay.PriceAndChange ? 1 : 0,
                OnTickerShowsChanged
            );
            group.AddCheckbox(
                Strings.Get("options.ticker.infoViews"),
                Settings.TickerInInfoViews,
                OnTickerInInfoViewsChanged
            );
            group.AddDropdown(
                Strings.Get("options.ticker.brightness"),
                new[]
                {
                    Strings.Get("options.ticker.brightness.classic"),
                    Strings.Get("options.ticker.brightness.bright"),
                    Strings.Get("options.ticker.brightness.brightest"),
                },
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
                Strings.Get("options.orders.lifetime"),
                lifetimeLabels,
                selected,
                OnOrderLifetimeChanged
            );
            group.AddCheckbox(
                Strings.Get("options.notices"),
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
                    Strings.Get("options.notices.coupons"),
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
                UIHelperBase flags = helper.AddGroup(Strings.Get("options.flags"));
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
                    Strings.Get("options.flag.monitor"),
                    Settings.Monitor.Enabled,
                    OnStockMonitorChanged
                );
                flags.AddCheckbox(
                    Strings.Get("options.flag.charts"),
                    Settings.ShareChartsEnabled,
                    OnShareChartsChanged
                );
                flags.AddCheckbox(
                    Strings.Get("options.flag.index"),
                    Settings.IndexStockEnabled,
                    OnIndexStockChanged
                );
                flags.AddCheckbox(
                    Strings.Get("options.flag.bonds"),
                    Settings.CityBondsEnabled,
                    OnCityBondsChanged
                );
                flags.AddCheckbox(
                    Strings.Get("options.flag.localIpos"),
                    Settings.LocalIposEnabled,
                    OnLocalIposChanged
                );
                flags.AddCheckbox(
                    Strings.Get("options.flag.testStock"),
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
                helper.AddButton(Strings.Get("options.copyBuildInfo"), OnCopyBuildInfo)
                as ColossalFramework.UI.UIButton;
        }

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
                    copyBuildInfoButton.text = Strings.Get("options.copiedBuildInfo");
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

        private static void OnLocalIposChanged(bool enabled)
        {
            ModSettings Settings = Mod.EnsureSettings();
            Settings.LocalIposEnabled = enabled;
            Mod.SaveSettings();
            ModLog.Info("local ipos enabled=" + enabled);
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

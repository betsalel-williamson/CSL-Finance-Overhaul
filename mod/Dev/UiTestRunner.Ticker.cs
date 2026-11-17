using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using ColossalFramework.UI;
using FinanceOverhaul.Core;
using UnityEngine;

namespace FinanceOverhaul
{
    /// <summary>Game UI test commands: The Stock Exchange LED ticker and info views.</summary>
    public partial class UiTestRunner
    {
        private IEnumerator TickerSteps(
            string line,
            string cmd,
            string a,
            string b,
            Context ctx,
            StepResult r
        )
        {
            switch (cmd)
            {
                case "info-view":
                {
                    // info-view traffic|population|none — opens or closes an info view (the
                    // player's own view comes back at the end).
                    if (!ctx.InfoBefore.HasValue)
                    {
                        ctx.InfoBefore = InfoManager.instance.CurrentMode;
                        ctx.InfoSubBefore = InfoManager.instance.CurrentSubMode;
                    }

                    InfoManager.InfoMode mode =
                        a == "traffic" ? InfoManager.InfoMode.Traffic
                        : a == "population" ? InfoManager.InfoMode.Density
                        : InfoManager.InfoMode.None;
                    InfoManager.instance.SetCurrentMode(mode, InfoManager.SubInfoMode.Default);
                    yield return new WaitForSeconds(1f);
                    break;
                }
                case "live-ticker":
                {
                    // live-ticker on|off — the Options ticker toggle (restored at the end).
                    if (!ctx.LiveTickerBefore.HasValue)
                    {
                        ctx.LiveTickerBefore = Mod.Settings.LiveTickerEnabled;
                    }

                    Mod.Settings.LiveTickerEnabled = a == "on";
                    yield return new WaitForSeconds(0.6f);
                    break;
                }
                case "ticker-in-info-views":
                {
                    // ticker-in-info-views on|off — Options checkbox (restored at the end).
                    if (!ctx.InInfoViewsBefore.HasValue)
                    {
                        ctx.InInfoViewsBefore = Mod.Settings.TickerInInfoViews;
                    }

                    Mod.Settings.TickerInInfoViews = a == "on";
                    yield return new WaitForSeconds(0.6f);
                    break;
                }
                case "expect-ticker-style":
                {
                    // expect-ticker-style — the boards' screens are drawn in the Options style.
                    TickerDisplay want =
                        Mod.Settings != null
                            ? Mod.Settings.TickerShows
                            : TickerDisplay.ChangePercent;
                    if (TickerOverlay.DrawnStyle != want)
                    {
                        r.Error =
                            "boards drawn as "
                            + (
                                TickerOverlay.DrawnStyle.HasValue
                                    ? TickerOverlay.DrawnStyle.Value.ToString()
                                    : "nothing"
                            )
                            + ", Options says "
                            + want;
                    }

                    break;
                }
                case "expect-ticker-full":
                {
                    // The facade boards have today's entries (no "Waiting for market data").
                    TickerSnapshot snap = TickerFeed.Current;
                    if (snap == null || snap.Count == 0)
                    {
                        r.Error = "the ticker has no entries yet";
                    }

                    break;
                }
                case "info-view-swap":
                {
                    // info-view-swap traffic|none shown|hidden — switches the info view and checks,
                    // inside the game's own "info view changed" event (before that frame is
                    // drawn), that the live boards and the vanilla ones have already swapped.
                    string seen = null;
                    bool want = b == "shown";
                    InfoManager.InfoModeChangedEventHandler probe = (m, s) =>
                        seen =
                            "live="
                            + TickerOverlay.ShownNow
                            + " vanillaHidden="
                            + VanillaBoards.AnyHidden;
                    if (!ctx.InfoBefore.HasValue)
                    {
                        ctx.InfoBefore = InfoManager.instance.CurrentMode;
                        ctx.InfoSubBefore = InfoManager.instance.CurrentSubMode;
                    }

                    InfoManager.instance.EventInfoModeChanged += probe;
                    InfoManager.instance.SetCurrentMode(
                        a == "traffic" ? InfoManager.InfoMode.Traffic : InfoManager.InfoMode.None,
                        InfoManager.SubInfoMode.Default
                    );
                    float until = Time.unscaledTime + 3f;
                    while (seen == null && Time.unscaledTime < until)
                    {
                        yield return null;
                    }

                    InfoManager.instance.EventInfoModeChanged -= probe;
                    ModLog.Info("uitest: at the switch " + seen);
                    string expected = "live=" + want + " vanillaHidden=" + want;
                    if (seen != expected)
                    {
                        r.Error =
                            "at the switch " + (seen ?? "no event") + ", expected " + expected;
                    }

                    break;
                }
                case "expect-ticker":
                {
                    // expect-ticker shown|hidden — the live LED boards right now.
                    bool want = a == "shown";
                    if (TickerOverlay.ShownNow != want)
                    {
                        r.Error =
                            "ticker is "
                            + (TickerOverlay.ShownNow ? "shown" : "hidden")
                            + ", expected "
                            + a;
                    }

                    break;
                }
                case "turn-exchange":
                {
                    // turn-exchange DEGREES|restore — turns the first Stock Exchange where it
                    // stands (as the game's relocate tool or Move It does) and puts it back.
                    if (a == "restore")
                    {
                        RestoreExchange(ctx);
                        yield return new WaitForSeconds(0.5f);
                        break;
                    }

                    float degrees;
                    if (!float.TryParse(a, out degrees))
                    {
                        r.Error = "turn-exchange needs degrees or restore";
                        break;
                    }

                    Building[] all = BuildingManager.instance.m_buildings.m_buffer;
                    ushort found = ctx.TurnedExchange;
                    for (int i = 1; found == 0 && i < all.Length; i++)
                    {
                        if (
                            (all[i].m_flags & Building.Flags.Created) != 0
                            && all[i].Info != null
                            && all[i].Info.m_buildingAI is StockExchangeAI
                        )
                        {
                            found = (ushort)i;
                            ctx.TurnedExchange = found;
                            ctx.ExchangePositionBefore = all[i].m_position;
                            ctx.ExchangeAngleBefore = all[i].m_angle;
                        }
                    }

                    if (found == 0)
                    {
                        r.Error = "no Stock Exchange in this city";
                        break;
                    }

                    Vector3 where = ctx.ExchangePositionBefore;
                    float angle = ctx.ExchangeAngleBefore + degrees * Mathf.Deg2Rad;
                    SimulationManager.instance.AddAction(() =>
                        BuildingManager.instance.RelocateBuilding(found, where, angle)
                    );
                    break;
                }
                case "expect-ticker-on-building":
                {
                    // Every set of live boards stands where its Stock Exchange stands now.
                    TickerOverlay overlay = UnityEngine.Object.FindObjectOfType<TickerOverlay>();
                    string problem = overlay == null ? "no ticker overlay" : overlay.Misplaced();
                    if (problem != null)
                    {
                        r.Error = problem;
                    }

                    break;
                }
                case "ticker-brightness":
                {
                    // ticker-brightness classic|bright|brightest|restore — the LED boards'
                    // Options brightness (the player's choice is put back at the end).
                    if (Mod.Settings == null)
                    {
                        r.Error = "no settings";
                        break;
                    }

                    if (a == "restore")
                    {
                        if (ctx.BrightnessBefore.HasValue)
                        {
                            Mod.Settings.TickerBrightness = ctx.BrightnessBefore.Value;
                            ctx.BrightnessBefore = null;
                        }

                        break;
                    }

                    if (!ctx.BrightnessBefore.HasValue)
                    {
                        ctx.BrightnessBefore = Mod.Settings.TickerBrightness;
                    }

                    Mod.Settings.TickerBrightness =
                        a == "classic" ? LedBrightness.Classic
                        : a == "brightest" ? LedBrightness.Brightest
                        : LedBrightness.Bright;
                    LedRgb gain = LedPalette.Colour(LedText.Gain, Mod.Settings.TickerBrightness);
                    ModLog.Info(
                        "uitest: ticker brightness "
                            + Mod.Settings.TickerBrightness
                            + " gain=#"
                            + gain.R.ToString("X2")
                            + gain.G.ToString("X2")
                            + gain.B.ToString("X2")
                    );
                    yield return new WaitForSeconds(0.5f); // boards repaint on the next scroll
                    break;
                }
                case "ticker-shows":
                {
                    // ticker-shows percent|price|restore — switches Options → "Stock Exchange
                    // ticker shows" (the player's choice is put back by restore and at the end)
                    // and logs today's entries as the boards will draw them.
                    if (Mod.Settings == null)
                    {
                        r.Error = "no settings";
                        break;
                    }

                    if (a == "restore")
                    {
                        if (ctx.TickerBefore.HasValue)
                        {
                            Mod.Settings.TickerShows = ctx.TickerBefore.Value;
                            ctx.TickerBefore = null;
                        }

                        break;
                    }

                    if (!ctx.TickerBefore.HasValue)
                    {
                        ctx.TickerBefore = Mod.Settings.TickerShows;
                    }

                    Mod.Settings.TickerShows =
                        a == "price" ? TickerDisplay.PriceAndChange : TickerDisplay.ChangePercent;
                    TickerSnapshot snap = TickerFeed.Current;
                    var sb = new System.Text.StringBuilder();
                    for (int i = 0; snap != null && i < snap.Count; i++)
                    {
                        sb.Append(snap.Symbols[i])
                            .Append(
                                LedText.ChangePart(
                                    i < snap.Prices.Length ? snap.Prices[i] : float.NaN,
                                    snap.Rates[i],
                                    Mod.Settings.TickerShows
                                )
                            )
                            .Append("  ");
                    }

                    ModLog.Info("uitest: ticker " + Mod.Settings.TickerShows + ": " + sb);
                    break;
                }
                default:
                    r.Unknown = true; // not one of this group's commands
                    yield break;
            }
        }

        /// <summary>Puts the Stock Exchange a scenario turned back as it stood.</summary>
        private static void RestoreExchange(Context ctx)
        {
            if (ctx.TurnedExchange == 0)
            {
                return;
            }

            ushort id = ctx.TurnedExchange;
            Vector3 where = ctx.ExchangePositionBefore;
            float angle = ctx.ExchangeAngleBefore;
            ctx.TurnedExchange = 0;
            SimulationManager.instance.AddAction(() =>
                BuildingManager.instance.RelocateBuilding(id, where, angle)
            );
        }
    }
}

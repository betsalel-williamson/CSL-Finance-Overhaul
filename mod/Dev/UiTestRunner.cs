using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using ColossalFramework.UI;
using FinanceOverhaul.Core;
using HarmonyLib;
using UnityEngine;

namespace FinanceOverhaul
{
    /// <summary>
    /// Dev-only game UI scenario runner (--dev builds). Drop a script into
    /// <c>FinanceOverhaul.uitest</c> next to the settings file; each line is one step that
    /// drives the real game UI (find by name, SimulateClick, type) or checks the result.
    /// Lines after <c>cleanup:</c> always run. The run ends with one log line,
    /// <c>uitest: PASS name</c> or <c>uitest: FAIL name step N: reason</c>, which
    /// scripts/game-ui-test.sh waits for. Steps:
    /// <list type="bullet">
    /// <item>name NAME · show-buy SYMBOL · show-sell SYMBOL · close-dialog</item>
    /// <item>click CONTROL · type CONTROL TEXT · wait SECONDS · run-days N</item>
    /// <item>expect-visible CONTROL · expect-hidden CONTROL · expect-text CONTROL TEXT</item>
    /// <item>expect-slider CONTROL [max] (thumb drawn at the value) · $owned:SYMBOL in any step</item>
    /// <item>remember SYMBOL · expect-owned-delta SYMBOL N · expect-orders-delta N</item>
    /// <item>expect-log TEXT · capture LABEL · cancel-new-orders · sell-back SYMBOL N</item>
    /// </list>
    /// CONTROL is a component name; the open Invest dialog is searched first.
    /// </summary>
    public class UiTestRunner : MonoBehaviour
    {
        public const string TriggerName = "FinanceOverhaul.uitest";

        private float nextCheck;
        private bool running;

        private void Update()
        {
            if (running || Time.unscaledTime < nextCheck)
            {
                return;
            }

            nextCheck = Time.unscaledTime + 1f;
            try
            {
                string path = Path.Combine(DevCapture.Folder, TriggerName);
                if (!File.Exists(path))
                {
                    return;
                }

                string script = File.ReadAllText(path);
                File.Delete(path);
                running = true;
                StartCoroutine(Run(script));
            }
            catch (Exception ex)
            {
                running = false;
                ModLog.Warn("uitest: could not start: " + ex.Message);
            }
        }

        private sealed class Context
        {
            public string Name = "scenario";
            public int OrdersAtStart;
            public HashSet<int> OrdersBefore = new HashSet<int>();

            /// <summary>Bonds this scenario sold (cleanup redeems them).</summary>
            public readonly List<int> Bonds = new List<int>();

            /// <summary>The player's ticker choice, kept while a scenario switches it.</summary>
            public TickerDisplay? TickerBefore;

            /// <summary>The player's info view and "ticker in info views", kept while a scenario changes them.</summary>
            public InfoManager.InfoMode? InfoBefore;
            public InfoManager.SubInfoMode InfoSubBefore;
            public bool? InInfoViewsBefore;
            public bool? LiveTickerBefore;

            /// <summary>The player's LED brightness, kept while a scenario switches it.</summary>
            public LedBrightness? BrightnessBefore;

            /// <summary>Bonds the city owed when the scenario started (the player's).</summary>
            public readonly HashSet<int> BondsBefore = new HashSet<int>();
            public readonly Dictionary<string, uint> Owned = new Dictionary<string, uint>();
            public long LogStart;

            /// <summary>Newest order this scenario created; "$order" in a step expands to it.</summary>
            public int LastOrderId;
        }

        private IEnumerator Run(string script)
        {
            var ctx = new Context();
            ctx.LogStart = LogLength();
            foreach (TradeOrder o in OrderDesk.Snapshot())
            {
                ctx.OrdersBefore.Add(o.Id);
            }

            ctx.OrdersAtStart = ctx.OrdersBefore.Count;
            foreach (IssuedBond b in BondDesk.Snapshot().Bonds)
            {
                ctx.BondsBefore.Add(b.Id);
            }

            // Everything already open is the player's: frozen until the scenario ends, so a
            // scenario that advances days can never fill, expire or move it.
            OrderDesk.Freeze(ctx.OrdersBefore);
            ChirpNotices.Muted = true; // tests never chirp in the player's city
            var steps = new List<string>();
            var cleanup = new List<string>();
            List<string> target = steps;
            foreach (string raw in script.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                if (line == "cleanup:")
                {
                    target = cleanup;
                    continue;
                }

                target.Add(line);
            }

            string failure = null;
            for (int i = 0; i < steps.Count && failure == null; i++)
            {
                var result = new StepResult();
                yield return StartCoroutine(Step(steps[i], ctx, result));
                if (result.Error != null)
                {
                    failure = "step " + (i + 1) + " '" + steps[i] + "': " + result.Error;
                }
                else
                {
                    ModLog.Info("uitest: ok " + steps[i]);
                }
            }

            foreach (string line in cleanup)
            {
                var result = new StepResult();
                yield return StartCoroutine(Step(line, ctx, result));
                ModLog.Info(
                    "uitest: cleanup "
                        + line
                        + (result.Error == null ? " ok" : " FAILED: " + result.Error)
                );
            }

            OrderDesk.UnfreezeAll();
            BondDesk.ClearTestFinances();
            CityBondsSection.ForceShow = false;
            ChirpNotices.Muted = false;
            if (ctx.TickerBefore.HasValue && Mod.Settings != null)
            {
                Mod.Settings.TickerShows = ctx.TickerBefore.Value; // the player's choice
            }

            if (ctx.LiveTickerBefore.HasValue && Mod.Settings != null)
            {
                Mod.Settings.LiveTickerEnabled = ctx.LiveTickerBefore.Value;
            }

            if (ctx.InInfoViewsBefore.HasValue && Mod.Settings != null)
            {
                Mod.Settings.TickerInInfoViews = ctx.InInfoViewsBefore.Value;
            }

            if (ctx.InfoBefore.HasValue && InfoManager.exists)
            {
                InfoManager.instance.SetCurrentMode(ctx.InfoBefore.Value, ctx.InfoSubBefore);
            }

            if (ctx.BrightnessBefore.HasValue && Mod.Settings != null)
            {
                Mod.Settings.TickerBrightness = ctx.BrightnessBefore.Value;
            }
            ModLog.Info(
                failure == null
                    ? "uitest: PASS " + ctx.Name + " (" + steps.Count + " steps)"
                    : "uitest: FAIL " + ctx.Name + " " + failure
            );
            running = false;
        }

        private sealed class StepResult
        {
            public string Error;
        }

        private IEnumerator Step(string line, Context ctx, StepResult r)
        {
            foreach (TradeOrder o in OrderDesk.Snapshot())
            {
                if (!ctx.OrdersBefore.Contains(o.Id) && o.Id > ctx.LastOrderId)
                {
                    ctx.LastOrderId = o.Id;
                }
            }

            line = line.Replace("$order", ctx.LastOrderId.ToString(CultureInfo.InvariantCulture));
            if (line.Contains("$bond"))
            {
                // "$bond" — the newest bond this scenario sold (through the desk or the UI).
                int newest = 0;
                foreach (IssuedBond owed in BondDesk.Snapshot().Bonds)
                {
                    if (!ctx.BondsBefore.Contains(owed.Id) && owed.Id > newest)
                    {
                        newest = owed.Id;
                    }
                }

                line = line.Replace("$bond", newest.ToString(CultureInfo.InvariantCulture));
            }
            line = OwnedTokens(line);
            string[] w = line.Split(new[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);
            string cmd = w[0];
            string a = w.Length > 1 ? w[1] : null;
            string b = w.Length > 2 ? w[2] : null;
            switch (cmd)
            {
                case "name":
                    ctx.Name = line.Substring(5).Trim();
                    break;
                case "show-buy":
                case "show-sell":
                {
                    int id = Find(a);
                    if (id < 0)
                    {
                        r.Error = "no investment " + a;
                        break;
                    }

                    BuySellInvestmentPanel.ShowPanel(
                        (byte)id,
                        cmd == "show-buy"
                            ? BuySellInvestmentPanel.Mode.Buy
                            : BuySellInvestmentPanel.Mode.Sell
                    );
                    yield return new WaitForSeconds(0.3f);
                    break;
                }
                case "show-economy":
                {
                    // Opens Economy on the Investments tab (or `show-economy loans`), like the
                    // toolbar button.
                    FieldInfo tabField = AccessTools.Field(
                        typeof(EconomyPanel),
                        a == "loans" ? "kLoansTab" : "kInvestmentTab"
                    );
                    int tab = tabField == null ? 3 : (int)tabField.GetValue(null);
                    ToolsModifierControl.mainToolbar.ShowEconomyPanel(tab);
                    yield return new WaitForSeconds(1f);
                    break;
                }
                case "close-economy":
                {
                    // Closes the Economy panel a scenario opened with show-economy.
                    EconomyPanel economy = UnityEngine.Object.FindObjectOfType<EconomyPanel>();
                    if (economy != null && economy.component.isVisible)
                    {
                        // Same as the panel's close button (it animates out).
                        AccessTools
                            .Method(typeof(EconomyPanel), "OnCloseButton")
                            .Invoke(economy, null);
                    }

                    yield return new WaitForSeconds(0.8f);
                    break;
                }
                case "close-dialog":
                {
                    BuySellInvestmentPanel panel = BuySellInvestmentPanel.instance;
                    if (panel != null && panel.component.isVisible)
                    {
                        AccessTools
                            .Method(typeof(BuySellInvestmentPanel), "OnClosed")
                            .Invoke(panel, null);
                    }

                    yield return null;
                    break;
                }
                case "click":
                {
                    UIComponent c = FindControl(a);
                    if (c == null)
                    {
                        r.Error = "no visible control " + a;
                        break;
                    }

                    c.SimulateClick();
                    yield return null;
                    break;
                }
                case "type":
                {
                    UITextField f = FindControl(a) as UITextField;
                    if (f == null)
                    {
                        r.Error = "no visible text field " + a;
                        break;
                    }

                    // Like a player: focus, type, then tab out (applies the value).
                    f.Focus();
                    yield return null;
                    if (!f.hasFocus)
                    {
                        r.Error = a + " did not take focus";
                        break;
                    }

                    f.text = b ?? string.Empty;
                    yield return null;
                    f.Unfocus();
                    yield return null;
                    break;
                }
                case "set-text":
                {
                    // Simulates a stale label (e.g. left by an older build).
                    UIComponent c = FindControl(a);
                    if (c is UIButton)
                    {
                        ((UIButton)c).text = b ?? string.Empty;
                    }
                    else if (c is UILabel)
                    {
                        ((UILabel)c).text = b ?? string.Empty;
                    }
                    else
                    {
                        r.Error = "no visible label or button " + a;
                    }

                    yield return null;
                    break;
                }
                case "set-price":
                {
                    // TST only: the price from the next daily update on.
                    if (!string.Equals(a, "TST", StringComparison.OrdinalIgnoreCase))
                    {
                        r.Error = "set-price only drives the dev test stock TST";
                        break;
                    }

                    TestStockListing.SetPrice(ParseFloat(b, 0f));
                    break;
                }
                case "expect-price":
                {
                    int id = Find(a);
                    if (id < 0)
                    {
                        r.Error = "no investment " + a + " (is the dev test stock on?)";
                        break;
                    }

                    float mark = EconomyManager.instance.m_investments.m_buffer[id].value;
                    float want = ParseFloat(b, -1f);
                    if (Mathf.Abs(mark - want) > 0.01f)
                    {
                        r.Error = a + " price is " + mark + ", expected " + want;
                    }

                    break;
                }
                case "expect-graph-last":
                {
                    int id = Find(a);
                    if (id < 0)
                    {
                        r.Error = "no investment " + a;
                        break;
                    }

                    var graph = new float[Investment.kPastValueCount];
                    EconomyManager.instance.m_investments.m_buffer[id].FillGraphValues(graph);
                    float last = graph[graph.Length - 1];
                    if (Mathf.Abs(last - ParseFloat(b, -1f)) > 0.01f)
                    {
                        r.Error = a + " chart ends at " + last + ", expected " + b;
                    }

                    break;
                }
                case "dump":
                {
                    UIComponent c = FindControl(a, includeHidden: true);
                    if (c == null)
                    {
                        r.Error = "no control " + a;
                        break;
                    }

                    DevCapture.DumpTree(c, 0);
                    break;
                }
                case "share-charts":
                {
                    // share-charts on|off — the Options flag, for before/after captures.
                    if (Mod.Settings == null || (a != "on" && a != "off"))
                    {
                        r.Error = "share-charts needs on or off";
                        break;
                    }

                    Mod.Settings.ShareChartsEnabled = a == "on";
                    if (a == "off")
                    {
                        ShareChartOverlay.RemoveAll();
                    }

                    yield return null;
                    break;
                }
                case "inspect-parents":
                {
                    // Dev: the control and every parent — absolute box, background, clipping.
                    UIComponent c = FindControl(a, includeHidden: true);
                    if (c == null)
                    {
                        r.Error = "no control " + a;
                        break;
                    }

                    for (UIComponent p = c; p != null; p = p.parent)
                    {
                        var panel = p as UIPanel;
                        ModLog.Info(
                            "uitest: parent "
                                + p.name
                                + " <"
                                + p.GetType().Name
                                + "> abs="
                                + p.absolutePosition
                                + " size="
                                + p.size
                                + " bg='"
                                + (panel == null ? "" : panel.backgroundSprite)
                                + "' clip="
                                + p.clipChildren
                                + " padding="
                                + (
                                    panel == null || panel.autoLayoutPadding == null
                                        ? ""
                                        : panel.autoLayoutPadding.ToString()
                                )
                        );
                    }

                    break;
                }
                case "inspect":
                {
                    UIComponent c = FindControl(a, includeHidden: true);
                    if (c == null)
                    {
                        r.Error = "no control " + a;
                        break;
                    }

                    var t = c as UITextComponent;
                    ModLog.Info(
                        "uitest: inspect "
                            + a
                            + " <"
                            + c.GetType().Name
                            + "> pos="
                            + c.relativePosition
                            + " size="
                            + c.size
                            + (
                                t == null
                                    ? ""
                                    : " text='"
                                        + t.text
                                        + "' localeID='"
                                        + t.localeID
                                        + "' isLocalized="
                                        + t.isLocalized
                            )
                            + (
                                c is UILabel
                                    ? " prefix='"
                                        + ((UILabel)c).prefix
                                        + "' suffix='"
                                        + ((UILabel)c).suffix
                                        + "' autoSize="
                                        + ((UILabel)c).autoSize
                                    : ""
                            )
                            + " color="
                            + Rgba(c.color)
                            + (t == null ? "" : " textColor=" + Rgba(t.textColor))
                            + (
                                t == null || t.font == null
                                    ? ""
                                    : " font=" + t.font.name + " scale=" + t.textScale
                            )
                            + " shownOpacity="
                            + ShownOpacity(c).ToString("0.00")
                    );
                    break;
                }
                case "log-fonts":
                {
                    // Every UI font the game has loaded, for choosing a weight.
                    var names = new List<string>();
                    foreach (UIFont f in Resources.FindObjectsOfTypeAll<UIFont>())
                    {
                        if (f != null && !names.Contains(f.name))
                        {
                            names.Add(f.name);
                        }
                    }

                    names.Sort();
                    ModLog.Info("uitest: fonts " + string.Join(", ", names.ToArray()));
                    break;
                }
                case "game-colors":
                {
                    // The colours the game itself uses for money that went up, down or
                    // stayed (Economy panel), so mod UI inside that panel can be checked
                    // against them.
                    EconomyPanel economy = UnityEngine.Object.FindObjectOfType<EconomyPanel>();
                    if (economy == null)
                    {
                        r.Error = "no Economy panel";
                        break;
                    }

                    ModLog.Info(
                        "uitest: game-colors positive="
                            + Rgba(economy.m_positiveNumberColor)
                            + " negative="
                            + Rgba(economy.m_negativeNumberColor)
                            + " neutral="
                            + Rgba(economy.m_neutralNumberColor)
                    );
                    // "game-colors expect": the theme's copies must still be the game's values.
                    if (
                        a == "expect"
                        && (
                            !Same(economy.m_positiveNumberColor, FoTheme.GamePositive)
                            || !Same(economy.m_negativeNumberColor, FoTheme.GameNegative)
                            || !Same(economy.m_neutralNumberColor, FoTheme.GameValue)
                        )
                    )
                    {
                        r.Error =
                            "FoTheme no longer matches the game: theme positive="
                            + Rgba(FoTheme.GamePositive)
                            + " negative="
                            + Rgba(FoTheme.GameNegative)
                            + " value="
                            + Rgba(FoTheme.GameValue);
                    }

                    break;
                }
                case "who-covers":
                {
                    // Lists visible, mouse-interactive components whose box covers the
                    // control's centre (excluding the control's own ancestors).
                    UIComponent target = FindControl(a);
                    if (target == null)
                    {
                        r.Error = "no visible control " + a;
                        break;
                    }

                    Vector3 tp = target.absolutePosition;
                    Vector2 centre = new Vector2(
                        tp.x + target.width / 2f,
                        tp.y + target.height / 2f
                    );
                    var hits = new System.Text.StringBuilder();
                    foreach (UIComponent c in UnityEngine.Object.FindObjectsOfType<UIComponent>())
                    {
                        if (c == target || !IsShown(c) || !c.isInteractive || IsAncestor(c, target))
                        {
                            continue;
                        }

                        Vector3 p = c.absolutePosition;
                        if (
                            centre.x >= p.x
                            && centre.x <= p.x + c.width
                            && centre.y >= p.y
                            && centre.y <= p.y + c.height
                        )
                        {
                            hits.Append(c.name)
                                .Append(" <")
                                .Append(c.GetType().Name)
                                .Append("> parent=")
                                .Append(c.parent == null ? "-" : c.parent.name)
                                .Append(" z=")
                                .Append(c.zOrder)
                                .Append(" depth=")
                                .Append(c.renderOrder)
                                .Append(" box=")
                                .Append(p)
                                .Append(" ")
                                .Append(c.size)
                                .Append("; ");
                        }
                    }

                    ModLog.Info(
                        "uitest: who-covers "
                            + a
                            + " (renderOrder "
                            + target.renderOrder
                            + " at "
                            + centre
                            + "): "
                            + (hits.Length == 0 ? "nothing" : hits.ToString())
                    );
                    break;
                }
                case "log-history":
                {
                    int id = Find(a);
                    if (id < 0)
                    {
                        r.Error = "no investment " + a;
                        break;
                    }

                    Investment inv = EconomyManager.instance.m_investments.m_buffer[id];
                    var q = inv.allValues;
                    var sb = new System.Text.StringBuilder();
                    for (int k = 0; q != null && k < q.Count && k < 10; k++)
                    {
                        sb.Append(q.GetFromTop(k).ToString("0.##")).Append(' ');
                    }

                    var graph = new float[Investment.kPastValueCount];
                    inv.FillGraphValues(graph);
                    ModLog.Info(
                        "uitest: "
                            + a
                            + " value="
                            + inv.value
                            + " newest-first: "
                            + sb
                            + "| graph last 3: "
                            + graph[graph.Length - 3].ToString("0.##")
                            + " "
                            + graph[graph.Length - 2].ToString("0.##")
                            + " "
                            + graph[graph.Length - 1].ToString("0.##")
                    );
                    break;
                }
                case "wait":
                    yield return new WaitForSeconds(ParseFloat(a, 1f));
                    break;
                case "log-basket":
                {
                    // Dev audit: every created investment's daily history (oldest first),
                    // availability and symbol, for checking the Index math offline.
                    Investment[] buf = EconomyManager.instance.m_investments.m_buffer;
                    for (int i = 0; i < buf.Length; i++)
                    {
                        InvestmentInfo info = buf[i].Info;
                        if (
                            (buf[i].m_flags & Investment.Flags.Created) == 0
                            || info == null
                            || info.m_investmentAI == null
                        )
                        {
                            continue;
                        }

                        Investment.Queue<float> h = buf[i].allValues;
                        int n = h == null ? 0 : h.Count;
                        var sb = new System.Text.StringBuilder();
                        for (int k = n - 1; k >= 0; k--)
                        {
                            sb.Append(
                                    h.GetFromTop(k).ToString("0.####", CultureInfo.InvariantCulture)
                                )
                                .Append(k > 0 ? "," : "");
                        }

                        ModLog.Info(
                            "basket: "
                                + StockSymbols.For(info)
                                + "|"
                                + info.name
                                + "|available="
                                + info.m_investmentAI.IsAvailable((byte)i, ref buf[i])
                                + "|"
                                + sb
                        );
                    }

                    break;
                }
                case "log-symbols":
                    StockSymbols.Dump();
                    break;
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
                case "expect-opacity":
                {
                    // expect-opacity CONTROL on|off — a fade or overlay is (fully) shown or hidden.
                    UIComponent c = FindControl(a, includeHidden: true);
                    if (c == null)
                    {
                        r.Error = "no control " + a;
                        break;
                    }

                    bool on = c.isVisible && c.opacity > 0.99f;
                    bool off = !c.isVisible || c.opacity < 0.01f;
                    if (b == "on" ? !on : !off)
                    {
                        r.Error =
                            a + " opacity is " + c.opacity.ToString("0.00") + ", expected " + b;
                    }

                    break;
                }
                case "monitor-sort":
                {
                    // monitor-sort Change|Symbol|Price|ValueHeld — natural direction (restore with
                    // monitor restore).
                    MonitorSort sort;
                    try
                    {
                        sort = (MonitorSort)Enum.Parse(typeof(MonitorSort), a, true);
                    }
                    catch (ArgumentException)
                    {
                        r.Error = "no sort " + a;
                        break;
                    }

                    Mod.Settings.Monitor.Sort = sort;
                    Mod.Settings.Monitor.SortReversed = false;
                    yield return new WaitForSeconds(1.2f); // the window refreshes on its own tick
                    break;
                }
                case "monitor-change":
                {
                    // monitor-change percent|currency — what the pills show (monitor restore).
                    Mod.Settings.Monitor.ChangeAsCurrency = a == "currency";
                    yield return new WaitForSeconds(1.2f);
                    break;
                }
                case "monitor-compact":
                {
                    // monitor-compact on|off — the Compact rows setting (monitor restore).
                    Mod.Settings.Monitor.CompactRows = a == "on";
                    yield return new WaitForSeconds(1.2f);
                    break;
                }
                case "expect-monitor-pills-desc":
                {
                    // The visible ₡ pills, top to bottom, never increase (Sort: Change ₡).
                    var pills = new List<UIComponent>();
                    foreach (UIComponent c in UnityEngine.Object.FindObjectsOfType<UIComponent>())
                    {
                        if (
                            c.name != null
                            && c.name.StartsWith(
                                "FinanceOverhaulStockMonitorChange-",
                                StringComparison.Ordinal
                            )
                            && IsShown(c)
                        )
                        {
                            pills.Add(c);
                        }
                    }

                    pills.Sort((x, y) => x.absolutePosition.y.CompareTo(y.absolutePosition.y));
                    var shown = new List<string>();
                    double last = double.MaxValue;
                    foreach (UIComponent c in pills)
                    {
                        string t = TextOf(c) ?? "";
                        shown.Add(t);
                        if (t == "--")
                        {
                            continue;
                        }

                        string digits = t.Replace("+", "")
                            .Replace("−", "-")
                            .Replace("₡", "")
                            .Replace(",", "");
                        double v;
                        if (
                            !double.TryParse(
                                digits,
                                System.Globalization.NumberStyles.Float,
                                CultureInfo.InvariantCulture,
                                out v
                            )
                        )
                        {
                            r.Error = "pill '" + t + "' is not ₡";
                            break;
                        }

                        if (v > last + 1e-9)
                        {
                            r.Error = "pills rise: " + string.Join(" ", shown.ToArray());
                            break;
                        }

                        last = v;
                    }

                    ModLog.Info("uitest: monitor pills " + string.Join(" ", shown.ToArray()));
                    break;
                }
                case "expect-monitor-order":
                {
                    // expect-monitor-order az|za — the visible rows' symbols, top to bottom.
                    var shown = new List<UILabel>();
                    foreach (UILabel l in UnityEngine.Object.FindObjectsOfType<UILabel>())
                    {
                        if (
                            l.name != null
                            && l.name.StartsWith(
                                "FinanceOverhaulStockMonitorSymbol-",
                                StringComparison.Ordinal
                            )
                            && IsShown(l)
                        )
                        {
                            shown.Add(l);
                        }
                    }

                    shown.Sort((x, y) => x.absolutePosition.y.CompareTo(y.absolutePosition.y));
                    var symbols = new List<string>();
                    foreach (UILabel l in shown)
                    {
                        symbols.Add(l.text);
                    }

                    ModLog.Info("uitest: monitor order " + string.Join(" ", symbols.ToArray()));
                    for (int i = 1; i < symbols.Count; i++)
                    {
                        int c = string.CompareOrdinal(symbols[i - 1], symbols[i]);
                        if (a == "az" ? c > 0 : c < 0)
                        {
                            r.Error =
                                "rows are not " + a + ": " + string.Join(" ", symbols.ToArray());
                            break;
                        }
                    }

                    if (symbols.Count < 2)
                    {
                        r.Error = "need at least two rows, got " + symbols.Count;
                    }

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
                case "notices":
                {
                    // notices on|off — let this scenario's notices reach Chirper (muted by default).
                    ChirpNotices.Muted = a != "on";
                    break;
                }
                case "pause":
                {
                    // pause on|off — pauses or resumes the simulation (for tests that span a mod
                    // reload; a second scenario must resume it).
                    SimulationManager.instance.SimulationPaused = a == "on";
                    yield return new WaitForSeconds(0.5f);
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
                case "bonds-section":
                {
                    // bonds-section show|auto — shows the Loans tab's City bonds section even
                    // with the Options flag off (auto: back to the flag).
                    CityBondsSection.ForceShow = a == "show";
                    yield return new WaitForSeconds(0.8f);
                    break;
                }
                case "bonds-amount":
                case "bonds-term":
                {
                    // bonds-amount TEXT · bonds-term YEARS — fills the section's amount or term.
                    if (CityBondsSection.Current == null)
                    {
                        r.Error = "no City bonds section";
                        break;
                    }

                    if (cmd == "bonds-amount")
                    {
                        CityBondsSection.Current.SetAmount(a);
                    }
                    else
                    {
                        CityBondsSection.Current.SetTerm((BondTerm)(int)ParseFloat(a, 5f));
                    }

                    yield return new WaitForSeconds(0.3f);
                    break;
                }
                case "bond-finances":
                {
                    // bond-finances DEBT INCOME — fixes the city's debt and annual income (₡)
                    // for bond pricing until the scenario ends; "real" goes back to the city's
                    // own; no argument logs what pricing sees now.
                    if (a == "real")
                    {
                        BondDesk.ClearTestFinances();
                    }
                    else if (!string.IsNullOrEmpty(a))
                    {
                        long setDebt,
                            setIncome;
                        if (
                            !long.TryParse(
                                a.Replace(",", string.Empty),
                                NumberStyles.Integer,
                                CultureInfo.InvariantCulture,
                                out setDebt
                            )
                            || !long.TryParse(
                                (b ?? string.Empty).Replace(",", string.Empty),
                                NumberStyles.Integer,
                                CultureInfo.InvariantCulture,
                                out setIncome
                            )
                        )
                        {
                            r.Error = "bond-finances DEBT INCOME (whole ₡) or bond-finances real";
                            break;
                        }

                        BondDesk.SetTestFinances(setDebt * 100, setIncome * 100);
                    }

                    long debt,
                        income;
                    BondDesk.CityFinances(out debt, out income);
                    ModLog.Info(
                        "uitest: bond-finances debt="
                            + debt
                            + " annualIncome="
                            + income
                            + " (cents)"
                    );
                    break;
                }
                case "issue-bond":
                {
                    // issue-bond YEARS FACE₡ — sells a bond at the offered rate with the
                    // simulation paused; the city's cash must rise by exactly the face.
                    long face = (long)ParseFloat(b, 0f) * 100;
                    SimulationManager sim = SimulationManager.instance;
                    bool paused = sim.SimulationPaused;
                    sim.SimulationPaused = true;
                    yield return new WaitForSeconds(0.5f);
                    long before = RawCash(EconomyManager.instance);
                    BondOffer offer;
                    IssuedBond bond = BondDesk.Issue(
                        (BondTerm)(int)ParseFloat(a, 1f),
                        face,
                        out offer
                    );
                    long delta = RawCash(EconomyManager.instance) - before;
                    sim.SimulationPaused = paused;
                    if (bond == null)
                    {
                        r.Error = "bond refused: " + offer.Reason;
                        break;
                    }

                    ctx.Bonds.Add(bond.Id);
                    ModLog.Info(
                        "uitest: issued bond #"
                            + bond.Id
                            + " at "
                            + BondPricing.RateText(offer.RateBasisPoints)
                            + ", cash +"
                            + delta
                            + " cents"
                    );
                    if (delta != face)
                    {
                        r.Error = "cash rose by " + delta + " cents, expected " + face;
                    }

                    break;
                }
                case "bond-backdate":
                {
                    // bond-backdate DAYS — moves this scenario's newest bond's schedule earlier,
                    // so the next coupon falls due without waiting a game month.
                    if (
                        ctx.Bonds.Count == 0
                        || !BondDesk.Backdate(
                            ctx.Bonds[ctx.Bonds.Count - 1],
                            (int)ParseFloat(a, 0f)
                        )
                    )
                    {
                        r.Error = "issue-bond first";
                    }

                    break;
                }
                case "stress-stocks":
                {
                    // stress-stocks add N | fill TOTAL | clear — dev stress listings (scenario 51):
                    // adds N copies of a share stock (or enough to reach TOTAL investments), each
                    // asking the game for a slot, or frees them all. Needs the dev test stock
                    // option (test saves only).
                    if (!StressStocks.Allowed)
                    {
                        r.Error = "turn on the dev test stock option (test save) first";
                        break;
                    }

                    if (a == "clear")
                    {
                        StressStocks.ClearSlots();
                        yield return new WaitForSeconds(1f);
                        StressStocks.NotifyInvestmentsChanged();
                        StressStocks.DestroyPrefabs();
                        yield return new WaitForSeconds(0.5f);
                    }
                    else if (a == "slots")
                    {
                        SimulationManager.instance.AddAction(StressStocks.LogSlots);
                        yield return new WaitForSeconds(1f);
                    }
                    else
                    {
                        int n = (int)ParseFloat(b, 0f);
                        if (a == "fill")
                        {
                            n -= StressStocks.SlotsUsed();
                        }

                        if (n > 0)
                        {
                            StressStocks.Add(n);
                        }

                        yield return new WaitForSeconds(1.5f);
                        StressStocks.NotifyInvestmentsChanged();
                        yield return new WaitForSeconds(0.5f);
                    }

                    break;
                }
                case "stress-measure":
                {
                    // stress-measure SECONDS — frame time and the daily investment update cost
                    // while the city runs, logged as "stress: measure …".
                    float seconds = ParseFloat(a, 5f);
                    StressStocks.ResetStepTiming();
                    StressStocks.Measuring = true;
                    float end = Time.unscaledTime + seconds;
                    int frames = 0;
                    float sum = 0f,
                        max = 0f;
                    while (Time.unscaledTime < end)
                    {
                        yield return null;
                        float dt = Time.unscaledDeltaTime * 1000f;
                        frames++;
                        sum += dt;
                        max = Mathf.Max(max, dt);
                    }

                    StressStocks.Measuring = false;
                    ModLog.Info(
                        "stress: measure "
                            + seconds
                            + " s with "
                            + StressStocks.SlotsUsed()
                            + " slots: frames avg "
                            + (frames == 0 ? 0f : sum / frames).ToString("0.0")
                            + " ms, max "
                            + max.ToString("0.0")
                            + " ms; "
                            + StressStocks.StepSummary()
                    );
                    break;
                }
                case "scroll-investments":
                {
                    // scroll-investments bottom — scrolls Economy → Investments' list of stocks
                    // to buy to its end (the vanilla list, found by the panel's own field).
                    EconomyPanel economy = UnityEngine.Object.FindObjectOfType<EconomyPanel>();
                    UIScrollablePanel listPanel =
                        economy == null
                            ? null
                            : AccessTools
                                .Field(typeof(EconomyPanel), "m_investmentsScrollablePanel")
                                .GetValue(economy) as UIScrollablePanel;
                    if (listPanel == null)
                    {
                        r.Error = "open Economy → Investments first";
                        break;
                    }

                    listPanel.ScrollToBottom();
                    yield return new WaitForSeconds(0.5f);
                    break;
                }
                case "count-investment-rows":
                {
                    // count-investment-rows — logs how many rows Economy → Investments shows for
                    // stocks to buy, against the number of investments.
                    EconomyPanel economy = UnityEngine.Object.FindObjectOfType<EconomyPanel>();
                    System.Collections.IDictionary rows =
                        economy == null
                            ? null
                            : AccessTools
                                .Field(typeof(EconomyPanel), "m_purchasableInvestments")
                                .GetValue(economy) as System.Collections.IDictionary;
                    if (rows == null)
                    {
                        r.Error = "open Economy → Investments first";
                        break;
                    }

                    int total = 0,
                        visible = 0;
                    foreach (System.Collections.DictionaryEntry e in rows)
                    {
                        total++;
                        UIComponent row = e.Value as UIComponent;
                        if (row != null && row.isVisible)
                        {
                            visible++;
                        }
                    }

                    ModLog.Info(
                        "investments panel: "
                            + visible
                            + " visible row(s) for "
                            + StressStocks.SlotsUsed()
                            + " investments ("
                            + total
                            + " built)"
                    );
                    break;
                }
                case "stress-panel":
                {
                    // stress-panel — opens Economy → Investments and logs how long that takes
                    // (the call itself and the worst frame in the next second), then closes it.
                    FieldInfo invTab = AccessTools.Field(typeof(EconomyPanel), "kInvestmentTab");
                    int tabIndex = invTab == null ? 3 : (int)invTab.GetValue(null);
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    ToolsModifierControl.mainToolbar.ShowEconomyPanel(tabIndex);
                    double openMs = sw.Elapsed.TotalMilliseconds;
                    float end = Time.unscaledTime + 1f;
                    float worst = 0f;
                    while (Time.unscaledTime < end)
                    {
                        yield return null;
                        worst = Mathf.Max(worst, Time.unscaledDeltaTime * 1000f);
                    }

                    ModLog.Info(
                        "stress: Investments panel with "
                            + StressStocks.SlotsUsed()
                            + " slots: open "
                            + openMs.ToString("0.0")
                            + " ms, worst frame "
                            + worst.ToString("0.0")
                            + " ms"
                    );
                    break;
                }
                case "away":
                {
                    // away DAYS — pretends the mod was off this many days: this scenario's bonds
                    // missed their payments and the Welcome back dialog should ask.
                    if (ctx.Bonds.Count == 0)
                    {
                        r.Error = "issue-bond first";
                        break;
                    }

                    BondDesk.SimulateAway((int)ParseFloat(a, 0f), ctx.Bonds);
                    yield return new WaitForSeconds(1f);
                    break;
                }
                case "welcome-back":
                {
                    // welcome-back pay|skip|close — answers the Welcome back dialog (close clicks
                    // ×, which starts fresh).
                    if (WelcomeBackDialog.Current == null || !WelcomeBackDialog.Current.IsShown)
                    {
                        r.Error = "Welcome back dialog not shown";
                        break;
                    }

                    if (a == "close")
                    {
                        WelcomeBackDialog.Current.ClickClose();
                    }
                    else
                    {
                        WelcomeBackDialog.Current.Choose(a == "pay");
                    }

                    yield return new WaitForSeconds(0.5f);
                    break;
                }
                case "expect-welcome-back":
                {
                    // expect-welcome-back shown|hidden
                    bool shown =
                        WelcomeBackDialog.Current != null && WelcomeBackDialog.Current.IsShown;
                    if (shown != (a == "shown"))
                    {
                        r.Error = "Welcome back dialog " + (shown ? "shown" : "hidden");
                    }

                    break;
                }
                case "expect-bond-coupons":
                {
                    // expect-bond-coupons N — coupons paid on this scenario's newest bond.
                    IssuedBond bond =
                        ctx.Bonds.Count == 0
                            ? null
                            : BondDesk.Snapshot().Find(ctx.Bonds[ctx.Bonds.Count - 1]);
                    int paid = bond == null ? -1 : bond.CouponsPaid;
                    if (paid != (int)ParseFloat(a, 0f))
                    {
                        r.Error = "bond has " + paid + " coupon(s) paid, expected " + a;
                    }

                    break;
                }
                case "redeem-new-bonds":
                    // Repays every bond this scenario sold (by the desk or the UI), never the
                    // player's.
                    foreach (IssuedBond owed in BondDesk.Snapshot().Bonds)
                    {
                        if (!ctx.BondsBefore.Contains(owed.Id))
                        {
                            BondDesk.Redeem(owed.Id);
                        }
                    }

                    ctx.Bonds.Clear();
                    break;
                case "money-probe":
                {
                    // money-probe AMOUNT — measures the unit of the game's budget calls: with the
                    // simulation paused, adds AMOUNT through the loan-taken line, reads the city's
                    // cash, then fetches the same AMOUNT back through the loan-payment line. Fails
                    // unless cash returns exactly to where it started.
                    int amount = (int)ParseFloat(a, 100f);
                    SimulationManager sim = SimulationManager.instance;
                    bool paused = sim.SimulationPaused;
                    sim.SimulationPaused = true;
                    yield return new WaitForSeconds(0.5f); // let a running step finish
                    EconomyManager em = EconomyManager.instance;
                    long cash0 = RawCash(em),
                        money0 = EconomyManager.playerMoney;
                    int added = em.AddResource(
                        EconomyManager.Resource.LoanAmount,
                        amount,
                        ItemClass.Service.None,
                        ItemClass.SubService.None,
                        ItemClass.Level.None
                    );
                    long cash1 = RawCash(em),
                        money1 = EconomyManager.playerMoney;
                    int fetched = em.FetchResource(
                        EconomyManager.Resource.LoanPayment,
                        amount,
                        ItemClass.Service.None,
                        ItemClass.SubService.None,
                        ItemClass.Level.None
                    );
                    long cash2 = RawCash(em),
                        money2 = EconomyManager.playerMoney;
                    sim.SimulationPaused = paused;
                    ModLog.Info(
                        "uitest: money-probe amount="
                            + amount
                            + " added="
                            + added
                            + " fetched="
                            + fetched
                            + " raw "
                            + cash0
                            + "→"
                            + cash1
                            + "→"
                            + cash2
                            + " playerMoney "
                            + money0
                            + "→"
                            + money1
                            + "→"
                            + money2
                    );
                    if (cash2 != cash0)
                    {
                        r.Error = "cash did not return: " + cash0 + " → " + cash2;
                    }

                    break;
                }
                case "hover":
                {
                    // hover CONTROL FX FY — simulated mouse at that fraction of the control
                    // (Stock Monitor hover); "hover off" returns to the real mouse.
                    if (a == "off")
                    {
                        StockMonitorHost.TestMouse = null;
                        break;
                    }

                    UIComponent target = FindControl(a);
                    string[] f = line.Split(' ');
                    if (target == null || f.Length < 4)
                    {
                        r.Error = "hover needs a visible control and two fractions";
                        break;
                    }

                    float fx = ParseFloat(f[2], 0.5f),
                        fy = ParseFloat(f[3], 0.5f);
                    Vector3 p = target.absolutePosition;
                    StockMonitorHost.TestMouse = new Vector2(
                        p.x + fx * target.width,
                        p.y + fy * target.height
                    );
                    yield return null;
                    yield return null;
                    break;
                }
                case "monitor":
                {
                    // monitor remember|restore — snapshot the player's monitor settings so a
                    // scenario can change them and put them back exactly.
                    if (a == "remember" || a == "restore")
                    {
                        if (Mod.Settings == null)
                        {
                            r.Error = "no settings loaded";
                            break;
                        }

                        if (a == "remember")
                        {
                            var sb = new System.Text.StringBuilder();
                            Mod.Settings.Monitor.Write(sb);
                            rememberedMonitor = sb.ToString();
                        }
                        else if (rememberedMonitor != null)
                        {
                            foreach (string kv in rememberedMonitor.Split('\n'))
                            {
                                int eq = kv.IndexOf('=');
                                if (eq > 0)
                                {
                                    Mod.Settings.Monitor.TryRead(
                                        kv.Substring(0, eq),
                                        kv.Substring(eq + 1)
                                    );
                                }
                            }

                            Mod.SaveSettings();
                            if (StockMonitorHost.Current != null)
                            {
                                StockMonitorHost.Current.ApplyFlag();
                            }
                        }

                        break;
                    }

                    // monitor open|close — sets the Stock Monitor window outright.
                    if (StockMonitorHost.Current == null || (a != "open" && a != "close"))
                    {
                        r.Error = "monitor needs open or close (and the monitor enabled)";
                        break;
                    }

                    StockMonitorHost.Current.SetOpen(a == "open");
                    yield return new WaitForSeconds(0.6f);
                    break;
                }
                case "set-flag":
                {
                    // set-flag <OrderKind | all> on|off — flips the Options feature flag.
                    bool on = b == "on";
                    if (b != "on" && b != "off")
                    {
                        r.Error = "set-flag needs on or off";
                        break;
                    }

                    bool found = false;
                    foreach (OrderKind kind in ModSettings.OrderKinds)
                    {
                        if (a == "all" || kind.ToString() == a)
                        {
                            ModOptions.OnOrderKindChanged(kind, on);
                            found = true;
                        }
                    }

                    if (!found)
                    {
                        r.Error = "no order type " + a;
                    }

                    yield return new WaitForSeconds(0.6f); // cards refresh every 0.5 s
                    break;
                }
                case "run-days":
                {
                    int days = (int)ParseFloat(a, 1f);
                    SimulationManager sim = SimulationManager.instance;
                    bool paused = sim.SimulationPaused;
                    int start = OrderDesk.Today;
                    sim.SimulationPaused = false;
                    float deadline = Time.unscaledTime + 30f * days + 10f;
                    while (OrderDesk.Today < start + days && Time.unscaledTime < deadline)
                    {
                        yield return new WaitForSeconds(0.25f);
                    }

                    // One more beat so the daily postfix (orders) has run.
                    yield return new WaitForSeconds(1f);
                    sim.SimulationPaused = paused;
                    if (OrderDesk.Today < start + days)
                    {
                        r.Error = "game time did not advance " + days + " day(s)";
                    }

                    break;
                }
                case "expect-visible":
                case "expect-hidden":
                {
                    UIComponent c = FindControl(a, includeHidden: true);
                    bool visible = c != null && c.isVisibleSelf && IsShown(c);
                    if (visible != (cmd == "expect-visible"))
                    {
                        r.Error = a + (visible ? " is visible" : " is not visible");
                    }

                    break;
                }
                case "expect-no-overflow":
                {
                    // Optional CONTROL: check inside that panel instead of the Invest dialog.
                    UIComponent root;
                    if (a != null)
                    {
                        root = FindControl(a);
                    }
                    else
                    {
                        BuySellInvestmentPanel dialog = BuySellInvestmentPanel.instance;
                        root =
                            dialog != null && dialog.component.isVisible ? dialog.component : null;
                    }

                    if (root == null)
                    {
                        r.Error = a == null ? "no open Invest dialog" : "no visible " + a;
                        break;
                    }

                    UIComponent offender = FirstOverflow(root, root);
                    if (offender != null)
                    {
                        r.Error =
                            offender.name
                            + " ("
                            + TextOf(offender)
                            + ") at "
                            + offender.absolutePosition
                            + " size "
                            + offender.size
                            + " leaves "
                            + root.name
                            + " at "
                            + root.absolutePosition
                            + " size "
                            + root.size;
                    }

                    break;
                }
                case "scroll-bottom":
                {
                    UIScrollablePanel scroll = FindControl(a) as UIScrollablePanel;
                    if (scroll == null)
                    {
                        r.Error = "no scrolling list " + a;
                        break;
                    }

                    scroll.ScrollToBottom();
                    yield return new WaitForSeconds(0.3f);
                    ModLog.Info(
                        "uitest: "
                            + a
                            + " scrollPosition="
                            + scroll.scrollPosition
                            + " view="
                            + scroll.CalculateViewSize()
                            + " size="
                            + scroll.size
                    );
                    break;
                }
                case "expect-at-bottom":
                {
                    // CONTROL (a scrolling list) is long enough to scroll and sits at its end.
                    UIScrollablePanel scroll = FindControl(a) as UIScrollablePanel;
                    if (scroll == null)
                    {
                        r.Error = "no scrolling list " + a;
                        break;
                    }

                    float max =
                        scroll.CalculateViewSize().y
                        - (scroll.height - scroll.scrollPadding.vertical);
                    if (max <= 0f)
                    {
                        r.Error = a + " does not scroll (content fits)";
                    }
                    else if (scroll.scrollPosition.y < max - 1f)
                    {
                        r.Error = a + " is at " + scroll.scrollPosition.y + ", its end is " + max;
                    }

                    break;
                }
                case "scroll-into-view":
                {
                    // Like a player scrolling until CONTROL shows (CONTROL may sit inside a
                    // list entry): find the scrolling list above it and scroll by the offset.
                    UIComponent c = FindControl(a);
                    UIScrollablePanel scroll = null;
                    for (
                        UIComponent p = c == null ? null : c.parent;
                        p != null && scroll == null;
                        p = p.parent
                    )
                    {
                        scroll = p as UIScrollablePanel;
                    }

                    if (scroll == null)
                    {
                        r.Error = a + " is not inside a scrolling list";
                        break;
                    }

                    float top = c.absolutePosition.y - scroll.absolutePosition.y;
                    float below = top + c.height - scroll.height;
                    Vector2 pos = scroll.scrollPosition;
                    if (below > 0f)
                    {
                        pos.y += below;
                    }
                    else if (top < 0f)
                    {
                        pos.y += top;
                    }

                    scroll.scrollPosition = pos;
                    yield return new WaitForSeconds(0.3f);
                    break;
                }
                case "expect-prev-has":
                {
                    // The entry before CONTROL in its list contains a label with TEXT.
                    UIComponent c = FindControl(a);
                    UIComponent parent = c == null ? null : c.parent;
                    if (parent == null)
                    {
                        r.Error = "no visible control " + a;
                        break;
                    }

                    int index = -1;
                    for (int k = 0; k < parent.components.Count; k++)
                    {
                        if (parent.components[k] == c)
                        {
                            index = k;
                        }
                    }

                    UIComponent prev = index > 0 ? parent.components[index - 1] : null;
                    bool has = false;
                    if (prev != null)
                    {
                        foreach (UILabel l in prev.GetComponentsInChildren<UILabel>())
                        {
                            has |= l.text != null && l.text.Contains(b ?? string.Empty);
                        }
                    }

                    if (!has)
                    {
                        r.Error = "the entry before " + a + " has no '" + b + "'";
                    }

                    break;
                }
                case "expect-in-view":
                {
                    // CONTROL's box must overlap CONTAINER's box (e.g. not scrolled out of a list).
                    UIComponent c1 = FindControl(a);
                    UIComponent c2 = FindControl(b);
                    if (c1 == null || c2 == null)
                    {
                        r.Error = "need two visible controls";
                        break;
                    }

                    Vector3 p1 = c1.absolutePosition,
                        p2 = c2.absolutePosition;
                    bool inView =
                        p1.x < p2.x + c2.width
                        && p2.x < p1.x + c1.width
                        && p1.y < p2.y + c2.height
                        && p2.y < p1.y + c1.height;
                    if (!inView)
                    {
                        r.Error =
                            a
                            + " at "
                            + p1
                            + " "
                            + c1.size
                            + " is outside "
                            + b
                            + " at "
                            + p2
                            + " "
                            + c2.size;
                    }

                    break;
                }
                case "expect-no-overlap":
                {
                    UIComponent c1 = FindControl(a);
                    UIComponent c2 = FindControl(b);
                    if (c1 == null || c2 == null)
                    {
                        r.Error = "need two visible controls";
                        break;
                    }

                    Vector3 p1 = c1.absolutePosition,
                        p2 = c2.absolutePosition;
                    bool overlap =
                        p1.x < p2.x + c2.width
                        && p2.x < p1.x + c1.width
                        && p1.y < p2.y + c2.height
                        && p2.y < p1.y + c1.height;
                    if (overlap)
                    {
                        r.Error =
                            a
                            + " at "
                            + p1
                            + " "
                            + c1.size
                            + " overlaps "
                            + b
                            + " at "
                            + p2
                            + " "
                            + c2.size;
                    }

                    break;
                }
                case "expect-slider":
                {
                    // expect-slider CONTROL [max]: the thumb is drawn where the value is
                    // (and, with max, the value is the slider's maximum).
                    UIComponent c = FindControl(a);
                    UISlider s = c as UISlider;
                    if (s == null && c != null)
                    {
                        s = c.GetComponentInChildren<UISlider>();
                    }

                    if (s == null || s.thumbObject == null)
                    {
                        r.Error = "no visible slider in " + a;
                        break;
                    }

                    float drawn = SliderThumb.DrawnFraction(
                        s.thumbObject.absolutePosition.x - s.absolutePosition.x,
                        s.thumbObject.width,
                        s.thumbOffset.x,
                        s.width
                    );
                    ModLog.Info(
                        "uitest: slider value="
                            + s.value
                            + " min="
                            + s.minValue
                            + " max="
                            + s.maxValue
                            + " thumb="
                            + drawn.ToString("0.000", CultureInfo.InvariantCulture)
                    );
                    if (!SliderThumb.Matches(drawn, s.value, s.minValue, s.maxValue))
                    {
                        r.Error =
                            "thumb drawn at "
                            + drawn.ToString("0.000", CultureInfo.InvariantCulture)
                            + " of the track, value "
                            + s.value
                            + " in "
                            + s.minValue
                            + ".."
                            + s.maxValue
                            + " belongs at "
                            + SliderThumb
                                .Fraction(s.value, s.minValue, s.maxValue)
                                .ToString("0.000", CultureInfo.InvariantCulture);
                    }
                    else if (b == "max" && !Mathf.Approximately(s.value, s.maxValue))
                    {
                        r.Error = "slider value " + s.value + " is not its maximum " + s.maxValue;
                    }

                    break;
                }
                case "expect-text-is":
                {
                    UIComponent c = FindControl(a);
                    string text = TextOf(c);
                    if (text != (b ?? string.Empty))
                    {
                        r.Error = a + " text is '" + text + "', expected exactly '" + b + "'";
                    }

                    break;
                }
                case "expect-text":
                {
                    UIComponent c = FindControl(a);
                    string text = TextOf(c);
                    if (
                        text == null
                        || text.IndexOf(b ?? string.Empty, StringComparison.Ordinal) < 0
                    )
                    {
                        r.Error = a + " text is '" + text + "', expected to contain '" + b + "'";
                    }

                    break;
                }
                case "remember":
                {
                    int id = Find(a);
                    if (id < 0)
                    {
                        r.Error = "no investment " + a;
                        break;
                    }

                    ctx.Owned[a] = Owned(id);
                    break;
                }
                case "expect-owned-delta":
                {
                    int id = Find(a);
                    uint before;
                    if (id < 0 || !ctx.Owned.TryGetValue(a, out before))
                    {
                        r.Error = "remember " + a + " first";
                        break;
                    }

                    long delta = (long)Owned(id) - before;
                    if (delta != (long)ParseFloat(b, 0f))
                    {
                        r.Error = a + " owned changed by " + delta + ", expected " + b;
                    }

                    break;
                }
                case "expect-orders-delta":
                {
                    // Only orders this scenario created (others may fill or expire meanwhile).
                    long created = 0;
                    foreach (TradeOrder o in OrderDesk.Snapshot())
                    {
                        if (!ctx.OrdersBefore.Contains(o.Id))
                        {
                            created++;
                        }
                    }

                    if (created != (long)ParseFloat(a, 0f))
                    {
                        r.Error = "this scenario has " + created + " open order(s), expected " + a;
                    }

                    break;
                }
                case "expect-log":
                {
                    string want = line.Substring(cmd.Length).Trim();
                    if (!LogSince(ctx.LogStart).Contains(want))
                    {
                        r.Error = "log has no '" + want + "' since the scenario started";
                    }

                    break;
                }
                case "capture":
                {
                    string file = Path.Combine(
                        DevCapture.Folder,
                        "FinanceOverhaul-uitest-" + a + ".png"
                    );
                    Application.CaptureScreenshot(file);
                    yield return new WaitForSeconds(0.5f);
                    break;
                }
                case "freeze-new-orders":
                {
                    // Treat this scenario's open orders like the player's (proves freezing).
                    var mine = new List<int>();
                    foreach (TradeOrder o in OrderDesk.Snapshot())
                    {
                        if (!ctx.OrdersBefore.Contains(o.Id))
                        {
                            mine.Add(o.Id);
                        }
                    }

                    OrderDesk.Freeze(mine);
                    break;
                }
                case "cancel-new-orders":
                    foreach (TradeOrder o in OrderDesk.Snapshot())
                    {
                        if (!ctx.OrdersBefore.Contains(o.Id))
                        {
                            OrderDesk.Cancel(o.Id);
                        }
                    }

                    break;
                case "sell-back":
                {
                    int id = Find(a);
                    uint n = (uint)ParseFloat(b, 0f);
                    if (id >= 0 && n > 0)
                    {
                        Investment[] buffer = EconomyManager.instance.m_investments.m_buffer;
                        InvestmentAI ai = buffer[id].Info.m_investmentAI;
                        ai.SellShares((byte)id, ref buffer[id], n);
                        // Like the panel's own Sell: an emptied holding leaves Your Portfolio.
                        if (ai.OwnedCount(ref buffer[id]) == 0)
                        {
                            EconomyPanel panel =
                                UnityEngine.Object.FindObjectOfType<EconomyPanel>();
                            if (panel != null)
                            {
                                panel.RemoveOwnedInvestment((byte)id);
                            }
                        }
                    }

                    yield return null;
                    break;
                }
                default:
                    r.Error = "unknown step";
                    break;
            }
        }

        private static string rememberedMonitor;

        private static UIComponent FindControl(string name, bool includeHidden = false)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            BuySellInvestmentPanel dialog = BuySellInvestmentPanel.instance;
            if (dialog != null && dialog.component.isVisible)
            {
                UIComponent inDialog = FindIn(dialog.component, name, includeHidden);
                if (inDialog != null)
                {
                    return inDialog;
                }
            }

            foreach (UIComponent c in UnityEngine.Object.FindObjectsOfType<UIComponent>())
            {
                if (c.name == name && (includeHidden || IsShown(c)))
                {
                    return c;
                }
            }

            return null;
        }

        private static UIComponent FindIn(UIComponent root, string name, bool includeHidden)
        {
            if (root.name == name && (includeHidden || IsShown(root)))
            {
                return root;
            }

            foreach (UIComponent child in root.components)
            {
                UIComponent hit = FindIn(child, name, includeHidden);
                if (hit != null)
                {
                    return hit;
                }
            }

            return null;
        }

        /// <summary>
        /// First visible descendant whose box is not inside the dialog's box (1 px slack), or
        /// one of our fixed-width single-line labels whose text is cut off.
        /// </summary>
        private static UIComponent FirstOverflow(UIComponent root, UIComponent c)
        {
            return FirstOverflow(root, c, null);
        }

        /// <param name="list">Nearest clipping (scrolling) ancestor below the root, or null.
        /// Inside it, rows beyond the visible area are hidden by design, so only the width is
        /// checked, against the list.</param>
        private static UIComponent FirstOverflow(UIComponent root, UIComponent c, UIComponent list)
        {
            foreach (UIComponent child in c.components)
            {
                if (!child.isVisibleSelf)
                {
                    continue;
                }

                if (IsClipped(child as UILabel))
                {
                    return child;
                }

                Vector3 p = child.absolutePosition;
                if (list != null)
                {
                    Vector3 lp = list.absolutePosition;
                    if (p.x < lp.x - 1f || p.x + child.width > lp.x + list.width + 1f)
                    {
                        return child;
                    }
                }
                else
                {
                    Vector3 r = root.absolutePosition;
                    if (
                        p.x < r.x - 1f
                        || p.y < r.y - 1f
                        || p.x + child.width > r.x + root.width + 1f
                        || p.y + child.height > r.y + root.height + 1f
                    )
                    {
                        return child;
                    }
                }

                UIComponent deeper = FirstOverflow(
                    root,
                    child,
                    list ?? (child.clipChildren ? child : null)
                );
                if (deeper != null)
                {
                    return deeper;
                }
            }

            return null;
        }

        /// <summary>
        /// Our own fixed-width, single-line label whose text is wider than the label (the game
        /// clips it silently). Measured by autosizing for a moment, then restoring.
        /// </summary>
        private static bool IsClipped(UILabel label)
        {
            if (
                label == null
                || label.autoSize
                || label.wordWrap
                || string.IsNullOrEmpty(label.text)
                || label.name == null
                || !label.name.StartsWith("FinanceOverhaul", StringComparison.Ordinal)
            )
            {
                return false;
            }

            Vector2 size = label.size;
            label.autoSize = true;
            float needed = label.width;
            label.autoSize = false;
            label.size = size;
            return needed > size.x + 1f;
        }

        private static bool IsAncestor(UIComponent maybeAncestor, UIComponent c)
        {
            for (UIComponent p = c.parent; p != null; p = p.parent)
            {
                if (p == maybeAncestor)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsShown(UIComponent c)
        {
            for (UIComponent p = c; p != null; p = p.parent)
            {
                if (!p.isVisibleSelf)
                {
                    return false;
                }
            }

            return true;
        }

        private static string TextOf(UIComponent c)
        {
            if (c is UILabel)
            {
                return ((UILabel)c).text;
            }

            if (c is UIButton)
            {
                return ((UIButton)c).text;
            }

            if (c is UITextField)
            {
                return ((UITextField)c).text;
            }

            return null;
        }

        private static int Find(string symbol)
        {
            if (string.IsNullOrEmpty(symbol))
            {
                return -1;
            }

            Investment[] buffer = EconomyManager.instance.m_investments.m_buffer;
            for (int i = 0; i < buffer.Length; i++)
            {
                InvestmentInfo info = buffer[i].Info;
                if ((buffer[i].m_flags & Investment.Flags.Created) == 0 || info == null)
                {
                    continue;
                }

                if (
                    info.name == symbol
                    || string.Equals(
                        StockSymbols.For(info),
                        symbol,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>A control's own opacity times every parent's: what it is drawn with.</summary>
        private static float ShownOpacity(UIComponent c)
        {
            float o = 1f;
            for (UIComponent p = c; p != null; p = p.parent)
            {
                o *= p.opacity;
            }

            return o;
        }

        private static bool Same(Color32 x, Color32 y)
        {
            return x.r == y.r && x.g == y.g && x.b == y.b && x.a == y.a;
        }

        private static string Rgba(Color32 c)
        {
            return "(" + c.r + ", " + c.g + ", " + c.b + ", " + c.a + ")";
        }

        /// <summary>Replaces each <c>$owned:SYMBOL</c> with the shares owned now.</summary>
        private static string OwnedTokens(string line)
        {
            const string Token = "$owned:";
            int at;
            while ((at = line.IndexOf(Token, StringComparison.Ordinal)) >= 0)
            {
                int end = line.IndexOf(' ', at);
                end = end < 0 ? line.Length : end;
                string symbol = line.Substring(at + Token.Length, end - at - Token.Length);
                int id = Find(symbol);
                string owned = id < 0 ? "0" : Owned(id).ToString(CultureInfo.InvariantCulture);
                line = line.Substring(0, at) + owned + line.Substring(end);
            }

            return line;
        }

        private static uint Owned(int id)
        {
            Investment[] buffer = EconomyManager.instance.m_investments.m_buffer;
            return buffer[id].Info.m_investmentAI.OwnedCount(ref buffer[id]);
        }

        /// <summary>The city's cash field itself (LastCashAmount only updates on a sim step).</summary>
        private static long RawCash(EconomyManager em)
        {
            FieldInfo f = typeof(EconomyManager).GetField(
                "m_cashAmount",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
            );
            return f == null ? long.MinValue : Convert.ToInt64(f.GetValue(em));
        }

        private static float ParseFloat(string s, float fallback)
        {
            float v;
            return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v)
                ? v
                : fallback;
        }

        private static long LogLength()
        {
            try
            {
                return new FileInfo(ModLog.LogPath).Length;
            }
            catch
            {
                return 0;
            }
        }

        private static string LogSince(long start)
        {
            try
            {
                using (
                    var fs = new FileStream(
                        ModLog.LogPath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.ReadWrite
                    )
                )
                {
                    fs.Seek(start, SeekOrigin.Begin);
                    using (var reader = new StreamReader(fs))
                    {
                        return reader.ReadToEnd();
                    }
                }
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}

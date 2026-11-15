#if HAS_CITIES
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using ColossalFramework.UI;
using FinanceOverhaul.Market;
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
            // Everything already open is the player's: frozen until the scenario ends, so a
            // scenario that advances days can never fill, expire or move it.
            OrderDesk.Freeze(ctx.OrdersBefore);
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
                    );
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
                case "bond-finances":
                {
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
                    foreach (int id in ctx.Bonds)
                    {
                        BondDesk.Redeem(id);
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
                            Mod.SetOrderKindEnabled(kind, on);
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
#endif

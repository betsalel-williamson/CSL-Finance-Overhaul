#if HAS_CITIES
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
            public readonly Dictionary<string, uint> Owned = new Dictionary<string, uint>();
            public long LogStart;
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
                case "wait":
                    yield return new WaitForSeconds(ParseFloat(a, 1f));
                    break;
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
                    BuySellInvestmentPanel dialog = BuySellInvestmentPanel.instance;
                    if (dialog == null || !dialog.component.isVisible)
                    {
                        r.Error = "no open Invest dialog";
                        break;
                    }

                    UIComponent offender = FirstOverflow(dialog.component, dialog.component);
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
                            + " leaves the dialog at "
                            + dialog.component.absolutePosition
                            + " size "
                            + dialog.component.size;
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
                        buffer[id].Info.m_investmentAI.SellShares((byte)id, ref buffer[id], n);
                    }

                    yield return null;
                    break;
                }
                default:
                    r.Error = "unknown step";
                    break;
            }
        }

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

        /// <summary>First visible descendant whose box is not inside the dialog's box (1 px slack).</summary>
        private static UIComponent FirstOverflow(UIComponent root, UIComponent c)
        {
            foreach (UIComponent child in c.components)
            {
                if (!child.isVisibleSelf)
                {
                    continue;
                }

                Vector3 p = child.absolutePosition;
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

                UIComponent deeper = FirstOverflow(root, child);
                if (deeper != null)
                {
                    return deeper;
                }
            }

            return null;
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
                        TickerSymbols.For(info.m_investmentAI.GetLocalizedName()),
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

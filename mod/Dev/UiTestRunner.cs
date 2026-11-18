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
    public partial class UiTestRunner : MonoBehaviour
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

        /// <summary>A company a scenario listed: its headquarters and whether that was Historical before.</summary>
        private sealed class TestCompany
        {
            public int Id;
            public BuildingKey Home;
            public bool WasHistorical;
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

            /// <summary>The Stock Exchange a scenario turned, and how it stood before.</summary>
            public ushort TurnedExchange;
            public Vector3 ExchangePositionBefore;
            public float ExchangeAngleBefore;

            /// <summary>Companies a scenario listed, with what to put back.</summary>
            public readonly List<TestCompany> TestCompanies = new List<TestCompany>();

            /// <summary>The player's Local IPOs flag, kept while a scenario switches it.</summary>
            public bool? LocalIposBefore;

            /// <summary>Cents a scenario took out of the city's cash with cash-set, to put back.</summary>
            public long CashTaken;

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
            IpoDialog.Silenced = true; // nor open the IPO dialog, unless a scenario asks
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
            IpoDialog.Silenced = false;
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

            RestoreExchange(ctx);
            RestoreCash(ctx);
            if (ctx.LocalIposBefore.HasValue && Mod.Settings != null)
            {
                Mod.Settings.LocalIposEnabled = ctx.LocalIposBefore.Value;
            }

            RemoveTestCompanies(ctx);
            CompanyDesk.Rules = null; // back to the real rules
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

            /// <summary>Set by a command group that does not have the command.</summary>
            public bool Unknown;
        }

        private delegate IEnumerator StepGroup(
            string line,
            string cmd,
            string a,
            string b,
            Context ctx,
            StepResult r
        );

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
            // The commands are grouped by what they drive, one UiTestRunner.*.cs file each.
            // A group that does not know the command says so and the next one is asked.
            StepGroup[] groups =
            {
                TradingSteps,
                InspectSteps,
                MonitorSteps,
                TickerSteps,
                BondsSteps,
                CompanySteps,
                StressSteps,
            };
            foreach (StepGroup group in groups)
            {
                r.Unknown = false;
                IEnumerator steps = group(line, cmd, a, b, ctx, r);
                while (true)
                {
                    // A command that throws fails its step instead of killing the scenario:
                    // the cleanup lines must still run (a scenario once died mid-run and left
                    // its changes in the city).
                    bool more;
                    try
                    {
                        more = steps.MoveNext();
                    }
                    catch (Exception ex)
                    {
                        r.Unknown = false;
                        r.Error = "the step threw " + ex.GetType().Name + ": " + ex.Message;
                        ModLog.Warn("uitest: '" + line + "' threw: " + ex);
                        yield break;
                    }

                    if (!more)
                    {
                        break;
                    }

                    yield return steps.Current;
                }

                if (!r.Unknown)
                {
                    yield break;
                }
            }

            r.Error = "unknown step";
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
        /// <summary>Puts back what cash-set took (the scenario's own payments stay paid).</summary>
        private static void RestoreCash(Context ctx)
        {
            if (ctx.CashTaken == 0)
            {
                return;
            }

            SetRawCash(RawCash(EconomyManager.instance) + ctx.CashTaken);
            ctx.CashTaken = 0;
        }

        private static bool SetRawCash(long cents)
        {
            FieldInfo f = GameInternals.Manager.CashAmount;
            if (f == null)
            {
                return false;
            }

            f.SetValue(EconomyManager.instance, Convert.ChangeType(cents, f.FieldType));
            return true;
        }

        private static bool TryCents(string text, out long cents)
        {
            decimal whole;
            cents = 0;
            if (
                !decimal.TryParse(
                    (text ?? string.Empty).Replace(",", string.Empty),
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out whole
                )
            )
            {
                return false;
            }

            cents = (long)Math.Round(whole * 100m);
            return true;
        }

        private static long RawCash(EconomyManager em)
        {
            FieldInfo f = GameInternals.Manager.CashAmount;
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

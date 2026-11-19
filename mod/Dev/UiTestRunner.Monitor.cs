using System; // ArgumentException, Enum, StringComparison
using System.Collections; // IEnumerator
using System.Collections.Generic; // List
using System.Globalization; // CultureInfo
using ColossalFramework.UI; // UIComponent, UILabel
using FinanceOverhaul.Core; // MonitorSort
using UnityEngine; // WaitForSeconds

namespace FinanceOverhaul
{
    /// <summary>Game UI test commands: The Stock Monitor window.</summary>
    public partial class UiTestRunner
    {
        private IEnumerator MonitorSteps(
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
                case "game-escape":
                {
                    // game-escape — the game's own Esc handling, as the key would run it.
                    if (!GameInternals.Keys.PressEscape())
                    {
                        r.Error = "the game's Esc handler is not available";
                    }

                    yield return new WaitForSeconds(0.3f);
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
                default:
                    r.Unknown = true; // not one of this group's commands
                    yield break;
            }
        }
    }
}

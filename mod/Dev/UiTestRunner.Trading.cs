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
    /// <summary>Game UI test commands: Placing and cancelling trades: the Buy and Sell dialog, the Economy panel, game time.</summary>
    public partial class UiTestRunner
    {
        private IEnumerator TradingSteps(
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
                    int tab = GameInternals.Economy.TabIndex(
                        a == "loans"
                            ? GameInternals.Economy.LoansTab
                            : GameInternals.Economy.InvestmentTab
                    );
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
                        GameInternals.Economy.OnCloseButton.Invoke(economy, null);
                    }

                    yield return new WaitForSeconds(0.8f);
                    break;
                }
                case "close-dialog":
                {
                    BuySellInvestmentPanel panel = BuySellInvestmentPanel.instance;
                    if (panel != null && panel.component.isVisible)
                    {
                        GameInternals.BuySell.OnClosed.Invoke(panel, null);
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
                case "keys":
                {
                    // keys CONTROL TOKEN… — real key presses, one a frame, through the field's
                    // own key handling (so the caret, selection and the mod's regrouping are
                    // the player's): a character types it; home, end, left, right, backspace
                    // and delete press that key. The field keeps focus (see `blur`).
                    UITextField f = FindControl(a) as UITextField;
                    if (f == null)
                    {
                        r.Error = "no visible text field " + a;
                        break;
                    }

                    if (!f.hasFocus)
                    {
                        f.Focus();
                        yield return null;
                    }

                    if (!f.hasFocus)
                    {
                        r.Error = a + " did not take focus";
                        break;
                    }

                    foreach (
                        string token in (b ?? string.Empty).Split(
                            new[] { ' ' },
                            StringSplitOptions.RemoveEmptyEntries
                        )
                    )
                    {
                        KeyCode key =
                            token == "home" ? KeyCode.Home
                            : token == "end" ? KeyCode.End
                            : token == "left" ? KeyCode.LeftArrow
                            : token == "right" ? KeyCode.RightArrow
                            : token == "backspace" ? KeyCode.Backspace
                            : token == "delete" ? KeyCode.Delete
                            : KeyCode.None;
                        if (key != KeyCode.None)
                        {
                            r.Error = PressKey(f, key, '\0');
                        }
                        else
                        {
                            foreach (char ch in token)
                            {
                                r.Error = r.Error ?? PressKey(f, KeyCode.None, ch);
                            }
                        }

                        if (r.Error != null)
                        {
                            break;
                        }

                        // A frame for the field, one for whoever regroups its text.
                        yield return null;
                        yield return null;
                    }

                    break;
                }
                case "blur":
                {
                    // blur CONTROL — the field gives up focus (applies what was typed).
                    UITextField f = FindControl(a) as UITextField;
                    if (f != null && f.hasFocus)
                    {
                        f.Unfocus();
                    }

                    yield return null;
                    break;
                }
                case "expect-caret":
                {
                    // expect-caret CONTROL N — the caret stands after N characters.
                    UITextField f = FindControl(a) as UITextField;
                    if (f == null || GameInternals.Ui.TextCursorIndex == null)
                    {
                        r.Error = "no visible text field " + a;
                        break;
                    }

                    int caret = (int)GameInternals.Ui.TextCursorIndex.GetValue(f);
                    if (caret.ToString() != b)
                    {
                        r.Error =
                            a + " caret is at " + caret + " in '" + f.text + "', expected " + b;
                    }

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
                case "wait":
                    yield return new WaitForSeconds(ParseFloat(a, 1f));
                    break;
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
                    r.Unknown = true; // not one of this group's commands
                    yield break;
            }
        }

        /// <summary>One key through the field's own handlers; an error text, or null.</summary>
        private static string PressKey(UITextField field, KeyCode key, char character)
        {
            UIKeyEventParameter p = GameInternals.Ui.KeyEvent(field, key, character);
            System.Reflection.MethodInfo handler =
                key == KeyCode.None
                    ? GameInternals.Ui.TextOnKeyPress
                    : GameInternals.Ui.TextOnKeyDown;
            if (p == null || handler == null)
            {
                return "the game's key handling was not found";
            }

            handler.Invoke(field, new object[] { p });
            return null;
        }
    }
}

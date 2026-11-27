using System; // ArgumentException, Enum, Exception
using System.Collections; // IEnumerator
using System.Collections.Generic; // List
using System.Globalization; // CultureInfo
using System.IO; // Path
// ColossalFramework.UI: UIButton, UIComponent, UIFont, UIInput, UILabel, UIPanel, UITextComponent,
//   UIView
using ColossalFramework.UI;
using FinanceOverhaul.Core; // DuplicateCopies
using UnityEngine; // Application, GameObject, Resources, Vector2, Vector3, WaitForSeconds

// Game classes used without a using (global namespace, Assembly-CSharp): BuySellInvestmentPanel,
//   ConfirmPanel, EconomyManager, EconomyPanel, EffectInfo, Investment, InvestmentInfo

namespace FinanceOverhaul
{
    /// <summary>Game UI test commands: looking at any control and the game (dumps, logs, inspecting).</summary>
    public partial class UiTestRunner
    {
        private const string BuildingPanelName = "(Library) ZonedBuildingWorldInfoPanel"; // not shown: a game control name

        private IEnumerator InspectSteps(
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
                case "dump":
                {
                    // "dump building-panel": the game's zoned building info panel; "dump
                    // invest-dialog": the game's Buy / Sell dialog. "dump
                    // (Library) X": any control whose name has a space in it.
                    string whole = line.Substring(cmd.Length).Trim();
                    UIComponent c =
                        a == "building-panel" ? UIView.GetAView().FindUIComponent(BuildingPanelName)
                        : a == "invest-dialog" ? BuySellInvestmentPanel.instance.component
                        : whole.IndexOf(' ') > 0 ? UIView.GetAView().FindUIComponent(whole)
                        : FindControl(a, includeHidden: true);
                    if (c == null)
                    {
                        r.Error = "no control " + a;
                        break;
                    }

                    DevCapture.DumpTree(c, 0);
                    break;
                }
                case "dump-all":
                {
                    // dump-all NAME — every control with that name, shown or not, with the
                    // path of its parents: finds copies a hot reload or a second dialog left.
                    int n = 0;
                    foreach (
                        UIComponent each in UnityEngine.Object.FindObjectsOfType<UIComponent>()
                    )
                    {
                        if (each.name != a)
                        {
                            continue;
                        }

                        n++;
                        string path = each.name;
                        for (UIComponent p = each.parent; p != null; p = p.parent)
                        {
                            path = p.name + "/" + path; // not shown: dev log
                        }

                        ModLog.Info(
                            "ui: copy " // not shown: dev log
                                + n
                                + " id="
                                + each.GetInstanceID()
                                + " visible="
                                + each.isVisible
                                + " h="
                                + each.height
                                + " "
                                + path
                        );
                        DevCapture.DumpTree(each, 1);
                    }

                    ModLog.Info("ui: " + n + " control(s) named " + a); // not shown: dev log
                    break;
                }
                case "log-modal":
                {
                    // log-modal — dev: the component that holds the game's modal (the only one
                    // that takes mouse clicks), and what the mouse input thinks is pressed.
                    UIComponent modal = UIView.GetModalComponent();
                    ModLog.Info(
                        "uitest: modal "
                            + (modal == null ? "none" : modal.name + " visible=" + modal.isVisible)
                            + " active="
                            + (
                                UIView.activeComponent == null
                                    ? "none"
                                    : UIView.activeComponent.name
                            )
                            + " hovered="
                            + (
                                UIInput.hoveredComponent == null
                                    ? "none"
                                    : UIInput.hoveredComponent.name
                            )
                    );
                    break;
                }
                case "button-state":
                {
                    // button-state NAME Normal|Hovered|Pressed|Focused|Disabled — dev: puts a
                    // button in that state, to capture its art (the real mouse sets it in play).
                    var button = FindControl(a, includeHidden: true) as UIButton;
                    UIButton.ButtonState state;
                    try
                    {
                        state = (UIButton.ButtonState)Enum.Parse(typeof(UIButton.ButtonState), b);
                    }
                    catch (ArgumentException)
                    {
                        r.Error = "no button state " + b;
                        break;
                    }

                    if (button == null)
                    {
                        r.Error = "no button " + a;
                        break;
                    }

                    button.state = state;
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
                case "duplicate-alert":
                {
                    // duplicate-alert preview|close — shows the "switched on twice" dialog for
                    // every installed copy (it changes nothing), or closes it with No.
                    // duplicate-alert live — the real check over every installed copy, as a
                    // rehearsal (answers are logged, nothing is switched off); end stops it.
                    // duplicate-alert busy — another question on the game's Yes / No dialog.
                    // duplicate-alert escape — closes the dialog as Escape does.
                    // duplicate-alert expect-ours|expect-not-ours — what the dialog asks now.
                    ConfirmPanel open = UIView.library.Get<ConfirmPanel>("ConfirmPanel");
                    bool ours = open != null && open.component.isVisible && AsksDuplicate(open);
                    if (a == "live")
                    {
                        DuplicateCopyAlert.Rehearsal = true;
                        DuplicateCopyAlert.Schedule();
                    }
                    else if (a == "end")
                    {
                        DuplicateCopyAlert.Rehearsal = false;
                        GameObject marker = GameObject.Find("FinanceOverhaulDuplicateCopyAlert");
                        if (marker != null)
                        {
                            UnityEngine.Object.Destroy(marker);
                        }
                    }
                    else if (a == "busy")
                    {
                        ConfirmPanel.ShowModal(
                            "uitest: another question", // not shown: dev test dialog
                            "uitest: asked before the duplicate check", // not shown: dev test dialog
                            (component, answer) =>
                                ModLog.Info("uitest: the other question got answer " + answer)
                        );
                    }
                    else if (a == "escape")
                    {
                        if (open != null && open.component.isVisible)
                        {
                            UIView.library.Hide("ConfirmPanel", -1);
                        }
                    }
                    else if (a == "expect-ours" || a == "expect-not-ours")
                    {
                        if (ours != (a == "expect-ours"))
                        {
                            r.Error = ours
                                ? "the dialog asks the duplicate question"
                                : "the dialog does not ask the duplicate question";
                        }

                        break;
                    }
                    else if (a == "close")
                    {
                        ConfirmPanel panel = UIView.library.Get<ConfirmPanel>("ConfirmPanel");
                        if (panel != null && panel.component.isVisible)
                        {
                            panel.OnNo();
                        }
                    }
                    else
                    {
                        int copies = DuplicateCopyAlert.Preview();
                        ModLog.Info("uitest: duplicate-alert copies installed=" + copies);
                        if (copies < 2)
                        {
                            r.Error = "only " + copies + " copy installed: nothing to show";
                        }
                    }

                    yield return new WaitForSeconds(0.6f);
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
                case "dump-library":
                case "peek-library":
                {
                    // dump-library NAME — logs the tree of one of the game's own dialogs
                    // (fetched from its panel library, where they wait unseen). peek-library
                    // NAME — shows it as it stands for two seconds, for a capture of its look
                    // (its contents are whatever it last showed), then hides it again.
                    UIComponent c = null;
                    try
                    {
                        c = UIView.library.Get<UIComponent>(a);
                    }
                    catch (Exception ex)
                    {
                        r.Error = "the library has no " + a + ": " + ex.Message;
                        break;
                    }

                    if (c == null)
                    {
                        r.Error = "the library has no " + a;
                        break;
                    }

                    if (cmd == "dump-library")
                    {
                        DevCapture.DumpTree(c, 0);
                        break;
                    }

                    bool was = c.isVisible;
                    float opacity = c.opacity;
                    c.isVisible = true;
                    c.opacity = 1f;
                    c.BringToFront();
                    c.CenterToParent();
                    yield return new WaitForSeconds(0.6f);
                    Application.CaptureScreenshot(
                        Path.Combine(
                            DevCapture.Folder,
                            "FinanceOverhaul-uitest-library-" + a + ".png"
                        )
                    );
                    yield return new WaitForSeconds(1.2f);
                    c.isVisible = was;
                    c.opacity = opacity;
                    break;
                }
                case "log-effects":
                {
                    // log-effects PATTERN — every effect the game has loaded whose name
                    // contains PATTERN (any case), with its type.
                    int found = 0;
                    foreach (EffectInfo e in Resources.FindObjectsOfTypeAll<EffectInfo>())
                    {
                        if (
                            e != null
                            && (
                                a == null
                                || e.name.ToLowerInvariant().Contains(a.ToLowerInvariant())
                            )
                        )
                        {
                            found++;
                            ModLog.Info("uitest: effect " + e.name + " <" + e.GetType().Name + ">");
                        }
                    }

                    ModLog.Info("uitest: " + found + " effect(s) match " + a);
                    break;
                }
                case "log-prices":
                {
                    // log-prices — every listed stock's daily prices as the game keeps them
                    // (up to 30, newest first) with its price rule, to measure how the game's
                    // stocks move.
                    Investment[] all = EconomyManager.instance.m_investments.m_buffer;
                    for (int i = 0; i < all.Length; i++)
                    {
                        Investment inv = all[i];
                        if (
                            (inv.m_flags & Investment.Flags.Created) == 0
                            || inv.Info == null
                            || inv.allValues == null
                        )
                        {
                            continue;
                        }

                        var kept = new System.Text.StringBuilder();
                        for (int k = 0; k < inv.allValues.Count; k++)
                        {
                            kept.Append(
                                    inv.allValues.GetFromTop(k)
                                        .ToString("0.##", CultureInfo.InvariantCulture)
                                )
                                .Append(' ');
                        }

                        ModLog.Info(
                            "uitest: prices|"
                                + inv.Info.name
                                + "|"
                                + (
                                    inv.Info.m_investmentAI == null
                                        ? "?"
                                        : inv.Info.m_investmentAI.GetType().Name
                                )
                                + "|"
                                + kept
                        );
                    }

                    break;
                }
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
                default:
                    r.Unknown = true; // not one of this group's commands
                    yield break;
            }
        }

        /// <summary>The game's Yes / No dialog is showing the duplicate-copies question.</summary>
        private static bool AsksDuplicate(ConfirmPanel panel)
        {
            string title = DuplicateCopies.Title();
            foreach (UILabel l in panel.component.GetComponentsInChildren<UILabel>())
            {
                if (l.text == title)
                {
                    return true;
                }
            }

            return false;
        }
    }
}

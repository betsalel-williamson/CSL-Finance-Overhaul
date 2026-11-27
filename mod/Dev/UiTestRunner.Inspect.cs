using System; // ArgumentException, Enum, Exception, StringComparison
using System.Collections; // IEnumerator
using System.Collections.Generic; // List
using System.Globalization; // CultureInfo
using System.IO; // Path
// ColossalFramework.UI: UIButton, UIComponent, UIFont, UIInput, UILabel, UIPanel,
//   UIScrollablePanel, UISlider, UITextComponent, UITextureSprite, UIView
using ColossalFramework.UI;
// FinanceOverhaul.Core: DuplicateCopies, InvestmentRow, MonitorSort, PortfolioOverview,
//   SliderThumb
using FinanceOverhaul.Core;
using UnityEngine; // Application, GameObject, Mathf, Resources, Vector2, Vector3, WaitForSeconds

// Game classes used without a using (global namespace, Assembly-CSharp): BuySellInvestmentPanel,
//   ConfirmPanel, EconomyManager, EconomyPanel, EffectInfo, Investment, InvestmentInfo

namespace FinanceOverhaul
{
    /// <summary>Game UI test commands: Looking at any control: finding, measuring, and the generic expect checks.</summary>
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
                case "investments-list":
                {
                    // investments-list on|off|restore — the Options flag for the one sectioned
                    // list in Economy → Investments (restore: as the player had it).
                    if (Mod.Settings == null)
                    {
                        r.Error = "no settings loaded";
                        break;
                    }

                    if (!ctx.InvestmentListBefore.HasValue)
                    {
                        ctx.InvestmentListBefore = Mod.Settings.InvestmentListEnabled;
                    }

                    Mod.Settings.InvestmentListEnabled =
                        a == "restore" ? ctx.InvestmentListBefore.Value : a == "on";
                    yield return null;
                    yield return null;
                    break;
                }
                case "investments-sort":
                {
                    // investments-sort sync on|off, or Change|Symbol|Price|ValueHeld [currency] —
                    // the list's own sort settings (put back when the scenario ends).
                    if (Mod.Settings == null)
                    {
                        r.Error = "no settings loaded";
                        break;
                    }

                    if (ctx.ListSortBefore == null)
                    {
                        ctx.ListSortBefore = new object[]
                        {
                            Mod.Settings.InvestmentListSortSynced,
                            Mod.Settings.InvestmentListSort,
                            Mod.Settings.InvestmentListSortReversed,
                            Mod.Settings.InvestmentListChangeAsCurrency,
                        };
                    }

                    if (a == "sync")
                    {
                        Mod.Settings.InvestmentListSortSynced = b == "on";
                    }
                    else
                    {
                        try
                        {
                            Mod.Settings.InvestmentListSort = (MonitorSort)
                                Enum.Parse(typeof(MonitorSort), a);
                        }
                        catch (ArgumentException)
                        {
                            r.Error = "no sort " + a;
                            break;
                        }

                        Mod.Settings.InvestmentListSortReversed = false;
                        Mod.Settings.InvestmentListChangeAsCurrency = b == "currency";
                    }

                    yield return new WaitForSeconds(0.5f);
                    break;
                }
                case "investments-scroll":
                {
                    // investments-scroll PIXELS — moves the list as the wheel would.
                    InvestmentsList shown = InvestmentsList.Current;
                    if (shown == null)
                    {
                        r.Error = "the investments list is not shown";
                        break;
                    }

                    shown.ScrollBy(float.Parse(a, CultureInfo.InvariantCulture));
                    yield return null;
                    break;
                }
                case "investments-scroll-to":
                {
                    // investments-scroll-to SYMBOL — that stock's line at the top of the list.
                    InvestmentsList shown = InvestmentsList.Current;
                    if (shown == null || !shown.ScrollTo(a))
                    {
                        r.Error = a + " is not in the investments list";
                    }

                    yield return null;
                    break;
                }
                case "expect-investments":
                {
                    // expect-investments SECTION SYMBOL — that stock is listed in that section
                    // (Portfolio, Local, All); logs the whole list first. SECTION "none": not listed.
                    InvestmentsList shown = InvestmentsList.Current;
                    if (shown == null)
                    {
                        r.Error = "the investments list is not shown";
                        break;
                    }

                    var sb = new System.Text.StringBuilder();
                    string found = "none";
                    foreach (InvestmentRow row in shown.Rows)
                    {
                        sb.Append(row.Key).Append(' ');
                        if (row.Stock != null && row.Stock.Symbol == b)
                        {
                            found = row.Section.ToString();
                        }
                    }

                    ModLog.Info("uitest: investments list " + sb + "offset " + shown.Offset);
                    if (!string.Equals(found, a, StringComparison.OrdinalIgnoreCase))
                    {
                        r.Error = b + " is in " + found + ", expected " + a;
                    }

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
                case "expect-wheel-step":
                {
                    // expect-wheel-step LIST MIN — the pixels the mouse wheel moves a scrolling
                    // list a notch must be at least MIN.
                    UIScrollablePanel list = FindControl(a) as UIScrollablePanel;
                    if (list == null)
                    {
                        r.Error = "no visible scrolling list " + a;
                    }
                    else if (list.scrollWheelAmount < (int)ParseFloat(b, 1f))
                    {
                        r.Error =
                            a + " moves " + list.scrollWheelAmount + " a notch, expected " + b;
                    }

                    break;
                }
                case "overview-orders":
                {
                    // overview-orders longest|off — the Portfolio view shows one order of each
                    // kind with the longest numbers (and a wide symbol) instead of the city's.
                    InvestmentsOverview.TestOrders =
                        a == "longest"
                            ? PortfolioOverview.LongestOrders("WWWWW", OrderDesk.Today) // not shown: a test symbol
                            : null;
                    break;
                }
                case "index-members":
                {
                    // index-members — every investment, its unit, whether it is on sale and
                    // whether the city index fund counts it.
                    foreach (string member in IndexListing.MemberReport())
                    {
                        ModLog.Info("uitest: index-member|" + member);
                    }

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
                case "hover":
                {
                    // hover CONTROL FX FY — simulated mouse at that fraction of the control
                    // (Stock Monitor hover); "hover off" returns to the real mouse.
                    if (a == "off")
                    {
                        FoPointer.Test = null;
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
                    FoPointer.Test = new Vector2(p.x + fx * target.width, p.y + fy * target.height);
                    yield return null;
                    yield return null;
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
                case "expect-sparks-in-rows":
                {
                    // Every 30-day line shown in the Investments list lies inside its own
                    // stock's line (…Row-GGISpark inside …Row-GGI): no line shows on another.
                    UIComponent view = FindControl(InvestmentsList.Prefix + "View");
                    if (view == null)
                    {
                        r.Error = "the investments list is not shown";
                        break;
                    }

                    int checkedLines = 0;
                    foreach (UIComponent c in view.components)
                    {
                        if (
                            !c.isVisibleSelf
                            || !(c is UITextureSprite)
                            || !c.name.StartsWith(InvestmentsList.Prefix + "Row-")
                            || !c.name.EndsWith("Spark")
                        )
                        {
                            continue;
                        }

                        string rowName = c.name.Substring(0, c.name.Length - "Spark".Length);
                        UIComponent row = null;
                        foreach (UIComponent other in view.components)
                        {
                            if (other.name == rowName && other.isVisibleSelf)
                            {
                                row = other;
                            }
                        }

                        Vector3 sp = c.absolutePosition,
                            rp = row == null ? Vector3.zero : row.absolutePosition;
                        bool inside =
                            row != null
                            && sp.x >= rp.x
                            && sp.x + c.width <= rp.x + row.width + 0.5f
                            && sp.y >= rp.y
                            && sp.y + c.height <= rp.y + row.height + 0.5f;
                        if (!inside)
                        {
                            r.Error =
                                c.name
                                + " at "
                                + sp
                                + (
                                    row == null
                                        ? " has no visible line"
                                        : " is outside " + row.name + " at " + rp
                                );
                            break;
                        }

                        checkedLines++;
                    }

                    if (r.Error == null && checkedLines == 0)
                    {
                        r.Error = "no 30-day lines shown";
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
                    // Under half a pixel is a shared edge (layouts at half-pixel positions add
                    // float noise), not an overlap anyone could see.
                    const float Edge = 0.5f;
                    bool overlap =
                        p1.x + Edge < p2.x + c2.width
                        && p2.x + Edge < p1.x + c1.width
                        && p1.y + Edge < p2.y + c2.height
                        && p2.y + Edge < p1.y + c1.height;
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

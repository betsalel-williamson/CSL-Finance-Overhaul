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
    /// <summary>Game UI test commands: Looking at any control: finding, measuring, and the generic expect checks.</summary>
    public partial class UiTestRunner
    {
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
                case "duplicate-alert":
                {
                    // duplicate-alert preview|close — shows the "switched on twice" dialog for
                    // every installed copy (it changes nothing), or closes it.
                    if (a == "close")
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
    }
}

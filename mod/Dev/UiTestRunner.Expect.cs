using System; // StringComparison
using System.Collections; // IEnumerator
using System.Globalization; // CultureInfo
using System.IO; // Path
using ColossalFramework.UI; // UIComponent, UILabel, UIScrollablePanel, UISlider
using FinanceOverhaul.Core; // SliderThumb
using UnityEngine; // Application, Mathf, Vector2, Vector3, WaitForSeconds

// Game classes used without a using (global namespace, Assembly-CSharp): BuySellInvestmentPanel

namespace FinanceOverhaul
{
    /// <summary>Game UI test commands: the generic checks on any control (shown, hidden, text, overflow, overlap, in view) and captures.</summary>
    public partial class UiTestRunner
    {
        private IEnumerator ExpectSteps(
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
    }
}

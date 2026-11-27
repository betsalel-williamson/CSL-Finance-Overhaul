using System; // ArgumentException, Enum, StringComparison
using System.Collections; // IEnumerator
using System.Globalization; // CultureInfo
// ColossalFramework.UI: UIComponent, UIMouseButton, UIMouseEventParameter, UIScrollablePanel,
//   UITextureSprite
using ColossalFramework.UI;
using FinanceOverhaul.Core; // InvestmentRow, MonitorSort, PortfolioOverview
using UnityEngine; // Mathf, Ray, Time, Vector2, Vector3, WaitForSeconds

namespace FinanceOverhaul
{
    /// <summary>Game UI test commands: Economy → Investments (the list, its scrolling and sort, the overview) and recording frames.</summary>
    public partial class UiTestRunner
    {
        private IEnumerator InvestmentsSteps(
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
                case "investments-scroll-smooth":
                {
                    // investments-scroll-smooth PIXELS SECONDS — a little every frame, as a
                    // wheel or a dragged scrollbar moves the list (for recordings).
                    InvestmentsList shown = InvestmentsList.Current;
                    if (shown == null)
                    {
                        r.Error = "the investments list is not shown";
                        break;
                    }

                    float total = float.Parse(a, CultureInfo.InvariantCulture);
                    float seconds = Mathf.Max(
                        0.05f,
                        float.Parse(b ?? "1", CultureInfo.InvariantCulture)
                    );
                    float done = 0f;
                    float start = Time.unscaledTime;
                    float t = 0f;
                    while (t < 1f)
                    {
                        t = Mathf.Clamp01((Time.unscaledTime - start) / seconds);
                        float step = total * t - done;
                        shown.ScrollBy(step);
                        done += step;
                        yield return null;
                    }

                    break;
                }
                case "record":
                {
                    // record NAME [CONTROL] — every shown frame to FinanceOverhaul-video-NAME/.
                    r.Error = FrameRecorder.Start(a, b);
                    yield return null;
                    break;
                }
                case "record-format":
                {
                    // record-format video|jpeg|count|probe — how the next recording is saved (count:
                    // frames counted, none read, for the game's own frame rate; probe: a 16 × 16 read only).
                    FrameRecorder.PreferJpeg = a == "jpeg";
                    FrameRecorder.CountOnly = a == "count";
                    FrameRecorder.ProbeOnly = a == "probe";
                    break;
                }
                case "record-stop":
                {
                    FrameRecorder.Stop();
                    ModLog.Info("uitest: recorded " + FrameRecorder.Frames + " frames");
                    if (FrameRecorder.Frames == 0)
                    {
                        r.Error = "the recording saved no frames";
                    }

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
                case "wheel":
                {
                    // wheel CONTROL NOTCHES — turns the mouse wheel over a control as the game
                    // delivers it (to the control, then up its parents): negative is down.
                    UIComponent target = FindControl(a);
                    if (target == null)
                    {
                        r.Error = a + " is not shown";
                        break;
                    }

                    var p = new UIMouseEventParameter(
                        target,
                        UIMouseButton.None,
                        0,
                        new Ray(),
                        Vector2.zero,
                        Vector2.zero,
                        ParseFloat(b, -1f)
                    );
                    GameInternals.Ui.MouseWheel.Invoke(target, new object[] { p });
                    yield return null;
                    break;
                }
                case "expect-scroll":
                {
                    // expect-scroll LIST top|moved — a scrolling list (or the investments list,
                    // FinanceOverhaulInvestmentsView) is at its top, or has moved from it.
                    float at;
                    if (a == InvestmentsList.Prefix + "View")
                    {
                        InvestmentsList shown = InvestmentsList.Current;
                        if (shown == null)
                        {
                            r.Error = "the investments list is not shown";
                            break;
                        }

                        at = shown.Offset;
                    }
                    else
                    {
                        UIScrollablePanel list = FindControl(a) as UIScrollablePanel;
                        if (list == null)
                        {
                            r.Error = "no visible scrolling list " + a;
                            break;
                        }

                        at = list.scrollPosition.y;
                    }

                    bool moved = at > 0.5f;
                    if ((b == "moved") != moved)
                    {
                        r.Error =
                            a
                            + " is at "
                            + at.ToString("0.0", CultureInfo.InvariantCulture)
                            + ", expected "
                            + b;
                    }

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
                default:
                    r.Unknown = true; // not one of this group's commands
                    yield break;
            }
        }
    }
}

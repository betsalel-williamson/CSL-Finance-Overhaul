using System.Collections; // IEnumerator
using ColossalFramework.UI; // UIComponent, UIScrollablePanel
using UnityEngine; // Mathf, Time, WaitForSeconds

// Game classes used without a using (global namespace, Assembly-CSharp): EconomyPanel,
//   SimulationManager, ToolsModifierControl

namespace FinanceOverhaul
{
    /// <summary>Game UI test commands: The many-stocks stress test (dev test saves).</summary>
    public partial class UiTestRunner
    {
        private IEnumerator StressSteps(
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
                case "stress-stocks":
                {
                    // stress-stocks add N | fill TOTAL | clear — dev stress listings (scenario 51):
                    // adds N copies of a share stock (or enough to reach TOTAL investments), each
                    // asking the game for a slot, or frees them all. Needs the dev test stock
                    // option (test saves only).
                    if (!StressStocks.Allowed)
                    {
                        r.Error = "turn on the dev test stock option (test save) first";
                        break;
                    }

                    if (a == "clear")
                    {
                        StressStocks.ClearSlots();
                        yield return new WaitForSeconds(1f);
                        StressStocks.NotifyInvestmentsChanged();
                        StressStocks.DestroyPrefabs();
                        yield return new WaitForSeconds(0.5f);
                    }
                    else if (a == "slots")
                    {
                        SimulationManager.instance.AddAction(StressStocks.LogSlots);
                        yield return new WaitForSeconds(1f);
                    }
                    else
                    {
                        int n = (int)ParseFloat(b, 0f);
                        if (a == "fill")
                        {
                            n -= StressStocks.SlotsUsed();
                        }

                        if (n > 0)
                        {
                            StressStocks.Add(n);
                        }

                        yield return new WaitForSeconds(1.5f);
                        StressStocks.NotifyInvestmentsChanged();
                        yield return new WaitForSeconds(0.5f);
                    }

                    break;
                }
                case "stress-measure":
                {
                    // stress-measure SECONDS — frame time and the daily investment update cost
                    // while the city runs, logged as "stress: measure …".
                    float seconds = ParseFloat(a, 5f);
                    StressStocks.ResetStepTiming();
                    StressStocks.Measuring = true;
                    float end = Time.unscaledTime + seconds;
                    int frames = 0;
                    float sum = 0f,
                        max = 0f;
                    while (Time.unscaledTime < end)
                    {
                        yield return null;
                        float dt = Time.unscaledDeltaTime * 1000f;
                        frames++;
                        sum += dt;
                        max = Mathf.Max(max, dt);
                    }

                    StressStocks.Measuring = false;
                    ModLog.Info(
                        "stress: measure "
                            + seconds
                            + " s with "
                            + StressStocks.SlotsUsed()
                            + " slots: frames avg "
                            + (frames == 0 ? 0f : sum / frames).ToString("0.0")
                            + " ms, max "
                            + max.ToString("0.0")
                            + " ms; "
                            + StressStocks.StepSummary()
                    );
                    break;
                }
                case "scroll-investments":
                {
                    // scroll-investments bottom — scrolls Economy → Investments' list of stocks
                    // to buy to its end (the vanilla list, found by the panel's own field).
                    EconomyPanel economy = UnityEngine.Object.FindObjectOfType<EconomyPanel>();
                    UIScrollablePanel listPanel =
                        economy == null
                            ? null
                            : GameInternals.Economy.InvestmentsList.GetValue(economy)
                                as UIScrollablePanel;
                    if (listPanel == null)
                    {
                        r.Error = "open Economy → Investments first";
                        break;
                    }

                    listPanel.ScrollToBottom();
                    yield return new WaitForSeconds(0.5f);
                    break;
                }
                case "count-investment-rows":
                {
                    // count-investment-rows — logs how many rows Economy → Investments shows for
                    // stocks to buy, against the number of investments.
                    EconomyPanel economy = UnityEngine.Object.FindObjectOfType<EconomyPanel>();
                    System.Collections.IDictionary rows =
                        economy == null
                            ? null
                            : GameInternals.Economy.PurchasableInvestments.GetValue(economy)
                                as System.Collections.IDictionary;
                    if (rows == null)
                    {
                        r.Error = "open Economy → Investments first";
                        break;
                    }

                    int total = 0,
                        visible = 0;
                    foreach (System.Collections.DictionaryEntry e in rows)
                    {
                        total++;
                        UIComponent row = e.Value as UIComponent;
                        if (row != null && row.isVisible)
                        {
                            visible++;
                        }
                    }

                    ModLog.Info(
                        "investments panel: "
                            + visible
                            + " visible row(s) for "
                            + StressStocks.SlotsUsed()
                            + " investments ("
                            + total
                            + " built)"
                    );
                    break;
                }
                case "stress-panel":
                {
                    // stress-panel — opens Economy → Investments and logs how long that takes
                    // (the call itself and the worst frame in the next second), then closes it.
                    int tabIndex = GameInternals.Economy.TabIndex(
                        GameInternals.Economy.InvestmentTab
                    );
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    ToolsModifierControl.mainToolbar.ShowEconomyPanel(tabIndex);
                    double openMs = sw.Elapsed.TotalMilliseconds;
                    float end = Time.unscaledTime + 1f;
                    float worst = 0f;
                    while (Time.unscaledTime < end)
                    {
                        yield return null;
                        worst = Mathf.Max(worst, Time.unscaledDeltaTime * 1000f);
                    }

                    ModLog.Info(
                        "stress: Investments panel with "
                            + StressStocks.SlotsUsed()
                            + " slots: open "
                            + openMs.ToString("0.0")
                            + " ms, worst frame "
                            + worst.ToString("0.0")
                            + " ms"
                    );
                    break;
                }
                default:
                    r.Unknown = true; // not one of this group's commands
                    yield break;
            }
        }
    }
}

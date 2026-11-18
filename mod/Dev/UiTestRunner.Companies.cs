using System.Collections;
using System.Text;
using FinanceOverhaul.Core;
using UnityEngine;

namespace FinanceOverhaul
{
    /// <summary>Game UI test commands: Local IPOs (the daily scan of businesses).</summary>
    public partial class UiTestRunner
    {
        private IEnumerator CompanySteps(
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
                case "local-ipos":
                {
                    // local-ipos on|off|restore — the Options flag (restore: as the player had it).
                    if (Mod.Settings == null)
                    {
                        r.Error = "no settings loaded";
                        break;
                    }

                    if (!ctx.LocalIposBefore.HasValue)
                    {
                        ctx.LocalIposBefore = Mod.Settings.LocalIposEnabled;
                    }

                    Mod.Settings.LocalIposEnabled =
                        a == "restore" ? ctx.LocalIposBefore.Value : a == "on";
                    break;
                }
                case "ipo-healthy-days":
                {
                    // ipo-healthy-days N — the healthy days a business needs to be a candidate,
                    // until the scenario ends (the real rule is 365).
                    CompanyDesk.Rules = new IpoRules { HealthyDays = (int)ParseFloat(a, 365f) };
                    break;
                }
                case "ipo-forget":
                    // Forgets the healthy-day counts in memory (the save is untouched).
                    CompanyDesk.ForgetCounts();
                    break;
                case "ipo-scan":
                {
                    // ipo-scan — reads every zoned business now, with the simulation paused, and
                    // logs what the city has of each kind beside the game's own district tallies.
                    SimulationManager sim = SimulationManager.instance;
                    bool paused = sim.SimulationPaused;
                    sim.SimulationPaused = true;
                    yield return new WaitForSeconds(0.5f); // let a running step finish
                    CompanyDesk.Scan();
                    sim.SimulationPaused = paused;
                    IpoRules rules = CompanyDesk.Rules ?? new IpoRules();
                    int candidates = 0;
                    foreach (KindFacts k in CompanyDesk.Kinds())
                    {
                        candidates += k.Candidates;
                        KindStatus status = IpoBoard.Status(k, true, rules);
                        ModLog.Info(
                            "uitest: ipo kind "
                                + k.Kind
                                + " squares="
                                + k.Squares
                                + " topLevel="
                                + k.TopLevelBuildings
                                + " candidates="
                                + k.Candidates
                                + " slots="
                                + status.Slots
                                + " state="
                                + status.State
                                + " bar="
                                + status.BarHave
                                + "/"
                                + status.BarNeed
                        );
                    }

                    District city = DistrictManager.instance.m_districts.m_buffer[0];
                    var game = new StringBuilder();
                    game.Append(" commercial ")
                        .Append(city.m_commercialData.m_finalBuildingCount)
                        .Append('/')
                        .Append(city.m_commercialData.m_finalBuildingArea);
                    game.Append(" office ")
                        .Append(city.m_officeData.m_finalBuildingCount)
                        .Append('/')
                        .Append(city.m_officeData.m_finalBuildingArea);
                    game.Append(" industrial ")
                        .Append(city.m_industrialData.m_finalBuildingCount)
                        .Append('/')
                        .Append(city.m_industrialData.m_finalBuildingArea);
                    game.Append(" level3 c/o/i ")
                        .Append(city.m_commercialData.m_finalLevel3)
                        .Append('/')
                        .Append(city.m_officeData.m_finalLevel3)
                        .Append('/')
                        .Append(city.m_industrialData.m_finalLevel3);
                    ModLog.Info("uitest: ipo game tallies (count/area):" + game);
                    ModLog.Info("uitest: ipo health:" + CompanyDesk.LastHealthBreakdown);
                    ModLog.Info(
                        "uitest: ipo scanned "
                            + CompanyDesk.LastBusinesses
                            + " business(es), "
                            + candidates
                            + " candidate(s), "
                            + CompanyDesk.LastScanMilliseconds.ToString("0.0")
                            + " ms, "
                            + GameAreaManager.instance.m_areaCount
                            + " tile(s) owned"
                    );
                    if (CompanyDesk.LastBusinesses == 0)
                    {
                        r.Error = "the scan found no businesses";
                    }

                    break;
                }
                case "ipo-income":
                {
                    // ipo-income — for shops, offices and industry: the weekly income the game
                    // reports, and the yearly earnings and offering price the mod would give
                    // each business of that zone (lowest, middle, highest; how many sit at the
                    // ₡1 floor or the ₡100,000 ceiling).
                    SimulationManager sim = SimulationManager.instance;
                    bool paused = sim.SimulationPaused;
                    sim.SimulationPaused = true;
                    yield return new WaitForSeconds(0.5f);
                    EconomyManager em = EconomyManager.instance;
                    IpoRules rules = CompanyDesk.Rules ?? new IpoRules();
                    Building[] buffer = BuildingManager.instance.m_buildings.m_buffer;
                    foreach (
                        ItemClass.Service service in new[]
                        {
                            ItemClass.Service.Commercial,
                            ItemClass.Service.Office,
                            ItemClass.Service.Industrial,
                        }
                    )
                    {
                        long weeklyIncome,
                            weeklyExpenses;
                        em.GetIncomeAndExpenses(
                            service,
                            ItemClass.SubService.None,
                            ItemClass.Level.None,
                            out weeklyIncome,
                            out weeklyExpenses
                        );
                        long zone = CompanyDesk.ZoneFormulaIncome(service);
                        var prices = new System.Collections.Generic.List<long>();
                        long taxBack = 0;
                        for (int i = 1; i < buffer.Length; i++)
                        {
                            BuildingInfo info = buffer[i].Info;
                            if (
                                (buffer[i].m_flags & Building.Flags.Created) == 0
                                || info == null
                                || info.m_class == null
                                || info.m_class.m_service != service
                                || !(info.m_buildingAI is PrivateBuildingAI)
                            )
                            {
                                continue;
                            }

                            int rate = em.GetTaxRate(info.m_class);
                            long earnings = IpoEarnings.AnnualCents(
                                CompanyDesk.IncomePerStep((ushort)i),
                                zone,
                                weeklyIncome,
                                rate
                            );
                            taxBack += earnings * rate / 100 / IpoEarnings.WeeksPerYear;
                            prices.Add(IpoPricing.OfferingPriceCents(earnings, rules));
                        }

                        prices.Sort();
                        int floor = 0,
                            ceiling = 0;
                        foreach (long price in prices)
                        {
                            floor += price <= IpoPricing.MinPriceCents ? 1 : 0;
                            ceiling += price >= IpoPricing.MaxPriceCents ? 1 : 0;
                        }

                        ModLog.Info(
                            "uitest: ipo income "
                                + service
                                + " businesses="
                                + prices.Count
                                + " gameWeeklyIncome="
                                + weeklyIncome
                                + " sharedBackOut="
                                + taxBack
                                + " offerPrice low/mid/high="
                                + (
                                    prices.Count == 0
                                        ? "n/a"
                                        : prices[0] / 100
                                            + "/"
                                            + prices[prices.Count / 2] / 100
                                            + "/"
                                            + prices[prices.Count - 1] / 100
                                )
                                + " atFloor="
                                + floor
                                + " atCeiling="
                                + ceiling
                        );
                    }

                    sim.SimulationPaused = paused;
                    break;
                }
                case "expect-ipo-candidates":
                {
                    // expect-ipo-candidates none|some — candidates after the last scan.
                    int candidates = 0;
                    foreach (KindFacts k in CompanyDesk.Kinds())
                    {
                        candidates += k.Candidates;
                    }

                    if ((candidates > 0) != (a == "some"))
                    {
                        r.Error = candidates + " candidate(s), expected " + a;
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

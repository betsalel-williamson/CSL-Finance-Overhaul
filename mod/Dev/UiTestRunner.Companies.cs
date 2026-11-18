using System.Collections;
using System.Collections.Generic;
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
                        long zone = CompanyDesk.ZoneEarningsWeight(service);
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
                                CompanyDesk.EarningsWeight((ushort)i),
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
                case "ipo-list-now":
                case "ipo-first-day":
                case "ipo-buyout":
                case "ipo-remove-test":
                {
                    // ipo-list-now — lists the first candidate as a company (after ipo-scan).
                    // ipo-first-day — closes its offering window today. ipo-buyout — it is
                    // bought out today. ipo-remove-test — removes what this scenario listed, as
                    // if it never had (also done when the scenario ends).
                    SimulationManager sim = SimulationManager.instance;
                    bool paused = sim.SimulationPaused;
                    sim.SimulationPaused = true;
                    yield return new WaitForSeconds(0.5f); // let a running step finish
                    if (cmd == "ipo-remove-test")
                    {
                        RemoveTestCompanies(ctx);
                    }
                    else if (cmd == "ipo-list-now")
                    {
                        bool wasHistorical;
                        LocalCompany listed = CompanyDesk.TestListFirstCandidate(out wasHistorical);
                        if (listed == null)
                        {
                            r.Error = "no candidate to list (ipo-healthy-days and ipo-scan first)";
                        }
                        else
                        {
                            ctx.TestCompanies.Add(
                                new TestCompany
                                {
                                    Id = listed.Id,
                                    Home = listed.Home,
                                    WasHistorical = wasHistorical,
                                }
                            );
                        }
                    }
                    else if (ctx.TestCompanies.Count == 0)
                    {
                        r.Error = "ipo-list-now first";
                    }
                    else if (cmd == "ipo-first-day")
                    {
                        CompanyDesk.TestOpenFirstDay(
                            ctx.TestCompanies[ctx.TestCompanies.Count - 1].Id
                        );
                    }
                    else
                    {
                        CompanyDesk.TestBuyOut(ctx.TestCompanies[ctx.TestCompanies.Count - 1].Id);
                    }

                    sim.SimulationPaused = paused;
                    yield return new WaitForSeconds(1.5f); // the stock type and slot follow
                    break;
                }
                case "expect-company":
                {
                    // expect-company offering|trading|gone [listed|unlisted] — this scenario's
                    // newest company, and whether the game has a stock for it.
                    if (ctx.TestCompanies.Count == 0)
                    {
                        r.Error = "ipo-list-now first";
                        break;
                    }

                    int id = ctx.TestCompanies[ctx.TestCompanies.Count - 1].Id;
                    LocalCompany c = CompanyDesk.CompanyOf(id);
                    string state =
                        c == null ? "gone"
                        : c.Status == CompanyStatus.Offering ? "offering"
                        : "trading";
                    int slot = CompanyListing.SlotOf(id);
                    ModLog.Info(
                        "uitest: company "
                            + id
                            + " "
                            + state
                            + (
                                c == null
                                    ? ""
                                    : " '"
                                        + c.Name
                                        + "' price "
                                        + c.PriceCents
                                        + " offer "
                                        + c.OfferPriceCents
                            )
                            + " slot "
                            + slot
                            + (
                                slot > 0
                                    ? " game price "
                                        + EconomyManager.instance.m_investments.m_buffer[slot].value
                                    : ""
                            )
                    );
                    if (state != a)
                    {
                        r.Error = "the company is " + state + ", expected " + a;
                    }
                    else if (b == "listed" && slot <= 0)
                    {
                        r.Error = "the game has no stock for the company";
                    }
                    else if (b == "unlisted" && slot > 0)
                    {
                        r.Error = "the game still has a stock for the company";
                    }

                    break;
                }
                case "ipo-remove-all":
                {
                    // ipo-remove-all — dev clean-up: every local company is removed as if none
                    // had ever listed (no payout) and the city's mod data is rewritten.
                    SimulationManager sim = SimulationManager.instance;
                    bool paused = sim.SimulationPaused;
                    sim.SimulationPaused = true;
                    yield return new WaitForSeconds(0.5f);
                    int removed = CompanyDesk.TestRemoveAll();
                    sim.SimulationPaused = paused;
                    yield return new WaitForSeconds(1f);
                    ModLog.Info(
                        "uitest: ipo-remove-all removed "
                            + removed
                            + "; company stocks left "
                            + CompanyListing.Listed
                    );
                    break;
                }
                case "historical-off":
                {
                    // historical-off NAME… — takes the Historical flag off every zoned business
                    // with exactly this name (undoing a listing whose record was lost).
                    string wanted = (a + " " + (b ?? string.Empty)).Trim();
                    Building[] buildings = BuildingManager.instance.m_buildings.m_buffer;
                    int cleared = 0;
                    for (int i = 1; i < buildings.Length; i++)
                    {
                        if (
                            (buildings[i].m_flags & Building.Flags.Created) == 0
                            || (buildings[i].m_flags & Building.Flags.Historical) == 0
                            || buildings[i].Info == null
                            || !(buildings[i].Info.m_buildingAI is PrivateBuildingAI)
                        )
                        {
                            continue;
                        }

                        string name = BuildingManager.instance.GetBuildingName(
                            (ushort)i,
                            new InstanceID { Building = (ushort)i }
                        );
                        if (name == wanted)
                        {
                            buildings[i].m_flags &= ~Building.Flags.Historical;
                            cleared++;
                            ModLog.Info(
                                "uitest: historical off for building " + i + " '" + name + "'"
                            );
                        }
                    }

                    ModLog.Info("uitest: historical-off '" + wanted + "' cleared " + cleared);
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

        /// <summary>Removes every company a scenario listed, as if it never had.</summary>
        private static void RemoveTestCompanies(Context ctx)
        {
            Building[] buildings = BuildingManager.instance.m_buildings.m_buffer;
            foreach (TestCompany one in ctx.TestCompanies)
            {
                CompanyDesk.TestRemove(one.Id, one.WasHistorical);
                // A company the scenario had bought out is already gone from the records, but
                // its building keeps the Historical flag: put that back too.
                if (
                    !one.WasHistorical
                    && buildings[one.Home.Id].m_buildIndex == one.Home.BuildIndex
                )
                {
                    buildings[one.Home.Id].m_flags &= ~Building.Flags.Historical;
                }
            }

            ctx.TestCompanies.Clear();
        }
    }
}

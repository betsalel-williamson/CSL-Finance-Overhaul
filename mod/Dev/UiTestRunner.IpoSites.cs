using System.Collections; // IEnumerator
using System.Collections.Generic; // Dictionary, KeyValuePair, List, SortedDictionary
using System.Text; // StringBuilder
// FinanceOverhaul.Core: BondText, BusinessKind, BusinessKinds, BusinessSite, IpoBoard, IpoEarnings,
//   IpoPricing, IpoRules, KindFacts, KindStatus, LocalCompany
using FinanceOverhaul.Core;
using UnityEngine; // Mathf, Vector3, WaitForSeconds

// Game classes used without a using (global namespace, Assembly-CSharp): Building, BuildingInfo,
//   BuildingManager, DefaultTool, District, DistrictManager, DistrictPark, EconomyManager,
//   GameAreaManager, InstanceID, ItemClass, PrivateBuildingAI, SimulationManager, UniqueFactoryAI

namespace FinanceOverhaul
{
    /// <summary>Game UI test commands: the Local IPOs industry sites and the city scan (profit, workers, areas and factories, income).</summary>
    public partial class UiTestRunner
    {
        private IEnumerator IpoSiteSteps(
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
                case "ipo-site-profit":
                {
                    // ipo-site-profit CENTS|real — the weekly profit (in cents) every industry area
                    // and unique factory reports until the scenario ends; real: the game's. A
                    // scenario never assumes what the city's areas earn. Not recorded in their
                    // history.
                    IndustrySites.TestWeeklyProfitCents =
                        a == "real" ? (long?)null : (long)ParseFloat(a, 0f);
                    break;
                }
                case "ipo-site-workers":
                {
                    // ipo-site-workers N|real — the workers every industry area and unique
                    // factory reports until the scenario ends; real: the game's. A scenario
                    // never assumes how many work at the city's areas and factories.
                    IndustrySites.TestWorkers = a == "real" ? (int?)null : (int)ParseFloat(a, 0f);
                    break;
                }
                case "ipo-credit-sites-again":
                {
                    // ipo-credit-sites-again — dev: industry areas and unique factories are
                    // forgotten and their one-time credit starts again from the next scan, as for
                    // a city updating to 0.2.0 (kept: it changes the city's counts as an update
                    // would).
                    CompanyDesk.TestCreditSitesAgain();
                    break;
                }
                case "ipo-area-panel":
                {
                    // ipo-area-panel — opens the industry area's own panel (Info, Policies and,
                    // listed, Shares) for this scenario's newest company, an area.
                    LocalCompany c =
                        ctx.TestCompanies.Count == 0
                            ? null
                            : CompanyDesk.CompanyOf(
                                ctx.TestCompanies[ctx.TestCompanies.Count - 1].Id
                            );
                    if (c == null || c.Site != BusinessSite.Area)
                    {
                        r.Error = "ipo-list-now area first";
                        break;
                    }

                    Vector3 at = BuildingManager
                        .instance
                        .m_buildings
                        .m_buffer[c.Home.Id]
                        .m_position;
                    byte park = DistrictManager.instance.GetPark(at);
                    if (park == 0)
                    {
                        r.Error = "the area's main building stands in no area";
                        break;
                    }

                    DefaultTool.OpenWorldInfoPanel(new InstanceID { Park = park }, at);
                    yield return null;
                    break;
                }
                case "expect-company-site":
                {
                    // expect-company-site area|factory|zoned — what this scenario's newest
                    // company is; logs its name, kind, headquarters and price.
                    if (ctx.TestCompanies.Count == 0)
                    {
                        r.Error = "ipo-list-now first";
                        break;
                    }

                    LocalCompany c = CompanyDesk.CompanyOf(
                        ctx.TestCompanies[ctx.TestCompanies.Count - 1].Id
                    );
                    string site = c == null ? "gone" : c.Site.ToString().ToLowerInvariant();
                    ModLog.Info(
                        "uitest: company "
                            + (c == null ? "-" : c.Name + " (" + c.Kind + ", " + c.SiteRef)
                            + ") headquarters "
                            + (c == null ? "-" : c.Home.Id.ToString())
                            + " offered at "
                            + (c == null ? "-" : BondText.WholeMoney(c.OfferPriceCents))
                    );
                    if (site != a)
                    {
                        r.Error = "the company is " + site + ", expected " + a;
                    }

                    break;
                }
                case "ipo-industry":
                {
                    // ipo-industry — logs every Industries DLC area (kind, name, level, workers,
                    // the main building, a week's export income, raw material bought, upkeep)
                    // and every unique factory (workers, production, a week's income, inputs,
                    // upkeep), as the game's own panels work them out. For the industry-area
                    // design of Local IPOs; changes nothing.
                    LogIndustryAreas();
                    break;
                }
                case "ipo-workers":
                {
                    // ipo-workers — counts every business in the city as the game's panels do:
                    // each zoned kind grouped by building type, every industry area summed over
                    // its buildings, and every unique factory. Jobs and workers today.
                    Building[] buffer = BuildingManager.instance.m_buildings.m_buffer;
                    DistrictManager districts = DistrictManager.instance;
                    var groups = new SortedDictionary<string, int[]>();
                    var parkJobs = new Dictionary<byte, int[]>();
                    for (int i = 1; i < buffer.Length; i++)
                    {
                        if ((buffer[i].m_flags & Building.Flags.Created) == 0)
                        {
                            continue;
                        }

                        BuildingInfo info = buffer[i].Info;
                        if (info == null || info.m_class == null || info.m_buildingAI == null)
                        {
                            continue;
                        }

                        var id = (ushort)i;
                        BusinessKind kind;
                        bool zoned =
                            info.m_buildingAI is PrivateBuildingAI
                            && BusinessKinds.TryFromGame(
                                info.m_class.m_service.ToString(),
                                info.m_class.m_subService.ToString(),
                                out kind
                            );
                        bool industry = info.m_class.m_service == ItemClass.Service.PlayerIndustry;
                        if (!zoned && !industry)
                        {
                            continue;
                        }

                        int jobs = BuildingStaff.Jobs(id);
                        int workers = BuildingStaff.Workers(id);
                        if (zoned)
                        {
                            string key =
                                info.m_class.m_subService
                                + " "
                                + info.m_buildingAI.GetType().Name
                                + " "
                                + buffer[i].Width
                                + "x"
                                + buffer[i].Length; // not shown: the log
                            int[] g;
                            if (!groups.TryGetValue(key, out g))
                            {
                                g = new[] { 0, 0, 0, int.MaxValue, 0 };
                                groups[key] = g;
                            }

                            g[0]++;
                            g[1] += jobs;
                            g[2] += workers;
                            g[3] = Mathf.Min(g[3], workers);
                            g[4] = Mathf.Max(g[4], workers);
                            continue;
                        }

                        if (info.m_buildingAI is UniqueFactoryAI)
                        {
                            ModLog.Info(
                                "uitest: workers factory '"
                                    + info.name
                                    + "' building "
                                    + i
                                    + " jobs "
                                    + jobs
                                    + " workers "
                                    + workers
                            );
                        }

                        byte park = districts.GetPark(buffer[i].m_position);
                        ModLog.Info(
                            "uitest: workers industry building "
                                + i
                                + " '"
                                + info.name
                                + "' "
                                + info.m_buildingAI.GetType().Name
                                + " area "
                                + park
                                + " jobs "
                                + jobs
                                + " workers "
                                + workers
                        );
                        int[] pj;
                        if (!parkJobs.TryGetValue(park, out pj))
                        {
                            pj = new int[3];
                            parkJobs[park] = pj;
                        }

                        pj[0]++;
                        pj[1] += jobs;
                        pj[2] += workers;
                    }

                    foreach (KeyValuePair<string, int[]> g in groups)
                    {
                        ModLog.Info(
                            "uitest: workers zoned "
                                + g.Key
                                + " n="
                                + g.Value[0]
                                + " jobs="
                                + g.Value[1]
                                + " workers="
                                + g.Value[2]
                                + " min="
                                + g.Value[3]
                                + " max="
                                + g.Value[4]
                        );
                    }

                    foreach (KeyValuePair<byte, int[]> pj in parkJobs)
                    {
                        DistrictPark park = districts.m_parks.m_buffer[pj.Key];
                        ModLog.Info(
                            "uitest: workers area "
                                + pj.Key
                                + " '"
                                + (pj.Key == 0 ? "(no area)" : districts.GetParkName(pj.Key))
                                + "' type "
                                + park.m_parkType
                                + " level "
                                + park.m_parkLevel
                                + " gate "
                                + park.m_mainGate
                                + " buildings "
                                + pj.Value[0]
                                + " jobs "
                                + pj.Value[1]
                                + " workers "
                                + pj.Value[2]
                        );
                    }

                    break;
                }
                case "ipo-scan":
                {
                    // ipo-scan — reads every zoned business now, with the simulation paused, and
                    // logs what the city has of each kind beside the game's own district tallies.
                    SimulationManager sim = SimulationManager.instance;
                    bool paused = sim.SimulationPaused;
                    sim.SimulationPaused = true;
                    yield return new WaitForSeconds(0.5f); // let a running step finish
                    CompanyDesk.MeasureJobSizes = true;
                    CompanyDesk.Scan();
                    CompanyDesk.MeasureJobSizes = false;
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
                    ModLog.Info("uitest: ipo areas and factories:" + CompanyDesk.LastSites);
                    ModLog.Info("uitest: ipo jobs:" + CompanyDesk.LastJobSizes);
                    ModLog.Info("uitest: ipo shortlists (workers):" + CompanyDesk.LastShortlists);
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
                default:
                    r.Unknown = true; // not one of this group's commands
                    yield break;
            }
        }
    }
}

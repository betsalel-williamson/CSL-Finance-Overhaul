using System.Collections; // IEnumerator
using System.Collections.Generic; // Dictionary, List
using System.Text; // StringBuilder
// ColossalFramework.UI: LayoutDirection, UIButton, UICheckBox, UIComponent, UILabel,
//   UIScrollablePanel, UISlider, UISprite, UITextField, UITextureAtlas, UIView
using ColossalFramework.UI;
// FinanceOverhaul.Core: BoardView, BondText, BuildingFraming, BusinessSite, CompanyStatus,
//   IpoBoard, IpoCardText, IpoEarnings, IpoPricing, IpoRules, IpoTuning, IpoUserPreset, KindFacts,
//   KindState, KindStatus, LocalCompany, ModSettings, Strings
using FinanceOverhaul.Core;
using UnityEngine; // Mathf, Object, RectOffset, Vector2, Vector3, WaitForSeconds

// Game classes used without a using (global namespace, Assembly-CSharp): Building, BuildingInfo,
//   BuildingManager, CameraController, District, DistrictAreaResourceData, DistrictManager,
//   DistrictPark, EconomyManager, FastList, GameAreaManager, IndustryBuildingAI, InstanceID,
//   Investment, ItemClass, LocaleFormatter, PrivateBuildingAI, SimulationManager,
//   ToolsModifierControl, TransferManager, UIHelper, UniqueFactoryAI

namespace FinanceOverhaul
{
    /// <summary>Game UI test commands: Local IPOs (the daily scan of businesses).</summary>
    public partial class UiTestRunner
    {
        private const string OptionsPreviewName = "FinanceOverhaulOptionsPreview";

        // The player's Local IPOs values and saved presets, kept by ipo-options-keep.
        private static IpoRules keptValues;
        private static List<IpoUserPreset> keptPresets;

        // Options this scenario typed into, with their values before (put back by "restore").
        private static readonly Dictionary<string, int> typedBefore = new Dictionary<string, int>();

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

                    if (a == "keep")
                    {
                        // local-ipos keep — on, saved, and left on when the scenario ends (for
                        // switching the feature on in a running test city).
                        Mod.Settings.LocalIposEnabled = true;
                        ctx.LocalIposBefore = true;
                        Mod.SaveSettings();
                        break;
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
                case "options-preview":
                {
                    // options-preview on|off|scroll Y — builds the Options page (Options → Finance
                    // Overhaul) into a panel over the game, as the game would, so a scenario can
                    // read and capture it; logs how many Local IPOs sliders it has. scroll Y
                    // scrolls the preview down Y pixels.
                    if (a == "scroll")
                    {
                        var shown = FindControl(OptionsPreviewName, true) as UIScrollablePanel;
                        string[] parts = line.Substring(cmd.Length).Trim().Split(' ');
                        if (shown != null && parts.Length == 2)
                        {
                            shown.scrollPosition = new Vector2(0f, ParseFloat(parts[1], 0f));
                        }

                        yield return new WaitForSeconds(0.3f);
                        break;
                    }

                    UIComponent old = FindControl(OptionsPreviewName, true);
                    if (old != null)
                    {
                        Object.Destroy(old.gameObject);
                    }

                    if (a != "on")
                    {
                        break;
                    }

                    UIView view = UIView.GetAView();
                    var root = view.AddUIComponent(typeof(UIScrollablePanel)) as UIScrollablePanel;
                    root.name = OptionsPreviewName;
                    root.backgroundSprite = "MenuPanel2"; // not shown: a game sprite name
                    root.size = new Vector2(1860f, 1080f);
                    root.relativePosition = new Vector3(30f, 40f);
                    root.autoLayout = true;
                    root.autoLayoutDirection = LayoutDirection.Horizontal;
                    root.wrapLayout = true;
                    root.autoLayoutPadding = new RectOffset(10, 10, 10, 0);
                    root.clipChildren = true;
                    ModOptions.Build(new UIHelper(root));
                    int sliders = 0;
                    foreach (UISlider s in root.GetComponentsInChildren<UISlider>())
                    {
                        if (s.name.StartsWith("FinanceOverhaulIpoOption-"))
                        {
                            sliders++;
                        }
                    }

                    ModLog.Info("uitest: options preview has " + sliders + " Local IPOs slider(s)");
                    yield return new WaitForSeconds(0.5f);
                    break;
                }
                case "ipo-option-type":
                {
                    // ipo-option-type KEY TEXT|restore — types TEXT into that option's number
                    // field in the Options preview and submits it (restore: the value it had
                    // before this scenario first typed into it). Logs the field afterwards.
                    string[] parts = line.Substring(cmd.Length).Trim().Split(new[] { ' ' }, 2);
                    IpoTuning t = IpoTuning.Find(parts[0]);
                    if (t == null || parts.Length < 2)
                    {
                        r.Error = "ipo-option-type KEY TEXT";
                        break;
                    }

                    ModSettings settings = Mod.EnsureSettings();
                    if (!typedBefore.ContainsKey(t.Key))
                    {
                        typedBefore[t.Key] = t.Get(settings.Ipo);
                    }

                    string text =
                        parts[1] == "restore" ? t.Shown(typedBefore[t.Key]).ToString() : parts[1];
                    string shown = IpoOptions.TestType(t.Key, text);
                    if (shown == null)
                    {
                        r.Error = "no number field for " + t.Key + " (options-preview on first)";
                        break;
                    }

                    if (parts[1] == "restore")
                    {
                        typedBefore.Remove(t.Key);
                    }

                    ModLog.Info(
                        "uitest: typed '" + text + "' into " + t.Key + "; it shows " + shown
                    );
                    break;
                }
                case "expect-ipo-option":
                {
                    // expect-ipo-option KEY VALUE — the option's value as the player sees it,
                    // in the settings, in its label and in its number field.
                    string[] parts = line.Substring(cmd.Length).Trim().Split(' ');
                    IpoTuning t = IpoTuning.Find(parts[0]);
                    int want;
                    if (t == null || parts.Length != 2 || !int.TryParse(parts[1], out want))
                    {
                        r.Error = "expect-ipo-option KEY VALUE";
                        break;
                    }

                    int have = t.Shown(t.Get(Mod.EnsureSettings().Ipo));
                    UITextField field =
                        FindControl(IpoOptions.FieldPrefix + t.Key, true) as UITextField;
                    if (have != want)
                    {
                        r.Error = t.Key + " is " + have + ", expected " + want;
                    }
                    else if (field == null || field.text != t.FieldText(Mod.EnsureSettings().Ipo))
                    {
                        r.Error =
                            t.Key + "'s field reads '" + (field == null ? "-" : field.text) + "'";
                    }

                    break;
                }
                case "ipo-options-keep":
                case "ipo-options-put-back":
                {
                    // ipo-options-keep — remembers the player's Local IPOs values and saved
                    // presets; ipo-options-put-back puts them back (and the page shows them).
                    ModSettings settings = Mod.EnsureSettings();
                    if (cmd == "ipo-options-keep")
                    {
                        keptValues = new IpoRules();
                        foreach (IpoTuning t in IpoTuning.All)
                        {
                            t.Set(keptValues, t.Get(settings.Ipo));
                        }

                        keptPresets = new List<IpoUserPreset>(settings.IpoUserPresets);
                    }
                    else if (keptValues != null)
                    {
                        IpoOptions.TestPutBack(keptValues, keptPresets);
                        keptValues = null;
                    }

                    break;
                }
                case "ipo-section":
                {
                    // ipo-section GROUP LessLikely|Default|MoreLikely — clicks that section's button.
                    string[] parts = line.Substring(cmd.Length).Trim().Split(' ');
                    UIButton sectionButton =
                        parts.Length == 2
                            ? FindControl(
                                IpoOptions.SectionButtonPrefix + parts[0] + "-" + parts[1],
                                true
                            ) as UIButton
                            : null;
                    if (sectionButton == null)
                    {
                        r.Error = "no such section button (options-preview on first)";
                        break;
                    }

                    sectionButton.SimulateClick();
                    break;
                }
                case "ipo-preset-save":
                {
                    // ipo-preset-save NAME — Save as…, types NAME and clicks OK.
                    string name = line.Substring(cmd.Length).Trim();
                    UIButton save = FindControl(IpoOptions.SaveAsName, true) as UIButton;
                    if (save == null)
                    {
                        r.Error = "no Save as… button (options-preview on first)";
                        break;
                    }

                    save.SimulateClick();
                    yield return new WaitForSeconds(0.3f);
                    if (FoNameDialog.Field == null)
                    {
                        r.Error = "the name dialog did not open";
                        break;
                    }

                    FoNameDialog.Field.text = name;
                    FoNameDialog.TestOkOpen();
                    yield return new WaitForSeconds(0.3f);
                    if (FoNameDialog.IsOpen)
                    {
                        ModLog.Info("uitest: the name dialog stayed open for '" + name + "'");
                        FoNameDialog.Close();
                    }

                    break;
                }
                case "ipo-preset-delete":
                {
                    UIButton delete = FindControl(IpoOptions.DeleteName, true) as UIButton;
                    if (delete == null)
                    {
                        r.Error = "no Delete button (options-preview on first)";
                        break;
                    }

                    delete.SimulateClick();
                    break;
                }
                case "expect-ipo-preset":
                {
                    // expect-ipo-preset NAME — the presets dropdown shows NAME.
                    string want = line.Substring(cmd.Length).Trim();
                    string shown = IpoOptions.ShownPreset;
                    ModLog.Info("uitest: preset shown: " + shown);
                    if (shown != want)
                    {
                        r.Error = "the preset shown is '" + shown + "', expected '" + want + "'";
                    }

                    break;
                }
                case "expect-ipo-options":
                {
                    // expect-ipo-options N — the Options preview shows N Local IPOs sliders, each
                    // label saying its value.
                    UIComponent root = FindControl(OptionsPreviewName, true);
                    if (root == null)
                    {
                        r.Error = "options-preview on first";
                        break;
                    }

                    int found = 0;
                    foreach (UISlider s in root.GetComponentsInChildren<UISlider>())
                    {
                        if (!s.name.StartsWith("FinanceOverhaulIpoOption-"))
                        {
                            continue;
                        }

                        found++;
                        UITextField field = s.parent.Find<UITextField>(
                            IpoOptions.FieldPrefix
                                + s.name.Substring("FinanceOverhaulIpoOption-".Length)
                        );
                        if (field == null)
                        {
                            r.Error = s.name + " has no number field";
                            break;
                        }

                        UILabel label = s.parent.Find<UILabel>("Label");
                        IpoTuning t = IpoTuning.Find(
                            s.name.Substring("FinanceOverhaulIpoOption-".Length)
                        );
                        string want = t == null ? null : t.Label(CompanyDesk.Current);
                        if (label == null || label.text != want)
                        {
                            r.Error =
                                s.name + " reads '" + (label == null ? "-" : label.text) + "'";
                            break;
                        }
                    }

                    ModLog.Info("uitest: " + found + " Local IPOs slider(s) with their values");
                    if (r.Error == null && found != (int)ParseFloat(a, -1f))
                    {
                        r.Error = found + " Local IPOs sliders, expected " + a;
                    }

                    break;
                }
                case "sprites":
                {
                    // sprites PATTERN — logs the game's default atlas sprite names that contain
                    // PATTERN (case ignored), to find an icon by name.
                    var found = new StringBuilder();
                    int count = 0;
                    foreach (
                        UITextureAtlas.SpriteInfo info in UIView.GetAView().defaultAtlas.sprites
                    )
                    {
                        if (
                            info != null
                            && info.name.ToLowerInvariant().Contains(a.ToLowerInvariant())
                        )
                        {
                            found.Append(' ').Append(info.name);
                            count++;
                        }
                    }

                    ModLog.Info("uitest: " + count + " sprite(s) with '" + a + "':" + found);
                    break;
                }
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
                case "ipo-credit-sites-again":
                {
                    // ipo-credit-sites-again — dev: industry areas and unique factories are
                    // forgotten and their one-time credit starts again from the next scan, as for
                    // a city updating to 0.2.0 (kept: it changes the city's counts as an update
                    // would).
                    CompanyDesk.TestCreditSitesAgain();
                    break;
                }
                case "ipo-home-back":
                {
                    // ipo-home-back — every building this scenario treated as lost stands
                    // again, and today's step runs now (a company in its grace period whose
                    // headquarters stands gets it back).
                    SimulationManager sim = SimulationManager.instance;
                    bool paused = sim.SimulationPaused;
                    sim.SimulationPaused = true;
                    yield return new WaitForSeconds(0.5f); // let a running step finish
                    CompanyDesk.TestFindHomes();
                    CompanyDesk.TestStepToday();
                    sim.SimulationPaused = paused;
                    yield return new WaitForSeconds(1f);
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
                case "ipo-exchange-level":
                {
                    // ipo-exchange-level N|real — the Stock Exchange level IPOs see, until the
                    // scenario ends (0 = none); real reads the city's own. Logs both.
                    CompanyDesk.TestExchangeLevel = -1;
                    int own = CompanyDesk.ExchangeLevel();
                    CompanyDesk.TestExchangeLevel = a == "real" ? -1 : (int)ParseFloat(a, -1f);
                    ModLog.Info(
                        "uitest: Stock Exchange level "
                            + CompanyDesk.ExchangeLevel()
                            + " (the city's own: "
                            + own
                            + ")"
                    );
                    break;
                }
                case "expect-exchange-note":
                {
                    // expect-exchange-note on|off — on: every card with a slot open says the
                    // Stock Exchange must grow (locked and full cards keep their own lines);
                    // off: no card says so.
                    bool on = a == "on";
                    BoardView board = CompanyDesk.Board();
                    string needs = Strings.Format(
                        "ipo.card.note.exchange",
                        CompanyDesk.Current.ExchangeLevel
                    );
                    int checkedCards = 0;
                    foreach (KindStatus k in board.Kinds)
                    {
                        UILabel note = FindControl(CompanyCards.NotePrefix + k.Kind) as UILabel;
                        string text = note == null ? string.Empty : note.text;
                        bool slotOpen = k.State != KindState.Locked && k.State != KindState.Full;
                        ModLog.Info("uitest: card " + k.Kind + " note: " + text);
                        if (on && slotOpen && text != needs)
                        {
                            r.Error = k.Kind + " says \"" + text + "\", not \"" + needs + "\"";
                            break;
                        }

                        if (!on && text == needs)
                        {
                            r.Error = k.Kind + " still says \"" + needs + "\"";
                            break;
                        }

                        checkedCards += slotOpen ? 1 : 0;
                    }

                    if (r.Error == null && on && checkedCards == 0)
                    {
                        r.Error = "no card has a slot open (ipo-scan first)";
                    }

                    break;
                }
                case "ipo-grace":
                {
                    // ipo-grace N — days a company with no headquarters stays listed, until the
                    // scenario ends (the real rule is 90). After ipo-healthy-days, which resets
                    // the rules.
                    IpoRules rules = CompanyDesk.Rules ?? new IpoRules();
                    rules.HomeGraceDays = (int)ParseFloat(a, 90f);
                    CompanyDesk.Rules = rules;
                    break;
                }
                case "ipo-lose-home":
                {
                    // ipo-lose-home [nowhere] — this scenario's newest company loses its
                    // headquarters at today's step, which runs now; with nowhere, every
                    // candidate of its kind counts as lost too. Only for this scenario: the
                    // buildings themselves are untouched.
                    if (ctx.TestCompanies.Count == 0)
                    {
                        r.Error = "ipo-list-now first";
                        break;
                    }

                    SimulationManager sim = SimulationManager.instance;
                    bool paused = sim.SimulationPaused;
                    sim.SimulationPaused = true;
                    yield return new WaitForSeconds(0.5f); // let a running step finish
                    int lost = CompanyDesk.TestLoseHome(
                        ctx.TestCompanies[ctx.TestCompanies.Count - 1].Id,
                        a == "nowhere"
                    );
                    ModLog.Info("uitest: " + lost + " building(s) count as lost");
                    CompanyDesk.TestStepToday();
                    sim.SimulationPaused = paused;
                    yield return new WaitForSeconds(1f);
                    break;
                }
                case "expect-company-home":
                {
                    // expect-company-home moved|same|lost — where this scenario's newest
                    // company has its headquarters: another building than the one it listed
                    // on, the same one, or none (in its grace period).
                    if (ctx.TestCompanies.Count == 0)
                    {
                        r.Error = "ipo-list-now first";
                        break;
                    }

                    TestCompany listed = ctx.TestCompanies[ctx.TestCompanies.Count - 1];
                    LocalCompany c = CompanyDesk.CompanyOf(listed.Id);
                    string home =
                        c == null ? "gone"
                        : c.HomeLostDay >= 0 ? "lost"
                        : c.Home.Equals(listed.Home) ? "same"
                        : "moved";
                    ModLog.Info(
                        "uitest: company "
                            + listed.Id
                            + " home "
                            + home
                            + (c == null ? "" : " building " + c.Home.Id)
                    );
                    if (home != a)
                    {
                        r.Error = "the company's headquarters is " + home + ", expected " + a;
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
                        // ipo-list-now area|factory — the first industry area or unique factory.
                        BusinessSite site =
                            a == "area" ? BusinessSite.Area
                            : a == "factory" ? BusinessSite.Factory
                            : BusinessSite.Zoned;
                        LocalCompany listed = CompanyDesk.TestListFirstCandidate(
                            out wasHistorical,
                            site
                        );
                        if (listed == null)
                        {
                            r.Error =
                                "no "
                                + (site == BusinessSite.Zoned ? "business" : a)
                                + " to list (ipo-healthy-days and ipo-scan first)";
                        }
                        else if (a == "keep")
                        {
                            // ipo-list-now keep — the company stays listed after the scenario.
                            ModLog.Info("uitest: kept company " + listed.Id + " " + listed.Name);
                        }
                        else
                        {
                            ctx.TestCompanies.Add(
                                new TestCompany
                                {
                                    Id = listed.Id,
                                    Home = listed.Home,
                                    WasHistorical = wasHistorical,
                                    AnnouncedDay = listed.AnnouncedDay,
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

                    // An announcement with its dialog pauses the game itself: that stays.
                    if (cmd != "ipo-list-now" || IpoDialog.Silenced)
                    {
                        sim.SimulationPaused = paused;
                    }

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
                case "expect-company-first":
                {
                    // expect-company-first — the first card of Purchasable Investments is the
                    // scenario's newest company.
                    UIComponent list = FindControl("InvestmentsScrollablePanel");
                    UIComponent first = null;
                    foreach (
                        UIComponent c in list == null
                            ? new List<UIComponent>()
                            : new List<UIComponent>(list.components)
                    )
                    {
                        if (c != null && c.isVisible && (first == null || c.zOrder < first.zOrder))
                        {
                            first = c;
                        }
                    }

                    UILabel title = first == null ? null : first.Find<UILabel>("InvestmentName");
                    LocalCompany newest =
                        ctx.TestCompanies.Count == 0
                            ? null
                            : CompanyDesk.CompanyOf(
                                ctx.TestCompanies[ctx.TestCompanies.Count - 1].Id
                            );
                    ModLog.Info("uitest: first card " + (title == null ? "(none)" : title.text));
                    if (newest == null)
                    {
                        r.Error = "ipo-list-now first";
                    }
                    else if (title == null || title.text != newest.Name)
                    {
                        r.Error = "the first card is not " + newest.Name;
                    }
                    else
                    {
                        // Its icon is a coin with the icon of its kind of business (or its
                        // symbol where the game has no such icon), not the stack of money.
                        UISprite icon = first.Find<UISprite>("InvestmentIcon");
                        UIComponent glyph = icon == null ? null : icon.Find(CoinIcons.GlyphName);
                        UIComponent letters = icon == null ? null : icon.Find(CoinIcons.SymbolName);
                        bool drawn =
                            (glyph != null && glyph.isVisible)
                            || (letters != null && letters.isVisible);
                        if (icon == null || icon.spriteName != CoinIcons.SpriteName || !drawn)
                        {
                            r.Error = "the first card's icon is not the company's coin";
                        }
                    }

                    break;
                }
                case "expect-headquarters":
                {
                    // expect-headquarters historical|as-before|locked — the newest test
                    // company's building is a Historical building; or is as it was before the
                    // company listed (after a buy-out); or its panel's Historical tick cannot be
                    // changed.
                    if (ctx.TestCompanies.Count == 0)
                    {
                        r.Error = "ipo-list-now first";
                        break;
                    }

                    TestCompany one = ctx.TestCompanies[ctx.TestCompanies.Count - 1];
                    bool historical =
                        (
                            BuildingManager.instance.m_buildings.m_buffer[one.Home.Id].m_flags
                            & Building.Flags.Historical
                        ) != 0;
                    if (a == "locked")
                    {
                        UICheckBox tick = FindControl("CheckboxHistorical") as UICheckBox;
                        if (tick == null || !tick.readOnly || !tick.isChecked)
                        {
                            r.Error = "the Historical Building tick is not ticked and locked";
                        }
                    }
                    else if (historical != (a == "historical" || one.WasHistorical))
                    {
                        r.Error =
                            "the headquarters is " + (historical ? "" : "not ") + "Historical";
                    }

                    break;
                }
                case "expect-company-moving":
                {
                    // expect-company-moving DAYS — the game's stock of this scenario's newest
                    // company closed at a different price on each of its last DAYS days.
                    if (ctx.TestCompanies.Count == 0)
                    {
                        r.Error = "ipo-list-now first";
                        break;
                    }

                    int days = Mathf.Max(2, (int)ParseFloat(a, 3f));
                    int slot = CompanyListing.SlotOf(
                        ctx.TestCompanies[ctx.TestCompanies.Count - 1].Id
                    );
                    Investment.Queue<float> values =
                        slot <= 0
                            ? null
                            : EconomyManager.instance.m_investments.m_buffer[slot].allValues;
                    if (values == null || values.Count < days)
                    {
                        r.Error = "the company's stock has fewer than " + days + " prices";
                        break;
                    }

                    var prices = new StringBuilder();
                    int still = 0;
                    for (int k = 0; k < days; k++)
                    {
                        prices
                            .Append(k == 0 ? "" : ", ")
                            .Append(values.GetFromTop(k).ToString("0.00")); // not shown: a log line
                        still += k > 0 && values.GetFromTop(k) == values.GetFromTop(k - 1) ? 1 : 0;
                    }

                    ModLog.Info("uitest: company prices, newest first: " + prices);
                    if (still > 0)
                    {
                        r.Error = "the price stood still on " + still + " day(s): " + prices;
                    }

                    break;
                }
                case "ipo-look-behind":
                    // ipo-look-behind — dev: the next scan credits healthy businesses with the
                    // full period again, as the first scan of a city does.
                    CompanyDesk.TestLookBehindAgain();
                    break;
                case "ipo-forget-pacing":
                    // ipo-forget-pacing — dev clean-up: forgets the IPO days counted towards
                    // the year's limit (test listings used to leave theirs behind).
                    ModLog.Info(
                        "uitest: forgot " + CompanyDesk.TestForgetPacing() + " IPO day(s) of pacing"
                    );
                    break;
                case "ipo-view":
                    // ipo-view — presses the dialog's View headquarters button.
                    if (IpoDialog.Current == null || !IpoDialog.Current.View())
                    {
                        r.Error = "no IPO dialog is open";
                    }

                    break;
                case "expect-building-panel":
                {
                    // expect-building-panel — a building's info panel is open and the camera
                    // stands between the nearest and farthest framing distances.
                    UIComponent info = UIView
                        .GetAView()
                        .FindUIComponent("(Library) ZonedBuildingWorldInfoPanel");
                    CameraController cam = ToolsModifierControl.cameraController;
                    ModLog.Info(
                        "uitest: building panel "
                            + (info != null && info.isVisible)
                            + " camera size "
                            + cam.m_targetSize.ToString("0")
                            + " pitch "
                            + cam.m_targetAngle.y.ToString("0")
                    );
                    if (info == null || !info.isVisible)
                    {
                        r.Error = "no building info panel is open";
                    }
                    else if (
                        cam.m_targetSize < BuildingFraming.Nearest
                        || cam.m_targetSize > BuildingFraming.Farthest
                    )
                    {
                        r.Error =
                            "camera size " + cam.m_targetSize + " is outside the framing range";
                    }

                    break;
                }
                case "expect-fireworks":
                {
                    // expect-fireworks — firework bursts were launched since the last check.
                    int now = IpoFireworks.Launched;
                    ModLog.Info(
                        "uitest: fireworks launched " + now + " (was " + ctx.Fireworks + ")"
                    );
                    if (now <= ctx.Fireworks)
                    {
                        r.Error = "no fireworks were launched";
                    }

                    ctx.Fireworks = now;
                    break;
                }
                case "ipo-dialog":
                    // ipo-dialog on|off|close — lets this scenario's listing open the IPO dialog
                    // (off by default in tests), or closes it like the × button.
                    if (a == "close")
                    {
                        if (IpoDialog.Current != null)
                        {
                            IpoDialog.Current.Close();
                        }
                    }
                    else
                    {
                        IpoDialog.Silenced = a != "on";
                    }

                    break;
                case "ipo-buy":
                {
                    // ipo-buy SHARES — the city buys shares of this scenario's company through
                    // the game's own purchase (simulation paused).
                    if (ctx.TestCompanies.Count == 0)
                    {
                        r.Error = "ipo-list-now first";
                        break;
                    }

                    SimulationManager sim = SimulationManager.instance;
                    bool paused = sim.SimulationPaused;
                    sim.SimulationPaused = true;
                    yield return new WaitForSeconds(0.5f);
                    int slot = CompanyListing.SlotOf(
                        ctx.TestCompanies[ctx.TestCompanies.Count - 1].Id
                    );
                    if (slot <= 0)
                    {
                        r.Error = "the game has no stock for the company";
                    }
                    else
                    {
                        Investment[] buffer = EconomyManager.instance.m_investments.m_buffer;
                        int cost = buffer[slot]
                            .Info.m_investmentAI.BuyShares(
                                (byte)slot,
                                ref buffer[slot],
                                (uint)ParseFloat(a, 1f)
                            );
                        ModLog.Info(
                            "uitest: ipo-buy cost "
                                + cost
                                + ", holds "
                                + buffer[slot].m_playerOwnedShareCount
                                + " at game price "
                                + buffer[slot].value
                        );
                    }

                    sim.SimulationPaused = paused;
                    break;
                }
                case "expect-company-cards":
                {
                    // expect-company-cards — Economy → Investments shows one card for every
                    // kind of business the city has, each with its line and bar text.
                    BoardView board = CompanyDesk.Board();
                    int found = 0;
                    foreach (KindStatus k in board.Kinds)
                    {
                        UIComponent card = FindControl(CompanyCards.CardPrefix + k.Kind);
                        UILabel cardLine = FindControl(CompanyCards.LinePrefix + k.Kind) as UILabel;
                        UILabel bar = FindControl(CompanyCards.BarTextPrefix + k.Kind) as UILabel;
                        if (
                            card == null
                            || cardLine == null
                            || (bar == null && IpoCardText.HasBar(k))
                        )
                        {
                            r.Error = "no card for " + k.Kind;
                            break;
                        }

                        found++;
                        ModLog.Info(
                            "uitest: card "
                                + k.Kind
                                + ": "
                                + cardLine.text
                                + " | "
                                + (bar == null ? "no bar" : bar.text)
                        );
                    }

                    if (r.Error == null && found == 0)
                    {
                        r.Error = "the city has no kind of business to show (ipo-scan first)";
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
                CompanyDesk.TestRemove(one.Id, one.AnnouncedDay);
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

        /// <summary>Dev: a week's money of one area resource as the game's industry panel shows it (whole ₡).</summary>
        private static long Week(uint amount, TransferManager.TransferReason reason)
        {
            return (long)amount * IndustryBuildingAI.GetResourcePrice(reason) / 10000;
        }

        private static void LogIndustryAreas()
        {
            DistrictManager districts = DistrictManager.instance;
            Building[] buildings = BuildingManager.instance.m_buildings.m_buffer;
            int areas = 0;
            for (int p = 1; p < districts.m_parks.m_size; p++)
            {
                DistrictPark park = districts.m_parks.m_buffer[p];
                if ((park.m_flags & DistrictPark.Flags.Created) == 0 || !park.IsIndustry)
                {
                    continue;
                }

                areas++;
                DistrictAreaResourceData raw,
                    a,
                    b;
                TransferManager.TransferReason rr,
                    ra,
                    rb;
                switch (park.m_parkType)
                {
                    case DistrictPark.ParkType.Farming:
                        raw = park.m_grainData;
                        a = park.m_animalProductsData;
                        b = park.m_floursData;
                        rr = TransferManager.TransferReason.Grain;
                        ra = TransferManager.TransferReason.AnimalProducts;
                        rb = TransferManager.TransferReason.Flours;
                        break;
                    case DistrictPark.ParkType.Forestry:
                        raw = park.m_logsData;
                        a = park.m_planedTimberData;
                        b = park.m_paperData;
                        rr = TransferManager.TransferReason.Logs;
                        ra = TransferManager.TransferReason.PlanedTimber;
                        rb = TransferManager.TransferReason.Paper;
                        break;
                    case DistrictPark.ParkType.Oil:
                        raw = park.m_oilData;
                        a = park.m_petroleumData;
                        b = park.m_plasticsData;
                        rr = TransferManager.TransferReason.Oil;
                        ra = TransferManager.TransferReason.Petroleum;
                        rb = TransferManager.TransferReason.Plastics;
                        break;
                    case DistrictPark.ParkType.Ore:
                        raw = park.m_oreData;
                        a = park.m_glassData;
                        b = park.m_metalsData;
                        rr = TransferManager.TransferReason.Ore;
                        ra = TransferManager.TransferReason.Glass;
                        rb = TransferManager.TransferReason.Metals;
                        break;
                    default:
                        raw = a = b = default(DistrictAreaResourceData);
                        rr = ra = rb = TransferManager.TransferReason.None;
                        break;
                }

                long income =
                    rr == TransferManager.TransferReason.None
                        ? 0
                        : Week(raw.m_finalExport, rr)
                            + Week(a.m_finalExport, ra)
                            + Week(b.m_finalExport, rb);
                long bought =
                    rr == TransferManager.TransferReason.None ? 0 : Week(raw.m_finalImport, rr);
                int upkeep = 0;
                int count = 0;
                FastList<ushort> service = BuildingManager.instance.GetServiceBuildings(
                    ItemClass.Service.PlayerIndustry
                );
                for (int i = 0; i < service.m_size; i++)
                {
                    ushort id = service.m_buffer[i];
                    if (districts.GetPark(buildings[id].m_position) != p)
                    {
                        continue;
                    }

                    count++;
                    upkeep += buildings[id]
                        .Info.m_buildingAI.GetResourceRate(
                            id,
                            ref buildings[id],
                            EconomyManager.Resource.Maintenance
                        );
                }

                ModLog.Info(
                    "uitest: industry area "
                        + p
                        + " "
                        + park.m_parkType
                        + " '"
                        + districts.GetParkName((byte)p)
                        + "' level "
                        + (int)park.m_parkLevel
                        + " workers "
                        + park.m_finalWorkerCount
                        + " buildings "
                        + count
                        + " main "
                        + park.m_mainGate
                        + (
                            park.m_mainGate != 0
                            && (buildings[park.m_mainGate].m_flags & Building.Flags.Active) != 0
                                ? " (active)"
                                : " (not active)"
                        )
                        + " production "
                        + park.m_totalProductionAmount
                        + " week: exports "
                        + income
                        + " bought "
                        + bought
                        + " upkeep "
                        + LocaleFormatter.FormatUpkeep(upkeep, false)
                        + " (rate "
                        + upkeep
                        + ")"
                );
            }

            int factories = 0;
            for (int id = 1; id < buildings.Length; id++)
            {
                if ((buildings[id].m_flags & Building.Flags.Created) == 0)
                {
                    continue;
                }

                var factory =
                    buildings[id].Info == null
                        ? null
                        : buildings[id].Info.m_buildingAI as UniqueFactoryAI;
                if (factory == null)
                {
                    continue;
                }

                factories++;
                int made = buildings[id].m_education3 * factory.m_outputRate * 16 / 100;
                long income =
                    (long)made
                    * IndustryBuildingAI.GetResourcePrice(factory.m_outputResource)
                    / 10000;
                ModLog.Info(
                    "uitest: unique factory "
                        + id
                        + " '"
                        + BuildingManager.instance.GetBuildingName(
                            (ushort)id,
                            new InstanceID { Building = (ushort)id }
                        )
                        + "' "
                        + buildings[id].Info.name
                        + " workplaces "
                        + (
                            factory.m_workPlaceCount0
                            + factory.m_workPlaceCount1
                            + factory.m_workPlaceCount2
                            + factory.m_workPlaceCount3
                        )
                        + " production rate "
                        + buildings[id].m_productionRate
                        + "% park "
                        + districts.GetPark(buildings[id].m_position)
                        + " problems "
                        + buildings[id].m_problems
                        + " week: income "
                        + income
                        + " upkeep rate "
                        + factory.GetResourceRate(
                            (ushort)id,
                            ref buildings[id],
                            EconomyManager.Resource.Maintenance
                        )
                );
            }

            ModLog.Info(
                "uitest: " + areas + " industry area(s), " + factories + " unique factory(ies)"
            );
        }
    }
}

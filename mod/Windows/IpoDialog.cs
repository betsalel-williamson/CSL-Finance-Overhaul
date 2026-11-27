using System; // Action, DateTime, Exception
using System.Collections.Generic; // Queue
// ColossalFramework.UI: UIButton, UIComponent, UIHorizontalAlignment, UILabel, UIPanel,
//   UISlicedSprite, UISprite, UIView
using ColossalFramework.UI;
// FinanceOverhaul.Core: BuildingKey, CompanyStatus, DialogSide, IpoDialogText, LocalCompany,
//   ModSettings
using FinanceOverhaul.Core;
using UnityEngine; // AudioClip, Color32, Mathf, MonoBehaviour, RectOffset, Time, Vector2, Vector3

// Game classes used without a using (global namespace, Assembly-CSharp): AudioManager, Building,
//   BuildingManager, BuySellInvestmentPanel, DistrictManager, LoadingManager, NetManager,
//   NetSegment, SimulationManager

namespace FinanceOverhaul
{
    /// <summary>
    /// The IPO dialog (Figma UI Map band 16; docs/features/local-ipos.md, "The IPO moment"):
    /// shown on the day a local company is announced, while the offering window is open. The
    /// game pauses the moment the company is announced and stays paused when the dialog closes:
    /// the offering window is only seven days, so the player resumes when ready. It states only what is
    /// fixed that day. Buy opens the game's own Buy dialog for the new stock; View moves the
    /// camera to the headquarters; the checkbox is the Options preference read the other way.
    /// It waits while the Welcome back dialog is up. It looks and behaves like the game's own
    /// celebration dialogs: their frame, a banner with a large headline, a fade-in and a sound.
    /// </summary>
    public class IpoDialog : MonoBehaviour
    {
        public const string PanelName = "FinanceOverhaulIpoDialog";
        private const float Width = 520f;
        private const float Pad = 20f;

        // Control name suffixes for game UI tests.
        private const string NamePrice = "Price"; // not shown: a control name
        private const string NameWindow = "Window"; // not shown: a control name
        private const string NameCanBuy = "CanBuy"; // not shown: a control name
        private const string NameFirstDay = "FirstDay"; // not shown: a control name
        private const string NameWhere = "Where"; // not shown: a control name
        private const string NameLead = "Lead"; // not shown: a control name

        private static readonly Color32 Frame = FoTheme.Dialog;
        private static readonly Color32 Header = FoTheme.Header;
        private static readonly Color32 Card = FoTheme.PanelAlt;
        private static readonly Color32 White = FoTheme.TextPrimary;
        private static readonly Color32 Body = FoTheme.TextLabel;
        private static readonly Color32 Muted = FoTheme.TextMuted;
        private static readonly Color32 Value = FoTheme.GameValue;
        private static readonly Color32 Price = FoTheme.GamePositive;

        private static readonly object Sync = new object();
        private static readonly Queue<int> Waiting = new Queue<int>();

        /// <summary>Game UI tests: no dialog unless a scenario asks for it.</summary>
        public static bool Silenced;

        private const float BannerHeight = 96f;
        private const float FadeSeconds = 0.15f; // the game's own show time for these dialogs

        private UIPanel panel;
        private float shownAt;
        private int shownCompany;
        private float nextCheck;

        public static IpoDialog Current { get; private set; }

        public bool IsShown
        {
            get { return panel != null && panel.isVisible; }
        }

        /// <summary>Any thread: a company was announced; show its dialog when possible.</summary>
        public static void Announce(int companyId)
        {
            if (Silenced || Mod.Settings == null || !Mod.Settings.IpoDialogEnabled)
            {
                return;
            }

            lock (Sync)
            {
                Waiting.Enqueue(companyId);
            }

            // At once, on whichever thread announced it: the dialog only opens on the next
            // check, and at full speed a day would pass before it does.
            SimulationManager.instance.SimulationPaused = true;
        }

        /// <summary>Level unload: nothing waits for the next city.</summary>
        public static void Reset()
        {
            lock (Sync)
            {
                Waiting.Clear();
            }
        }

        private void Awake()
        {
            Current = this;
        }

        private void Update()
        {
            if (panel != null && panel.opacity < 1f)
            {
                panel.opacity = Mathf.Clamp01((Time.unscaledTime - shownAt) / FadeSeconds);
            }

            if (panel != null || Time.unscaledTime < nextCheck)
            {
                return;
            }

            nextCheck = Time.unscaledTime + 0.5f;
            try
            {
                // One full-screen dialog at a time, and never while a city is still loading.
                if (
                    !LoadingManager.exists
                    || !LoadingManager.instance.m_loadingComplete
                    || (WelcomeBackDialog.Current != null && WelcomeBackDialog.Current.IsShown)
                )
                {
                    return;
                }

                int id;
                lock (Sync)
                {
                    if (Waiting.Count == 0)
                    {
                        return;
                    }

                    id = Waiting.Dequeue();
                }

                LocalCompany company = CompanyDesk.CompanyOf(id);
                if (company != null && company.Status == CompanyStatus.Offering)
                {
                    Build(company);
                }
            }
            catch (Exception ex)
            {
                ModLog.Warn("ipo dialog: show failed: " + ex);
                nextCheck = Time.unscaledTime + 10f;
            }
        }

        /// <summary>Esc, before the game acts on it: closes the dialog. False if none is open.</summary>
        public bool HandleEscape()
        {
            if (!IsShown)
            {
                return false;
            }

            Close();
            return true;
        }

        /// <summary>Closes the dialog. The game stays paused: the player resumes it.</summary>
        public void Close()
        {
            if (panel == null)
            {
                return;
            }

            Destroy(panel.gameObject);
            panel = null;
            nextCheck = Time.unscaledTime + 0.5f;
        }

        private void OnDestroy()
        {
            try
            {
                Close();
            }
            catch
            {
                // fail soft on unload
            }

            if (Current == this)
            {
                Current = null;
            }
        }

        /// <summary>
        /// One sound as the dialog opens: the game's own sound for a sale at a gain (the game
        /// ships no Stock Exchange bell). Played as a UI sound, so it follows the player's
        /// volume sliders and is heard while the game is paused.
        /// </summary>
        private static void PlayFanfare()
        {
            try
            {
                BuySellInvestmentPanel invest = BuySellInvestmentPanel.instance;
                AudioClip clip = invest == null ? null : invest.m_sellingSoundGood;
                if (clip != null)
                {
                    AudioManager.instance.PlaySound(clip);
                }
            }
            catch (Exception ex)
            {
                ModLog.Info("ipo dialog: no sound (" + ex.Message + ")");
            }
        }

        private void OnBuy()
        {
            int company = shownCompany;
            Close();
            int slot = CompanyListing.SlotOf(company);
            if (slot > 0)
            {
                TradeDialog.Show(slot, DialogSide.Buy);
            }
        }

        /// <summary>As the View headquarters button; false when no company is shown.</summary>
        public bool View()
        {
            if (panel == null || CompanyDesk.CompanyOf(shownCompany) == null)
            {
                return false;
            }

            OnView();
            return true;
        }

        private void OnView()
        {
            LocalCompany company = CompanyDesk.CompanyOf(shownCompany);
            Close();
            CompanyHome.Show(company, true);
        }

        private void Build(LocalCompany company)
        {
            UIView view = UIView.GetAView();
            UIComponent stale = view.FindUIComponent(PanelName);
            if (stale != null)
            {
                Destroy(stale.gameObject);
            }

            shownCompany = company.Id;
            SimulationManager.instance.SimulationPaused = true;

            // The frame of the game's own celebration dialogs (a park levelling up, the end of
            // an academic year): its panel sprite, a banner across the top with a large
            // headline, and a dark body below.
            panel = (UIPanel)view.AddUIComponent(typeof(UIPanel));
            panel.name = PanelName;
            panel.backgroundSprite = FoTheme.GameLoanCardSprite;
            panel.width = Width;

            // The game has banner art for parks, industry, airports and campuses but none for
            // the Stock Exchange, so the banner is the green of the game's loan offers with its
            // stack of money on either side.
            UIPanel banner = panel.AddUIComponent<UIPanel>();
            banner.name = PanelName + "Banner";
            banner.backgroundSprite = FoTheme.GameLoanCardSprite;
            banner.color = FoTheme.GameLoanOffer;
            banner.size = new Vector2(Width - 6f, BannerHeight);
            banner.relativePosition = new Vector3(3f, 3f);
            banner.clipChildren = true;
            foreach (float x in new[] { 22f, Width - 6f - 22f - 64f })
            {
                UISprite money = banner.AddUIComponent<UISprite>();
                money.spriteName = "MoneyThumb";
                money.size = new Vector2(64f, 64f);
                money.relativePosition = new Vector3(x, (BannerHeight - 64f) / 2f);
            }

            // The headline and the name sit between the two stacks; a long name wraps and the
            // banner grows with it.
            float textLeft = 22f + 64f + 10f;
            float textWidth = Width - 6f - 2f * textLeft;
            UILabel congrats = Label(banner, IpoDialogText.Congratulations, 2f, White, textWidth);
            congrats.name = PanelName + "Congrats";
            congrats.textAlignment = UIHorizontalAlignment.Center;
            congrats.relativePosition = new Vector3(textLeft, 12f);
            // The company's name, once: here.
            UILabel title = Label(banner, IpoDialogText.Title(company), 1.125f, White, textWidth);
            title.name = PanelName + "Title";
            title.textAlignment = UIHorizontalAlignment.Center;
            title.relativePosition = new Vector3(textLeft, 12f + congrats.height + 2f);
            float bannerHeight = Mathf.Max(
                BannerHeight,
                title.relativePosition.y + title.height + 12f
            );
            banner.height = bannerHeight;
            foreach (UISprite money in banner.GetComponentsInChildren<UISprite>())
            {
                money.relativePosition = new Vector3(
                    money.relativePosition.x,
                    (bannerHeight - money.height) / 2f
                );
            }

            UISlicedSprite body = panel.AddUIComponent<UISlicedSprite>();
            body.spriteName = "UnlockingPanel";
            body.color = FoTheme.GameDialogBody;
            body.relativePosition = new Vector3(3f, 3f + bannerHeight);

            UIButton close = panel.AddUIComponent<UIButton>();
            close.name = PanelName + "Close";
            close.size = new Vector2(32f, 32f);
            close.normalBgSprite = "buttonclose";
            close.hoveredBgSprite = "buttonclosehover";
            close.pressedBgSprite = "buttonclosepressed";
            close.relativePosition = new Vector3(Width - 34f, 2f);
            close.tooltip = IpoDialogText.CloseTip;
            close.eventClick += (c, p) => Close();

            float inner = Width - 2 * Pad;
            float y = 3f + bannerHeight + 16f;

            // Where it is from. The name is said once, in the title above.
            UIPanel card = panel.AddUIComponent<UIPanel>();
            card.backgroundSprite = "GenericPanelWhite";
            card.color = Card;
            card.width = inner;
            card.relativePosition = new Vector3(Pad, y);
            string street,
                cross,
                district;
            Address(company.Home, out street, out cross, out district);
            UILabel where = Label(
                card,
                IpoDialogText.Where(company, street, cross, district),
                0.875f,
                Value,
                inner - 24f
            );
            where.name = PanelName + NameWhere;
            where.relativePosition = new Vector3(12f, 10f);
            card.height = where.relativePosition.y + where.height + 10f;
            y += card.height + 12f;

            UILabel lead = Label(panel, IpoDialogText.LeadFor(company), 0.875f, White, inner);
            lead.name = PanelName + NameLead;
            y = Place(lead, y, 8f);
            DateTime now = SimulationManager.instance.m_currentGameTime;
            int days = company.FirstTradingDay - company.AnnouncedDay;
            y = Row(y, IpoDialogText.PriceLabel, IpoDialogText.Price(company), Price, NamePrice);
            y = Row(
                y,
                IpoDialogText.WindowLabel,
                IpoDialogText.Window(company, Date(now.AddDays(days - 1))),
                Value,
                NameWindow
            );
            y = Row(
                y,
                IpoDialogText.CanBuyLabel,
                IpoDialogText.CanBuy(company, CompanyDesk.Current),
                Value,
                NameCanBuy
            );
            y = Row(
                y,
                IpoDialogText.FirstDayLabel,
                IpoDialogText.FirstDay(Date(now.AddDays(days))),
                Value,
                NameFirstDay
            );
            y += 4f;
            y = Place(
                Label(panel, IpoDialogText.SmallPrintFor(company), 0.6875f, Muted, inner),
                y,
                12f
            );

            // Two buttons of one size, centred like the game's OK: the same height and text, and
            // the width of the wider.
            y += 4f;
            UIButton buy = Button(IpoDialogText.Buy, PanelName + "Buy");
            UIButton look = Button(IpoDialogText.View, PanelName + "View");
            float buttonWidth = Mathf.Max(201f, Mathf.Max(buy.width, look.width));
            buy.width = buttonWidth;
            look.width = buttonWidth;
            float buttonsLeft = (Width - (2f * buttonWidth + 12f)) / 2f;
            buy.relativePosition = new Vector3(buttonsLeft, y);
            buy.eventClick += (c, p) => OnBuy();
            look.relativePosition = new Vector3(buttonsLeft + buttonWidth + 12f, y);
            look.eventClick += (c, p) => OnView();
            y += buy.height + 12f;

            // The checkbox is the Options preference, read the other way.
            UIPanel dont = panel.AddUIComponent<UIPanel>();
            dont.name = PanelName + "DontShow";
            Action<bool> tick = FoCheckbox.AddBox(dont, 0f, 2f);
            tick(false);
            UILabel dontLabel = dont.AddUIComponent<UILabel>();
            dontLabel.textScale = 0.875f;
            dontLabel.textColor = Value; // the game's colour for this line in its own dialogs
            dontLabel.text = IpoDialogText.DontShow;
            dontLabel.relativePosition = new Vector3(FoCheckbox.Size + 8f, 2f);
            dont.size = new Vector2(FoCheckbox.Size + 8f + dontLabel.width, 20f);
            dont.relativePosition = new Vector3((Width - dont.width) / 2f, y);
            dont.eventClick += (c, p) =>
            {
                ModSettings settings = Mod.EnsureSettings();
                settings.IpoDialogEnabled = !settings.IpoDialogEnabled;
                Mod.SaveSettings();
                tick(!settings.IpoDialogEnabled);
                ModLog.Info("ipo dialog: show for future IPOs=" + settings.IpoDialogEnabled);
            };
            y += dont.height + 14f;

            panel.height = y;
            body.size = new Vector2(Width - 6f, panel.height - bannerHeight - 6f);
            FoWindow.CenterOnScreen(panel);
            panel.BringToFront();
            panel.opacity = 0f; // fades in, as the game's dialogs do
            shownAt = Time.unscaledTime;
            PlayFanfare();
            ModLog.Info("ipo dialog: shown for " + company.Name);
        }

        /// <summary>
        /// The street the building is reached from, the nearest cross street if there is one,
        /// and its district if it stands in one. Each is null when there is none.
        /// </summary>
        private static void Address(
            BuildingKey home,
            out string street,
            out string cross,
            out string district
        )
        {
            street = null;
            cross = null;
            district = null;
            Building building = BuildingManager.instance.m_buildings.m_buffer[home.Id];
            if (building.m_buildIndex != home.BuildIndex)
            {
                return;
            }

            byte id = DistrictManager.instance.GetDistrict(building.m_position);
            district = id == 0 ? null : DistrictManager.instance.GetDistrictName(id);

            NetManager net = NetManager.instance;
            ushort segment = building.m_accessSegment;
            if (segment == 0)
            {
                return;
            }

            street = net.GetSegmentName(segment);
            NetSegment road = net.m_segments.m_buffer[segment];
            ushort start = road.m_startNode,
                end = road.m_endNode;
            ushort near =
                (net.m_nodes.m_buffer[start].m_position - building.m_position).sqrMagnitude
                <= (net.m_nodes.m_buffer[end].m_position - building.m_position).sqrMagnitude
                    ? start
                    : end;
            for (int i = 0; i < 8; i++)
            {
                ushort other = net.m_nodes.m_buffer[near].GetSegment(i);
                if (other == 0 || other == segment)
                {
                    continue;
                }

                string name = net.GetSegmentName(other);
                if (!string.IsNullOrEmpty(name) && name != street)
                {
                    cross = name;
                    return;
                }
            }
        }

        private float Row(float y, string label, string value, Color32 colour, string name)
        {
            UILabel l = Label(panel, label, 0.8125f, Body, 150f);
            l.relativePosition = new Vector3(Pad, y);
            UILabel v = Label(panel, value, 0.8125f, colour, Width - 2 * Pad - 160f);
            v.name = PanelName + name;
            v.relativePosition = new Vector3(Pad + 160f, y);
            return y + Mathf.Max(l.height, v.height) + 6f;
        }

        private UIButton Button(string text, string name)
        {
            UIButton b = panel.AddUIComponent<UIButton>();
            b.name = name;
            b.text = text;
            b.textScale = 0.875f;
            b.normalBgSprite = "ButtonMenu";
            b.hoveredBgSprite = "ButtonMenuHovered";
            b.pressedBgSprite = "ButtonMenuPressed";
            b.textColor = FoTheme.GameText;
            b.autoSize = true;
            b.textPadding = new RectOffset(14, 14, 14, 12);
            b.autoSize = false;
            b.height = 47f; // the game's OK button on its own dialogs
            return b;
        }

        private float Place(UILabel l, float y, float gapAfter)
        {
            l.relativePosition = new Vector3(Pad, y);
            return y + l.height + gapAfter;
        }

        private static string Date(DateTime day)
        {
            return day.ToString("dd/MM/yyyy");
        }

        private static UILabel Label(
            UIComponent parent,
            string text,
            float scale,
            Color32 colour,
            float width
        )
        {
            UILabel l = parent.AddUIComponent<UILabel>();
            l.textScale = scale;
            l.textColor = colour;
            l.wordWrap = true;
            l.autoSize = false;
            l.autoHeight = true;
            l.width = width;
            l.text = text;
            return l;
        }
    }
}

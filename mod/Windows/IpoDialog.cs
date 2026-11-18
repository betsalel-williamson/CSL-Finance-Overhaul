using System;
using System.Collections.Generic;
using ColossalFramework.UI;
using FinanceOverhaul.Core;
using UnityEngine;

namespace FinanceOverhaul
{
    /// <summary>
    /// The IPO dialog (Figma UI Map band 16; docs/features/local-ipos.md, "The IPO moment"):
    /// shown on the day a local company is announced, while the offering window is open. It
    /// pauses the game while open and puts the pause back as it was. It states only what is
    /// fixed that day. Buy opens the game's own Buy dialog for the new stock; View moves the
    /// camera to the headquarters; the checkbox is the Options preference read the other way.
    /// It waits while the Welcome back dialog is up. No bell yet.
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

        private UIPanel panel;
        private int shownCompany;
        private bool pausedBefore;
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

        /// <summary>Closes the dialog and puts the pause back as the player had it.</summary>
        public void Close()
        {
            if (panel == null)
            {
                return;
            }

            Destroy(panel.gameObject);
            panel = null;
            SimulationManager.instance.SimulationPaused = pausedBefore;
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

        private void OnBuy()
        {
            int company = shownCompany;
            Close();
            int slot = CompanyListing.SlotOf(company);
            if (slot > 0)
            {
                BuySellInvestmentPanel.ShowPanel((byte)slot, BuySellInvestmentPanel.Mode.Buy);
            }
        }

        private void OnView()
        {
            LocalCompany company = CompanyDesk.CompanyOf(shownCompany);
            Close();
            CameraController camera = ToolsModifierControl.cameraController;
            if (company == null || camera == null)
            {
                return;
            }

            Building home = BuildingManager.instance.m_buildings.m_buffer[company.Home.Id];
            if (home.m_buildIndex == company.Home.BuildIndex)
            {
                camera.SetTarget(
                    new InstanceID { Building = company.Home.Id },
                    home.m_position,
                    true
                );
            }
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
            pausedBefore = SimulationManager.instance.SimulationPaused;
            SimulationManager.instance.SimulationPaused = true;

            panel = (UIPanel)view.AddUIComponent(typeof(UIPanel));
            panel.name = PanelName;
            panel.backgroundSprite = "GenericPanelWhite";
            panel.color = Frame;
            panel.width = Width;

            UIPanel header = panel.AddUIComponent<UIPanel>();
            header.backgroundSprite = "GenericPanelWhite";
            header.color = Header;
            header.size = new Vector2(Width, 36f);
            header.relativePosition = Vector3.zero;
            UILabel title = Label(header, IpoDialogText.Title(company), 1.1f, White, Width - 60f);
            title.name = PanelName + "Title";
            title.relativePosition = new Vector3(12f, 8f);

            UIButton close = header.AddUIComponent<UIButton>();
            close.name = PanelName + "Close";
            close.size = new Vector2(30f, 30f);
            close.normalBgSprite = "buttonclose";
            close.hoveredBgSprite = "buttonclosehover";
            close.pressedBgSprite = "buttonclosepressed";
            close.relativePosition = new Vector3(Width - 34f, 3f);
            close.tooltip = IpoDialogText.CloseTip;
            close.eventClick += (c, p) => Close();

            float inner = Width - 2 * Pad;
            float y = 48f;

            // The company: its name and where it is from.
            UIPanel card = panel.AddUIComponent<UIPanel>();
            card.backgroundSprite = "GenericPanelWhite";
            card.color = Card;
            card.width = inner;
            card.relativePosition = new Vector3(Pad, y);
            UILabel name = Label(card, company.Name, 1.0f, White, inner - 24f);
            name.relativePosition = new Vector3(12f, 10f);
            // The company carries its building's name, so "where" is the district it stands in.
            Vector3 at = BuildingManager.instance.m_buildings.m_buffer[company.Home.Id].m_position;
            byte district = DistrictManager.instance.GetDistrict(at);
            string home = district == 0 ? null : DistrictManager.instance.GetDistrictName(district);
            UILabel where = Label(
                card,
                IpoDialogText.Header(company, home),
                0.8125f,
                Value,
                inner - 24f
            );
            where.relativePosition = new Vector3(12f, 12f + name.height + 4f);
            card.height = where.relativePosition.y + where.height + 10f;
            y += card.height + 12f;

            y = Place(Label(panel, IpoDialogText.Lead, 0.875f, White, inner), y, 8f);
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
                IpoDialogText.CanBuy(company, CompanyDesk.Rules ?? new IpoRules()),
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
            y = Place(Label(panel, IpoDialogText.SmallPrint, 0.6875f, Muted, inner), y, 12f);

            UIButton buy = Button(IpoDialogText.Buy, PanelName + "Buy", true);
            buy.relativePosition = new Vector3(Pad, y);
            buy.eventClick += (c, p) => OnBuy();
            UIButton look = Button(IpoDialogText.View, PanelName + "View", false);
            look.relativePosition = new Vector3(
                Pad + buy.width + 10f,
                y + (buy.height - look.height) / 2f
            );
            look.eventClick += (c, p) => OnView();
            y += buy.height + 12f;

            // The checkbox is the Options preference, read the other way.
            UIPanel dont = panel.AddUIComponent<UIPanel>();
            dont.name = PanelName + "DontShow";
            dont.size = new Vector2(inner, 20f);
            dont.relativePosition = new Vector3(Pad, y);
            Action<bool> tick = FoCheckbox.AddBox(dont, 0f, 2f);
            tick(false);
            UILabel dontLabel = Label(dont, IpoDialogText.DontShow, 0.8125f, Body, inner - 24f);
            dontLabel.relativePosition = new Vector3(FoCheckbox.Size + 8f, 2f);
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
            FoWindow.CenterOnScreen(panel);
            panel.BringToFront();
            ModLog.Info("ipo dialog: shown for " + company.Name);
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

        private UIButton Button(string text, string name, bool main)
        {
            UIButton b = panel.AddUIComponent<UIButton>();
            b.name = name;
            b.text = text;
            b.textScale = main ? 0.875f : 0.8125f;
            b.normalBgSprite = "ButtonMenu";
            b.hoveredBgSprite = "ButtonMenuHovered";
            b.pressedBgSprite = "ButtonMenuPressed";
            b.textColor = FoTheme.GameText;
            b.autoSize = true;
            b.textPadding = new RectOffset(14, 14, main ? 10 : 6, main ? 8 : 5);
            b.autoSize = false;
            b.height = main ? 36f : 28f;
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

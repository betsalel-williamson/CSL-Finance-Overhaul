using System;
using ColossalFramework.UI;
using FinanceOverhaul.Core;
using UnityEngine;

namespace FinanceOverhaul
{
    /// <summary>
    /// The game's building info panel, for a headquarters whose company is listed (Figma UI Map
    /// band 19, designs B and C): a gold title bar with the company's coin in place of the zone
    /// icon, for as long as the company is listed, and a listing section above the Historical
    /// Building line (the offering in its first week, the listing and the city's stake after).
    /// The mod only adds: the game's rows keep their place, the Historical Building line moves
    /// down by the section's height, and everything is put back for any other building. Lives
    /// on the overlay root.
    /// </summary>
    public class CompanyBuildingPanel : MonoBehaviour
    {
        public const string Prefix = "FinanceOverhaulBuilding";
        public const string GoldName = Prefix + "Gold";
        public const string CoinName = Prefix + "Coin";
        public const string SectionName = Prefix + "Listing";
        private const string GamePanel = "(Library) ZonedBuildingWorldInfoPanel"; // not shown: a game control name

        private const float Pad = 12f;
        private const float BarHeight = 4f;
        private const int SparkW = 84,
            SparkH = 24,
            SparkScale = 2;

        private static readonly Color32 Gold = new Color32(240, 170, 36, 255);
        private static readonly Color32 GoldHover = new Color32(255, 200, 80, 255);
        private static readonly Color32 GoldText = new Color32(58, 36, 4, 255);
        private static readonly Color32 SectionFill = new Color32(30, 36, 44, 255);
        private static readonly Color32 Track = new Color32(8, 12, 18, 255);
        private static readonly Color32 White = FoTheme.TextPrimary;
        private static readonly Color32 Muted = FoTheme.TextMuted;
        private static readonly Color32 Note = FoTheme.TickerSymbol;

        private UIPanel panel;
        private UIPanel gold;
        private UISprite coin;
        private UIPanel section;
        private UISprite sectionCoin;
        private UILabel symbol;
        private UILabel badge;
        private UILabel line;
        private UILabel price;
        private UILabel note;
        private UILabel change;
        private UITextureSprite spark;
        private Texture2D sparkTexture;
        private UILabel stake;
        private UILabel gain;
        private UIPanel barFill;
        private UIButton buy;
        private UIButton sell;
        private UIButton show;

        private bool decorated;
        private Color32 nameColour;
        private float historicalY;
        private float zoneInfoY;
        private float panelHeight;
        private int shownCompany = -1;
        private int slot;
        private string shown;
        private float nextCheck;

        private void Update()
        {
            if (Time.unscaledTime < nextCheck)
            {
                return;
            }

            nextCheck = Time.unscaledTime + 0.2f;
            try
            {
                if (panel == null)
                {
                    panel = UIView.GetAView().FindUIComponent(GamePanel) as UIPanel;
                }

                if (panel == null || !panel.isVisible)
                {
                    return;
                }

                LocalCompany company = CompanyAt(WorldInfoPanel.GetCurrentInstanceID().Building);
                int listed = company == null ? 0 : CompanyListing.SlotOf(company.Id);
                if (company == null || listed <= 0)
                {
                    Undecorate();
                    return;
                }

                slot = listed;
                Decorate(company);
            }
            catch (Exception ex)
            {
                ModLog.Warn("building panel: " + ex.Message);
                nextCheck = Time.unscaledTime + 10f;
            }
        }

        private void OnDestroy()
        {
            try
            {
                Undecorate();
                foreach (UIComponent c in new UIComponent[] { gold, coin, section })
                {
                    if (c != null)
                    {
                        Destroy(c.gameObject);
                    }
                }

                if (sparkTexture != null)
                {
                    Destroy(sparkTexture);
                }
            }
            catch
            {
                // fail soft on unload
            }
        }

        /// <summary>The listed company whose headquarters is that building, or null.</summary>
        private static LocalCompany CompanyAt(ushort building)
        {
            if (building == 0)
            {
                return null;
            }

            uint index = BuildingManager.instance.m_buildings.m_buffer[building].m_buildIndex;
            foreach (LocalCompany c in CompanyDesk.Companies())
            {
                if (c.Home.Id == building && c.Home.BuildIndex == index)
                {
                    return c;
                }
            }

            return null;
        }

        private void Undecorate()
        {
            if (!decorated || panel == null)
            {
                return;
            }

            decorated = false;
            shownCompany = -1;
            shown = null;
            gold.isVisible = false;
            coin.isVisible = false;
            section.isVisible = false;
            UIComponent zone = panel.Find("Zone");
            if (zone != null)
            {
                zone.isVisible = true;
            }

            UITextField name = panel.Find<UITextField>("BuildingName");
            if (name != null)
            {
                name.textColor = nameColour;
            }

            UIComponent historical = panel.Find("MakeHistoricalPanel");
            if (historical != null)
            {
                historical.relativePosition = new Vector3(
                    historical.relativePosition.x,
                    historicalY
                );
            }

            panel.height = panelHeight;
            // After the height: both follow the bottom edge, so they are set last.
            UIComponent zoneInfo = panel.Find("ZoneTypeInfo");
            if (zoneInfo != null)
            {
                zoneInfo.relativePosition = new Vector3(zoneInfo.relativePosition.x, zoneInfoY);
            }

            if (historical != null)
            {
                historical.relativePosition = new Vector3(
                    historical.relativePosition.x,
                    historicalY
                );
            }
        }

        private void Decorate(LocalCompany company)
        {
            UIComponent zone = panel.Find("Zone");
            UITextField name = panel.Find<UITextField>("BuildingName");
            UIComponent historical = panel.Find("MakeHistoricalPanel");
            UIComponent zoneInfo = panel.Find("ZoneTypeInfo");
            if (historical == null)
            {
                return;
            }

            if (gold == null)
            {
                Build();
            }

            if (!decorated)
            {
                decorated = true;
                nameColour = name == null ? (Color32)Color.white : name.textColor;
                historicalY = historical.relativePosition.y;
                zoneInfoY = zoneInfo == null ? 0f : zoneInfo.relativePosition.y;
                panelHeight = panel.height;
                ModLog.Info("building panel: listing shown for " + company.Name);
            }

            // Every time: the game shows the zone icon and lays the panel out again when the
            // player picks another building.
            gold.isVisible = true;
            coin.isVisible = true;
            section.isVisible = true;
            if (zone != null)
            {
                zone.isVisible = false;
            }

            if (name != null)
            {
                name.textColor = GoldText;
            }

            section.relativePosition = new Vector3(Pad, historicalY);
            historical.relativePosition = new Vector3(
                historical.relativePosition.x,
                historicalY + CompanyPanelText.SectionSpace
            );
            panel.height = panelHeight + CompanyPanelText.SectionSpace;
            // The game keeps the workers block and the Historical line at a fixed distance
            // from the panel's bottom edge, so both moved down with it: the workers block goes
            // back where it was, and the section takes the room under it.
            if (zoneInfo != null)
            {
                zoneInfo.relativePosition = new Vector3(zoneInfo.relativePosition.x, zoneInfoY);
            }

            historical.relativePosition = new Vector3(
                historical.relativePosition.x,
                historicalY + CompanyPanelText.SectionSpace
            );
            Fill(company);
        }

        private void Fill(LocalCompany company)
        {
            int today = OrderDesk.Today;
            Investment[] buffer = EconomyManager.instance.m_investments.m_buffer;
            InvestmentInfo info = buffer[slot].Info;
            InvestmentAI ai = info == null ? null : info.m_investmentAI;
            if (ai == null)
            {
                return;
            }

            uint owned = ai.OwnedCount(ref buffer[slot]);
            long spent = buffer[slot].m_playerSpentMoney;
            long worth = owned == 0 ? 0 : ai.CalculatePrice(ref buffer[slot], owned);
            float rate = ai.CalculateChangeRate((byte)slot, ref buffer[slot], 5);
            string signature =
                company.Id
                + "|"
                + company.FirstTradingDay
                + "|"
                + today
                + "|"
                + company.PriceCents
                + "|"
                + owned
                + "|"
                + spent
                + "|"
                + worth
                + "|"
                + rate.ToString("0.##")
                + "|"
                + Strings.Language;
            if (signature == shown)
            {
                return;
            }

            shown = signature;
            if (shownCompany != company.Id)
            {
                shownCompany = company.Id;
                ai.SetSpriteName(coin);
                ai.SetSpriteName(sectionCoin);
            }

            bool offering = CompanyPanelText.Offering(company, today);
            DateTime now = SimulationManager.instance.m_currentGameTime;
            string firstDay = now.AddDays(company.FirstTradingDay - today).ToString("dd/MM/yyyy");
            string listed = now.AddDays(company.AnnouncedDay - today).ToString("dd/MM/yyyy");
            float width = section.width;

            symbol.text = StockSymbols.For(info);
            badge.text = CompanyPanelText.Badge(company, today);
            badge.color = offering ? Gold : (Color32)FoTheme.TickerGain;
            badge.relativePosition = new Vector3(
                symbol.relativePosition.x + symbol.width + 8f,
                10f
            );

            price.text = CompanyPanelText.Price(company);
            price.relativePosition = new Vector3(width - 8f - price.width, 6f);
            string offeringNote = CompanyPanelText.OfferingNote(company, today);
            note.isVisible = offeringNote != null;
            change.isVisible = offeringNote == null;
            spark.isVisible = offeringNote == null;
            if (offeringNote != null)
            {
                note.text = offeringNote;
                note.relativePosition = new Vector3(width - 8f - note.width, 28f);
            }
            else
            {
                Color32 colour = FoTheme.Change(StockMonitor.ChangeColour(rate));
                change.text = StockMonitor.ChangeText(rate);
                change.color = colour;
                var history = new float[Investment.kPastValueCount];
                buffer[slot].FillGraphValues(history);
                FoSparkline.Draw(sparkTexture, history, colour, SparkScale + 1);
            }

            // The line under the symbol ends before whatever stands at the right of its row.
            float lineEnd =
                offeringNote != null ? note.relativePosition.x : spark.relativePosition.x;
            FoText.Fit(
                line,
                CompanyPanelText.Line(company, today, firstDay, listed),
                0.7f,
                0.5f,
                lineEnd - 10f - 52f,
                14f
            );

            FoText.Fit(
                stake,
                CompanyPanelText.Stake(company, CompanyDesk.Rules, today, owned, spent),
                0.75f,
                0.6f,
                width - 16f - 90f,
                15f
            );
            string gained = CompanyPanelText.Gain(owned, spent, worth);
            gain.isVisible = gained != null;
            if (gained != null)
            {
                int sign = CompanyPanelText.GainSign(spent, worth);
                gain.text = gained;
                gain.textColor =
                    sign > 0 ? FoTheme.GamePositive
                    : sign < 0 ? FoTheme.GameNegative
                    : FoTheme.GameValue;
                gain.relativePosition = new Vector3(width - 8f - gain.width, 54f);
            }

            barFill.width = Mathf.Max(0f, (width - 16f) * CompanyPanelText.StakeFraction(owned));
            barFill.isVisible = owned > 0;

            // Offering: one wide gold Buy. Trading: Buy and Sell at the same size.
            buy.text = CompanyPanelText.Buy(company, today);
            buy.width = offering ? 200f : 96f;
            // Gold needs the game's white button art: its dark menu button turns gold to mud.
            string art = offering ? "ButtonWhite" : "ButtonMenu";
            buy.normalBgSprite = art;
            buy.hoveredBgSprite = offering ? art : "ButtonMenuHovered";
            buy.pressedBgSprite = offering ? art : "ButtonMenuPressed";
            buy.disabledBgSprite = offering ? art : "ButtonMenuDisabled";
            buy.focusedBgSprite = art;
            buy.hoveredColor = offering ? GoldHover : (Color32)Color.white;
            buy.pressedColor = offering ? Gold : (Color32)Color.white;
            buy.focusedColor = offering ? Gold : (Color32)Color.white;
            buy.color = offering ? Gold : (Color32)Color.white;
            buy.textColor = offering ? GoldText : White;
            buy.hoveredTextColor = buy.textColor;
            buy.pressedTextColor = buy.textColor;
            buy.focusedTextColor = buy.textColor;
            buy.isEnabled = ai.AvailableForBuyingCount(ref buffer[slot]) > 0;
            sell.isVisible = !offering;
            sell.isEnabled = owned > 0;
            sell.relativePosition = new Vector3(8f + 96f + 6f, 88f);
        }

        private void Build()
        {
            UiCleanup.RemoveChildrenNamed(panel, GoldName, CoinName, SectionName);
            float width = panel.width;

            gold = panel.AddUIComponent<UIPanel>();
            gold.name = GoldName;
            gold.backgroundSprite = "GenericPanelWhite";
            gold.color = Gold;
            gold.size = new Vector2(width - 4f, 38f);
            gold.relativePosition = new Vector3(2f, 2f);
            gold.isInteractive = false;
            gold.zOrder = 0; // under the game's title, pin and close button

            coin = panel.AddUIComponent<UISprite>();
            coin.name = CoinName;
            coin.size = new Vector2(52f, 52f);
            coin.relativePosition = new Vector3(-8f, -8f);
            coin.isInteractive = false;

            section = panel.AddUIComponent<UIPanel>();
            section.name = SectionName;
            section.backgroundSprite = "GenericPanelWhite";
            section.color = SectionFill;
            section.size = new Vector2(width - 2f * Pad, CompanyPanelText.SectionHeight);
            float w = section.width;

            sectionCoin = section.AddUIComponent<UISprite>();
            sectionCoin.name = Prefix + "ListingCoin";
            sectionCoin.size = new Vector2(40f, 40f);
            sectionCoin.relativePosition = new Vector3(6f, 5f);

            symbol = FoText.Label(section, Prefix + "Symbol", "", 1.0f, White);
            symbol.relativePosition = new Vector3(52f, 6f);
            badge = FoPill.AddBadge(section, 0.6f, GoldText, Gold);
            badge.name = Prefix + "Badge";
            badge.useDropShadow = false;
            line = FoText.Label(section, Prefix + "Line", "", 0.7f, Muted);
            line.relativePosition = new Vector3(52f, 28f);

            price = FoText.Label(section, Prefix + "Price", "", 1.0f, FoTheme.GameValue);
            note = FoText.Label(section, Prefix + "Note", "", 0.7f, Note);
            change = FoPill.AddValue(section, new Vector2(62f, 16f), 0.7f, FoTheme.Header);
            change.name = Prefix + "Change";
            change.relativePosition = new Vector3(w - 8f - 62f, 27f);

            sparkTexture = new Texture2D(
                SparkW * SparkScale,
                SparkH * SparkScale,
                TextureFormat.ARGB32,
                false
            );
            sparkTexture.filterMode = FilterMode.Bilinear;
            sparkTexture.wrapMode = TextureWrapMode.Clamp;
            spark = section.AddUIComponent<UITextureSprite>();
            spark.name = Prefix + "Spark";
            spark.texture = sparkTexture;
            spark.size = new Vector2(SparkW, SparkH);
            spark.relativePosition = new Vector3(w - 8f - 62f - 70f - SparkW, 10f);

            stake = FoText.Label(section, Prefix + "Stake", "", 0.75f, White);
            stake.relativePosition = new Vector3(8f, 54f);
            gain = FoText.Label(section, Prefix + "Gain", "", 0.75f, FoTheme.GamePositive);

            UIPanel track = section.AddUIComponent<UIPanel>();
            track.backgroundSprite = "EmptySprite";
            track.color = Track;
            track.size = new Vector2(w - 16f, BarHeight);
            track.relativePosition = new Vector3(8f, 74f);
            barFill = section.AddUIComponent<UIPanel>();
            barFill.name = Prefix + "StakeBar";
            barFill.backgroundSprite = "EmptySprite";
            barFill.color = Gold;
            barFill.size = new Vector2(0f, BarHeight);
            barFill.relativePosition = new Vector3(8f, 74f);

            buy = FoButton.Add(section, Prefix + "Buy", "", new Vector2(96f, 30f), 0.8f);
            buy.relativePosition = new Vector3(8f, 88f);
            buy.eventClick += (c, p) => Trade(BuySellInvestmentPanel.Mode.Buy);
            sell = FoButton.Add(
                section,
                Prefix + "Sell",
                CompanyPanelText.Sell,
                new Vector2(96f, 30f),
                0.8f
            );
            sell.eventClick += (c, p) => Trade(BuySellInvestmentPanel.Mode.Sell);
            show = FoButton.Add(
                section,
                Prefix + "Show",
                CompanyPanelText.ShowInInvestments,
                new Vector2(160f, 30f),
                0.8f
            );
            show.relativePosition = new Vector3(w - 8f - 160f, 88f);
            show.eventClick += (c, p) =>
                ToolsModifierControl.mainToolbar.ShowEconomyPanel(
                    GameInternals.Economy.TabIndex(GameInternals.Economy.InvestmentTab)
                );
        }

        private void Trade(BuySellInvestmentPanel.Mode mode)
        {
            if (slot > 0)
            {
                BuySellInvestmentPanel.ShowPanel((byte)slot, mode);
            }
        }
    }
}

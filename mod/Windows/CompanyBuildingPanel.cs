using System; // Exception
using ColossalFramework.UI; // UICheckBox, UIComponent, UIPanel, UISprite, UITextField, UIView
using FinanceOverhaul.Core; // CompanyPanelText, LocalCompany
using UnityEngine; // Color32, MonoBehaviour, Time, Vector2, Vector3

// Game classes used without a using (global namespace, Assembly-CSharp): BuildingManager,
//   EconomyManager, Investment, InvestmentInfo, WorldInfoPanel

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
        private static readonly Color32 Gold = FoTheme.OfferGold;
        private static readonly Color32 GoldText = FoTheme.OfferGoldText;

        private UIPanel panel;
        private UIPanel gold;
        private UISprite coin;
        private ListingSection listing;

        private bool decorated;
        private Color32 nameColour;
        private float historicalY;
        private float zoneInfoY;
        private float panelHeight;
        private int shownCompany = -1;
        private int slot;
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
                foreach (UIComponent c in new UIComponent[] { gold, coin })
                {
                    if (c != null)
                    {
                        Destroy(c.gameObject);
                    }
                }

                if (listing != null)
                {
                    listing.Destroy();
                }
            }
            catch
            {
                // fail soft on unload
            }
        }

        /// <summary>The listed company whose headquarters is that building, or null.</summary>
        internal static LocalCompany CompanyAt(ushort building)
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
            gold.isVisible = false;
            coin.isVisible = false;
            listing.Hide();
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

            UICheckBox tick =
                historical == null ? null : historical.Find<UICheckBox>("CheckboxHistorical");
            if (tick != null)
            {
                tick.readOnly = false;
                tick.tooltip = string.Empty;
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
                nameColour = name == null ? FoTheme.GameText : name.textColor;
                historicalY = historical.relativePosition.y;
                zoneInfoY = zoneInfo == null ? 0f : zoneInfo.relativePosition.y;
                panelHeight = panel.height;
                ModLog.Info("building panel: listing shown for " + company.Name);
            }

            // Every time: the game shows the zone icon and lays the panel out again when the
            // player picks another building.
            gold.isVisible = true;
            coin.isVisible = true;
            if (zone != null)
            {
                zone.isVisible = false;
            }

            if (name != null)
            {
                name.textColor = GoldText;
            }

            listing.Panel.relativePosition = new Vector3(Pad, historicalY);
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
            // A listed company's headquarters stays Historical: the tick is shown, not offered.
            UICheckBox tick = historical.Find<UICheckBox>("CheckboxHistorical");
            if (tick != null)
            {
                tick.readOnly = true;
                tick.tooltip = CompanyPanelText.HistoricalLocked;
            }

            CompanyDesk.KeepHistorical(company);
            Fill(company);
        }

        private void Fill(LocalCompany company)
        {
            listing.Show(company, slot);
            if (shownCompany != company.Id)
            {
                Investment[] buffer = EconomyManager.instance.m_investments.m_buffer;
                InvestmentInfo info = buffer[slot].Info;
                if (info != null && info.m_investmentAI != null)
                {
                    shownCompany = company.Id;
                    info.m_investmentAI.SetSpriteName(coin);
                }
            }
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

            listing = new ListingSection(panel, Prefix, width - 2f * Pad);
        }
    }
}

using System; // Exception
using ColossalFramework.UI; // UIButton, UIComponent, UIPanel, UITabContainer, UITabstrip, UIView
using FinanceOverhaul.Core; // BusinessSite, CompanyPanelText, LocalCompany
using UnityEngine; // Mathf, MonoBehaviour, Time, Vector2, Vector3

// Game classes used without a using (global namespace, Assembly-CSharp): WorldInfoPanel

namespace FinanceOverhaul
{
    /// <summary>
    /// A "Shares" tab on the industry area's own panel (the one with the production diagram and
    /// the Info and Policies tabs), for as long as the area is a listed company: the listing
    /// section (<see cref="ListingSection"/>) on a page of its own, since the Info page has no
    /// room left. The tab is made with the game's own tab strip from its Info tab, so it looks
    /// like the others; it is hidden for an area that is not listed, and the panel goes back to
    /// Info if it was open. Lives on the overlay root.
    /// </summary>
    public class CompanyAreaTab : MonoBehaviour
    {
        public const string Prefix = "FinanceOverhaulAreaTab";
        public const string TabName = Prefix + "Button";
        public const string PageName = Prefix + "Page";

        private const string GamePanel = "(Library) IndustryWorldInfoPanel"; // not shown: a game control name
        private const float Pad = 12f;

        private UIPanel panel;
        private UITabstrip strip;
        private UIButton tab;
        private UIPanel page;
        private ListingSection listing;
        private bool shown;
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

                LocalCompany company = AreaCompany(WorldInfoPanel.GetCurrentInstanceID().Park);
                int slot = company == null ? 0 : CompanyListing.SlotOf(company.Id);
                if (slot <= 0)
                {
                    Hide();
                    return;
                }

                Show(company, slot);
            }
            catch (Exception ex)
            {
                ModLog.Warn("area tab: " + ex.Message);
                nextCheck = Time.unscaledTime + 10f;
            }
        }

        private void OnDestroy()
        {
            try
            {
                Hide();
                if (listing != null)
                {
                    listing.Destroy();
                }

                foreach (UIComponent c in new UIComponent[] { tab, page })
                {
                    if (c != null)
                    {
                        Destroy(c.gameObject);
                    }
                }
            }
            catch
            {
                // fail soft on unload
            }
        }

        /// <summary>The listed company that is this industry area, or null.</summary>
        private static LocalCompany AreaCompany(byte park)
        {
            if (park == 0)
            {
                return null;
            }

            ushort gate = IndustrySites.MainBuilding(park);
            LocalCompany company = CompanyBuildingPanel.CompanyAt(gate);
            return company != null && company.Site == BusinessSite.Area ? company : null;
        }

        private void Show(LocalCompany company, int slot)
        {
            if (listing == null && !Build())
            {
                return;
            }

            tab.text = CompanyPanelText.SharesTab;
            if (!shown)
            {
                shown = true;
                tab.isVisible = true;
                ModLog.Info("area tab: shown for " + company.Name);
            }

            listing.Show(company, slot);
        }

        private void Hide()
        {
            if (!shown)
            {
                return;
            }

            shown = false;
            listing.Hide();
            if (strip != null && tab != null && strip.selectedIndex == tab.zOrder)
            {
                strip.selectedIndex = 0;
            }

            tab.isVisible = false;
            page.isVisible = false;
        }

        private bool Build()
        {
            strip = panel.Find<UITabstrip>("Tabstrip");
            UITabContainer pages = panel.Find<UITabContainer>("TabContainer");
            UIButton template = strip == null ? null : strip.Find<UIButton>("TabBasicInfo");
            if (strip == null || pages == null || template == null)
            {
                ModLog.Warn("area tab: the area panel has no tab strip");
                return false;
            }

            // A hot reload leaves the last build's tab and page behind.
            UiCleanup.RemoveChildrenNamed(strip, TabName);
            UiCleanup.RemoveChildrenNamed(pages, PageName);
            int before = pages.components.Count;
            tab = strip.AddTab(CompanyPanelText.SharesTab, template, true);
            tab.name = TabName;
            // AddTab copies the look but not the text size: as the game's own tabs.
            tab.textScale = template.textScale;
            tab.autoSize = true;
            tab.autoSize = false;
            tab.size = new Vector2(Mathf.Max(template.width, tab.width), template.height);
            page = pages.components.Count > before ? pages.components[before] as UIPanel : null;
            if (page == null)
            {
                ModLog.Warn("area tab: the tab strip made no page");
                Destroy(tab.gameObject);
                tab = null;
                return false;
            }

            page.name = PageName;
            // The container hides a page only when another is picked: a new one starts shown,
            // over the selected tab's page.
            page.isVisible = false;
            listing = new ListingSection(page, Prefix, page.width - 2f * Pad);
            listing.Panel.relativePosition = new Vector3(Pad, Pad);
            tab.isVisible = false;
            return true;
        }
    }
}

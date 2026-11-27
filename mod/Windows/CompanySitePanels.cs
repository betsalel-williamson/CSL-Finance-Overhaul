using System; // Exception
using System.Collections.Generic; // Dictionary, KeyValuePair
using ColossalFramework.UI; // UIComponent, UIPanel, UITextField, UIView
using FinanceOverhaul.Core; // BusinessSite, CompanyPanelText, LocalCompany
using UnityEngine; // Color32, MonoBehaviour, Time, Vector2, Vector3

// Game classes used without a using (global namespace, Assembly-CSharp): WorldInfoPanel

namespace FinanceOverhaul
{
    /// <summary>
    /// The listing section on the info panels of an industry area's main building and of a
    /// unique factory, when that area or factory is a listed company: a gold title bar and the
    /// section the zoned headquarters' panel has (<see cref="ListingSection"/>), at the bottom.
    /// The main building's panel is the game's service-building panel, which stacks its rows and
    /// fits itself to them, so the section is one more row; the factory's panel places its rows
    /// by hand, so it grows by the section and its rows are put back where they were. Every
    /// other building's panel is left as the game made it. Lives on the overlay root.
    /// </summary>
    public class CompanySitePanels : MonoBehaviour
    {
        public const string AreaPrefix = "FinanceOverhaulAreaBuilding";
        public const string FactoryPrefix = "FinanceOverhaulFactory";

        private const string ServicePanel = "(Library) CityServiceWorldInfoPanel"; // not shown: a game control name
        private const string FactoryPanel = "(Library) UniqueFactoryWorldInfoPanel"; // not shown: a game control name
        private const float Pad = 12f;

        /// <summary>
        /// The service panel's background (InfoBubbleService) ends in its pointer tail: this much
        /// more under the section leaves the same 12 visible above the panel's edge as at its sides.
        /// </summary>
        private const float StackedEdge = 16f;

        /// <summary>The factory panel's rows end this far above its bottom edge.</summary>
        private const float FactoryBottomInset = 17f;

        private readonly Host area = new Host(ServicePanel, AreaPrefix, BusinessSite.Area, true);
        private readonly Host factory = new Host(
            FactoryPanel,
            FactoryPrefix,
            BusinessSite.Factory,
            false
        );
        private float nextCheck;

        private void Update()
        {
            if (Time.unscaledTime < nextCheck)
            {
                return;
            }

            nextCheck = Time.unscaledTime + 0.2f;
            foreach (Host host in new[] { area, factory })
            {
                try
                {
                    host.Update();
                }
                catch (Exception ex)
                {
                    ModLog.Warn("site panel: " + ex.Message);
                    nextCheck = Time.unscaledTime + 10f;
                }
            }
        }

        private void OnDestroy()
        {
            foreach (Host host in new[] { area, factory })
            {
                try
                {
                    host.Remove();
                }
                catch
                {
                    // fail soft on unload
                }
            }
        }

        /// <summary>One game panel and what the mod added to it.</summary>
        private sealed class Host
        {
            private readonly string gamePanel;
            private readonly string prefix;
            private readonly BusinessSite site;
            private readonly bool stacked;

            private UIPanel panel;
            private UIPanel holder;
            private UIPanel gold;
            private UIPanel captionPanel;
            private string captionSprite;
            private Color32 captionColour;
            private ListingSection listing;
            private bool decorated;
            private Color32 nameColour;
            private float panelHeight;
            private readonly Dictionary<UIComponent, Vector3> rows =
                new Dictionary<UIComponent, Vector3>();

            public Host(string gamePanel, string prefix, BusinessSite site, bool stacked)
            {
                this.gamePanel = gamePanel;
                this.prefix = prefix;
                this.site = site;
                this.stacked = stacked;
            }

            public void Update()
            {
                if (panel == null)
                {
                    panel = UIView.GetAView().FindUIComponent(gamePanel) as UIPanel;
                }

                if (panel == null || !panel.isVisible)
                {
                    return;
                }

                LocalCompany company = CompanyBuildingPanel.CompanyAt(
                    WorldInfoPanel.GetCurrentInstanceID().Building
                );
                int slot =
                    company == null || company.Site != site ? 0 : CompanyListing.SlotOf(company.Id);
                if (slot <= 0)
                {
                    Undecorate();
                    return;
                }

                Decorate(company, slot);
            }

            public void Remove()
            {
                Undecorate();
                ShowGold(false);
                if (gold != null)
                {
                    UnityEngine.Object.Destroy(gold.gameObject);
                }

                if (listing != null)
                {
                    listing.Destroy();
                }

                if (holder != null)
                {
                    UnityEngine.Object.Destroy(holder.gameObject);
                }
            }

            private void Decorate(LocalCompany company, int slot)
            {
                if (listing == null && !Build())
                {
                    return;
                }

                UITextField name = panel.Find<UITextField>("BuildingName");
                if (!decorated)
                {
                    decorated = true;
                    nameColour = name == null ? FoTheme.GameText : name.textColor;
                    ModLog.Info("site panel: listing shown for " + company.Name);
                    if (!stacked)
                    {
                        // Every row as the game placed it: some follow the bottom edge.
                        rows.Clear();
                        panelHeight = panel.height;
                        foreach (UIComponent c in panel.components)
                        {
                            if (c != gold && c != listing.Panel)
                            {
                                rows[c] = c.relativePosition;
                            }
                        }
                    }
                }

                ShowGold(true);
                if (name != null)
                {
                    name.textColor = FoTheme.OfferGoldText;
                }

                if (stacked)
                {
                    holder.isVisible = true;
                }
                else
                {
                    listing.Panel.relativePosition = new Vector3(
                        Pad,
                        panelHeight - FactoryBottomInset
                    );
                    panel.height = panelHeight + CompanyPanelText.SectionSpace;
                    PutRowsBack();
                }

                listing.Show(company, slot);
            }

            private void Undecorate()
            {
                if (!decorated || panel == null)
                {
                    return;
                }

                decorated = false;
                ShowGold(false);
                listing.Hide();
                UITextField name = panel.Find<UITextField>("BuildingName");
                if (name != null)
                {
                    name.textColor = nameColour;
                }

                if (stacked)
                {
                    holder.isVisible = false;
                }
                else
                {
                    panel.height = panelHeight;
                    PutRowsBack();
                }
            }

            private void ShowGold(bool on)
            {
                if (gold != null)
                {
                    gold.isVisible = on;
                }

                if (captionPanel != null)
                {
                    captionPanel.backgroundSprite = on ? "GenericPanelWhite" : captionSprite;
                    captionPanel.color = on ? FoTheme.OfferGold : captionColour;
                }
            }

            private void PutRowsBack()
            {
                foreach (KeyValuePair<UIComponent, Vector3> row in rows)
                {
                    if (row.Key != null)
                    {
                        row.Key.relativePosition = row.Value;
                    }
                }
            }

            private bool Build()
            {
                UIComponent caption = panel.Find(stacked ? "CaptionPanel" : "Caption");
                UIComponent parent = stacked ? panel.Find("Wrapper") : panel;
                if (caption == null || parent == null)
                {
                    ModLog.Warn("site panel: " + gamePanel + " has no caption or wrapper");
                    return false;
                }

                UiCleanup.RemoveChildrenNamed(caption, prefix + "Gold");
                UiCleanup.RemoveChildrenNamed(parent, prefix + "Holder", prefix + "Listing");
                captionPanel = caption as UIPanel;
                if (stacked && captionPanel != null)
                {
                    // The service panel lays its caption's children out in a row, so a gold
                    // panel among them would take a place in it: the caption itself turns gold.
                    captionSprite = captionPanel.backgroundSprite;
                    captionColour = captionPanel.color;
                }
                else
                {
                    captionPanel = null;
                }

                gold = caption.AddUIComponent<UIPanel>();
                gold.name = prefix + "Gold";
                gold.backgroundSprite = "GenericPanelWhite";
                gold.color = FoTheme.OfferGold;
                gold.size = new Vector2(caption.width - 4f, caption.height - 2f);
                gold.relativePosition = new Vector3(2f, 2f);
                gold.isInteractive = false;
                gold.zOrder = 0; // under the game's switch, name and buttons
                gold.isVisible = false;
                if (captionPanel != null)
                {
                    UnityEngine.Object.Destroy(gold.gameObject);
                    gold = null;
                }

                float width = parent.width - 2f * Pad;
                if (stacked)
                {
                    // One more row of the stack: the section and the gap under it.
                    holder = parent.AddUIComponent<UIPanel>();
                    holder.name = prefix + "Holder";
                    holder.size = new Vector2(
                        parent.width,
                        CompanyPanelText.SectionSpace + StackedEdge
                    );
                    holder.isVisible = false;
                    listing = new ListingSection(holder, prefix, width);
                    listing.Panel.relativePosition = new Vector3(Pad, 0f);
                }
                else
                {
                    listing = new ListingSection(parent, prefix, width);
                }

                return true;
            }
        }
    }
}

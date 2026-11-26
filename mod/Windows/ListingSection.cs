using System; // DateTime
using ColossalFramework.UI; // UIButton, UIComponent, UILabel, UIPanel, UISprite, UITextureSprite
using FinanceOverhaul.Core; // CompanyPanelText, LocalCompany, StockMonitor, Strings
// UnityEngine: Color32, FilterMode, Mathf, Texture2D, TextureFormat, TextureWrapMode, Vector2,
//   Vector3
using UnityEngine;

// Game classes used without a using (global namespace, Assembly-CSharp): BuySellInvestmentPanel,
//   EconomyManager, Investment, InvestmentAI, InvestmentInfo, SimulationManager,
//   ToolsModifierControl

namespace FinanceOverhaul
{
    /// <summary>
    /// The listing section a listed company's info panel shows (Figma UI Map band 19, designs
    /// B and C): the coin, symbol and badge, the price with its change and sparkline (or the
    /// offering note in the first week), the city's stake with its bar, and Buy, Sell and Show
    /// in Investments. The host panel says where it goes; every control is named
    /// <c>prefix + part</c> ("…Badge", "…Stake") so game UI tests find it.
    /// </summary>
    public sealed class ListingSection
    {
        private const float BarHeight = 4f;
        private const int SparkW = 84,
            SparkH = 24,
            SparkScale = 2;

        private static readonly Color32 Gold = FoTheme.OfferGold;
        private static readonly Color32 GoldHover = FoTheme.OfferGoldHover;
        private static readonly Color32 GoldText = FoTheme.OfferGoldText;
        private static readonly Color32 White = FoTheme.TextPrimary;
        private static readonly Color32 Muted = FoTheme.TextMuted;
        private static readonly Color32 Note = FoTheme.TickerSymbol;

        private readonly UIPanel section;
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

        private int shownCompany = -1;
        private int slot;
        private string shown;

        /// <summary>Adds the section to <paramref name="parent"/>, hidden, named <c>prefix + "Listing"</c>.</summary>
        public ListingSection(UIComponent parent, string prefix, float width)
        {
            section = parent.AddUIComponent<UIPanel>();
            section.name = prefix + "Listing";
            section.backgroundSprite = "GenericPanelWhite";
            section.color = FoTheme.SectionFill;
            section.size = new Vector2(width, CompanyPanelText.SectionHeight);
            section.isVisible = false;
            Build(prefix);
        }

        /// <summary>The section itself, for the host to place.</summary>
        public UIPanel Panel
        {
            get { return section; }
        }

        /// <summary>Shown for this company, listed in that investment slot; refills on change only.</summary>
        public void Show(LocalCompany company, int listedSlot)
        {
            section.isVisible = true;
            if (listedSlot != slot)
            {
                shown = null;
            }

            slot = listedSlot;
            Fill(company);
        }

        public void Hide()
        {
            section.isVisible = false;
            shownCompany = -1;
            shown = null;
        }

        /// <summary>Removes the section and its texture (unload, hot reload).</summary>
        public void Destroy()
        {
            if (section != null)
            {
                UnityEngine.Object.Destroy(section.gameObject);
            }

            if (sparkTexture != null)
            {
                UnityEngine.Object.Destroy(sparkTexture);
            }
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
                history = CompanyListing.ChartDays(ai, history);
                FoSparkline.Draw(sparkTexture, history, colour, SparkScale + 1);
            }

            // The line under the symbol ends before whatever stands at the right of its row.
            float lineEnd =
                offeringNote != null ? note.relativePosition.x : spark.relativePosition.x;
            // Too long for the row even scaled down: the kind goes (the panel names it).
            const float lineHeight = 14f;
            float lineWidth = lineEnd - 10f - 52f;
            if (
                FoText.Fit(
                    line,
                    CompanyPanelText.Line(company, today, firstDay, listed),
                    0.7f,
                    0.5f,
                    lineWidth,
                    lineHeight
                ) > lineHeight
            )
            {
                FoText.Fit(
                    line,
                    CompanyPanelText.ShortLine(company, today, firstDay, listed),
                    0.7f,
                    0.5f,
                    lineWidth,
                    lineHeight
                );
            }

            string gained = CompanyPanelText.Gain(owned, spent, worth);
            FoText.Fit(
                stake,
                CompanyPanelText.Stake(company, CompanyDesk.Current, today, owned, spent),
                0.75f,
                0.6f,
                width - 16f - (gained == null ? 0f : 90f),
                15f
            );
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

            // Offering, none held yet: one wide gold Buy. Otherwise Buy and Sell at the same size
            // (gold while offering), so a city that bought in can always get back to the dialog.
            buy.text = CompanyPanelText.Buy(company, today, owned);
            bool wide = CompanyPanelText.WideBuy(company, today, owned);
            buy.width = wide ? 200f : 96f;
            // Gold needs the game's white button art: its dark menu button turns gold to mud.
            string art = offering ? "ButtonWhite" : "ButtonMenu";
            buy.normalBgSprite = art;
            buy.hoveredBgSprite = offering ? art : "ButtonMenuHovered";
            buy.pressedBgSprite = offering ? art : "ButtonMenuPressed";
            buy.disabledBgSprite = offering ? art : "ButtonMenuDisabled";
            buy.focusedBgSprite = art;
            buy.hoveredColor = offering ? GoldHover : FoTheme.Untinted;
            buy.pressedColor = offering ? Gold : FoTheme.Untinted;
            buy.focusedColor = offering ? Gold : FoTheme.Untinted;
            buy.color = offering ? Gold : FoTheme.Untinted;
            buy.textColor = offering ? GoldText : White;
            buy.hoveredTextColor = buy.textColor;
            buy.pressedTextColor = buy.textColor;
            buy.focusedTextColor = buy.textColor;
            buy.isEnabled = ai.AvailableForBuyingCount(ref buffer[slot]) > 0;
            sell.isVisible = !wide;
            sell.isEnabled = owned > 0;
            sell.relativePosition = new Vector3(8f + 96f + 6f, 88f);
        }

        private void Build(string prefix)
        {
            float w = section.width;

            sectionCoin = section.AddUIComponent<UISprite>();
            sectionCoin.name = prefix + "ListingCoin";
            sectionCoin.size = new Vector2(40f, 40f);
            sectionCoin.relativePosition = new Vector3(6f, 5f);

            symbol = FoText.Label(section, prefix + "Symbol", "", 1.0f, White);
            symbol.relativePosition = new Vector3(52f, 6f);
            badge = FoPill.AddBadge(section, 0.6f, GoldText, Gold);
            badge.name = prefix + "Badge";
            badge.useDropShadow = false;
            line = FoText.Label(section, prefix + "Line", "", 0.7f, Muted);
            line.relativePosition = new Vector3(52f, 28f);

            price = FoText.Label(section, prefix + "Price", "", 1.0f, FoTheme.GameValue);
            note = FoText.Label(section, prefix + "Note", "", 0.7f, Note);
            change = FoPill.AddValue(section, new Vector2(62f, 16f), 0.7f, FoTheme.Header);
            change.name = prefix + "Change";
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
            spark.name = prefix + "Spark";
            spark.texture = sparkTexture;
            spark.size = new Vector2(SparkW, SparkH);
            spark.relativePosition = new Vector3(w - 8f - 62f - 70f - SparkW, 10f);

            stake = FoText.Label(section, prefix + "Stake", "", 0.75f, White);
            stake.relativePosition = new Vector3(8f, 54f);
            gain = FoText.Label(section, prefix + "Gain", "", 0.75f, FoTheme.GamePositive);

            UIPanel track = section.AddUIComponent<UIPanel>();
            track.backgroundSprite = "EmptySprite";
            track.color = FoTheme.BarTrack;
            track.size = new Vector2(w - 16f, BarHeight);
            track.relativePosition = new Vector3(8f, 74f);
            barFill = section.AddUIComponent<UIPanel>();
            barFill.name = prefix + "StakeBar";
            barFill.backgroundSprite = "EmptySprite";
            barFill.color = Gold;
            barFill.size = new Vector2(0f, BarHeight);
            barFill.relativePosition = new Vector3(8f, 74f);

            buy = FoButton.Add(section, prefix + "Buy", "", new Vector2(96f, 30f), 0.8f);
            buy.relativePosition = new Vector3(8f, 88f);
            buy.eventClick += (c, p) => Trade(BuySellInvestmentPanel.Mode.Buy);
            sell = FoButton.Add(
                section,
                prefix + "Sell",
                CompanyPanelText.Sell,
                new Vector2(96f, 30f),
                0.8f
            );
            sell.eventClick += (c, p) => Trade(BuySellInvestmentPanel.Mode.Sell);
            UIButton show = FoButton.Add(
                section,
                prefix + "Show",
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

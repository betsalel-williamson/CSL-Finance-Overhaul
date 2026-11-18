using System;
using System.Globalization;

namespace FinanceOverhaul.Core
{
    /// <summary>
    /// What the game's building panel says about a headquarters whose company is listed (Figma
    /// UI Map band 19, designs B and C): the listing section's wording and the stake bar, in
    /// its two states. Offering: the 7 days the city can buy at the offering price. Trading:
    /// every day after. The host formats dates and reads what the city holds from the game.
    /// </summary>
    public static class CompanyPanelText
    {
        /// <summary>The section's height: the panel grows by this and the gap under it.</summary>
        public const float SectionHeight = 128f;

        /// <summary>Space the section takes in the panel, with the gap below it.</summary>
        public const float SectionSpace = SectionHeight + 8f;

        /// <summary>Still in its offering window: the price is the offering price.</summary>
        public static bool Offering(LocalCompany company, int today)
        {
            return company != null && today < company.FirstTradingDay;
        }

        /// <summary>"OFFERING" or "TRADING".</summary>
        public static string Badge(LocalCompany company, int today)
        {
            return Strings.Get(
                Offering(company, today) ? "company.panel.offering" : "company.panel.trading"
            );
        }

        /// <summary>
        /// "Office · First trading day 21/08/2116", then "Office · Listed 14/08/2116" from the first trading day on.
        /// </summary>
        public static string Line(LocalCompany company, int today, string firstDay, string listed)
        {
            string kind = IpoDialogText.KindName(company.Kind);
            return Offering(company, today)
                ? Strings.Format("company.panel.line.offering", kind, firstDay)
                : Strings.Format("company.panel.line.trading", kind, listed);
        }

        /// <summary>"₡432": the price a share now.</summary>
        public static string Price(LocalCompany company)
        {
            return BondText.WholeMoney(company.PriceCents);
        }

        /// <summary>"Offering price · 5 days left"; null once it trades.</summary>
        public static string OfferingNote(LocalCompany company, int today)
        {
            if (!Offering(company, today))
            {
                return null;
            }

            int days = Math.Max(1, company.FirstTradingDay - today);
            return Strings.Plural(
                "company.panel.offeringNote",
                days,
                days.ToString(CultureInfo.InvariantCulture)
            );
        }

        /// <summary>
        /// "The city can buy up to 1,000 shares (a 10% stake, ₡432,000)" while it holds none in
        /// the offering window; otherwise "City holds 250 of 1,000 shares · avg ₡432.00" (the
        /// average left out when none are held).
        /// </summary>
        public static string Stake(
            LocalCompany company,
            IpoRules rules,
            int today,
            uint owned,
            long spent
        )
        {
            string cap = IpoPricing.HoldingCap.ToString("#,0", CultureInfo.InvariantCulture);
            if (owned == 0)
            {
                return Offering(company, today)
                    ? Strings.Format(
                        "company.panel.canBuy",
                        cap,
                        (rules ?? new IpoRules()).CityStakePercent.ToString(
                            CultureInfo.InvariantCulture
                        ),
                        BondText.WholeMoney(company.OfferPriceCents * IpoPricing.HoldingCap)
                    )
                    : Strings.Format("company.panel.holdsNone", cap);
            }

            return Strings.Plural(
                "company.panel.holds",
                owned,
                owned.ToString("#,0", CultureInfo.InvariantCulture),
                cap,
                CostBasis.FormatAverage(spent, owned)
            );
        }

        /// <summary>"+₡1,000", "−₡250", "₡0": what the holding has gained; null when none is held.</summary>
        public static string Gain(uint owned, long spent, long worth)
        {
            if (owned == 0)
            {
                return null;
            }

            long gain = worth - spent;
            string sign =
                gain > 0 ? "+"
                : gain < 0 ? "−"
                : string.Empty;
            return sign + "₡" + Math.Abs(gain).ToString("N0", CultureInfo.InvariantCulture);
        }

        /// <summary>1 a gain, −1 a loss, 0 neither.</summary>
        public static int GainSign(long spent, long worth)
        {
            return worth > spent ? 1
                : worth < spent ? -1
                : 0;
        }

        /// <summary>The stake bar, 0–1: shares held of the most the city may hold.</summary>
        public static float StakeFraction(uint owned)
        {
            return Math.Max(0f, Math.Min(1f, owned / (float)IpoPricing.HoldingCap));
        }

        /// <summary>"Buy at the offering price" in the window, "Buy" after.</summary>
        public static string Buy(LocalCompany company, int today)
        {
            return Offering(company, today) ? IpoDialogText.Buy : Strings.Get("investments.buy");
        }

        public static string Sell
        {
            get { return Strings.Get("investments.sell"); }
        }

        /// <summary>Why the Historical Building tick cannot be changed while the company is listed.</summary>
        public static string HistoricalLocked
        {
            get { return Strings.Get("company.panel.historical.tip"); }
        }

        public static string ShowInInvestments
        {
            get { return Strings.Get("company.panel.show"); }
        }
    }
}

using System.Globalization;

namespace FinanceOverhaul.Core
{
    /// <summary>
    /// What the IPO dialog says (Figma UI Map band 16; docs/features/local-ipos.md, "The IPO
    /// moment"): only what is fixed on the day a company is announced. No forecast and no
    /// first-day pop. The host formats dates and supplies the building's name.
    /// </summary>
    public static class IpoDialogText
    {
        public static string Title(LocalCompany company)
        {
            return Strings.Format("ipo.dialog.title", company.Name);
        }

        /// <summary>The kind of business, e.g. "IT cluster".</summary>
        public static string KindName(BusinessKind kind)
        {
            return Strings.Get("company.kind." + kind);
        }

        /// <summary>Under the name: "Office · headquarters in Tech Park", or the kind alone outside any district.</summary>
        public static string Header(LocalCompany company, string district)
        {
            return string.IsNullOrEmpty(district)
                ? KindName(company.Kind)
                : Strings.Format("ipo.dialog.header", KindName(company.Kind), district);
        }

        public static string Lead
        {
            get { return Strings.Get("ipo.dialog.lead"); }
        }

        public static string PriceLabel
        {
            get { return Strings.Get("ipo.dialog.price"); }
        }

        /// <summary>"₡52 a share"</summary>
        public static string Price(LocalCompany company)
        {
            return Strings.Format(
                "ipo.dialog.price.value",
                BondText.WholeMoney(company.OfferPriceCents)
            );
        }

        public static string WindowLabel
        {
            get { return Strings.Get("ipo.dialog.window"); }
        }

        /// <summary>"7 days · until 16/06/2111"</summary>
        public static string Window(LocalCompany company, string lastDay)
        {
            int days = company.FirstTradingDay - company.AnnouncedDay;
            return Strings.Plural(
                "ipo.dialog.window.value",
                days,
                days.ToString(CultureInfo.InvariantCulture),
                lastDay
            );
        }

        public static string CanBuyLabel
        {
            get { return Strings.Get("ipo.dialog.canBuy"); }
        }

        /// <summary>"up to 1,000 shares (a 10% stake, ₡52,000)"</summary>
        public static string CanBuy(LocalCompany company, IpoRules rules)
        {
            return Strings.Format(
                "ipo.dialog.canBuy.value",
                IpoPricing.HoldingCap.ToString("#,0", CultureInfo.InvariantCulture),
                rules.CityStakePercent.ToString(CultureInfo.InvariantCulture),
                BondText.WholeMoney(company.OfferPriceCents * IpoPricing.HoldingCap)
            );
        }

        public static string FirstDayLabel
        {
            get { return Strings.Get("ipo.dialog.firstDay"); }
        }

        /// <summary>"17/06/2111 · the price then moves freely"</summary>
        public static string FirstDay(string date)
        {
            return Strings.Format("ipo.dialog.firstDay.value", date);
        }

        public static string SmallPrint
        {
            get { return Strings.Get("ipo.dialog.smallPrint"); }
        }

        public static string Buy
        {
            get { return Strings.Get("ipo.dialog.buy"); }
        }

        public static string View
        {
            get { return Strings.Get("ipo.dialog.view"); }
        }

        public static string DontShow
        {
            get { return Strings.Get("ipo.dialog.dontShow"); }
        }

        public static string CloseTip
        {
            get { return Strings.Get("ipo.dialog.closeTip"); }
        }
    }
}

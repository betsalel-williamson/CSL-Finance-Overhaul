using System.Globalization; // CultureInfo

namespace FinanceOverhaul.Core
{
    /// <summary>
    /// What the IPO dialog says (Figma UI Map band 16; docs/features/local-ipos.md, "The IPO
    /// moment"): only what is fixed on the day a company is announced. No forecast and no
    /// first-day pop. The host formats dates and supplies the building's name.
    /// </summary>
    public static class IpoDialogText
    {
        /// <summary>The headline, as on the game's own celebration dialogs.</summary>
        public static string Congratulations
        {
            get { return Strings.Get("ipo.dialog.congrats"); }
        }

        public static string Title(LocalCompany company)
        {
            return Strings.Format("ipo.dialog.title", company.Name);
        }

        /// <summary>The kind of business, e.g. "IT cluster".</summary>
        public static string KindName(BusinessKind kind)
        {
            return Strings.Get("company.kind." + kind);
        }

        /// <summary>
        /// Where the headquarters stands, with its kind: "IT cluster · on Green Avenue at Pearl
        /// Street, Tech Park". The street, the cross street and the district are each left out
        /// when the building has none. The company's name is not repeated here: the dialog says
        /// it once, in its title.
        /// </summary>
        public static string Where(
            LocalCompany company,
            string street,
            string cross,
            string district
        )
        {
            string kind =
                company.Site == BusinessSite.Area
                    ? Strings.Format("company.kind.area", KindName(company.Kind))
                    : KindName(company.Kind);
            bool hasStreet = !string.IsNullOrEmpty(street);
            bool hasCross = hasStreet && !string.IsNullOrEmpty(cross) && cross != street;
            bool hasDistrict = !string.IsNullOrEmpty(district);
            if (hasCross)
            {
                return hasDistrict
                    ? Strings.Format(
                        "ipo.dialog.where.crossDistrict",
                        kind,
                        street,
                        cross,
                        district
                    )
                    : Strings.Format("ipo.dialog.where.cross", kind, street, cross);
            }

            if (hasStreet)
            {
                return hasDistrict
                    ? Strings.Format("ipo.dialog.where.streetDistrict", kind, street, district)
                    : Strings.Format("ipo.dialog.where.street", kind, street);
            }

            return hasDistrict ? Strings.Format("ipo.dialog.where.district", kind, district) : kind;
        }

        public static string Lead
        {
            get { return Strings.Get("ipo.dialog.lead"); }
        }

        /// <summary>The line under the title: what kind of company is listing.</summary>
        public static string LeadFor(LocalCompany company)
        {
            return company == null || company.Site == BusinessSite.Zoned
                ? Lead
                : Strings.Get("ipo.dialog.lead." + company.Site);
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

        /// <summary>
        /// The small print: a zoned headquarters becomes Historical; an industry area's main
        /// building or a unique factory cannot (service buildings), so that sentence is left out.
        /// </summary>
        public static string SmallPrintFor(LocalCompany company)
        {
            return company == null || company.Site == BusinessSite.Zoned
                ? SmallPrint
                : Strings.Get("ipo.dialog.smallPrint.site");
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

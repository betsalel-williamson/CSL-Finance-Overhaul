using System; // Math
using System.Collections.Generic; // IEnumerable, List
using System.Globalization; // CultureInfo

namespace FinanceOverhaul.Core
{
    /// <summary>
    /// The city figure the game reads to price a stock (each stock class's own rule; see
    /// docs/developer/game-code-notes.md), in the order the City view groups them.
    /// </summary>
    public enum CityDriver
    {
        /// <summary>Generic industry: its average productivity and its area.</summary>
        GenericIndustry,
        Farming,
        Forestry,
        Ore,
        Oil,

        /// <summary>Healthcare: the city's health (the sicker, the higher), care and capacity.</summary>
        Health,

        /// <summary>Healthcare with a random streak: the sick, the population and the capacity.</summary>
        HealthRisk,

        /// <summary>Deathcare: its productivity and capacity.</summary>
        DeathCare,

        /// <summary>The number of vehicles in the city.</summary>
        Vehicles,

        /// <summary>The traffic flow (squared).</summary>
        Traffic,

        /// <summary>The crime rate.</summary>
        Crime,

        /// <summary>Plane passengers.</summary>
        Flights,

        /// <summary>Ship passengers.</summary>
        Ships,

        /// <summary>A local company: its building's earnings.</summary>
        Company,

        /// <summary>The city index fund: the average of the other stocks.</summary>
        Index,

        /// <summary>A coin: random swings, no city figure.</summary>
        Coin,

        /// <summary>No known rule (a stock from another mod, the test stock).</summary>
        None,
    }

    /// <summary>
    /// What the game reads for one stock, as the host read it today. Which of
    /// <see cref="A"/>, <see cref="B"/> and <see cref="C"/> mean what depends on the driver
    /// (see <see cref="CityOverview.FigureText"/>).
    /// </summary>
    public sealed class CityFigure
    {
        public CityDriver Driver = CityDriver.None;
        public double A;
        public double B;
        public double C;

        /// <summary>A local company's kind (<see cref="CityDriver.Company"/> only).</summary>
        public BusinessKind Kind;
    }

    /// <summary>One line of the City view.</summary>
    public struct CityRow
    {
        public MonitorQuote Quote;
        public CityDriver Driver;

        /// <summary>"Oil industry".</summary>
        public string Name;

        /// <summary>"72% productive · area 3,400".</summary>
        public string Figure;

        /// <summary>"Rises with productivity and more industry".</summary>
        public string Rule;

        /// <summary>The stock's change over the range in percent (NaN when unknown).</summary>
        public float Change;
    }

    /// <summary>
    /// The City view of the overview (Figma band 20, "Two columns"): each stock beside the city
    /// figure the game prices it from, what moves it, and its change over 7 or 30 days. Every
    /// rule here is the game's own (or the mod's for its companies and fund). Cities-free.
    /// </summary>
    public static class CityOverview
    {
        /// <summary>
        /// The stocks in the Investments list's order (<see cref="InvestmentList.Ordered"/>): its
        /// sort and direction, with the pills' unit for Change, and the locked ones last A–Z (their
        /// figure already applies).
        /// </summary>
        public static List<CityRow> Rows(
            IEnumerable<MonitorQuote> quotes,
            int days,
            MonitorSort sort,
            bool reversed,
            bool changeInCurrency
        )
        {
            var rows = new List<CityRow>();
            var all = new List<MonitorQuote>();
            foreach (MonitorQuote q in quotes ?? new List<MonitorQuote>())
            {
                if (q != null)
                {
                    all.Add(q);
                }
            }

            foreach (
                MonitorQuote q in InvestmentList.Ordered(all, sort, reversed, changeInCurrency)
            )
            {
                rows.Add(Row(q, days));
            }

            return rows;
        }

        private static CityRow Row(MonitorQuote q, int days)
        {
            CityFigure f = q.Figure ?? new CityFigure();
            return new CityRow
            {
                Quote = q,
                Driver = f.Driver,
                Name = DriverName(f),
                Figure = FigureText(f),
                Rule = Rule(f.Driver),
                Change = HeatmapOverview.Change(q, days),
            };
        }

        /// <summary>"Oil industry", "Healthcare", "Local company".</summary>
        public static string DriverName(CityFigure f)
        {
            return Strings.Get("overview.city.driver." + (f ?? new CityFigure()).Driver);
        }

        /// <summary>The figure as the player reads it, e.g. "72% productive · area 3,400".</summary>
        public static string FigureText(CityFigure f)
        {
            f = f ?? new CityFigure();
            switch (f.Driver)
            {
                case CityDriver.GenericIndustry:
                case CityDriver.Farming:
                case CityDriver.Forestry:
                case CityDriver.Ore:
                case CityDriver.Oil:
                    return Strings.Format("overview.city.figure.industry", Whole(f.A), Whole(f.B));
                case CityDriver.Health:
                    return Strings.Format("overview.city.figure.health", Whole(f.A), Whole(f.C));
                case CityDriver.HealthRisk:
                    return Strings.Format(
                        "overview.city.figure.healthRisk",
                        Whole(f.A),
                        Whole(f.B),
                        Whole(f.C)
                    );
                case CityDriver.DeathCare:
                    return Strings.Format(
                        "overview.city.figure.deathCare",
                        Whole(f.A),
                        Whole(f.B + f.C)
                    );
                case CityDriver.Vehicles:
                    return Strings.Format("overview.city.figure.vehicles", Whole(f.A));
                case CityDriver.Traffic:
                    return Strings.Format("overview.city.figure.traffic", Whole(f.A));
                case CityDriver.Crime:
                    return Strings.Format("overview.city.figure.crime", Whole(f.A));
                case CityDriver.Flights:
                    return Strings.Format("overview.city.figure.flights", Whole(f.A));
                case CityDriver.Ships:
                    return Strings.Format("overview.city.figure.ships", Whole(f.A));
                case CityDriver.Company:
                    return f.A > 0
                        ? Strings.Format(
                            "overview.city.figure.company",
                            Strings.Get("company.kind." + f.Kind),
                            PortfolioOverview.Money(f.A / 100.0)
                        )
                        : Strings.Format(
                            "overview.city.figure.companyNew",
                            Strings.Get("company.kind." + f.Kind)
                        );
                case CityDriver.Index:
                    return Strings.Plural("overview.city.figure.index", (long)f.A, Whole(f.A));
                case CityDriver.Coin:
                    return Strings.Get("overview.city.figure.coin");
                default:
                    return Strings.Get("overview.city.figure.none");
            }
        }

        /// <summary>What moves the price, in a few words: "Rises with crime".</summary>
        public static string Rule(CityDriver driver)
        {
            switch (driver)
            {
                case CityDriver.GenericIndustry:
                case CityDriver.Farming:
                case CityDriver.Forestry:
                case CityDriver.Ore:
                case CityDriver.Oil:
                    return Strings.Get("overview.city.rule.industry");
                case CityDriver.Flights:
                case CityDriver.Ships:
                    return Strings.Get("overview.city.rule.passengers");
                default:
                    return Strings.Get("overview.city.rule." + driver);
            }
        }

        private static string Whole(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return "--";
            }

            return Math.Round(value).ToString("N0", CultureInfo.InvariantCulture);
        }
    }
}

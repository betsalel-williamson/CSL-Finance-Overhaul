using System.Collections.Generic; // List
// FinanceOverhaul.Core: BusinessKind, CityDriver, CityFigure, CityOverview, CityRow, MonitorQuote
using FinanceOverhaul.Core;
using Xunit; // Assert, FactAttribute

namespace FinanceOverhaul.Core.Tests
{
    public class CityOverviewTests
    {
        private static MonitorQuote Q(string symbol, CityFigure figure)
        {
            return new MonitorQuote
            {
                Symbol = symbol,
                Figure = figure,
                History = new[] { 100f, 105f },
                Price = 105f,
            };
        }

        [Fact]
        public void RowsGroupByDriverIndustryFirst()
        {
            var quotes = new List<MonitorQuote>
            {
                Q("VLC", new CityFigure { Driver = CityDriver.Crime, A = 12 }),
                Q(
                    "OIL",
                    new CityFigure
                    {
                        Driver = CityDriver.Oil,
                        A = 72.4,
                        B = 3400,
                    }
                ),
                Q(
                    "FRM",
                    new CityFigure
                    {
                        Driver = CityDriver.Farming,
                        A = 50,
                        B = 10,
                    }
                ),
                Q("XXX", null),
                Q("IDX", new CityFigure { Driver = CityDriver.Index, A = 14 }),
            };
            List<CityRow> rows = CityOverview.Rows(quotes, 7);
            Assert.Equal(
                new[] { "FRM", "OIL", "VLC", "IDX", "XXX" },
                rows.ConvertAll(r => r.Quote.Symbol).ToArray()
            );
            Assert.Equal("Oil industry", rows[1].Name);
            Assert.Equal("72% productive · area 3,400", rows[1].Figure);
            Assert.Equal("Rises with productivity and more industry", rows[1].Rule);
            Assert.Equal(5f, rows[1].Change, 3);
            Assert.Equal("Crime rate 12%", rows[2].Figure);
            Assert.Equal("Rises with crime", rows[2].Rule);
            Assert.Equal("Follows 14 stocks equally", rows[3].Figure);
            Assert.Equal("No city figure", rows[4].Figure);
            Assert.Equal("Other", rows[4].Name);
        }

        [Fact]
        public void EveryDriverHasWording()
        {
            foreach (CityDriver d in (CityDriver[])System.Enum.GetValues(typeof(CityDriver)))
            {
                var f = new CityFigure
                {
                    Driver = d,
                    A = 1,
                    B = 2,
                    C = 3,
                };
                string name = CityOverview.DriverName(f);
                string figure = CityOverview.FigureText(f);
                string rule = CityOverview.Rule(d);
                // A missing id reads back as the id itself.
                Assert.DoesNotContain("overview.", name + figure + rule);
                Assert.DoesNotContain("{", figure);
            }
        }

        [Fact]
        public void ACompanyShowsItsEarningsOnceKnown()
        {
            var f = new CityFigure
            {
                Driver = CityDriver.Company,
                Kind = BusinessKind.Oil,
                A = 123456700,
            };
            Assert.Equal("Oil business · earns ₡1,234,567 a year", CityOverview.FigureText(f));
            f.A = 0;
            Assert.Equal("Oil business in your city", CityOverview.FigureText(f));
            Assert.Equal(
                "Follows 1 stock equally",
                CityOverview.FigureText(new CityFigure { Driver = CityDriver.Index, A = 1 })
            );
            Assert.Equal(
                "8 sick of 1,200 citizens · care for 40",
                CityOverview.FigureText(
                    new CityFigure
                    {
                        Driver = CityDriver.HealthRisk,
                        A = 8,
                        B = 1200,
                        C = 40,
                    }
                )
            );
        }
    }
}

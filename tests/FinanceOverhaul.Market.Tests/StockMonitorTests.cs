using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace FinanceOverhaul.Market.Tests
{
    public class StockMonitorTests
    {
        private static MonitorQuote Q(
            string sym,
            float price,
            float rate,
            uint owned = 0,
            int orders = 0
        ) =>
            new MonitorQuote
            {
                Symbol = sym,
                Name = sym + " Inc.",
                Price = price,
                ChangeRate = rate,
                Owned = owned,
                OpenOrders = orders,
            };

        private static readonly List<MonitorQuote> Sample = new List<MonitorQuote>
        {
            Q("Oil", 942f, 1.07f, owned: 200, orders: 1),
            Q("GGI", 924f, -1.38f, owned: 200, orders: 2),
            Q("CC", 4563f, 2.52f),
            Q("HS", 1013f, 0f),
            Q("IDX", 54191f, 0.42f, owned: 1),
        };

        private static string Syms(IEnumerable<MonitorQuote> rows) =>
            string.Join(",", rows.Select(r => r.Symbol));

        [Fact]
        public void SortsByChangeBiggestGainFirst()
        {
            Assert.Equal(
                "CC,Oil,IDX,HS,GGI",
                Syms(StockMonitor.Rows(Sample, MonitorFilter.All, MonitorSort.Change))
            );
        }

        [Fact]
        public void SortsBySymbolPriceAndValueHeld()
        {
            Assert.Equal(
                "CC,GGI,HS,IDX,Oil",
                Syms(StockMonitor.Rows(Sample, MonitorFilter.All, MonitorSort.Symbol))
            );
            Assert.Equal(
                "IDX,CC,HS,Oil,GGI",
                Syms(StockMonitor.Rows(Sample, MonitorFilter.All, MonitorSort.Price))
            );
            // Oil 188,400 > GGI 184,800 > IDX 54,191 > not held (by symbol)
            Assert.Equal(
                "Oil,GGI,IDX,CC,HS",
                Syms(StockMonitor.Rows(Sample, MonitorFilter.All, MonitorSort.ValueHeld))
            );
        }

        [Fact]
        public void FiltersPortfolioAndOrders()
        {
            Assert.Equal(
                "GGI,IDX,Oil",
                Syms(StockMonitor.Rows(Sample, MonitorFilter.Portfolio, MonitorSort.Symbol))
            );
            Assert.Equal(
                "GGI,Oil",
                Syms(StockMonitor.Rows(Sample, MonitorFilter.Orders, MonitorSort.Symbol))
            );
        }

        [Fact]
        public void BadNumbersSortLastAndNullsAreSkipped()
        {
            var rows = StockMonitor.Rows(
                new[] { Q("A", float.NaN, float.NaN), null, Q("B", 1f, -5f) },
                MonitorFilter.All,
                MonitorSort.Change
            );
            Assert.Equal("B,A", Syms(rows));
            Assert.Empty(StockMonitor.Rows(null, MonitorFilter.All, MonitorSort.Change));
        }

        [Fact]
        public void SortCycles()
        {
            MonitorSort s = MonitorSort.Change;
            var seen = new List<MonitorSort>();
            for (int i = 0; i < 4; i++)
            {
                seen.Add(s);
                s = StockMonitor.NextSort(s);
            }

            Assert.Equal(MonitorSort.Change, s);
            Assert.Equal(4, seen.Distinct().Count());
            Assert.Equal("Sort: Value held", StockMonitor.SortLabel(MonitorSort.ValueHeld));
        }

        [Fact]
        public void WordingMatchesTheGameAndTheLed()
        {
            Assert.Equal("₡54,191", StockMonitor.PriceText(54190.6f));
            Assert.Equal("₡942", StockMonitor.PriceText(942f));
            Assert.Equal("--", StockMonitor.PriceText(float.NaN));
            Assert.Equal("+1.07%", StockMonitor.ChangeText(1.07f));
            Assert.Equal("0.00%", StockMonitor.ChangeText(0.004f));
            Assert.Equal("--", StockMonitor.ChangeText(float.NaN));
            Assert.Equal(LedText.Flat, StockMonitor.ChangeColour(0.004f));
            Assert.Equal(LedText.Gain, StockMonitor.ChangeColour(1.07f));
            Assert.Equal(LedText.Loss, StockMonitor.ChangeColour(-1.38f));
            Assert.Null(StockMonitor.OrdersBadge(0));
            Assert.Equal("1 order", StockMonitor.OrdersBadge(1));
            Assert.Equal("2 orders", StockMonitor.OrdersBadge(2));
            Assert.Equal(
                "Daily close · 29 Oct 2077",
                StockMonitor.Footer(new DateTime(2077, 10, 29))
            );
            Assert.Equal("No open orders.", StockMonitor.EmptyText(MonitorFilter.Orders));
        }
    }

    public class SparklineTests
    {
        private static int RowOf(byte[] m, int w, int h, int x, byte kind)
        {
            for (int y = 0; y < h; y++)
            {
                if (m[y * w + x] == kind)
                {
                    return y;
                }
            }

            return -1;
        }

        [Fact]
        public void RisingSeriesGoesFromBottomLeftToTopRight()
        {
            byte[] m = Sparkline.Render(new[] { 1f, 2f, 3f, 4f }, 10, 8);
            Assert.Equal(7, RowOf(m, 10, 8, 0, Sparkline.Line));
            Assert.Equal(0, RowOf(m, 10, 8, 9, Sparkline.Line));
        }

        [Fact]
        public void EveryColumnHasTheLine()
        {
            byte[] m = Sparkline.Render(new[] { 5f, 1f, 9f, 3f, 7f }, 30, 12);
            for (int x = 0; x < 30; x++)
            {
                Assert.True(RowOf(m, 30, 12, x, Sparkline.Line) >= 0, "gap at column " + x);
            }
        }

        [Fact]
        public void FlatSeriesSitsInTheMiddle()
        {
            byte[] m = Sparkline.Render(new[] { 3f, 3f, 3f }, 9, 9);
            Assert.Equal(4, RowOf(m, 9, 9, 0, Sparkline.Line));
            Assert.Equal(4, RowOf(m, 9, 9, 8, Sparkline.Line));
        }

        [Fact]
        public void YesterdayIsADashedLine()
        {
            // yesterday (3rd of 4) is the low → bottom row, dashed: 3 on, 3 off
            byte[] m = Sparkline.Render(new[] { 10f, 20f, 0f, 20f }, 24, 10);
            int dashes = 0;
            for (int x = 0; x < 24; x++)
            {
                if (m[9 * 24 + x] == Sparkline.Baseline)
                {
                    dashes++;
                }
            }

            Assert.InRange(dashes, 6, 12); // the line covers a few dash pixels near day 3
            Assert.Equal(Sparkline.Empty, m[9 * 24 + 4]);
        }

        [Fact]
        public void ThickLinesStayInsideTheMask()
        {
            byte[] m = Sparkline.Render(new[] { 0f, 100f }, 20, 10, thickness: 3);
            Assert.Equal(200, m.Length);
            Assert.Contains(Sparkline.Line, m);
        }

        [Fact]
        public void BadInputGivesAnEmptyOrTinyMask()
        {
            Assert.Empty(Sparkline.Render(new[] { 1f }, 0, 5));
            Assert.All(Sparkline.Render(null, 4, 4), b => Assert.Equal(Sparkline.Empty, b));
            Assert.All(
                Sparkline.Render(new[] { float.NaN, float.PositiveInfinity }, 4, 4),
                b => Assert.Equal(Sparkline.Empty, b)
            );
            byte[] one = Sparkline.Render(new[] { 5f }, 4, 4);
            Assert.Equal(1, one.Count(b => b == Sparkline.Line));
        }
    }
}

namespace FinanceOverhaul.Market.Tests
{
    public class StockMonitorSettingsTests
    {
        [Fact]
        public void DefaultsAreOnClosedAndDefaultPlaced()
        {
            StockMonitorSettings m = SettingsFile.Parse("").Monitor;
            Assert.True(m.Enabled);
            Assert.False(m.Open);
            Assert.True(float.IsNaN(m.WindowX));
            Assert.True(float.IsNaN(m.ButtonY));
            Assert.Equal(0.35f, m.Opacity);
            Assert.Equal(MonitorFilter.All, m.Filter);
            Assert.Equal(MonitorSort.Change, m.Sort);
        }

        [Fact]
        public void RoundTripsEverything()
        {
            var s = new ModSettings();
            s.Monitor.Enabled = false;
            s.Monitor.Open = true;
            s.Monitor.WindowX = 412.5f;
            s.Monitor.WindowY = 96f;
            s.Monitor.ButtonX = 230f;
            s.Monitor.ButtonY = 14f;
            s.Monitor.Opacity = 0.6f;
            s.Monitor.Filter = MonitorFilter.Orders;
            s.Monitor.Sort = MonitorSort.ValueHeld;
            s.LiveTickerEnabled = false;
            ModSettings back = SettingsFile.Parse(SettingsFile.Format(s));
            Assert.False(back.Monitor.Enabled);
            Assert.True(back.Monitor.Open);
            Assert.Equal(412.5f, back.Monitor.WindowX);
            Assert.Equal(96f, back.Monitor.WindowY);
            Assert.Equal(230f, back.Monitor.ButtonX);
            Assert.Equal(14f, back.Monitor.ButtonY);
            Assert.Equal(0.6f, back.Monitor.Opacity, 3);
            Assert.Equal(MonitorFilter.Orders, back.Monitor.Filter);
            Assert.Equal(MonitorSort.ValueHeld, back.Monitor.Sort);
            Assert.False(back.LiveTickerEnabled);
        }

        [Fact]
        public void DefaultPositionsStayDefault()
        {
            string text = SettingsFile.Format(new ModSettings());
            Assert.Contains("monitor.windowX=default\n", text);
            Assert.True(float.IsNaN(SettingsFile.Parse(text).Monitor.WindowX));
        }

        [Fact]
        public void BadValuesFallBackSafely()
        {
            StockMonitorSettings m = SettingsFile
                .Parse(
                    "monitor.opacity=7\nmonitor.windowX=wide\nmonitor.filter=Bogus\nmonitor.sort=symbol\nmonitor.enabled=maybe\n"
                )
                .Monitor;
            Assert.Equal(0.9f, m.Opacity);
            Assert.True(float.IsNaN(m.WindowX));
            Assert.Equal(MonitorFilter.All, m.Filter);
            Assert.Equal(MonitorSort.Symbol, m.Sort);
            Assert.True(m.Enabled);
            Assert.Equal(0f, SettingsFile.Parse("monitor.opacity=-1").Monitor.Opacity);
            Assert.Equal(0.35f, SettingsFile.Parse("monitor.opacity=NaN").Monitor.Opacity);
        }
    }
}

namespace FinanceOverhaul.Market.Tests
{
    public class StockMonitorHoverTests
    {
        [Fact]
        public void SolidWhileHoveredLikeCityVitals()
        {
            var m = new StockMonitorSettings();
            Assert.True(m.TransparentUnhovered);
            Assert.Equal(0.35f, m.BackgroundOpacity(false));
            Assert.Equal(0.9f, m.BackgroundOpacity(true));
            m.Opacity = 0f;
            Assert.Equal(0f, m.BackgroundOpacity(false));
            m.TransparentUnhovered = false;
            Assert.Equal(0f, m.BackgroundOpacity(true));
            Assert.False(
                SettingsFile
                    .Parse("monitor.transparentUnhovered=false")
                    .Monitor.TransparentUnhovered
            );
            Assert.Contains(
                "monitor.transparentUnhovered=true",
                SettingsFile.Format(new ModSettings())
            );
        }
    }
}

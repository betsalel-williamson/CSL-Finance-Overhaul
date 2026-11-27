using System.Collections.Generic; // List
using FinanceOverhaul.Core; // HeatGrid, HeatTile, HeatmapOverview, MonitorQuote
using Xunit; // Assert, FactAttribute

namespace FinanceOverhaul.Core.Tests
{
    public class HeatmapOverviewTests
    {
        private static MonitorQuote Q(string symbol, float rate, float[] history, uint owned = 0)
        {
            return new MonitorQuote
            {
                Symbol = symbol,
                Name = symbol + " Inc",
                ChangeRate = rate,
                History = history,
                Price = history[history.Length - 1],
                Owned = owned,
            };
        }

        private static List<MonitorQuote> Market()
        {
            return new List<MonitorQuote>
            {
                Q("AAA", 3.2f, new[] { 100f, 110f }, 50),
                Q("BBB", -1.5f, new[] { 100f, 80f }),
                Q("CCC", 0f, new[] { 100f, 100f }),
                new MonitorQuote
                {
                    Symbol = "LCK",
                    Locked = true,
                    History = new[] { 1f, 2f },
                },
                Q("NEW", float.NaN, new[] { 50f }),
            };
        }

        [Fact]
        public void TilesGoFromTheBiggestRiseToTheBiggestFallThenLocked()
        {
            List<HeatTile> tiles = HeatmapOverview.Tiles(Market(), HeatmapOverview.Today);
            Assert.Equal(
                new[] { "AAA", "CCC", "BBB", "NEW", "LCK" },
                tiles.ConvertAll(t => t.Quote.Symbol).ToArray()
            );
            // Four in five moves are within 3.2%: the scale is the next round step, 5%.
            Assert.Equal(3.2f / 5f, tiles[0].Heat, 4);
            Assert.Equal(-1.5f / 5f, tiles[2].Heat, 4);
            Assert.Equal(0f, tiles[3].Heat);
            Assert.True(tiles[0].Held);
            Assert.True(tiles[4].Locked);
            Assert.Equal("Locked", HeatmapOverview.TileText(tiles[4], false));
            Assert.Equal("--", HeatmapOverview.TileText(tiles[3], false));
            Assert.Equal("+3.20%", HeatmapOverview.TileText(tiles[0], false));
        }

        [Fact]
        public void AWeeksChangeComesFromThePrices()
        {
            List<HeatTile> tiles = HeatmapOverview.Tiles(Market(), 7);
            // AAA +10%, BBB −20%: the scale is 20%, BBB reads −1 and sorts last of the known.
            Assert.Equal("AAA", tiles[0].Quote.Symbol);
            Assert.Equal(10f, tiles[0].Change, 3);
            Assert.Equal(0.5f, tiles[0].Heat, 3);
            Assert.Equal("BBB", tiles[2].Quote.Symbol);
            Assert.Equal(-1f, tiles[2].Heat, 3);
            Assert.Equal("−₡20", HeatmapOverview.TileText(tiles[2], true));
            Assert.Equal("+₡10", HeatmapOverview.TileText(tiles[0], true));
            Assert.True(float.IsNaN(HeatmapOverview.Change(Market()[4], 7)));
        }

        [Fact]
        public void TheScaleIsARoundStepThatHoldsTheBiggestMove()
        {
            var tiles = new List<HeatTile> { new HeatTile { Change = 0.4f } };
            Assert.Equal(1f, HeatmapOverview.Scale(tiles));
            tiles.Add(new HeatTile { Change = -12f });
            Assert.Equal(20f, HeatmapOverview.Scale(tiles));
            tiles.Add(new HeatTile { Change = 400f });
            Assert.Equal(100f, HeatmapOverview.Scale(tiles));
            // One outlier among ten does not stretch the scale: nine moves of 3%, one of 31%.
            var day = new List<HeatTile> { new HeatTile { Change = 31f } };
            for (int i = 0; i < 9; i++)
            {
                day.Add(new HeatTile { Change = i % 2 == 0 ? 3f : -3f });
            }

            Assert.Equal(5f, HeatmapOverview.Scale(day));
            Assert.Equal(1f, HeatmapOverview.Scale(new List<HeatTile>()));
            Assert.Equal("−5%", HeatmapOverview.ScaleEnd(5f, false));
            Assert.Equal("+0.5%", HeatmapOverview.ScaleEnd(0.5f, true));
        }

        [Fact]
        public void TheGridFitsEveryTileInTheArea()
        {
            foreach (int count in new[] { 1, 5, 16, 28, 40 })
            {
                HeatGrid g = HeatmapOverview.Grid(count, 400f, 300f, 4f);
                Assert.True(g.Columns * g.Rows >= count);
                Assert.True(g.Columns * g.TileWidth + (g.Columns - 1) * 4f <= 400.01f);
                Assert.True(g.Rows * g.TileHeight + (g.Rows - 1) * 4f <= 300.01f);
                Assert.True(g.TileHeight <= HeatmapOverview.MaxTileHeight);
                // Never a column or row more than it needs.
                Assert.True((g.Rows - 1) * g.Columns < count);
            }

            HeatGrid sixteen = HeatmapOverview.Grid(16, 400f, 300f, 4f);
            Assert.True(sixteen.TileWidth >= sixteen.TileHeight); // wider than tall
        }

        [Fact]
        public void SpansAndWording()
        {
            Assert.Equal(HeatmapOverview.Today, HeatmapOverview.Span(0));
            Assert.Equal(7, HeatmapOverview.Span(7));
            Assert.Equal(HeatmapOverview.Today, HeatmapOverview.Span(14));
            Assert.Equal("Today's change", HeatmapOverview.Heading(1));
            Assert.Equal("Change over 30 days", HeatmapOverview.Heading(30));
            Assert.Equal("Today", HeatmapOverview.SpanTab(1));
            Assert.Equal("−₡0.40", HeatmapOverview.MoneyChange(-0.4));
            Assert.Equal("₡0", HeatmapOverview.MoneyChange(0.001));
            List<HeatTile> tiles = HeatmapOverview.Tiles(Market(), 1);
            Assert.Equal("AAA Inc (AAA) · ₡110 · 50 shares held", HeatmapOverview.Tip(tiles[0]));
            Assert.Equal("CCC Inc (CCC) · ₡100", HeatmapOverview.Tip(tiles[1]));
        }
    }
}

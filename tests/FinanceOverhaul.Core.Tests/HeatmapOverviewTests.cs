using System.Collections.Generic; // List
using FinanceOverhaul.Core; // HeatRect, HeatTile, HeatmapOverview, MonitorQuote
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
                Available = 1000u - owned, // as the game: 1,000 shares, the rest on sale
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
        public void TilesGoFromTheLargestToTheSmallestThenLocked()
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
            // AAA +10%, BBB −20%: the scale is 20%, BBB reads −1 (it is third by size).
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
        public void TheTreemapGivesEachTileItsShareOfTheArea()
        {
            var weights = new List<double> { 60, 30, 20, 10, 5, 5, 3, 1 };
            const float W = 400f,
                H = 300f;
            HeatRect[] rects = HeatmapOverview.Treemap(weights, W, H, 0f);
            double total = 134;
            double sum = 0;
            for (int i = 0; i < rects.Length; i++)
            {
                HeatRect r = rects[i];
                Assert.True(r.X >= -0.01f && r.Y >= -0.01f);
                Assert.True(r.X + r.Width <= W + 0.01f && r.Y + r.Height <= H + 0.01f);
                // Each tile's area is its weight's share of the whole.
                Assert.Equal(weights[i] / total * W * H, (double)r.Width * r.Height, 0);
                sum += r.Width * r.Height;
                for (int k = 0; k < i; k++)
                {
                    HeatRect o = rects[k];
                    bool apart =
                        r.X >= o.X + o.Width - 0.01f
                        || o.X >= r.X + r.Width - 0.01f
                        || r.Y >= o.Y + o.Height - 0.01f
                        || o.Y >= r.Y + r.Height - 0.01f;
                    Assert.True(apart, "tiles " + k + " and " + i + " overlap");
                }
            }

            Assert.Equal(W * H, sum, 0);
            // Squarified: the big tiles are not slivers.
            Assert.True(
                rects[0].Width / rects[0].Height < 3f && rects[0].Height / rects[0].Width < 3f
            );
        }

        [Fact]
        public void TheTreemapLeavesGapsOnlyBetweenTiles()
        {
            HeatRect[] rects = HeatmapOverview.Treemap(new List<double> { 1, 1 }, 200f, 100f, 4f);
            // Two equal halves side by side, 4 apart, flush with the edges.
            Assert.Equal(0f, rects[0].X, 3);
            Assert.Equal(98f, rects[0].Width, 3);
            Assert.Equal(102f, rects[1].X, 3);
            Assert.Equal(200f, rects[1].X + rects[1].Width, 3);
            Assert.Equal(100f, rects[0].Height, 3);
            Assert.Empty(HeatmapOverview.Treemap(new List<double>(), 10f, 10f, 0f));
            Assert.Equal(
                0f,
                HeatmapOverview.Treemap(new List<double> { 0 }, 10f, 10f, 0f)[0].Width
            );
        }

        [Fact]
        public void ATilesSizeFollowsTheStocksValue()
        {
            var cheap = new MonitorQuote { Price = 25f, Available = 1000 };
            var dear = new MonitorQuote
            {
                Price = 2500f,
                Available = 900,
                Owned = 100,
            };
            // 100 times the value: 10 times the area (square-root damped).
            Assert.Equal(10d, HeatmapOverview.Weight(dear) / HeatmapOverview.Weight(cheap), 6);
            var locked = new MonitorQuote { Price = float.NaN, Locked = true };
            Assert.Equal(System.Math.Sqrt(1000d), HeatmapOverview.Weight(locked), 6);
            List<HeatTile> tiles = HeatmapOverview.Tiles(Market(), 1);
            // The locked tile takes the smallest size on sale.
            Assert.Equal(tiles[3].Weight, tiles[4].Weight, 6);
            Assert.Equal("₡110 · 50 shares", HeatmapOverview.Detail(tiles[0]));
            Assert.Equal("₡100", HeatmapOverview.Detail(tiles[1]));
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

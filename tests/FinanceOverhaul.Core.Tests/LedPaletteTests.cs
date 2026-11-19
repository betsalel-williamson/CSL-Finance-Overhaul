using Xunit; // Assert, FactAttribute

namespace FinanceOverhaul.Core.Tests
{
    /// <summary>
    /// LED brightness levels (Options → LED ticker brightness), for every dot colour:
    /// Classic is the Figma tokens; each brighter level never lowers perceived brightness of a
    /// lit dot, keeps its dominant channel (hue family), and darkens unlit dots, so lit-to-unlit
    /// contrast only grows; gain/loss/symbol stay distinct.
    /// </summary>
    public class LedPaletteTests
    {
        private static readonly LedBrightness[] Levels =
        {
            LedBrightness.Classic,
            LedBrightness.Bright,
            LedBrightness.Brightest,
        };

        [Fact]
        public void ClassicIsTheFigmaTokens()
        {
            LedRgb gain = LedPalette.Colour(LedText.Gain, LedBrightness.Classic);
            Assert.Equal(new LedRgb(0x4E, 0xF4, 0x65), gain);
            Assert.Equal(
                new LedRgb(0x29, 0x3A, 0x46),
                LedPalette.Colour(LedText.Off, LedBrightness.Classic)
            );
        }

        [Fact]
        public void BrightScalesToFullStrength()
        {
            for (byte cell = 1; cell < LedPalette.Count; cell++)
            {
                Assert.Equal(255, LedPalette.Colour(cell, LedBrightness.Bright).Max);
            }
        }

        [Fact]
        public void BrighterNeverDimmerAndKeepsHue()
        {
            for (byte cell = 1; cell < LedPalette.Count; cell++)
            {
                for (int i = 1; i < Levels.Length; i++)
                {
                    LedRgb before = LedPalette.Colour(cell, Levels[i - 1]);
                    LedRgb after = LedPalette.Colour(cell, Levels[i]);
                    Assert.True(after.Luminance >= before.Luminance, "dimmer: cell " + cell);
                    Assert.Equal(
                        Dominant(LedPalette.Colour(cell, LedBrightness.Classic)),
                        Dominant(after)
                    );
                }
            }
        }

        [Fact]
        public void ContrastOnlyGrows()
        {
            for (byte cell = 1; cell < LedPalette.Count; cell++)
            {
                double last = -1;
                foreach (LedBrightness level in Levels)
                {
                    double c =
                        LedPalette.Colour(cell, level).Luminance
                        - LedPalette.Colour(LedText.Off, level).Luminance;
                    Assert.True(c > last);
                    last = c;
                }
            }
        }

        [Fact]
        public void GainLossAndSymbolStayDistinct()
        {
            foreach (LedBrightness level in Levels)
            {
                LedRgb g = LedPalette.Colour(LedText.Gain, level),
                    l = LedPalette.Colour(LedText.Loss, level),
                    s = LedPalette.Colour(LedText.Symbol, level);
                Assert.True(g.G > g.R && l.R > l.G, level.ToString());
                Assert.NotEqual(s, g);
                Assert.NotEqual(s, l);
            }
        }

        [Fact]
        public void GapsGlowButDotsStayDistinct()
        {
            for (byte cell = 1; cell < LedPalette.Count; cell++)
            {
                double last = -1;
                foreach (LedBrightness level in Levels)
                {
                    LedRgb gap = LedPalette.Gap(cell, level);
                    LedRgb dot = LedPalette.Colour(cell, level);
                    Assert.True(gap.Luminance < dot.Luminance, "gap as bright as the dot");
                    Assert.True(gap.Luminance >= last); // more glow at each level
                    last = gap.Luminance;
                }
            }

            Assert.Equal(LedPalette.Board, LedPalette.Gap(LedText.Gain, LedBrightness.Classic));
            Assert.Equal(LedPalette.Board, LedPalette.Gap(LedText.Off, LedBrightness.Brightest));
        }

        [Fact]
        public void UnknownCellsDrawAsOff()
        {
            Assert.Equal(
                LedPalette.Colour(LedText.Off, LedBrightness.Bright),
                LedPalette.Colour(200, LedBrightness.Bright)
            );
        }

        [Fact]
        public void SettingDefaultsToBrightAndRoundTrips()
        {
            Assert.Equal(LedBrightness.Bright, SettingsFile.Parse("").TickerBrightness);
            foreach (LedBrightness level in Levels)
            {
                var s = new ModSettings { TickerBrightness = level };
                Assert.Equal(level, SettingsFile.Parse(SettingsFile.Format(s)).TickerBrightness);
            }

            Assert.Equal(
                LedBrightness.Bright,
                SettingsFile.Parse("tickerBrightness=neon\n").TickerBrightness
            );
        }

        [Fact]
        public void InfoViewsShowTheTickerUnlessThePlayerHidesIt()
        {
            var s = new ModSettings();
            Assert.True(s.TickerInInfoViews); // default: as today, always shown
            Assert.True(s.TickerShownNow(false));
            Assert.True(s.TickerShownNow(true));
            s.TickerInInfoViews = false;
            Assert.True(s.TickerShownNow(false));
            Assert.False(s.TickerShownNow(true)); // like the original in info views
            s.LiveTickerEnabled = false;
            Assert.False(s.TickerShownNow(false));
            Assert.False(SettingsFile.Parse(SettingsFile.Format(s)).TickerInInfoViews);
            Assert.True(SettingsFile.Parse("").TickerInInfoViews);
        }

        private static char Dominant(LedRgb c)
        {
            return c.R >= c.G && c.R >= c.B ? 'R'
                : c.G >= c.B ? 'G'
                : 'B';
        }
    }
}

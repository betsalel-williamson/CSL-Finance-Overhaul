using System;
using Xunit;

namespace FinanceOverhaul.Core.Tests
{
    /// <summary>
    /// Options → "Stock Exchange ticker shows": the LED entry text in both modes. Properties over
    /// 5,000 seeded quotes: every character has an LED glyph (nothing prints as "?"); the colour
    /// is the same in both modes (one rule everywhere) and agrees with the sign printed in both;
    /// the entry text is exactly the Stock Monitor's pill text; entries for moves within ±50% fit
    /// the width the marquee reserves (wilder ones are clipped).
    /// </summary>
    public class TickerDisplayTests
    {
        [Theory]
        [InlineData(951f, 3.22f, " +3.22%", " ₡951 +₡30")]
        [InlineData(1809f, -4.23f, " -4.23%", " ₡1,809 −₡80")]
        [InlineData(863f, 0.64f, " +0.64%", " ₡863 +₡5.49")]
        [InlineData(73f, 0f, " 0.00%", " ₡73 ₡0")]
        [InlineData(90f, float.NaN, " --", " ₡90 --")]
        [InlineData(float.NaN, 1f, " +1.00%", " --")]
        public void EntryText(float price, float rate, string percent, string value)
        {
            Assert.Equal(percent, LedText.ChangePart(price, rate, TickerDisplay.ChangePercent));
            Assert.Equal(value, LedText.ChangePart(price, rate, TickerDisplay.PriceAndChange));
        }

        [Fact]
        public void NewGlyphsExist()
        {
            foreach (char c in "%,₡−")
            {
                Assert.True(LedFont.Has(c), "no glyph for " + c);
            }
        }

        [Fact]
        public void EveryEntryPrintsAndAgrees()
        {
            var rng = new Random(2207);
            for (int k = 0; k < 5000; k++)
            {
                float price = (float)Math.Exp(rng.NextDouble() * Math.Log(9_000_000)) + 0.5f;
                float rate = (float)((rng.NextDouble() - 0.5) * 2 * (k % 10 == 0 ? 0.02 : 99));
                foreach (
                    TickerDisplay d in new[]
                    {
                        TickerDisplay.ChangePercent,
                        TickerDisplay.PriceAndChange,
                    }
                )
                {
                    string text = "WWWW" + LedText.ChangePart(price, rate, d);
                    foreach (char c in text)
                    {
                        Assert.True(LedFont.Has(c), "no glyph for '" + c + "' in " + text);
                    }

                    // Moves within ±50% fit the slot; wilder ones are clipped by the marquee.
                    if (Math.Abs(rate) <= 50f)
                    {
                        Assert.True(
                            LedText.MeasureEntry("WWWW", price, rate, d) <= LedMarquee.MaxEntryDots,
                            text
                        );
                    }
                }

                // Same text as the Stock Monitor pills.
                Assert.Equal(
                    " " + StockMonitor.ChangeText(rate),
                    LedText.ChangePart(price, rate, TickerDisplay.ChangePercent)
                );
                Assert.EndsWith(
                    StockMonitor.ChangeAmountFromRate(price, rate),
                    LedText.ChangePart(price, rate, TickerDisplay.PriceAndChange)
                );

                // One colour rule, and the printed sign agrees with it in both modes.
                byte tone = LedText.RateColour(rate);
                string pct = LedText.ChangePart(price, rate, TickerDisplay.ChangePercent);
                string val = LedText.ChangePart(price, rate, TickerDisplay.PriceAndChange);
                if (tone == LedText.Gain)
                {
                    Assert.StartsWith(" +", pct);
                    Assert.Contains("+₡", val);
                }
                else if (tone == LedText.Loss)
                {
                    Assert.StartsWith(" -", pct);
                    Assert.Contains("−₡", val);
                }
                else if (tone == LedText.Flat)
                {
                    Assert.Equal(" 0.00%", pct);
                    Assert.EndsWith(" ₡0", val);
                }
            }
        }

        [Fact]
        public void RenderedWidthMatchesMeasureInBothModes()
        {
            LedStrip s = LedText.RenderRow(
                new[] { "DS" },
                new[] { 951f },
                new[] { 3.22f },
                0,
                1,
                TickerDisplay.PriceAndChange
            );
            Assert.Equal(
                LedText.MeasureEntry("DS", 951f, 3.22f, TickerDisplay.PriceAndChange),
                s.Width
            );
            Assert.Contains(LedText.Gain, s.Cells);
        }

        [Fact]
        public void MarqueeWritesTheChosenMode()
        {
            var snap = new TickerSnapshot(new[] { "DS" }, new[] { 3.22f }, new[] { 951f });
            var pct = new LedMarquee(400, null);
            pct.SetContent(snap, 0, 1);
            var val = new LedMarquee(400, null) { Display = TickerDisplay.PriceAndChange };
            val.SetContent(snap, 0, 1);
            long a,
                b;
            pct.Restart();
            val.Restart();
            pct.Advance(0, out a, out b);
            Assert.NotEqual(Lit(pct.Strip), Lit(val.Strip)); // different text on the board
        }

        [Fact]
        public void ARestartedScreenIsAllInOneStyle()
        {
            // Two entries, price style: after Restart the whole window is those entries tiled in
            // that style (no leftover % entries), column for column.
            var snap = new TickerSnapshot(
                new[] { "DS", "HS" },
                new[] { 3.22f, -2f },
                new[] { 951f, 318f }
            );
            foreach (
                TickerDisplay d in new[]
                {
                    TickerDisplay.ChangePercent,
                    TickerDisplay.PriceAndChange,
                }
            )
            {
                var m = new LedMarquee(300, null);
                m.SetContent(snap, 0, 2);
                m.Display = TickerDisplay.ChangePercent;
                m.Restart(); // a screen in the other style first
                m.Display = d;
                m.Restart();
                LedStrip expected = LedText.RenderRow(
                    snap.Symbols,
                    snap.Prices,
                    snap.Rates,
                    0,
                    2,
                    d
                );
                int start = (int)(m.Offset % m.RingDots);
                for (int x = 0; x < m.WindowDots; x++)
                {
                    for (int y = 0; y < LedFont.Height; y++)
                    {
                        Assert.Equal(
                            expected.Get(x % expected.Width, y),
                            m.Strip.Get((start + x) % m.RingDots, y)
                        );
                    }
                }
            }
        }

        [Fact]
        public void SettingRoundTripsAndDefaultsToPercent()
        {
            Assert.Equal(TickerDisplay.ChangePercent, SettingsFile.Parse("").TickerShows);
            var s = new ModSettings { TickerShows = TickerDisplay.PriceAndChange };
            Assert.Equal(
                TickerDisplay.PriceAndChange,
                SettingsFile.Parse(SettingsFile.Format(s)).TickerShows
            );
            Assert.Equal(
                TickerDisplay.ChangePercent,
                SettingsFile.Parse("tickerShows=somethingNew\n").TickerShows
            );
        }

        private static int Lit(LedStrip s)
        {
            int n = 0;
            foreach (byte b in s.Cells)
            {
                if (b != LedText.Off)
                {
                    n++;
                }
            }

            return n;
        }
    }
}

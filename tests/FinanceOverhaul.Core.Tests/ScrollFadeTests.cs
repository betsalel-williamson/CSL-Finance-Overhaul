using System; // Math, Random
using Xunit; // Assert, FactAttribute

namespace FinanceOverhaul.Core.Tests
{
    /// <summary>
    /// Edge fades, over 5,000 seeded lists and scroll positions: alphas stay in 0..1; nothing
    /// shows when everything fits; within a unit of the top the top fade is gone and within a
    /// unit of the bottom the bottom fade is gone; a fade is fully on once there is at least a fade-length more to
    /// scroll past that edge; and scrolling down never strengthens the bottom fade nor weakens
    /// the top one (monotone, so fades never flicker).
    /// </summary>
    public class ScrollFadeTests
    {
        private const float L = ScrollFade.DefaultLength;

        [Fact]
        public void RowsFadeOnlyAtAnEdgeWithMorePastIt()
        {
            // A row well inside the view is whole.
            Assert.Equal(1f, ScrollFade.RowOpacity(100f, 30f, 300f, 1f, 1f, L));
            // Its middle at the top edge with more above: gone; with nothing above: whole.
            Assert.Equal(0f, ScrollFade.RowOpacity(-15f, 30f, 300f, 1f, 1f, L), 3);
            Assert.Equal(1f, ScrollFade.RowOpacity(-15f, 30f, 300f, 0f, 1f, L), 3);
            // Half way into the fade at the bottom: half.
            Assert.Equal(0.5f, ScrollFade.RowOpacity(300f - 15f - L / 2f, 30f, 300f, 1f, 1f, L), 3);
            // An edge only half faded in (just started scrolling) fades its row half as much.
            Assert.Equal(0.5f, ScrollFade.RowOpacity(-15f, 30f, 300f, 0.5f, 1f, L), 3);
            Assert.Equal(1f, ScrollFade.RowOpacity(0f, 30f, 0f, 1f, 1f, L));
        }

        [Fact]
        public void Examples()
        {
            float top,
                bottom;
            ScrollFade.Alphas(0, 160, 500, L, out top, out bottom); // at the top
            Assert.Equal(0f, top);
            Assert.Equal(1f, bottom);
            ScrollFade.Alphas(170, 160, 500, L, out top, out bottom); // middle
            Assert.Equal(1f, top);
            Assert.Equal(1f, bottom);
            ScrollFade.Alphas(340, 160, 500, L, out top, out bottom); // at the bottom
            Assert.Equal(1f, top);
            Assert.Equal(0f, bottom);
            ScrollFade.Alphas(12, 160, 500, L, out top, out bottom); // easing in
            Assert.Equal(0.5f, top, 3);
            ScrollFade.Alphas(339.2f, 160, 500, L, out top, out bottom); // a hair short of the end
            Assert.Equal(0f, bottom);
            ScrollFade.Alphas(0, 160, 150, L, out top, out bottom); // fits
            Assert.Equal(0f, top);
            Assert.Equal(0f, bottom);
        }

        [Fact]
        public void Properties()
        {
            var rng = new Random(1212);
            for (int k = 0; k < 5000; k++)
            {
                float view = 20f + (float)rng.NextDouble() * 400f;
                float content = (float)rng.NextDouble() * 1500f;
                float max = Math.Max(0f, content - view);
                float s1 = (float)(rng.NextDouble() * (max + 40) - 20);
                float s2 = s1 + (float)rng.NextDouble() * 60f;
                float t1,
                    b1,
                    t2,
                    b2;
                ScrollFade.Alphas(s1, view, content, L, out t1, out b1);
                ScrollFade.Alphas(s2, view, content, L, out t2, out b2);
                foreach (float a in new[] { t1, b1, t2, b2 })
                {
                    Assert.InRange(a, 0f, 1f);
                }

                if (max <= 0.5f)
                {
                    Assert.Equal(0f, t1 + b1);
                    continue;
                }

                Assert.True(t2 >= t1 && b2 <= b1, "fades must be monotone in scroll");
                const float D = ScrollFade.DeadZone;
                if (s1 <= D)
                {
                    Assert.Equal(0f, t1);
                }

                if (s1 >= max - D)
                {
                    Assert.Equal(0f, b1);
                }

                if (s1 >= L + D && s1 <= max)
                {
                    Assert.Equal(1f, t1);
                }

                if (max - s1 >= L + D && s1 >= 0)
                {
                    Assert.Equal(1f, b1);
                }
            }
        }

        [Fact]
        public void BadInputShowsNothing()
        {
            float top,
                bottom;
            ScrollFade.Alphas(float.NaN, 100, 500, L, out top, out bottom);
            Assert.Equal(0f, top + bottom);
            ScrollFade.Alphas(0, 0, 500, L, out top, out bottom);
            Assert.Equal(0f, top + bottom);
        }
    }
}

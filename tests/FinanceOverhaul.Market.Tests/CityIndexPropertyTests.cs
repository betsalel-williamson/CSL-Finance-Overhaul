using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace FinanceOverhaul.Market.Tests
{
    /// <summary>
    /// Formal properties of the Index (index-number axioms), checked over seeded random
    /// baskets: 200 trials, 2–13 members, up to 30 days, prices ₡1–₡100,000, daily moves
    /// inside the clamp. With p(i,t) the price of member i on day t and n members:
    /// <list type="bullet">
    /// <item>Identity: no member moves → the Index does not move.</item>
    /// <item>Proportionality: every member × k → the Index × k.</item>
    /// <item>Path independence (circularity): after T days the Index is
    /// I0 · Π_i (p(i,T)/p(i,0))^(1/n), whatever the path.</item>
    /// <item>Time reversal: the same days forward then backward → back to I0.</item>
    /// <item>Mean-value bounds: a day's factor lies between the smallest and largest member move.</item>
    /// <item>Commensurability: rescaling one member's prices (its units) changes nothing.</item>
    /// <item>No drift: stocks that oscillate between fixed levels leave the Index where it was.</item>
    /// </list>
    /// The old rule (chained mean of returns, "Carli") fails time reversal and drifts up: by the
    /// AM–GM inequality its daily factor is ≥ the geometric one, strictly whenever members move
    /// differently, and the gap compounds.
    /// </summary>
    public class CityIndexPropertyTests
    {
        private const int Trials = 200;

        private static float Step(float level, float[] last, float[] prev)
        {
            int n = last.Length;
            var names = Enumerable.Range(0, n).Select(i => "S" + i).ToList();
            float mark;
            Assert.True(
                CityIndexBasket.TryComputeChainedMark(
                    level,
                    names,
                    last,
                    prev,
                    Enumerable.Repeat(true, n).ToList(),
                    out mark
                )
            );
            return mark;
        }

        private static double Chain(double start, float[][] path)
        {
            // path[t][i]: price of member i on day t
            double level = start;
            for (int t = 1; t < path.Length; t++)
            {
                level = Step((float)level, path[t], path[t - 1]);
            }

            return level;
        }

        private static float[][] RandomPath(Random rng, int members, int days)
        {
            var path = new float[days][];
            path[0] = new float[members];
            for (int i = 0; i < members; i++)
            {
                path[0][i] = (float)Math.Exp(rng.NextDouble() * Math.Log(100000));
            }

            for (int t = 1; t < days; t++)
            {
                path[t] = new float[members];
                for (int i = 0; i < members; i++)
                {
                    path[t][i] = path[t - 1][i] * (float)(0.6 + rng.NextDouble());
                }
            }

            return path;
        }

        private static void Trial(Action<Random, int, int> check)
        {
            var rng = new Random(20261114);
            for (int k = 0; k < Trials; k++)
            {
                check(rng, rng.Next(2, 14), rng.Next(2, 31));
            }
        }

        private static void Close(double expected, double actual, double rel = 2e-4)
        {
            Assert.InRange(actual, expected * (1 - rel), expected * (1 + rel));
        }

        [Fact]
        public void Identity()
        {
            Trial(
                (rng, n, _) =>
                {
                    float[] p = RandomPath(rng, n, 1)[0];
                    Close(1000.0, Step(1000f, p, p), 1e-6);
                }
            );
        }

        [Fact]
        public void Proportionality()
        {
            Trial(
                (rng, n, _) =>
                {
                    float[] p = RandomPath(rng, n, 1)[0];
                    float k = (float)(0.6 + rng.NextDouble());
                    Close(1000.0 * k, Step(1000f, p.Select(x => x * k).ToArray(), p), 1e-5);
                }
            );
        }

        [Fact]
        public void PathIndependence()
        {
            Trial(
                (rng, n, days) =>
                {
                    float[][] path = RandomPath(rng, n, days);
                    double geo = Math.Exp(
                        Enumerable
                            .Range(0, n)
                            .Average(i => Math.Log(path[days - 1][i] / (double)path[0][i]))
                    );
                    Close(1000.0 * geo, Chain(1000.0, path));
                }
            );
        }

        [Fact]
        public void TimeReversal()
        {
            Trial(
                (rng, n, days) =>
                {
                    float[][] path = RandomPath(rng, n, days);
                    float[][] there = path;
                    float[][] back = path.Reverse().ToArray();
                    Close(1000.0, Chain(Chain(1000.0, there), back));
                }
            );
        }

        [Fact]
        public void MeanValueBounds()
        {
            Trial(
                (rng, n, _) =>
                {
                    float[][] path = RandomPath(rng, n, 2);
                    double[] r = Enumerable
                        .Range(0, n)
                        .Select(i => path[1][i] / (double)path[0][i])
                        .ToArray();
                    double f = Step(1000f, path[1], path[0]) / 1000.0;
                    Assert.InRange(f, r.Min() * (1 - 1e-5), r.Max() * (1 + 1e-5));
                }
            );
        }

        [Fact]
        public void Commensurability()
        {
            Trial(
                (rng, n, days) =>
                {
                    float[][] path = RandomPath(rng, n, days);
                    int j = rng.Next(n);
                    float c = (float)Math.Exp(rng.NextDouble() * 6 - 3);
                    float[][] rescaled = path.Select(day =>
                            day.Select((p, i) => i == j ? p * c : p).ToArray()
                        )
                        .ToArray();
                    Close(Chain(1000.0, path), Chain(1000.0, rescaled));
                }
            );
        }

        [Fact]
        public void OscillatingStocksDoNotDrift()
        {
            // The bug the player saw: members that bounce between fixed levels (like Oil,
            // 46 ↔ 73) pushed the old Index up every round trip.
            var path = new List<float[]>();
            for (int t = 0; t <= 360; t++)
            {
                bool odd = t % 2 == 1;
                path.Add(new[] { odd ? 73f : 46f, odd ? 880f : 930f, 1000f, odd ? 5200f : 5600f });
            }

            Close(1000.0, Chain(1000.0, path.ToArray()), 1e-3);
        }

        [Fact]
        public void TheOldRuleWouldHaveDrifted()
        {
            // Documents the AM–GM gap: mean of returns over the same oscillation.
            double carli = 1.0;
            for (int t = 1; t <= 360; t++)
            {
                bool odd = t % 2 == 1;
                double r1 = odd ? 73.0 / 46.0 : 46.0 / 73.0;
                double r2 = odd ? 880.0 / 930.0 : 930.0 / 880.0;
                carli *= (r1 + r2) / 2.0;
            }

            Assert.True(carli > 50.0, "chained mean of returns: x" + carli.ToString("0.0"));
        }

        [Fact]
        public void ClampIsSymmetricSoAJumpAndItsReversalCancel()
        {
            // A stock unlocking from ₡1 to ₡1,000 and back is capped the same way both ways.
            float up = Step(1000f, new[] { 1000f, 100f }, new[] { 1f, 100f });
            float back = Step(up, new[] { 1f, 100f }, new[] { 1000f, 100f });
            Close(1000.0, back, 1e-5);
            Assert.InRange(up, 1000f, 1000f * (float)Math.Sqrt(2.0) * 1.0001f);
        }

        [Fact]
        public void BackfillIsTheExactInverseOfTheChain()
        {
            Trial(
                (rng, n, days) =>
                {
                    float[][] path = RandomPath(rng, n, days); // oldest first
                    double today = Chain(1000.0, path);
                    var names = Enumerable.Range(0, n).Select(i => "S" + i).ToList();
                    var newestFirst = Enumerable
                        .Range(0, n)
                        .Select(i =>
                            Enumerable.Range(0, days).Select(t => path[days - 1 - t][i]).ToArray()
                        )
                        .ToList();
                    float[] older = CityIndexBasket.BackfillOlder(
                        (float)today,
                        1,
                        names,
                        newestFirst,
                        Enumerable.Repeat(true, n).ToList(),
                        days
                    );
                    if (days > 1)
                    {
                        Close(1000.0, older[older.Length - 1]);
                    }
                }
            );
        }
    }
}

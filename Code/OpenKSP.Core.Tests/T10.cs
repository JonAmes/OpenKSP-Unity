using OpenKSP.Core.World;
using OpenKSP.Core.Math;
using System;
using Xunit;

namespace OpenKSP.Core.Tests
{
    /// <summary>
    /// T10: Origin crossing — a body near the local-radius threshold;
    /// small origin slides cross IN/OUT; the local->global composition
    /// (identity) holds <= 1e-6 relative every step; the canonical global
    /// position stays continuous (no jumps beyond the integrator error).
    /// </summary>
    public class T10
    {
        private const double Mu = 3.986004418e14;
        private const double R = 7.0e6;
        private const double Dt = 1.0 / 1200.0;
        private const int Steps = 2000;
        private const double SlideM = 1e5;       // tiny origin nudge each step
        private const double Radius = 1.0e6;     // local-radius threshold
        private const double RelEps = 1e-6;

        private static double Norm(D3 v)
            => System.Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);

        // One RK4 position step of a body around a fixed primary at the origin.
        private static D3 NextPos(D3 pos, double mu, double dt)
        {
            D3 Accel(D3 p)
            {
                var r = Norm(p);
                var invR3 = mu / (r * r * r);
                return new D3(-p.X * invR3, -p.Y * invR3, -p.Z * invR3);
            }
            D3 a1 = Accel(pos);
            D3 p2 = pos + a1 * (dt * 0.5);
            D3 a2 = Accel(p2);
            D3 p3 = pos + a2 * (dt * 0.5);
            D3 a3 = Accel(p3);
            D3 p4 = pos + a3 * dt;
            D3 a4 = Accel(p4);
            return pos + (a1 + a2 * 2.0 + a3 * 2.0 + a4) * (dt / 6.0);
        }

        [Fact]
        public void OriginSlideCrossingKeepsCompositionIdentityAndCanonicalContinuity()
        {
            var vCirc = System.Math.Sqrt(Mu / R);
            D3 pos = new D3(R, 0.0, 0.0);

            FloatingOrigin origin = new FloatingOrigin(new D3(0.0, 0.0, 0.0), Radius);

            D3 prev = pos;
            bool sawIn = origin.DistanceFromOrigin(pos) < Radius;
            bool sawOut = !sawIn;

            for (var s = 0; s < Steps; s++)
            {
                // Advance one canonical (global) step.
                pos = NextPos(pos, Mu, Dt);

                // Slide the origin by a small nudge each step (crossing regime).
                D3 slide = new D3(SlideM * System.Math.Sin(s * 0.01), 0.0, 0.0);
                origin = origin.SlideTo(origin.Origin + slide);

                // Composition identity, every step (local -> global == global).
                var rt = origin.ToGlobal(origin.ToLocal(pos));
                var denom = System.Math.Max(1.0, Norm(pos));
                double dx = System.Math.Abs(rt.X - pos.X);
                double dy = System.Math.Abs(rt.Y - pos.Y);
                double dz = System.Math.Abs(rt.Z - pos.Z);
                Assert.True(dx / denom < RelEps, $"step {s}: x residual {dx}");
                Assert.True(dy / denom < RelEps, $"step {s}: y residual {dy}");
                Assert.True(dz / denom < RelEps, $"step {s}: z residual {dz}");

                // Canonical position continuity: per-step move must be small
                // (no jumps beyond the integrator error ~ O(dt * v)).
                D3 jump = pos - prev;
                var jumpMag = Norm(jump);
                double expectedMax = vCirc * Dt * 1.5; // ~ one step of motion, slack
                Assert.True(jumpMag < expectedMax, $"step {s}: canonical jump {jumpMag}");

                // Track whether both IN and OUT regimes are exercised.
                if (origin.DistanceFromOrigin(pos) < Radius) sawIn = true;
                else sawOut = true;

                prev = pos;
            }

            // Both regimes were exercised.
            Assert.True(sawIn, "never saw body inside local radius");
            Assert.True(sawOut, "never saw body outside local radius");
        }
    }
}

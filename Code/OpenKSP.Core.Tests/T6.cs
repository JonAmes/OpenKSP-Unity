using OpenKSP.Core.World;
using OpenKSP.Core.Math;
using System;
using Xunit;

namespace OpenKSP.Core.Tests
{
    /// <summary>
    /// T6: Origin-slide round-trip — local -> slide -> global.
    /// ToGlobal(T local) and ToLocal(g global) compose to identity within 1e-6 relative.
    /// </summary>
    public class T6
    {
        private static double Norm(D3 v)
            => System.Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);

        private static D3 RoundTrip(D3 global, FloatingOrigin origin)
            => origin.ToGlobal(origin.ToLocal(global));

        private static void AssertRoundTripRelative(D3 global, FloatingOrigin origin, double relEps)
        {
            var rt = RoundTrip(global, origin);
            var denom = System.Math.Max(1.0, Norm(global));
            var dx = System.Math.Abs(rt.X - global.X);
            var dy = System.Math.Abs(rt.Y - global.Y);
            var dz = System.Math.Abs(rt.Z - global.Z);
            Assert.True(dx / denom < relEps, $"x residual {dx}");
            Assert.True(dy / denom < relEps, $"y residual {dy}");
            Assert.True(dz / denom < relEps, $"z residual {dz}");
        }

        [Fact]
        public void RoundTripIsIdentityWithinRelativeTolerance()
        {
            var origin = new FloatingOrigin(new D3(5.0e3, 0.0, 0.0), 3.0e6);

            // A point near the origin (local frame, float32) - high precision.
            var gNear = new D3(5.0e3 + 100.0, 200.0, -300.0);
            AssertRoundTripRelative(gNear, origin, 1e-6);

            // A point moderately farther away.
            var gFar = new D3(5.0e3 + 2.5e6, 1.2e6, -9.0e5);
            AssertRoundTripRelative(gFar, origin, 1e-6);
        }

        [Fact]
        public void SlideToReanchorsAndRoundTripHolds()
        {
            var origin = new FloatingOrigin(new D3(0.0, 0.0, 0.0), 2.0e6);
            var target = new D3(3.0e5, 4.0e5, -1.0e5);
            var slid = origin.SlideTo(target);

            Assert.True(slid.Origin == target);           // exact reanchor
            Assert.True(slid.LocalRadius == origin.LocalRadius); // radius preserved

            var g = new D3(3.0e5 + 1.2e4, -8.0e3, 7.0e3);
            AssertRoundTripRelative(g, slid, 1e-6);
        }
    }
}

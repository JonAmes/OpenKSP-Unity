using System;
using OpenKSP.Core.Celestial;
using OpenKSP.Core.Math;
using OpenKSP.Core.World;
using Xunit;

namespace OpenKSP.Core.Tests
{
    /// <summary>
    /// T14: Phase 2 Task 3 — Core-side analogue of the §3 ordering loop.
    /// The floating origin maps canonical (double, global) state to local (float)
    /// proxy state and back; the canonical WorldState absorbs a proxy's read-back
    /// value through the allocation-free SetPosition seam. Per §3:
    ///   5. IN read-back   — g = origin + FromFloat(localPos); the canonical state absorbs g.
    ///   6. OUT advance    — rb.position = (globalPos' − origin); local form via ToLocal.
    ///   7. origin slide   — if the origin moved this step, re-snapshot from canonical state.
    /// The composition identity ToGlobal(ToLocal(p)) == p holds within ½ ulp of a
    /// float32 near magnitude ~1e5 m (~6 mm), matching the §2.3 residual bound.
    /// Everything here is Core-only (no UnityEngine); uses only allocation-free WorldState
    /// .Position / .SetPosition accessors (never the allocating Bodies property).
    /// </summary>
    public class T14
    {
        // ½ ulp of a float32 relative value near magnitude ~1e5 m
        // (eps * 1e5 ≈ 1.19e-2 m ulp -> ½-ulp ≈ 5.96e-3 m; bound set at 6.0e-3 m).
        private const double HalfUlpAt1e5 = 6.0e-3;

        private static double Dist3(D3 a, D3 b)
        {
            var d = a - b;
            return System.Math.Sqrt(d.X * d.X + d.Y * d.Y + d.Z * d.Z);
        }

        /// <summary>
        /// Composition identity: ToGlobal(ToLocal(p)) equals p within ½ ulp.
        /// At ~1e5 m from the origin the float cast is the only precision loss.
        /// </summary>
        [Fact]
        public void ToGlobalToLocalCompositionIsIdentityWithinHalfUlp()
        {
            var origin = new FloatingOrigin(new D3(0.0, 0.0, 0.0), 1.0e6);
            var p = new D3(1.0e5, -2.5e4, 3.75e4); // |p| ≈ 1.097e5 m, inside localRadius

            var local = origin.ToLocal(p);
            var roundTrip = origin.ToGlobal(local);

            var residual = Dist3(p, roundTrip);
            Assert.True(residual <= HalfUlpAt1e5, $"composition residual {residual} m (bound {HalfUlpAt1e5} m)");
        }

        /// <summary>
        /// Origin slide: after sliding the origin to a new target, the composition
        /// identity still holds against the new origin (the mapping is origin-consistent),
        /// and the slide is detected (origin differs). Per §3 step 7.
        /// </summary>
        [Fact]
        public void OriginSlidePreservesCompositionIdentity()
        {
            var origin0 = new FloatingOrigin(new D3(0.0, 0.0, 0.0), 1.0e6);
            var target = new D3(1.0e5, 0.0, 0.0);
            var origin1 = origin0.SlideTo(target);

            // A point a few km from the new origin maps identity under the slid origin.
            var p = target + new D3(1.2e3, -8.0e2, 6.4e2);

            var local = origin1.ToLocal(p);
            var roundTrip = origin1.ToGlobal(local);

            var residual = Dist3(p, roundTrip);
            // |p − origin1| ≈ 1.78e3 m; float ulp there is sub-mm.
            Assert.True(residual <= 1.0e-3, $"slid-origin residual {residual} m");

            // The origin actually slid — the re-snapshot branch is live.
            Assert.NotEqual(origin0.Origin, origin1.Origin);
        }

        /// <summary>
        /// Simulated read-back fold (per §3 step 5): a proxy folds its local state
        /// back into the canonical WorldState through the allocation-free SetPosition
        /// seam; the absorbed value differs from the original by at most ½ ulp.
        /// </summary>
        [Fact]
        public void ReadBackFoldIsAbsorbedIntoCanonicalState()
        {
            var origin = new FloatingOrigin(new D3(0.0, 0.0, 0.0), 1.0e6);
            var p = new D3(1.0e5, -2.5e4, 3.75e4);

            var sim = new WorldState(new[]
            {
                new CBody("sun", "Sun", D3.Zero, DVector.Zero, 6.96e8, 1.32712440018e20),
                new CBody("earth", "Earth", p, DVector.Zero, 6.371e6, 3.986004418e14, "sun"),
            });

            // Pre-step snap (steps 1-2): local form of the canonical position through the origin.
            var local = origin.ToLocal(sim.Position(1));

            // Read-back fold (step 5): global = origin + FromFloat(local), absorbed into canonical state.
            var readBackGlobal = origin.ToGlobal(local);
            sim.SetPosition(1, readBackGlobal);

            // The absorbed value differs from the original by at most ½ ulp.
            var residual = Dist3(p, sim.Position(1));
            Assert.True(residual <= HalfUlpAt1e5, $"read-back fold residual {residual} m (bound {HalfUlpAt1e5} m)");
        }
    }
}

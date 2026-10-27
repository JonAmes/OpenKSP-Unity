using OpenKSP.Core.Orbit;
using OpenKSP.Core.Celestial;
using OpenKSP.Core.Math;
using System;
using Xunit;

namespace OpenKSP.Core.Tests
{
    /// <summary>
    /// T12: parent-relative Kepler elements — WorldState.KeplerElements(i) round trip:
    /// delta position &lt; 1e-3 m, and A / E recovered to high precision.
    /// A real (bound, non-circular) state of earth about the sun, so the focus is the
    /// parent body (sun) and the seam uses the parent body's mu. Mirrors T8's
    /// round-trip style (build a state from known elements, then round-trip back).
    /// A small nonzero inclination / node keep the ascending-node branch well-defined
    /// (a strictly planar, node-less orbit is the degenerate case the existing
    /// FromState math does not handle).
    /// </summary>
    public class T12
    {
        private const double MuSun = 1.32712440018e20;  // mu_sun
        private const double A = 1.496e11;             // 1 AU (m)
        private const double E = 0.0167;             // earth eccentricity (-)
        private const double Inc = 0.10;            // rad (small tilt off the ecliptic)
        private const double Omega = 0.30;          // ascending node (rad)
        private const double W = 0.50;             // arg of pericenter (rad)

        [Fact]
        public void EarthAboutSunRoundTripDeltaPosUnder1e3m()
        {
            // Build a real earth-about-sun state from known elements, then round-trip
            // it through the parent-relative seam.
            var el0 = new OrbitalElements(A, E, Inc, 0.42, Omega, W);
            D3 r0 = KeplerianElements.ToState(el0, MuSun);
            DVector v0 = KeplerianElements.StateVelocity(el0, MuSun);

            var sim = new WorldState(new[]
            {
                // sun (root, parentless) — barycenter/origin and the focus.
                new CBody("sun", "Sun",
                    new D3(0.0, 0.0, 0.0), DVector.Zero, 6.96e8, MuSun),

                // earth (parent: sun) — the real about-sun state.
                new CBody("earth", "Earth", r0, v0, 6.371e6, 3.986004418e14, "sun"),
            });

            // Parent-relative elements: focus = parent (sun), mu = parent's mu (mu_sun).
            var el = sim.KeplerElements(1);

            // elements -> state (same focus frame as FromState).
            D3 r1 = KeplerianElements.ToState(el, MuSun);

            // The relative state the seam consumed is earth's position relative to the
            // sun's position; with the sun at the origin that is earth's world position.
            var delta = r1 - sim.Position(1);
            double deltaPos = System.Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y + delta.Z * delta.Z);

            Assert.True(deltaPos < 1e-3, $"delta position {deltaPos} m");

            // Semi-major axis / eccentricity recovered to high precision.
            Assert.True(System.Math.Abs(el.A - A) / A < 1e-9);
            Assert.True(System.Math.Abs(el.Ecc - E) < 1e-9);
        }
    }
}

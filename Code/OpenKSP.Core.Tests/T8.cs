using OpenKSP.Core.Orbit;
using OpenKSP.Core.Celestial;
using OpenKSP.Core.Math;
using System;
using Xunit;

namespace OpenKSP.Core.Tests
{
    /// <summary>
    /// T8: KeplerianElements state -> elements -> state round trip: delta position < 1e-3 m.
    /// A bound elliptical (non-circular) orbit with nonzero inclination.
    /// </summary>
    public class T8
    {
        private const double Mu = 3.986004418e14;   // mu_earth
        private const double A = 7.0e6;           // semi-major axis (m)
        private const double E = 0.20;           // eccentricity
        private const double Inc = 0.35;         // rad
        private const double Omega = 0.70;       // rad
        private const double W = 1.10;           // rad

        [Fact]
        public void StateToElementsToStateRoundTripDeltaPosUnder1e3m()
        {
            // Build a real state from known elements, then round-trip it back.
            var el0 = new OrbitalElements(A, E, Inc, 0.42, Omega, W);
            D3 r0 = KeplerianElements.ToState(el0, Mu);
            DVector v0 = KeplerianElements.StateVelocity(el0, Mu);

            // state -> elements
            var el = KeplerianElements.FromState(r0, v0, Mu);

            // elements -> state
            D3 r1 = KeplerianElements.ToState(el, Mu);

            var delta = r1 - r0;
            double deltaPos = System.Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y + delta.Z * delta.Z);

            Assert.True(deltaPos < 1e-3, $"delta position {deltaPos} m");

            // Eccentricity / semi-major axis recovered to high precision.
            Assert.True(System.Math.Abs(el.A - A) / A < 1e-9);
            Assert.True(System.Math.Abs(el.Ecc - E) < 1e-9);
        }
    }
}

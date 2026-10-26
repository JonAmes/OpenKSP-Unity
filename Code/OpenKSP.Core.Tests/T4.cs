using OpenKSP.Core.Celestial;
using OpenKSP.Core.Math;
using Xunit;

namespace OpenKSP.Core.Tests
{
    /// <summary>
    /// T4: Core-only RK4 circular orbit, r = 7.35e6 m around μ_earth,
    /// dt = 1/1200 s, ~8.6e3 steps (spec's stated step count), vs analytic
    /// circular radius. Max radial residual &lt; 100 m. No proxy involved.
    /// </summary>
    public class T4
    {
        private const double MuEarth = 3.986004418e14;
        private const double R = 7.35e6;
        private const double Dt = 1.0 / 1200.0;
        private const int Steps = 8600; // ~8.6e3 steps per spec
        private const double MaxRadialResidual = 100.0;

        [Fact]
        public void Rk4CCircularOrbitMatchesAnalyticKeplerWithin100m()
        {
            // Circular speed for a body at radius R orbiting μ_earth.
            var vCirc = System.Math.Sqrt(MuEarth / R);

            var sim = new WorldState(new[]
            {
                new CBody("earth", "Earth",
                    new D3(0.0, 0.0, 0.0), DVector.Zero, 6.371e6, MuEarth),
                new CBody("probe", "Probe",
                    new D3(R, 0.0, 0.0), new DVector(0.0, vCirc, 0.0), 1.0, 0.0)
            });

            var maxRes = 0.0;

            for (var s = 0; s < Steps; s++)
            {
                sim.Step(Dt);
                var p = sim.Bodies[1].Position;
                var rSim = System.Math.Sqrt(p.X * p.X + p.Y * p.Y + p.Z * p.Z);
                var resid = System.Math.Abs(rSim - R); // analytic circular radius is R
                if (resid > maxRes) maxRes = resid;
            }

            Assert.True(maxRes < MaxRadialResidual,
                $"max radial residual {maxRes} m >= {MaxRadialResidual} m");
        }
    }
}

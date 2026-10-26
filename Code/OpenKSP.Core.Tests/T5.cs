using OpenKSP.Core.Celestial;
using OpenKSP.Core.Math;
using Xunit;

namespace OpenKSP.Core.Tests
{
    /// <summary>
    /// T5: Energy conservation, 2-body, dt = 1/1200 s, 10^6 steps.
    /// TotalEnergy per doc §9 (Σ½mᵢvᵢ² − Σᵢ<ⱼ G mᵢmⱼ/rᵢⱼ). ΔE/E &lt; 1e-6.
    /// </summary>
    public class T5
    {
        private const double MuSun = 1.32712440018e20;
        private const double MuEarth = 3.986004418e14;
        private const double R = 1.496e11; // ~1 AU
        private const double Dt = 1.0 / 1200.0;
        private const int Steps = 1_000_000;
        private const double ToleranceRel = 1e-6;

        [Fact]
        public void TwoBodyTotalEnergyConservedWithin1e6()
        {
            // Earth in circular orbit around the Sun (Sun at origin).
            // Sun's circular speed around barycenter is negligible here; Sun is
            // held at origin, Earth's v_circ = sqrt(mu_sun / r).
            var vEarth = System.Math.Sqrt(MuSun / R);

            var sim = new WorldState(new[]
            {
                new CBody("sun", "Sun",
                    new D3(0.0, 0.0, 0.0), DVector.Zero, 6.96e8, MuSun),
                new CBody("earth", "Earth",
                    new D3(R, 0.0, 0.0), new DVector(0.0, vEarth, 0.0), 6.371e6, MuEarth)
            });

            var e0 = sim.TotalEnergy();
            for (var s = 0; s < Steps; s++)
            {
                sim.Step(Dt);
            }
            var e1 = sim.TotalEnergy();

            // ΔE/E — relative change, E0 is non-zero (bound orbit → negative).
            var relEnergy = System.Math.Abs(e1 - e0) / System.Math.Abs(e0);
            Assert.True(relEnergy < ToleranceRel,
                $"ΔE/E = {relEnergy} >= {ToleranceRel}; E0={e0}, E1={e1}");
        }
    }
}

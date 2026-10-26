using OpenKSP.Core.Celestial;
using OpenKSP.Core.Math;
using Xunit;

namespace OpenKSP.Core.Tests
{
    /// <summary>
    /// T9: Determinism — two identical runs → bit-identical final snapshot.
    /// Snapshots are compared with EXACT equality (==), not tolerance.
    /// </summary>
    public class T9
    {
        private const double MuSun = 1.32712440018e20;
        private const double MuEarth = 3.986004418e14;
        private const double R = 1.496e11;
        private const double Dt = 1.0 / 1200.0;
        private const int Steps = 1000;

        private static CBody[] World()
        {
            var vEarth = System.Math.Sqrt(MuSun / R);
            return new[]
            {
                new CBody("sun", "Sun",
                    new D3(0.0, 0.0, 0.0), DVector.Zero, 6.96e8, MuSun),
                new CBody("earth", "Earth",
                    new D3(R, 0.0, 0.0), new DVector(0.0, vEarth, 0.0), 6.371e6, MuEarth)
            };
        }

        [Fact]
        public void TwoIdenticalRunsYieldBitIdenticalSnapshot()
        {
            var a = new WorldState(World());
            var b = new WorldState(World());

            for (var s = 0; s < Steps; s++)
            {
                a.Step(Dt);
                b.Step(Dt);
            }

            var snapA = a.ToSnapshot();
            var snapB = b.ToSnapshot();

            // Guard: the state must actually be non-trivial (the runs must do work).
            Assert.True(!snapA.Velocities[1].Equals(DVector.Zero));
            Assert.True(!snapA.Positions[1].Equals(D3.Zero));

            // EXACT, bit-level equality — no relative tolerance.
            Assert.True(snapA == snapB);
            Assert.True(!snapA.Equals(default)); // a real state is not the default struct
        }
    }
}

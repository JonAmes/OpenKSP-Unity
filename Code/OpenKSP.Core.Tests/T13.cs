using OpenKSP.Core.Celestial;
using OpenKSP.Core.Math;
using Xunit;

namespace OpenKSP.Core.Tests
{
    /// <summary>
    /// T13: Determinism WITH parent hierarchy — two identical runs over a
    /// sun→earth→moon hierarchy must yield bit-identical final snapshots.
    /// Mirrors T9 (exact snapshot ==) but exercises a 3-body hierarchical set
    /// so the parent-hierarchy wiring (Constructor ResolveParentIndices) is
    /// proven deterministic, not just the RK4 integrator.
    /// The Core force model is full N-body (every body feels every other);
    /// the hierarchy is structural (ParentId / ParentIndex) and never changes the
    /// acceleration, so determinism must hold identically across both worlds.
    /// </summary>
    public class T13
    {
        private const double MuSun = 1.32712440018e20;
        private const double MuEarth = 3.986004418e14;
        private const double MuMoon = 4.9048695e12;

        private const double REarthAboutSun = 1.496e11;
        private const double RMoonAboutEarth = 3.844e8;

        private const double Dt = 1.0 / 1200.0;
        private const int Steps = 1000;

        private static CBody[] HierarchicalWorld()
        {
            // sun (root); earth→sun; moon→earth. Realistic circular-ish orbits.
            var vEarth = System.Math.Sqrt(MuSun / REarthAboutSun);
            var vMoonAboutEarth = System.Math.Sqrt(MuEarth / RMoonAboutEarth);

            var sunPos = new D3(0.0, 0.0, 0.0);
            var earthPos = new D3(REarthAboutSun, 0.0, 0.0);
            // moon offset perpendicular to earth's about-sun velocity,
            // so (moon - earth) is along +Y and the about-earth velocity is along +X.
            var moonPos = new D3(earthPos.X, earthPos.Y + RMoonAboutEarth, earthPos.Z);

            return new[]
            {
                new CBody("sun", "Sun",
                    sunPos, DVector.Zero, 6.96e8, MuSun, null),
                new CBody("earth", "Earth",
                    earthPos, new DVector(0.0, vEarth, 0.0), 6.371e6, MuEarth, "sun"),
                new CBody("moon", "Moon",
                    moonPos, new DVector(vMoonAboutEarth, vEarth, 0.0), 1.7374e6, MuMoon, "earth")
            };
        }

        [Fact]
        public void TwoIdenticalHierarchicalRunsYieldBitIdenticalSnapshot()
        {
            var a = new WorldState(HierarchicalWorld());
            var b = new WorldState(HierarchicalWorld());

            for (var s = 0; s < Steps; s++)
            {
                a.Step(Dt);
                b.Step(Dt);
            }

            var snapA = a.ToSnapshot();
            var snapB = b.ToSnapshot();

            // Guard: the state must actually be non-trivial (the runs must do work).
            Assert.True(!snapA.Positions[1].Equals(D3.Zero));
            Assert.True(!snapA.Positions[2].Equals(D3.Zero));
            Assert.True(snapA.BodyCount == 3);

            // Hierarchy resolves identically in both worlds (root→-1, else parent index).
            Assert.Equal(-1, a.ParentIndex(0));
            Assert.Equal(0, a.ParentIndex(1));
            Assert.Equal(1, a.ParentIndex(2));
            Assert.True(a.ParentIndex(0) == b.ParentIndex(0));
            Assert.True(a.ParentIndex(1) == b.ParentIndex(1));
            Assert.True(a.ParentIndex(2) == b.ParentIndex(2));

            // EXACT, bit-level equality — no relative tolerance (mirrors T9).
            Assert.True(snapA == snapB);
            Assert.True(!snapA.Equals(default)); // a real state is not the default struct
        }
    }
}

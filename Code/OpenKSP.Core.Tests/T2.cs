using OpenKSP.Core.Celestial;
using OpenKSP.Core.Math;
using Xunit;

namespace OpenKSP.Core.Tests
{
    /// <summary>
    /// T2: Single-body gravity vs closed-form μ/r².
    /// A test particle at (r,0,0) relative to a body at the origin must see
    /// acceleration = (μ/r², 0, 0). Relative error &lt; 1e-9.
    /// </summary>
    public class T2
    {
        private const double Tolerance = 1e-9;

        [Theory]
        [InlineData(3.986004418e14, 7.35e6, 0.0, 0.0)]
        [InlineData(3.986004418e14, 3.0e6, 4.0e6, 1.6e6)]
        [InlineData(1.32712440018e20, 1.0e11, 0.0, 5.0e10)]
        public void SingleBodyMatchesClosedForm(
            double mu, double px, double py, double pz)
        {
            var body = new CBody("earth", "Earth", mu);
            var field = new GravityField(new[] { body });

            var p = new DVector(px, py, pz);
            var r = System.Math.Sqrt(p * p);
            // Body at origin pulls probe at p toward the origin: a = -mu * p / r^3
            var ax = -mu * px / (r * r * r);
            var ay = -mu * py / (r * r * r);
            var az = -mu * pz / (r * r * r);

            var a = field.Acceleration(p);

            RelAssert.Close(a.X, ax, Tolerance);
            RelAssert.Close(a.Y, ay, Tolerance);
            RelAssert.Close(a.Z, az, Tolerance);
        }

        /// <summary>
        /// Loader smoke test: the shipped planets.json parses per §6,
        /// required fields present, mu values match the canonical constants.
        /// </summary>
        [Fact]
        public void PlanetsJsonParses()
        {
            var bodies = CBodyLoader.Load();

            Assert.True(bodies.Count >= 2);

            var sun = CBodyLoader.GetById(bodies, "sun");
            var earth = CBodyLoader.GetById(bodies, "earth");

            Assert.NotNull(sun);
            Assert.NotNull(earth);

            var sunBody = sun.Value;
            var earthBody = earth.Value;

            Assert.Equal(1.32712440018e20, sunBody.Mu3PerS2);
            Assert.Equal(6.96e8, sunBody.Radius);
            Assert.Equal(3.986004418e14, earthBody.Mu3PerS2);
            Assert.Equal(6.371e6, earthBody.Radius);
            Assert.Equal(D3.Zero, sunBody.Position);
        }

        /// <summary>
        /// Also: analytic 2-body helper TwoBodyAcc must agree with
        /// Acceleration evaluated on a one-body field.
        /// </summary>
        [Fact]
        public void TwoBodyHelperMatchesNBodyAcceleration()
        {
            var body = new CBody("sun", "Sun", 1.32712440018e20);
            var pos = new DVector(-4.0e11, 2.0e11, 1.0e11);
            var probe = new CBody("probe", "Probe", pos, DVector.Zero, 0.0, 0.0);

            var nBody = new GravityField(new[] { body });
            var a1 = nBody.Acceleration(pos);
            var a2 = nBody.TwoBodyAcc(body, probe);

            RelAssert.Close(a1.X, a2.X, Tolerance);
            RelAssert.Close(a1.Y, a2.Y, Tolerance);
            RelAssert.Close(a1.Z, a2.Z, Tolerance);
        }
    }
}

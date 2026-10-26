using System.Collections.Generic;
using OpenKSP.Core.Celestial;
using OpenKSP.Core.Math;
using Xunit;

namespace OpenKSP.Core.Tests
{
    /// <summary>
    /// T3: N-body sum (3 bodies) vs independent pairwise sum.
    /// Σᵢ μᵢ(rᵢ−p)/|rᵢ−p|³ computed directly must match GravityField.Acceleration(p)
    /// to relative error &lt; 1e-9.
    /// </summary>
    public class T3
    {
        private const double Tolerance = 1e-9;

        private static DVector ReferencePairwiseSum(IReadOnlyList<CBody> bodies, DVector p)
        {
            double sx = 0.0, sy = 0.0, sz = 0.0;
            foreach (var b in bodies)
            {
                var dx = b.Position.X - p.X;
                var dy = b.Position.Y - p.Y;
                var dz = b.Position.Z - p.Z;
                var r = System.Math.Sqrt(dx * dx + dy * dy + dz * dz);
                var invR3 = b.Mu3PerS2 / (r * r * r);
                sx += dx * invR3;
                sy += dy * invR3;
                sz += dz * invR3;
            }
            return new DVector(sx, sy, sz);
        }

        [Theory]
        [InlineData(0.0, 0.0, 0.0,
            1.0, 0.0, 0.0,
            0.0, 1.0, 0.0,
            0.0, 0.0, 1.0,
            1.0, 1.0, 2.0, 3.0)]
        [InlineData(0.5, 0.25, -0.125,
            10.0, -0.5, 0.25,
            1.2, 4.5e5, -8.8e4,
            2.2e3, 3.3e4, 100.0,
            4.5e5, 2.0, -0.5, 0.25)]
        [InlineData(1.0e12, 0.0, 0.0,
            0.0, 0.0, 1.0e12,
            1.0e11, 1.0e11, 0.0,
            0.0, 5.0e12, 0.0,
            4.9048695e12, 7.7, 2.1, 6.3)]
        public void NBodySumMatchesPairwise(
            double px, double py, double pz,
            double bx, double by, double bz,
            double cx, double cy, double cz,
            double ax, double ay, double az,
            double mu, double rA, double rB, double rC)
        {
            var b = new CBody("b", "B",
                new D3(bx, by, bz), DVector.Zero, rA, mu);
            var c = new CBody("c", "C",
                new D3(cx, cy, cz), DVector.Zero, rB, mu);
            var a = new CBody("a", "A",
                new D3(ax, ay, az), DVector.Zero, rC, mu);

            var field = new GravityField(new[] { b, c, a });

            var p = new DVector(px, py, pz);
            var sum = field.Acceleration(p);
            var expected = ReferencePairwiseSum(new[] { b, c, a }, p);

            RelAssert.Close(sum.X, expected.X, Tolerance);
            RelAssert.Close(sum.Y, expected.Y, Tolerance);
            RelAssert.Close(sum.Z, expected.Z, Tolerance);
        }
    }
}

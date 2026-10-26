using OpenKSP.Core.Math;
using Xunit;

namespace OpenKSP.Core.Tests
{
    /// <summary>
    /// T1: D3 round-trip ToFloat / FromFloat, per-conversion relative error &lt; 1e-6.
    /// Double &lt;-&gt; float casting bounds relative error at ~2^-24 (5.96e-8) for any
    /// magnitude, so the 1e-6 gate is comfortably met.
    /// </summary>
    public class T1
    {
        private const double Tolerance = 1e-6;

        private static void AssertRelativeClose(double a, double b)
        {
            var rel = System.Math.Abs(a - b) / System.Math.Abs(b);
            Xunit.Assert.True(rel < Tolerance, $"rel error {rel} >= {Tolerance}");
        }

        [Theory]
        [InlineData(1.23456789123e5, -3.14159265359e6, 7.77777777777e3)]
        [InlineData(-0.421536789e2, 6.031415927e7, -1.0e8)]
        [InlineData(1.0e-3, 9.87654321e4, 5.555555555e5)]
        public void D3_RoundTrip_PerConversion_RelativeError_Under_Tolerance(double x, double y, double z)
        {
            var global = new D3(x, y, z);
            var origin = D3.Zero;

            // Conversion 1: D3 -> F3
            var local = Coordinates.FromFloat(global, origin);
            // Conversion 2: F3 -> D3
            var back = Coordinates.ToFloat(local, origin);

            // F3->D3 conversion error measured against the original D3.
            AssertRelativeClose(back.X, global.X);
            AssertRelativeClose(back.Y, global.Y);
            AssertRelativeClose(back.Z, global.Z);

            // D3->F3 consistency: re-converting the back-converted D3 must
            // reproduce the original F3 exactly (float -> double -> float is exact).
            var local2 = Coordinates.FromFloat(back, origin);
            Xunit.Assert.Equal(local.X, local2.X);
            Xunit.Assert.Equal(local.Y, local2.Y);
            Xunit.Assert.Equal(local.Z, local2.Z);
        }
    }
}

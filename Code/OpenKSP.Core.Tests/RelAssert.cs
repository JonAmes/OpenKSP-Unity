using System;

namespace OpenKSP.Core.Tests
{
    // Shared assertion helper (xunit has no RelativeClose).
    public static class RelAssert
    {
        public static void Close(double actual, double expected, double relTolerance)
        {
            if (System.Math.Abs(expected) < 1e-300)
            {
                if (System.Math.Abs(actual) < 1e-300)
                {
                    return; // both numerically zero: error is exactly 0
                }
                throw new InvalidOperationException(
                    $"expected value is ~0; relative error is undefined (actual {actual})");
            }

            var rel = System.Math.Abs(actual - expected) / System.Math.Abs(expected);
            if (rel >= relTolerance)
            {
                throw new InvalidOperationException(
                    $"Relative error {rel} >= {relTolerance} (actual {actual}, expected {expected})");
            }
        }
    }
}

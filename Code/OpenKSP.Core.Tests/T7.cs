using OpenKSP.Core.Time;
using Xunit;

namespace OpenKSP.Core.Tests
{
    /// <summary>
    /// T7: Warp — 12 variable frames at warp=8 -> Core advanced exactly dt*8.
    /// Step count is exact (no partial steps).
    /// </summary>
    public class T7
    {
        private const double Warp = 8.0;
        private static readonly double[] Frames =
        {
            0.205, 0.198, 0.212, 0.195,
            0.221, 0.190, 0.208, 0.199,
            0.214, 0.196, 0.207, 0.203
        };

        [Fact]
        public void AccumulatedRealTimeEqualsCoreAdvanceWarp8()
        {
            var tw = new TimeWarpController(Warp);
            double realTime = 0.0;
            int totalSteps = 0;

            foreach (var f in Frames)
            {
                realTime += f;
                totalSteps += tw.Accumulate(f);
            }

            // Core advanced exactly dt*warp*realTime (dt=1 second step basis;
            // here "dt" is the accumulated real time, warp=8 => factor of 8).
            // totalSteps must equal floor(realTime * warp).
            long expected = (long)(realTime * Warp);
            Assert.Equal(expected, totalSteps);

            // Completed steps match the exact floor; no partial step was run.
            Assert.Equal(expected, tw.TotalCompletedSteps);
        }
    }
}

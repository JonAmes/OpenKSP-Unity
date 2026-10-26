using System;

namespace OpenKSP.Core.Time
{
    // Fixed-step time-warp accumulator.
    // Core advances in fixed dt steps independent of frame rate. `Accumulate(realTime)`
    // converts real elapsed time into a whole number of Core steps at the current warp
    // rate. Warp changes apply only at the variable-step boundary; never mid-step.
    public sealed class TimeWarpController
    {
        private double _warpRate;
        private double _realTimeAccumulator;
        private int _completedSteps;

        public double WarpRate => _warpRate;
        public int CompletedSteps => _completedSteps;
        public int PendingSteps { get { return (int)(_realTimeAccumulator * _warpRate); } }

        public TimeWarpController(double warpRate)
        {
            if (warpRate < 0)
                throw new ArgumentException("Warp rate must be non-negative.");
            _warpRate = warpRate;
        }

        // Change the warp rate; applies at the boundary (no mid-step effect).
        public void SetWarpRate(double warpRate)
        {
            if (warpRate < 0)
                throw new ArgumentException("Warp rate must be non-negative.");
            _warpRate = warpRate;
        }

        // Add real elapsed time (seconds). Returns the number of Core steps to run now
        // (whole steps at the current warp rate); partial time stays accumulated.
        public int Accumulate(double realTime)
        {
            _realTimeAccumulator += realTime;
            var steps = (int)(_realTimeAccumulator * _warpRate);
            _realTimeAccumulator -= steps / _warpRate;
            _completedSteps += steps;
            return steps;
        }

        // Total Core steps that have been fully accounted for.
        public int TotalCompletedSteps => _completedSteps;
    }
}

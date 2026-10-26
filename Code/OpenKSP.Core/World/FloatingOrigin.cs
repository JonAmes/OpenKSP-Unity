using OpenKSP.Core.Celestial;
using OpenKSP.Core.Math;
using System;

namespace OpenKSP.Core.World
{
    // FloatingOrigin: a global anchor point plus a local-radius threshold.
    // The local frame is float32 centered on Origin. Bodies inside localRadius
    // live as Float proxies (IN); outside they are advanced by Core double math (OUT).
    public struct FloatingOrigin
    {
        public D3 Origin { get; }
        public double LocalRadius { get; }

        public FloatingOrigin(D3 origin, double localRadius)
        {
            Origin = origin;
            LocalRadius = localRadius;
        }

        // global -> local (float): l = (global - origin), near zero by construction.
        public F3 ToLocal(D3 global)
        {
            return Coordinates.FromFloat(global, Origin);
        }

        // local -> global. g = origin + FromFloat(local).
        public D3 ToGlobal(F3 local)
        {
            var g = Coordinates.ToFloat(local, Origin);
            return g + Origin;
        }

        // Move the origin to `target`: Origin = target, LocalRadius preserved.
        public FloatingOrigin SlideTo(D3 target)
        {
            return new FloatingOrigin(target, LocalRadius);
        }

        // IN/OUT classification distance from the origin (double).
        public double DistanceFromOrigin(D3 global)
        {
            var d = global - Origin;
            return System.Math.Sqrt(d.X * d.X + d.Y * d.Y + d.Z * d.Z);
        }
    }
}

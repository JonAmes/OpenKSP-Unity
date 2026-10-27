using OpenKSP.Core.Celestial;
using OpenKSP.Core.Math;
using OpenKSP.Core.World;
using Xunit;

namespace OpenKSP.Core.Tests
{
    /// <summary>
    /// T15: Coordinates.Scale is identity — the local↔global float↔double mapping
    /// is a pure offset (no magnitudinal scaling), so for exactly-representable
    /// deltas the round trip is bit-exact. This is the identity check the HUD
    /// read-back fold (Core §3, PhysicsProxy) depends on: FloatingOrigin carries
    /// the offset, and Coordinates carries only the unit scale.
    /// </summary>
    public class T15
    {
        private static readonly D3 Origin = new D3(2.0, 3.0, 4.0);
        private static readonly D3 Global = new D3(11.0, 12.0, 13.0);
        // delta = Global - Origin = (9, 9, 9), exactly representable in both float and double.
        private static readonly D3 Delta = Global - Origin;

        [Fact]
        public void ScaleEqualsOne()
        {
            // Coordinates.Scale is the unit scale; there is no magnitudinal factor.
            Assert.Equal(1.0, Coordinates.Scale);
        }

        [Fact]
        public void ScaleRoundTripReproducesDeltaExactly()
        {
            // global -> local (F3) -> back to global (D3): with Scale == 1 the
            // result equals the offset delta, not distorted by any scaling.
            var local = Coordinates.FromFloat(Global, Origin);
            var back = Coordinates.ToFloat(local, Origin);

            // Exactly representable delta ⇒ bit-exact, no relative tolerance.
            Assert.True(back == Delta);
            // Per-component bit-exact identity (Scale==1 means float<->double are lossless here).
            Assert.Equal(Delta.X, back.X);
            Assert.Equal(Delta.Y, back.Y);
            Assert.Equal(Delta.Z, back.Z);
        }

        [Fact]
        public void FullRoundTripViaFloatingOriginReproducesGlobalExactly()
        {
            // The actual HUD fold: FloatingOrigin carries the offset, Coordinates
            // the scale. For a representable delta, to-local then to-global
            // restores the global position exactly.
            var fo = new FloatingOrigin(Origin, 1000.0);
            var local = fo.ToLocal(Global);
            var g2 = fo.ToGlobal(local);

            Assert.True(g2 == Global);
            Assert.Equal(Global.X, g2.X);
            Assert.Equal(Global.Y, g2.Y);
            Assert.Equal(Global.Z, g2.Z);
        }
    }
}

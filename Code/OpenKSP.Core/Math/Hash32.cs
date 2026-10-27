using System;

namespace OpenKSP.Core.Math
{
    // Deterministic, platform-stable hash helpers. System.HashCode / HashCode.Combine
    // require netstandard2.1+, which is unavailable to Unity's Mono runtime, so
    // these use only mscorlib facades (BitConverter.DoubleToInt64Bits, etc).
    public static class Hash32
    {
        // FNV-1a-style mix of the IEEE-754 bit patterns of a 3-component (double) value.
        public static int Triple(double x, double y, double z)
        {
            uint h = 2166136261;
            h = (h * 16777619) ^ (uint)BitConverter.DoubleToInt64Bits(x);
            h = (h * 16777619) ^ (uint)BitConverter.DoubleToInt64Bits(y);
            h = (h * 16777619) ^ (uint)BitConverter.DoubleToInt64Bits(z);
            return (int)h;
        }

        // Hash of a single double (e.g. DTime.TimeSeconds).
        public static int Single(double v)
        {
            uint h = 2166136261;
            h = (h * 16777619) ^ (uint)BitConverter.DoubleToInt64Bits(v);
            return (int)h;
        }
    }

    // Sequential hash (for WorldSnapshot bit-exact GetHashCode): add values one by one.
    // A class (not a struct) so it can carry a field initializer in C# 9.
    public sealed class HashAccum
    {
        private uint _h = 2166136261;

        public HashAccum Add(double v) { _h = (_h * 16777619) ^ (uint)BitConverter.DoubleToInt64Bits(v); return this; }
        public HashAccum Add(int v) { _h = (_h * 16777619) ^ (uint)v; return this; }
        public int ToHash() => (int)_h;
    }
}

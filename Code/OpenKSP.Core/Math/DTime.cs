using System;

namespace OpenKSP.Core.Math {

// Simulation time in double seconds relative to world start.
// <remarks>Pure abstraction - no wall-clock reads.</remarks>
public readonly struct DTime : IEquatable<DTime>
{
    public double TimeSeconds { get; }

    public DTime(double time) => TimeSeconds = time;

    public static readonly DTime Zero = new(0.0);

    public override bool Equals(object? obj) => 
        obj is DTime other && (this == other);

    public bool Equals(DTime other) => TimeSeconds == other.TimeSeconds;
    
    public static bool operator ==(DTime left, DTime right) => left.Equals(right);
    public static bool operator !=(DTime left, DTime right) => !left.Equals(right);

    public override int GetHashCode() => Hash32.Single(TimeSeconds);

    public override string ToString() => $"{TimeSeconds:F6}s";
    }
}
